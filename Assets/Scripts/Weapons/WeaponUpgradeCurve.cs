using System;
using System.Collections.Generic;
using UnityEngine;

public enum WeaponUpgradeEffectType
{
    None,
    StatModifier,
    ExtraProjectileChance,
    SpecialProjectileEveryNthShot,
    TimedBuffOnReload
}

[Serializable]
public class WeaponUpgradeMaterialCost
{
    public ItemDefinition item;
    [Min(1)] public int amount = 1;

    public bool IsValid => item != null && amount > 0;
}

[Serializable]
public class WeaponUpgradeLevelCost
{
    [Tooltip("Cost to reach this upgrade level. Example: targetLevel 10 means +9 -> +10.")]
    [Min(1)] public int targetLevel = 1;
    [Min(0)] public int goldCost = 0;
    [Min(0)] public int scrapCost = 0;
    public List<WeaponUpgradeMaterialCost> materials = new();
}

[Serializable]
public class WeaponUpgradeResolvedCost
{
    public int targetLevel;
    public int goldCost;
    public int scrapCost;
    public List<WeaponUpgradeMaterialCost> materials = new();
}

[Serializable]
public class WeaponRarityLevelCap
{
    public WeaponRarity rarity = WeaponRarity.Common;
    [Min(0)] public int maxLevel = 10;
}

[Serializable]
public class WeaponUpgradeStatBonus
{
    public StatType statType = StatType.Damage;
    public ModifierOp operation = ModifierOp.Flat;
    public float valuePerStep = 1f;
    [Min(1)] public int firstLevel = 1;
    [Min(1)] public int levelsPerStep = 1;
    public List<WeaponType> allowedWeaponTypes = new();

    public bool SupportsWeaponType(WeaponType weaponType)
    {
        return allowedWeaponTypes == null || allowedWeaponTypes.Count == 0 || allowedWeaponTypes.Contains(weaponType);
    }

    public float ResolveValue(int upgradeLevel)
    {
        if (upgradeLevel < firstLevel)
            return 0f;

        int steps = ((upgradeLevel - firstLevel) / Mathf.Max(1, levelsPerStep)) + 1;
        return valuePerStep * Mathf.Max(0, steps);
    }
}

[Serializable]
public class WeaponUpgradeEffect
{
    [Header("Identity")]
    public string effectId;
    public string displayName;
    [TextArea] public string description;

    [Header("Behavior")]
    public WeaponUpgradeEffectType effectType = WeaponUpgradeEffectType.StatModifier;
    public List<WeaponType> allowedWeaponTypes = new();

    [Header("Stat Modifier")]
    public StatType statType = StatType.Damage;
    public ModifierOp modifierOp = ModifierOp.Flat;
    public float value = 0f;

    [Header("Trigger")]
    [Range(0f, 1f)] public float procChance = 1f;
    [Min(1)] public int requiredShots = 1;

    [Header("Projectile")]
    public ProjectileConfig specialProjectileConfig;
    public GameObject specialProjectilePrefab;
    [Min(0f)] public float specialProjectileDamageMultiplier = 1f;
    [Min(0f)] public float specialProjectileSpeedMultiplier = 1f;
    [Min(1)] public int projectileCount = 1;

    [Header("Timed Reload Buff")]
    [Min(0f)] public float buffDurationSeconds = 4f;

    public bool SupportsWeaponType(WeaponType weaponType)
    {
        return allowedWeaponTypes == null || allowedWeaponTypes.Count == 0 || allowedWeaponTypes.Contains(weaponType);
    }

    public string ResolveId(string fallbackPrefix, int index)
    {
        if (!string.IsNullOrWhiteSpace(effectId))
            return effectId.Trim();

        return $"{fallbackPrefix}:effect:{index}";
    }
}

[Serializable]
public class WeaponUpgradeMilestone
{
    [Header("Identity")]
    public string milestoneId;
    public string displayName;
    [TextArea] public string description;

    [Header("Requirement")]
    [Min(1)] public int requiredLevel = 10;
    public List<WeaponType> allowedWeaponTypes = new();

