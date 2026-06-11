using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Weapons/Weapon Affix", fileName = "WeaponAffix")]
public class WeaponAffixDefinition : ScriptableObject
{
    [Header("Identity")]
    public string affixId;
    public string displayName;
    [TextArea]
    public string description;

    [Header("Rules")]
    public WeaponAffixSlot slot = WeaponAffixSlot.Sub;
    public WeaponAffixBehaviorType behaviorType = WeaponAffixBehaviorType.StatModifier;
    [Min(0.01f)] public float weight = 1f;
    public List<WeaponType> allowedWeaponTypes = new();

    [Header("Roll")]
    [Tooltip("For a Flat Stability affix, values are percentage points.")]
    public float minRollValue = 0f;
    [Tooltip("For a Flat Stability affix, values are percentage points.")]
    public float maxRollValue = 0f;

    [Header("Stat Modifier")]
    public StatType statType = StatType.CritRate;
    [Tooltip("For Stability, Flat adds percentage points. Final Stability is clamped to 0-100%.")]
    public ModifierOp modifierOp = ModifierOp.Flat;

    [Header("Timed Reload Buff")]
    [Min(0f)] public float buffDurationSeconds = 4f;

    [Header("Special Projectile")]
    [Min(1)] public int requiredShots = 10;
    [Range(0f, 1f)] public float procChance = 0.25f;
    public ProjectileConfig specialProjectileConfig;
    public GameObject specialProjectilePrefab;
    [Min(0f)] public float specialProjectileDamageMultiplier = 1f;
    [Min(0f)] public float specialProjectileSpeedMultiplier = 1f;

    public bool SupportsWeaponType(WeaponType weaponType)
    {
        return allowedWeaponTypes == null || allowedWeaponTypes.Count == 0 || allowedWeaponTypes.Contains(weaponType);
    }

    public float RollPrimaryValue()
    {
        if (Mathf.Approximately(minRollValue, maxRollValue))
            return minRollValue;

        return Random.Range(minRollValue, maxRollValue);
    }

    public float ResolvePrimaryValue(RolledAffixData rolledAffix)
    {
        if (rolledAffix != null && rolledAffix.hasPrimaryValue)
            return rolledAffix.primaryValue;

        return minRollValue;
    }
}
