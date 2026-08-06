using System;
using System.Collections.Generic;

[Serializable]
public class WeaponInstanceData
{
    public string instanceId;
    public string baseWeaponId;
    public WeaponRarity rarity = WeaponRarity.Common;
    public RolledAffixData mainAffix;
    public List<RolledAffixData> subAffixes = new();
    public int shotCounter;
    public int currentMagazine = -1;
    public int currentReserveAmmo = -1;
    public bool reserveAmmoInitialized;
    public int upgradeLevel;
    public int upgradeTier;
    public int upgradeExp;
    public List<string> unlockedUpgradeMilestoneIds = new();
    public List<WeaponAffixRuntimeStateData> affixRuntimeStates = new();

    public bool IsEmpty => string.IsNullOrWhiteSpace(baseWeaponId);
    public bool HasMainAffix => mainAffix != null && !mainAffix.IsEmpty;

    public WeaponInstanceData DeepClone()
    {
        var clone = new WeaponInstanceData
        {
            instanceId = instanceId,
            baseWeaponId = baseWeaponId,
            rarity = rarity,
            mainAffix = mainAffix != null ? mainAffix.DeepClone() : null,
            shotCounter = shotCounter,
            currentMagazine = currentMagazine,
            currentReserveAmmo = currentReserveAmmo,
            reserveAmmoInitialized = reserveAmmoInitialized,
            upgradeLevel = upgradeLevel,
            upgradeTier = upgradeTier,
            upgradeExp = upgradeExp,
            unlockedUpgradeMilestoneIds = unlockedUpgradeMilestoneIds != null
                ? new List<string>(unlockedUpgradeMilestoneIds)
                : new List<string>()
        };

        if (subAffixes != null)
        {
            for (int i = 0; i < subAffixes.Count; i++)
            {
                var affix = subAffixes[i];
                if (affix != null)
                    clone.subAffixes.Add(affix.DeepClone());
            }
        }

        if (affixRuntimeStates != null)
            for (int i = 0; i < affixRuntimeStates.Count; i++)
                if (affixRuntimeStates[i] != null) clone.affixRuntimeStates.Add(affixRuntimeStates[i].DeepClone());

        return clone;
    }

    public WeaponAffixRuntimeStateData GetOrCreateAffixState(string affixId, int schemaVersion = 1)
    {
        affixRuntimeStates ??= new List<WeaponAffixRuntimeStateData>();
        for (int i = 0; i < affixRuntimeStates.Count; i++)
            if (affixRuntimeStates[i] != null && string.Equals(affixRuntimeStates[i].affixId, affixId, StringComparison.Ordinal))
                return affixRuntimeStates[i];
        var state = new WeaponAffixRuntimeStateData { affixId = affixId, schemaVersion = schemaVersion };
        affixRuntimeStates.Add(state);
        return state;
    }
}