    [Header("Tier")]
    public bool increaseTier = true;
    [Min(1)] public int tierIncreaseAmount = 1;

    [Header("Effects")]
    public List<WeaponUpgradeEffect> effects = new();

    public bool SupportsWeaponType(WeaponType weaponType)
    {
        return allowedWeaponTypes == null || allowedWeaponTypes.Count == 0 || allowedWeaponTypes.Contains(weaponType);
    }

    public string ResolveId(int index)
    {
        if (!string.IsNullOrWhiteSpace(milestoneId))
            return milestoneId.Trim();

        return $"level-{Mathf.Max(1, requiredLevel)}:{index}";
    }
}

[CreateAssetMenu(menuName = "Game/Weapons/Weapon Upgrade Curve", fileName = "WeaponUpgradeCurve")]
public class WeaponUpgradeCurve : ScriptableObject
{
    [Header("Level Cap")]
    [Min(0)] public int maxLevel = 10;
    public List<WeaponRarityLevelCap> rarityLevelCaps = new();

    [Header("Fallback Cost")]
    [Min(0)] public int baseGoldCost = 100;
    [Min(0)] public int goldCostPerLevel = 25;
    [Min(0)] public int baseScrapCost = 10;
    [Min(0)] public int scrapCostPerLevel = 5;

    [Header("Explicit Cost Overrides")]
    public List<WeaponUpgradeLevelCost> levelCosts = new();

    [Header("Stat Scaling")]
    public List<WeaponUpgradeStatBonus> statBonuses = new();

    [Header("Milestones")]
    public List<WeaponUpgradeMilestone> milestones = new();

    public int GetMaxLevel(WeaponRarity rarity)
    {
        if (rarityLevelCaps != null)
        {
            for (int i = 0; i < rarityLevelCaps.Count; i++)
            {
                var cap = rarityLevelCaps[i];
                if (cap != null && cap.rarity == rarity)
                    return Mathf.Max(0, cap.maxLevel);
            }
        }

        return Mathf.Max(0, maxLevel);
    }

    public WeaponUpgradeResolvedCost GetCostForNextLevel(WeaponInstanceData instance)
    {
        int currentLevel = instance != null ? Mathf.Max(0, instance.upgradeLevel) : 0;
        return GetCostForTargetLevel(currentLevel + 1);
    }

    public WeaponUpgradeResolvedCost GetCostForTargetLevel(int targetLevel)
    {
        targetLevel = Mathf.Max(1, targetLevel);

        var resolved = new WeaponUpgradeResolvedCost
        {
            targetLevel = targetLevel,
            goldCost = Mathf.Max(0, baseGoldCost + goldCostPerLevel * Mathf.Max(0, targetLevel - 1)),
            scrapCost = Mathf.Max(0, baseScrapCost + scrapCostPerLevel * Mathf.Max(0, targetLevel - 1))
        };

        var explicitCost = FindExplicitCost(targetLevel);
        if (explicitCost != null)
        {
            resolved.goldCost = Mathf.Max(0, explicitCost.goldCost);
            resolved.scrapCost = Mathf.Max(0, explicitCost.scrapCost);
            CopyMaterialCosts(explicitCost.materials, resolved.materials);
        }

        return resolved;
    }

    public void AppendStatModifiers(
        List<RuntimeStatModifier> buffer,
        GunConfig weapon,
        WeaponInstanceData instance,
        string sourceId)
    {
        if (buffer == null || !weapon || instance == null)
            return;

        int upgradeLevel = Mathf.Max(0, instance.upgradeLevel);
        if (upgradeLevel <= 0)
            return;

        AppendLevelStatBonuses(buffer, weapon.WeaponType, upgradeLevel, sourceId);
        AppendMilestoneStatBonuses(buffer, weapon.WeaponType, upgradeLevel, sourceId);
    }

