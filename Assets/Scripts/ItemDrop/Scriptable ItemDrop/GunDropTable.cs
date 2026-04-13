using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public class GunDropEntry
{
    public GunConfig weapon;
    public float weight = 1f;
}

[CreateAssetMenu(menuName = "Game/Drop/Gun Drop Table")]
public class GunDropTable : ScriptableObject
{
    public List<GunDropEntry> entries = new List<GunDropEntry>();

    public GunConfig GetRandomWeapon()
    {
        if (entries == null || entries.Count == 0)
            return null;

        float total = 0f;
        foreach (var entry in entries)
            total += entry != null ? entry.weight : 0f;

        if (total <= 0f)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].weapon != null)
                    return entries[i].weapon;
            }

            return null;
        }

        float roll = Random.value * total;
        float cumulative = 0f;

        foreach (var entry in entries)
        {
            if (entry == null || entry.weapon == null)
                continue;

            cumulative += entry.weight;
            if (roll <= cumulative)
                return entry.weapon;
        }

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (entry != null && entry.weapon != null)
                return entry.weapon;
        }

        return null;
    }
}
