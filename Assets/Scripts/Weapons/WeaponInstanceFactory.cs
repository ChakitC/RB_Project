using System;
using System.Collections.Generic;
using UnityEngine;

public static class WeaponInstanceFactory
{
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
            instanceId = Guid.NewGuid().ToString("N"),
            baseWeaponId = ResolveBaseWeaponId(baseWeapon),
            rarity = rarity,
            currentMagazine = ResolveDefaultMagazine(baseWeapon),
            shotCounter = 0
        };
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
