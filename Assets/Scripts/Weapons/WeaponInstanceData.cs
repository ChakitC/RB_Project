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
            currentMagazine = currentMagazine
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

        return clone;
    }
}
