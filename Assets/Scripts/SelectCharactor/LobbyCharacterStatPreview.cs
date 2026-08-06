using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the final combined stats for a character while it only exists as data
/// (Basement / character-select hub), where there is no live character hierarchy and therefore no
/// <see cref="StatsHub"/> to ask.
///
/// Bonus sources included: equipped weapon (base + upgrade + static affixes), equipped accessories
/// (base + reforge modifier), and always-on passives. Temporary status effects and timed/stacking
/// weapon affix buffs are deliberately excluded - they can never be active in the lobby.
///
/// The math itself is not duplicated here: everything funnels through
/// <see cref="CharacterStatFormula"/>, the same code path <see cref="StatsHub"/> uses in combat.
/// </summary>
public static class LobbyCharacterStatPreview
{
    static readonly List<RuntimeStatModifier> ModifierBuffer = new();
    static readonly List<PassiveDefinition> PassiveBuffer = new();

    public static CharacterStatTotals Build(
        CharacterStats stats,
        int level,
        string characterId,
        PlayerInventory inventory,
        WeaponAffixDatabase affixDatabase = null)
    {
        if (stats == null)
            return default;

        float levelOffset = Mathf.Max(0f, level - 1f);
        string ownerId = CharacterEquipment.BuildCharacterOwnerId(characterId);

        InventorySlotData weaponSlot = EquipmentAssignmentService.GetEquippedSlotData(
            inventory,
            EquipmentItemKind.Weapon,
            ownerId);

        GunConfig weapon = weaponSlot != null && !weaponSlot.IsEmpty ? weaponSlot.item as GunConfig : null;
        WeaponInstanceData weaponInstance = weaponSlot != null ? weaponSlot.weaponInstance : null;

        var inputs = new CharacterStatInputs
        {
            // Same shape as StatsHub.GetCharacter*Base(): value + scaling * max(0, level - 1).
            CharacterDamage = stats.Damage + stats.DamageScaling * levelOffset,
            CharacterArmor = stats.armor + stats.ArmorScaling * levelOffset,
            CharacterMoveSpeed = stats.speed + stats.SpeedScaling * levelOffset,
            CharacterCritRate = stats.critRate + stats.CritrateScaling * levelOffset,
            CharacterCritMultiplier = Mathf.Max(1f, stats.critMultiplier + stats.CritDamageScaling * levelOffset),
            CharacterMaxHealth = stats.maxHP + stats.MAXHPScaling * levelOffset,
            CharacterMaxStamina = stats.maxStamina + stats.StaminaScaling * levelOffset,
            CharacterMaxEnergy = stats.Enagy + stats.EnagyScaling * levelOffset,
            Weapon = weapon
        };

        ModifierBuffer.Clear();
        PassiveBuffer.Clear();

        AppendWeaponUpgradeModifiers(ModifierBuffer, weapon, weaponInstance);
        AppendStaticWeaponAffixModifiers(ModifierBuffer, weaponInstance, affixDatabase);
        AppendAccessoryModifiers(ModifierBuffer, PassiveBuffer, inventory, ownerId);
        AppendPassiveList(PassiveBuffer, stats.passives);
        AppendPassiveModifiers(ModifierBuffer, PassiveBuffer);

        CharacterStatTotals totals = CharacterStatFormula.Compute(inputs, ModifierBuffer);

        ModifierBuffer.Clear();
        PassiveBuffer.Clear();
        return totals;
    }

    static void AppendWeaponUpgradeModifiers(
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
    /// HeadHunter, HotStreak, BloodMagazine) evaluate to zero outside combat anyway.
    ///
    /// <see cref="ConfiguredWeaponAffixBehavior"/> is currently the only
    /// <see cref="WeaponAffixBehavior"/> subclass. If another one is added with static stats, it
    /// must be handled here too or the lobby will silently under-report.
    /// </summary>
    static void AppendStaticWeaponAffixModifiers(
        List<RuntimeStatModifier> buffer,
        WeaponInstanceData instance,
        WeaponAffixDatabase affixDatabase)
    {
        if (instance == null)
            return;

        AppendStaticWeaponAffixModifier(buffer, instance, instance.mainAffix, affixDatabase);

        if (instance.subAffixes == null)
            return;

        for (int i = 0; i < instance.subAffixes.Count; i++)
            AppendStaticWeaponAffixModifier(buffer, instance, instance.subAffixes[i], affixDatabase);
    }

    static void AppendStaticWeaponAffixModifier(
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

    static void AppendAccessoryModifiers(
        List<RuntimeStatModifier> modifierBuffer,
        List<PassiveDefinition> passiveBuffer,
        PlayerInventory inventory,
        string ownerId)
    {
        int slotCount = AccessoryLoadout.ResolveOwnerSlotCount(ownerId);

        for (int i = 0; i < slotCount; i++)
        {
            InventorySlotData slotData = EquipmentAssignmentService.GetEquippedSlotData(
                inventory,
                EquipmentItemKind.Accessory,
                ownerId,
                i);

            if (slotData == null || slotData.IsEmpty || !slotData.HasAccessoryInstance)
                continue;

            if (slotData.item is not AccessoryDefinition definition)
                continue;

            AccessoryLoadout.AppendInstanceStatModifiers(modifierBuffer, definition, slotData.accessoryInstance);
            AccessoryLoadout.AppendInstancePassiveDefinitions(passiveBuffer, definition, slotData.accessoryInstance);
        }
    }

    static void AppendPassiveList(List<PassiveDefinition> buffer, List<PassiveDefinition> passives)
    {
        if (passives == null)
            return;

        for (int i = 0; i < passives.Count; i++)
        {
            if (passives[i] != null)
                buffer.Add(passives[i]);
        }
    }

    /// <summary>
    /// Mirrors PassiveController.AddAlwaysOnPassive: only always-on passives contribute stat
    /// modifiers, using the raw authored value with no stack multiplier.
    /// </summary>
    static void AppendPassiveModifiers(List<RuntimeStatModifier> buffer, List<PassiveDefinition> passives)
    {
        for (int i = 0; i < passives.Count; i++)
        {
            if (passives[i] is not AlwaysOnPassiveDef alwaysOn || alwaysOn.modifiers == null)
                continue;

            string modifierKey = $"passive:{alwaysOn.RuntimeId}";
            for (int j = 0; j < alwaysOn.modifiers.Count; j++)
            {
                PassiveStatModifier modifier = alwaysOn.modifiers[j];
                if (modifier == null)
                    continue;

                buffer.Add(new RuntimeStatModifier(
                    modifier.statType,
                    modifier.operation,
                    modifier.value,
                    modifierKey));
            }
        }
    }
}
