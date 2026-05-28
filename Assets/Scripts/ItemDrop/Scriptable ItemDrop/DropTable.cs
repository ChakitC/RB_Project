using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;

public enum DropEntryContentType
{
    Item,
    GunTable,
    AccessoryTable
}

[Serializable]
public class DropEntry
{
    const int MaxNestedTableDepth = 8;

    bool IsItemEntry => contentType == DropEntryContentType.Item;
    bool IsGunTableEntry => contentType == DropEntryContentType.GunTable;
    bool IsAccessoryTableEntry => contentType == DropEntryContentType.AccessoryTable;

    [LabelText("Source Type")]
    public DropEntryContentType contentType = DropEntryContentType.Item;

    [ShowIf(nameof(IsItemEntry))]
    public ItemDefinition item;

    [ShowIf(nameof(IsGunTableEntry))]
    public GunDropTable gunTable;

    [ShowIf(nameof(IsAccessoryTableEntry))]
    [LabelText("Accessory Table")]
    public DropTable accessoryTable;

    [Min(1)]
    public int amount = 1;

    [PropertyTooltip("Relative roll weight inside this DropTable. Chance = this weight / total positive weights. Example: weights 1, 3, 6 become 10%, 30%, 60%. If total weight is 0 or less, the first non-null entry is used.")]
    public float weight = 1f;

    public bool TryResolveItem(out ItemDefinition resolvedItem)
    {
        return TryResolveItem(out resolvedItem, 0);
    }

    bool TryResolveItem(out ItemDefinition resolvedItem, int nestedDepth)
    {
        resolvedItem = contentType switch
        {
            DropEntryContentType.GunTable => gunTable != null ? gunTable.GetRandomWeapon() : null,
            DropEntryContentType.AccessoryTable => TryResolveAccessoryTableItem(out ItemDefinition accessoryItem, nestedDepth)
                ? accessoryItem
                : null,
            _ => item
        };

        return resolvedItem != null;
    }

    public int ResolveAmount()
    {
        return Mathf.Max(1, amount);
    }

    bool TryResolveAccessoryTableItem(out ItemDefinition resolvedItem, int nestedDepth)
    {
        resolvedItem = null;

        if (accessoryTable == null || nestedDepth >= MaxNestedTableDepth)
            return false;

        DropEntry accessoryEntry = accessoryTable.GetRandomEntry();
        if (accessoryEntry == null || !accessoryEntry.TryResolveItem(out ItemDefinition candidate, nestedDepth + 1))
            return false;

        if (candidate is AccessoryDefinition)
        {
            resolvedItem = candidate;
            return true;
        }

        Debug.LogWarning($"[DropTable] Accessory Table resolved non-accessory item '{candidate.name}'.");
        return false;
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
