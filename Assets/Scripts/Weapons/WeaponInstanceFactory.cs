using System;
using System.Collections.Generic;
using UnityEngine;

public static class WeaponInstanceFactory
{
    public const int DefaultReserveMagazineMultiplier = 3;

    static readonly List<WeaponAffixDefinition> CandidateBuffer = new();

    public static WeaponInstanceData CreateInstance(GunConfig baseWeapon, WeaponRarity rarity, WeaponAffixDatabase affixDatabase)
    {
        if (!baseWeapon)
            return null;

        var instance = CreatePlainInstance(baseWeapon, rarity);
        RollAffixes(instance, baseWeapon, affixDatabase);
        return instance;
    }

    public static WeaponInstanceData CreatePlainInstance(GunConfig baseWeapon, WeaponRarity rarity = WeaponRarity.Common)
    {
        if (!baseWeapon)
            return null;

        return new WeaponInstanceData
        {
            instanceId = CreateInstanceId(),
            baseWeaponId = ResolveBaseWeaponId(baseWeapon),
            rarity = rarity,
            currentMagazine = ResolveDefaultMagazine(baseWeapon),
            currentReserveAmmo = ResolveDefaultReserveAmmo(baseWeapon),
            reserveAmmoInitialized = true,
            shotCounter = 0
        };
    }

    public static string CreateInstanceId()
    {
        return Guid.NewGuid().ToString("N");
    }

    public static void ApplyUpgradeLevel(
        WeaponInstanceData instance,
        GunConfig baseWeapon,
        int upgradeLevel,
        WeaponUpgradeCurve fallbackCurve)
    {
        if (instance == null || !baseWeapon)
            return;

        int level = Mathf.Max(0, upgradeLevel);
        var curve = WeaponUpgradeService.ResolveUpgradeCurve(baseWeapon, fallbackCurve);
        if (curve != null)
            level = Mathf.Clamp(level, 0, curve.GetMaxLevel(instance.rarity));

        instance.upgradeLevel = level;
        instance.upgradeTier = 0;
        instance.upgradeExp = 0;
        instance.unlockedUpgradeMilestoneIds ??= new List<string>();
        instance.unlockedUpgradeMilestoneIds.Clear();

        if (curve != null && instance.upgradeLevel > 0)
            curve.SyncUnlockedMilestones(instance, baseWeapon);
    }

    public static string ResolveBaseWeaponId(GunConfig baseWeapon)
    {
        if (!baseWeapon)
            return null;

        if (!string.IsNullOrWhiteSpace(baseWeapon.itemId))
            return baseWeapon.itemId;

        return baseWeapon.name;
    }

    public static int ResolveDefaultMagazine(GunConfig baseWeapon)
    {
        if (!baseWeapon)
            return 0;

        int maxMagazine = Mathf.Max(baseWeapon.maxMagazine, baseWeapon.magazine);
        int defaultMagazine = baseWeapon.magazine > 0 ? baseWeapon.magazine : maxMagazine;
        return Mathf.Clamp(defaultMagazine, 0, maxMagazine);
    }

    public static bool UsesFiniteReserveAmmo(GunConfig baseWeapon)
    {
        return baseWeapon != null && !baseWeapon.infiniteReserveAmmo;
    }

    public static int ResolveMaxReserveAmmo(GunConfig baseWeapon)
    {
        int maxMagazine = baseWeapon ? Mathf.Max(baseWeapon.maxMagazine, baseWeapon.magazine) : 0;
        return ResolveMaxReserveAmmo(baseWeapon, maxMagazine);
    }

    public static int ResolveMaxReserveAmmo(GunConfig baseWeapon, int maxMagazine)
    {
        if (!UsesFiniteReserveAmmo(baseWeapon))
            return 0;

        if (baseWeapon.maxReserveAmmo > 0)
            return baseWeapon.maxReserveAmmo;

        return Mathf.Max(0, maxMagazine) * DefaultReserveMagazineMultiplier;
    }