    public int SyncUnlockedMilestones(WeaponInstanceData instance, GunConfig weapon)
    {
        if (instance == null || !weapon)
            return 0;

        instance.unlockedUpgradeMilestoneIds ??= new List<string>();

        int tierIncrements = 0;
        int upgradeLevel = Mathf.Max(0, instance.upgradeLevel);

        if (milestones == null)
            return 0;

        for (int i = 0; i < milestones.Count; i++)
        {
            var milestone = milestones[i];
            if (milestone == null || upgradeLevel < Mathf.Max(1, milestone.requiredLevel))
                continue;

            if (!milestone.SupportsWeaponType(weapon.WeaponType))
                continue;

            string milestoneId = milestone.ResolveId(i);
            if (instance.unlockedUpgradeMilestoneIds.Contains(milestoneId))
                continue;

            instance.unlockedUpgradeMilestoneIds.Add(milestoneId);

            if (milestone.increaseTier)
                tierIncrements += Mathf.Max(1, milestone.tierIncreaseAmount);
        }

        if (tierIncrements > 0)
            instance.upgradeTier = Mathf.Max(0, instance.upgradeTier) + tierIncrements;

        return tierIncrements;
    }

    public void GetActiveMilestones(
        List<WeaponUpgradeMilestone> buffer,
        WeaponType weaponType,
        int upgradeLevel)
    {
        if (buffer == null)
            return;

        buffer.Clear();

        if (milestones == null || upgradeLevel <= 0)
            return;

        for (int i = 0; i < milestones.Count; i++)
        {
            var milestone = milestones[i];
            if (milestone == null)
                continue;

            if (upgradeLevel < Mathf.Max(1, milestone.requiredLevel))
                continue;

            if (!milestone.SupportsWeaponType(weaponType))
                continue;

            buffer.Add(milestone);
        }
    }

    void AppendLevelStatBonuses(
        List<RuntimeStatModifier> buffer,
        WeaponType weaponType,
        int upgradeLevel,
        string sourceId)
    {
        if (statBonuses == null)
            return;

        for (int i = 0; i < statBonuses.Count; i++)
        {
            var bonus = statBonuses[i];
            if (bonus == null || !bonus.SupportsWeaponType(weaponType))
                continue;

            float value = bonus.ResolveValue(upgradeLevel);
            if (Mathf.Approximately(value, 0f))
                continue;

            buffer.Add(new RuntimeStatModifier(bonus.statType, bonus.operation, value, sourceId));
        }
    }

    void AppendMilestoneStatBonuses(
        List<RuntimeStatModifier> buffer,
        WeaponType weaponType,
        int upgradeLevel,
        string sourceId)
    {
        if (milestones == null)
            return;

        for (int i = 0; i < milestones.Count; i++)
        {
            var milestone = milestones[i];
            if (milestone == null ||
                upgradeLevel < Mathf.Max(1, milestone.requiredLevel) ||
                !milestone.SupportsWeaponType(weaponType) ||
                milestone.effects == null)
            {
                continue;
            }

            for (int j = 0; j < milestone.effects.Count; j++)
            {
                var effect = milestone.effects[j];
                if (effect == null ||
                    effect.effectType != WeaponUpgradeEffectType.StatModifier ||
                    !effect.SupportsWeaponType(weaponType) ||
                    Mathf.Approximately(effect.value, 0f))
                {
                    continue;
                }

                string effectSourceId = $"{sourceId}:{milestone.ResolveId(i)}:{effect.ResolveId(milestone.ResolveId(i), j)}";
                buffer.Add(new RuntimeStatModifier(effect.statType, effect.modifierOp, effect.value, effectSourceId));
            }
        }
    }

    WeaponUpgradeLevelCost FindExplicitCost(int targetLevel)
    {
        if (levelCosts == null)
            return null;

        for (int i = 0; i < levelCosts.Count; i++)
        {
            var cost = levelCosts[i];
            if (cost != null && Mathf.Max(1, cost.targetLevel) == targetLevel)
                return cost;
        }

        return null;
    }

    static void CopyMaterialCosts(
        List<WeaponUpgradeMaterialCost> source,
        List<WeaponUpgradeMaterialCost> target)
    {
        target.Clear();

        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            var cost = source[i];
            if (cost == null || !cost.IsValid)
                continue;

            target.Add(new WeaponUpgradeMaterialCost
            {
                item = cost.item,
                amount = Mathf.Max(1, cost.amount)
            });
        }
    }
}
