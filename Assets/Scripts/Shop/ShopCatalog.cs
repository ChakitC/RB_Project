using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ShopCatalogEntry
{
    [Header("Identity")]
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

        return $"entry_{Mathf.Max(0, fallbackIndex):000}";
    }

    public string ResolveDisplayName()
    {
        if (item == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(item.displayName))
            return item.displayName.Trim();

        return item.name;
    }
}

[CreateAssetMenu(fileName = "ShopCatalog", menuName = "Game/Shop/Catalog")]
public class ShopCatalog : ScriptableObject
{
    public List<ShopCatalogEntry> entries = new();

    public int EntryCount => entries != null ? entries.Count : 0;

    public ShopCatalogEntry GetEntry(int index)
    {
        if (entries == null || index < 0 || index >= entries.Count)
            return null;

        return entries[index];
    }
}