    public static int ResolveDefaultReserveAmmo(GunConfig baseWeapon)
    {
        int maxMagazine = baseWeapon ? Mathf.Max(baseWeapon.maxMagazine, baseWeapon.magazine) : 0;
        return ResolveDefaultReserveAmmo(baseWeapon, maxMagazine);
    }

    public static int ResolveDefaultReserveAmmo(GunConfig baseWeapon, int maxMagazine)
    {
        if (!UsesFiniteReserveAmmo(baseWeapon))
            return -1;

        int maxReserveAmmo = ResolveMaxReserveAmmo(baseWeapon, maxMagazine);
        if (maxReserveAmmo <= 0)
            return 0;

        if (baseWeapon.overrideStartingReserveAmmo)
            return Mathf.Clamp(baseWeapon.startingReserveAmmo, 0, maxReserveAmmo);

        return maxReserveAmmo;
    }

    static void RollAffixes(WeaponInstanceData instance, GunConfig baseWeapon, WeaponAffixDatabase affixDatabase)
    {
        if (instance == null || !baseWeapon || affixDatabase == null)
            return;

        GetAffixCounts(instance.rarity, out int mainCount, out int subCount);
        var excludedIds = new HashSet<string>(StringComparer.Ordinal);

        if (mainCount > 0)
        {
            var mainDefinition = RollDefinition(affixDatabase, WeaponAffixSlot.Main, baseWeapon.WeaponType, excludedIds);
            if (mainDefinition != null)
            {
                instance.mainAffix = CreateRolledAffix(mainDefinition);
                excludedIds.Add(mainDefinition.affixId);
            }
        }

        for (int i = 0; i < subCount; i++)
        {
            var subDefinition = RollDefinition(affixDatabase, WeaponAffixSlot.Sub, baseWeapon.WeaponType, excludedIds);
            if (subDefinition == null)
                break;

            instance.subAffixes.Add(CreateRolledAffix(subDefinition));
            excludedIds.Add(subDefinition.affixId);
        }
    }

    static void GetAffixCounts(WeaponRarity rarity, out int mainCount, out int subCount)
    {
        switch (rarity)
        {
            case WeaponRarity.Rare:
                mainCount = 1;
                subCount = 1;
                break;

            case WeaponRarity.Epic:
                mainCount = 1;
                subCount = 2;
                break;

            default:
                mainCount = 0;
                subCount = 1;
                break;
        }
    }

    static WeaponAffixDefinition RollDefinition(
        WeaponAffixDatabase affixDatabase,
        WeaponAffixSlot slot,
        WeaponType weaponType,
        HashSet<string> excludedIds)
    {
        affixDatabase.GetCandidates(CandidateBuffer, slot, weaponType, excludedIds);
        if (CandidateBuffer.Count == 0)
            return null;

        float totalWeight = 0f;
        for (int i = 0; i < CandidateBuffer.Count; i++)
            totalWeight += Mathf.Max(0f, CandidateBuffer[i].weight);

        if (totalWeight <= 0f)
            return CandidateBuffer[UnityEngine.Random.Range(0, CandidateBuffer.Count)];

        float roll = UnityEngine.Random.value * totalWeight;
        float cumulative = 0f;

        for (int i = 0; i < CandidateBuffer.Count; i++)
        {
            var definition = CandidateBuffer[i];
            cumulative += Mathf.Max(0f, definition.weight);
            if (roll <= cumulative)
                return definition;
        }

        return CandidateBuffer[CandidateBuffer.Count - 1];
    }

    static RolledAffixData CreateRolledAffix(WeaponAffixDefinition definition)
    {
        if (!definition)
            return null;

        bool needsPrimaryRoll = definition.behaviorType == WeaponAffixBehaviorType.StatModifier ||
                                definition.behaviorType == WeaponAffixBehaviorType.TimedBuffOnReload;

        return new RolledAffixData
        {
            affixId = definition.affixId,
            hasPrimaryValue = needsPrimaryRoll,
            primaryValue = needsPrimaryRoll ? definition.RollPrimaryValue() : 0f
        };
    }
}
