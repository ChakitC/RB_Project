using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ShopCatalogEntry
{
    [Header("Identity")]
    [Tooltip("Optional legacy override. Leave empty to use an auto runtime id.")]
    public string entryId;

    [Header("Item")]
    public ItemDefinition item;
    [Min(1)] public int quantity = 1;

    [Header("Price")]
    [Min(0)] public int buyPrice;
    [Min(0)] public int sellPrice;

    [Header("Stock")]
    [Tooltip("-1 means unlimited. 0 means sold out.")]
    public int stock = -1;

    public bool HasLimitedStock => stock >= 0;
    public int QuantityPerPurchase => Mathf.Max(1, quantity);
    public int BuyPrice => Mathf.Max(0, buyPrice);
    public int SellPrice => Mathf.Max(0, sellPrice);

    public string ResolveRuntimeId(int fallbackIndex)
    {
        if (!string.IsNullOrWhiteSpace(entryId))
            return entryId.Trim();

        return BuildAutoRuntimeId("entry", 0, fallbackIndex);
    }

    public string BuildAutoRuntimeId(string scope, int generation, int index)
    {
        string scopePart = NormalizeRuntimeIdPart(scope, "entry");
        string itemPart = ResolveRuntimeItemIdPart();
        int safeIndex = Mathf.Max(0, index);

        if (generation > 0)
            return $"{scopePart}_{Mathf.Max(0, generation):000}_{safeIndex:000}_{itemPart}";

        return $"{scopePart}_{safeIndex:000}_{itemPart}";
    }

    public string ResolveDisplayName()
    {
        if (item == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(item.displayName))
            return item.displayName.Trim();

        return item.name;
    }

    string ResolveRuntimeItemIdPart()
    {
        if (item == null)
            return "missing_item";

        if (!string.IsNullOrWhiteSpace(item.itemId))
            return NormalizeRuntimeIdPart(item.itemId, "item");

        return NormalizeRuntimeIdPart(item.name, "item");
    }

    static string NormalizeRuntimeIdPart(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        char[] chars = value.Trim().ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = '_';
        }

        string normalized = new string(chars).Trim('_');
        return string.IsNullOrEmpty(normalized) ? fallback : normalized;
    }
}

[CreateAssetMenu(fileName = "ShopCatalog", menuName = "Game/Shop/Catalog")]
public class ShopCatalog : ShopCatalogBase
{
    public List<ShopCatalogEntry> entries = new();

    public override int EntryCount => entries != null ? entries.Count : 0;

    public override ShopCatalogEntry GetEntry(int index)
    {
        if (entries == null || index < 0 || index >= entries.Count)
            return null;

        return entries[index];
    }

    public override string ResolveEntryRuntimeId(int index, ShopCatalogEntry entry)
    {
        if (entry == null)
            return $"missing_{Mathf.Max(0, index):000}";

        if (!string.IsNullOrWhiteSpace(entry.entryId))
            return entry.entryId.Trim();

        return entry.BuildAutoRuntimeId("manual", 0, index);
    }
}
