using System;
using System.Collections.Generic;
using UnityEngine;

public enum WeaponAffixRuntimeKind
{
    StaticStat, ReloadBuff, EchoChamber, BreachChamber, ImpactCore, KillClip,
    ExecutionFeed, LastRound, HeadHunter, FreshChamber, HotStreak, PressurePoint,
    ConservationRound, Overkill, BrokenGuard, MarkedQuarry, BloodMagazine
}

[CreateAssetMenu(menuName = "Game/Weapons/Affixes/Configured Behavior", fileName = "WeaponAffixBehavior")]
public sealed class ConfiguredWeaponAffixBehavior : WeaponAffixBehavior
{
    public WeaponAffixRuntimeKind kind;
    public StatType statType;
    public ModifierOp modifierOp = ModifierOp.AddPercent;
    public float duration = 4f;
    public int cap = 1;
    public int threshold = 1;
    public float fixedValue;
    public float secondaryValue;
    public int minimumBaseMagazine;
    public float procChance = 1f;
    public ProjectileConfig projectileConfig;
    public GameObject projectilePrefab;

    // Kinds whose runtime arms the expiry timer (see ActivateTimer/TrackTarget/BrokenGuard below).
    // Every other kind is permanent-while-equipped or charge based, so a duration is meaningless for it.
    public override bool HasTimedEffect => kind is
        WeaponAffixRuntimeKind.ReloadBuff or WeaponAffixRuntimeKind.KillClip or
        WeaponAffixRuntimeKind.HeadHunter or WeaponAffixRuntimeKind.HotStreak or
        WeaponAffixRuntimeKind.PressurePoint or WeaponAffixRuntimeKind.MarkedQuarry or
        WeaponAffixRuntimeKind.BloodMagazine or WeaponAffixRuntimeKind.BrokenGuard;

    // Kinds where AppendStatModifiers feeds the roll straight into a RuntimeStatModifier using modifierOp.
    // Only those can have their unit derived; the rest spell their own unit out in the tooltip text.
    public override ModifierOp? RolledValueModifierOp => kind is
        WeaponAffixRuntimeKind.StaticStat or WeaponAffixRuntimeKind.ImpactCore or
        WeaponAffixRuntimeKind.ReloadBuff or WeaponAffixRuntimeKind.KillClip
        ? modifierOp
        : null;

    public override bool IsEligible(GunConfig weapon)
    {
        return base.IsEligible(weapon) && (minimumBaseMagazine <= 0 ||
            Mathf.Max(weapon.magazine, weapon.maxMagazine) >= minimumBaseMagazine);
    }

    public override IWeaponAffixRuntime CreateRuntime(WeaponAffixRuntimeContext context)
    {
        throw new NotSupportedException("Weapon affixes run through PassiveController. Migrate the definition recipe.");
    }
}
