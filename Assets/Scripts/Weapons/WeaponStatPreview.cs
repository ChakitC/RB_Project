using System.Collections.Generic;

/// <summary>
/// Builds a weapon's effective stats from data alone - base config plus its upgrade milestones and
/// its always-on affixes - with no live <see cref="StatsHub"/> and no character contribution.
///
/// Why not just ask StatsHub: it folds in character base stats and whatever status effects happen to
/// be active right now, and it only sees affix stats after the affix runtime has been created on
/// equip. None of that is true for a weapon sitting in the bag, and character bonuses would make two
/// weapons impossible to compare side by side.
///
/// The math is not duplicated here either: it funnels through <see cref="CharacterStatFormula"/>,
/// the same path combat uses, with the character inputs left at neutral.
/// <see cref="LobbyCharacterStatPreview"/> shares the modifier collection below.
/// </summary>
public static class WeaponStatPreview
{
    static readonly List<RuntimeStatModifier> ModifierBuffer = new();

    /// <summary>
    /// Weapon-only totals. Read the weapon fields off the result (Damage, FireInterval, MaxMagazine,
    /// ...); the character fields are meaningless because the character inputs are zeroed.
    /// </summary>
    public static CharacterStatTotals Build(
        GunConfig weapon,
        WeaponInstanceData instance,
        WeaponAffixDatabase affixDatabase = null)
    {
        var inputs = new CharacterStatInputs
        {
            // Neutral character: crit multiplier is multiplicative, so its identity is 1, not 0.
            CharacterCritMultiplier = CharacterStatFormula.BaseCritMultiplier,
            Weapon = weapon
        };

        ModifierBuffer.Clear();
        AppendWeaponUpgradeModifiers(ModifierBuffer, weapon, instance);
        AppendStaticAffixModifiers(ModifierBuffer, instance, affixDatabase);

        CharacterStatTotals totals = CharacterStatFormula.Compute(inputs, ModifierBuffer);
        ModifierBuffer.Clear();
        return totals;
    }

    public static void AppendWeaponUpgradeModifiers(
        List<RuntimeStatModifier> buffer,
        GunConfig weapon,
        WeaponInstanceData instance)
    {
        if (!weapon || instance == null)
            return;

        WeaponUpgradeCurve curve = WeaponUpgradeService.ResolveUpgradeCurve(weapon, null);
        if (curve == null)
            return;

        // Same key shape as WeaponUpgradeRuntimeController.BuildModifierKey.
        string modifierKey = string.IsNullOrWhiteSpace(instance.instanceId)
            ? "weapon-upgrade:unknown"
            : $"weapon-upgrade:{instance.instanceId}";

        curve.AppendStatModifiers(buffer, weapon, instance, modifierKey);
    }

    /// <summary>
    /// Reads static affix stats straight off the authored data. Never builds an affix runtime:
    /// CreateRuntime / OnEquip / GetOrCreateAffixState all have side effects.
    ///
    /// Only the unconditional kinds are included. Timed and stacking kinds (ReloadBuff, KillClip,
    /// HeadHunter, HotStreak, BloodMagazine) contribute nothing while the weapon is idle.
    ///
    /// <see cref="ConfiguredWeaponAffixBehavior"/> is currently the only
    /// <see cref="WeaponAffixBehavior"/> subclass. If another one is added with static stats, it
    /// must be handled here too or every preview will silently under-report.
    /// </summary>
    public static void AppendStaticAffixModifiers(
        List<RuntimeStatModifier> buffer,
        WeaponInstanceData instance,
        WeaponAffixDatabase affixDatabase)
    {
        if (instance == null)
            return;

        AppendStaticAffixModifier(buffer, instance, instance.mainAffix, affixDatabase);

        if (instance.subAffixes == null)
            return;

        for (int i = 0; i < instance.subAffixes.Count; i++)
            AppendStaticAffixModifier(buffer, instance, instance.subAffixes[i], affixDatabase);
    }

    static void AppendStaticAffixModifier(
        List<RuntimeStatModifier> buffer,
        WeaponInstanceData instance,
        RolledAffixData rolledAffix,
        WeaponAffixDatabase affixDatabase)
    {
        if (rolledAffix == null || string.IsNullOrWhiteSpace(rolledAffix.affixId))
            return;

        WeaponAffixDefinition definition = affixDatabase != null
            ? affixDatabase.GetById(rolledAffix.affixId)
            : WeaponAffixDatabase.GetLoadedAffixById(rolledAffix.affixId);

        if (definition == null || definition.rootBehavior is not ConfiguredWeaponAffixBehavior behavior)
            return;

        if (behavior.kind != WeaponAffixRuntimeKind.StaticStat &&
            behavior.kind != WeaponAffixRuntimeKind.ImpactCore)
        {
            return;
        }

        float roll = rolledAffix.hasPrimaryValue ? rolledAffix.primaryValue : 0f;
        string modifierKey = $"weapon:{instance.instanceId}:affix:{definition.affixId}";
        buffer.Add(new RuntimeStatModifier(behavior.statType, behavior.modifierOp, roll, modifierKey));
    }
}
