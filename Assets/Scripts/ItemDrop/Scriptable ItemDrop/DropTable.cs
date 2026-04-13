using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;

public enum DropEntryContentType
{
    Item,
    GunTable
}

[Serializable]
public class DropEntry
{
    bool IsItemEntry => contentType == DropEntryContentType.Item;
    bool IsGunTableEntry => contentType == DropEntryContentType.GunTable;

    [LabelText("Source Type")]
    public DropEntryContentType contentType = DropEntryContentType.Item;

    [ShowIf(nameof(IsItemEntry))]
    public ItemDefinition item;

    [ShowIf(nameof(IsGunTableEntry))]
    public GunDropTable gunTable;

    [Min(1)]
    public int amount = 1;

    public float weight = 1f;

    public bool TryResolveItem(out ItemDefinition resolvedItem)
    {
        resolvedItem = contentType switch
        {
            DropEntryContentType.GunTable => gunTable != null ? gunTable.GetRandomWeapon() : null,
            _ => item
        };

        return resolvedItem != null;
    }

    public int ResolveAmount()
    {
        return Mathf.Max(1, amount);
    }
}

[CreateAssetMenu(menuName = "Game/Drop/Drop Table")]
public class DropTable : ScriptableObject
{
    public List<DropEntry> entries = new List<DropEntry>();

    public DropEntry GetRandomEntry()
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
                if (entries[i] != null)
                    return entries[i];
            }

            return null;
        }

        float roll = Random.value * total;
        float cumulative = 0f;

        foreach (var entry in entries)
        {
            if (entry == null)
                continue;

            cumulative += entry.weight;
            if (roll <= cumulative)
                return entry;
        }

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i] != null)
                return entries[i];
        }

        return null;
    }
}
