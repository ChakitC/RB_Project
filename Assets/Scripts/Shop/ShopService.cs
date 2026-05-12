using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ShopService : MonoBehaviour
{
    [SerializeField] private ShopCatalog defaultCatalog;
    [SerializeField] private bool saveAfterPurchase = true;

    readonly Dictionary<string, int> remainingStockByEntry = new();

    public ShopCatalog DefaultCatalog => defaultCatalog;
    public event Action<ShopCatalog, int> OnStockChanged;

    public bool CanBuy(PlayerInventory buyer, ShopCatalog catalog, int entryIndex, out string reason)
    {
        reason = string.Empty;

        catalog = ResolveCatalog(catalog);
        var entry = catalog != null ? catalog.GetEntry(entryIndex) : null;
        if (!ValidateEntry(entry, out reason))
            return false;

        if (buyer == null)
        {
            reason = "Missing player inventory.";
            return false;
        }

        int remainingStock = GetRemainingStock(catalog, entryIndex);
        if (remainingStock == 0)
        {
            reason = "Sold out.";
            return false;
        }

        if (buyer.Gold < entry.BuyPrice)
        {
            reason = "Not enough gold.";
            return false;
        }

        if (!buyer.CanAddItem(entry.item, entry.QuantityPerPurchase))
        {
            reason = "Inventory is full.";
            return false;
        }

        return true;
    }

    public bool TryBuy(PlayerInventory buyer, ShopCatalog catalog, int entryIndex, out string reason)
    {
        reason = string.Empty;

        catalog = ResolveCatalog(catalog);
        var entry = catalog != null ? catalog.GetEntry(entryIndex) : null;
        if (!ValidateEntry(entry, out reason))
            return false;

        if (!CanBuy(buyer, catalog, entryIndex, out reason))
            return false;

        int price = entry.BuyPrice;
        if (price > 0 && !buyer.SpendGold(price))
        {
            reason = "Not enough gold.";
            return false;
        }

        if (!buyer.AddItem(entry.item, entry.QuantityPerPurchase))
        {
            if (price > 0)
                buyer.AddGold(price);

            reason = "Inventory is full.";
            return false;
        }

        ConsumeStock(catalog, entryIndex, entry);

        if (saveAfterPurchase && SaveManager.Instance != null)
            SaveManager.Instance.Save();

        return true;
    }

    public int GetRemainingStock(ShopCatalog catalog, int entryIndex)
    {
        catalog = ResolveCatalog(catalog);
        var entry = catalog != null ? catalog.GetEntry(entryIndex) : null;

        if (entry == null || !entry.HasLimitedStock)
            return -1;

        string key = BuildStockKey(catalog, entryIndex, entry);
        if (!remainingStockByEntry.TryGetValue(key, out int stock))
        {
            stock = Mathf.Max(0, entry.stock);
            remainingStockByEntry[key] = stock;
        }

        return stock;
    }

    ShopCatalog ResolveCatalog(ShopCatalog catalog)
    {
        return catalog != null ? catalog : defaultCatalog;
    }

    bool ValidateEntry(ShopCatalogEntry entry, out string reason)
    {
        if (entry == null)
        {
            reason = "Missing shop item.";
            return false;
        }

        if (entry.item == null)
        {
            reason = "Missing item definition.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    void ConsumeStock(ShopCatalog catalog, int entryIndex, ShopCatalogEntry entry)
    {
        if (entry == null || !entry.HasLimitedStock)
            return;

        string key = BuildStockKey(catalog, entryIndex, entry);
        int stock = GetRemainingStock(catalog, entryIndex);
        remainingStockByEntry[key] = Mathf.Max(0, stock - 1);
        OnStockChanged?.Invoke(catalog, entryIndex);
    }

    string BuildStockKey(ShopCatalog catalog, int entryIndex, ShopCatalogEntry entry)
    {
        int catalogId = catalog != null ? catalog.GetInstanceID() : 0;
        string entryId = entry != null ? entry.ResolveRuntimeId(entryIndex) : $"entry_{entryIndex:000}";
        return $"{catalogId}:{entryId}";
    }
}
