using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ShopService : MonoBehaviour
{
    [SerializeField] private ShopCatalogBase defaultCatalog;
    [SerializeField] private bool saveAfterPurchase = true;

    [Header("Weapon Instance")]
    [SerializeField] private WeaponAffixDatabase weaponAffixDatabase;
    [SerializeField] private WeaponUpgradeCurve fallbackUpgradeCurve;

    readonly Dictionary<string, int> remainingStockByEntry = new();

    public ShopCatalogBase DefaultCatalog => defaultCatalog;
    public WeaponAffixDatabase WeaponAffixDatabase => weaponAffixDatabase;
    public WeaponUpgradeCurve FallbackUpgradeCurve => fallbackUpgradeCurve;
    public event Action<ShopCatalogBase, int> OnStockChanged;

    public bool CanBuy(PlayerInventory buyer, ShopCatalogBase catalog, int entryIndex, out string reason)
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

        if (!CanAddPurchase(buyer, entry, out reason))
            return false;

        return true;
    }

    public bool TryBuy(PlayerInventory buyer, ShopCatalogBase catalog, int entryIndex, out string reason)
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

        if (!TryAddPurchase(buyer, entry, out reason))
        {
            if (price > 0)
                buyer.AddGold(price);

            return false;
        }

        ConsumeStock(catalog, entryIndex, entry);

        if (saveAfterPurchase && SaveManager.Instance != null)
            SaveManager.Instance.Save();

        return true;
    }

    public int GetRemainingStock(ShopCatalogBase catalog, int entryIndex)
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

    ShopCatalogBase ResolveCatalog(ShopCatalogBase catalog)
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

    bool CanAddPurchase(PlayerInventory buyer, ShopCatalogEntry entry, out string reason)
    {
        reason = string.Empty;

        if (buyer == null || entry == null || entry.item == null)
        {
            reason = "Missing shop item.";
            return false;
        }

        if (entry.item is GunConfig gun)
        {
            if (!buyer.CanAddWeaponInstance(gun, entry.QuantityPerPurchase))
            {
                reason = "Inventory is full.";
                return false;
            }

            return true;
        }

        if (!buyer.CanAddItem(entry.item, entry.QuantityPerPurchase))
        {
            reason = "Inventory is full.";
            return false;
        }

        return true;
    }

    bool TryAddPurchase(PlayerInventory buyer, ShopCatalogEntry entry, out string reason)
    {
        reason = string.Empty;

        if (buyer == null || entry == null || entry.item == null)
        {
            reason = "Missing shop item.";
            return false;
        }

        if (entry.item is GunConfig gun)
            return TryAddWeaponPurchase(buyer, entry, gun, out reason);

        if (!buyer.AddItem(entry.item, entry.QuantityPerPurchase))
        {
            reason = "Inventory is full.";
            return false;
        }

        return true;
    }

    bool TryAddWeaponPurchase(PlayerInventory buyer, ShopCatalogEntry entry, GunConfig gun, out string reason)
    {
        reason = string.Empty;

        var addedInstanceIds = new List<string>();
        int count = entry.QuantityPerPurchase;

        for (int i = 0; i < count; i++)
        {
            var instance = CreateShopWeaponInstance(gun, entry);
            if (instance == null)
            {
                RollbackAddedWeaponInstances(buyer, addedInstanceIds);
                reason = "Could not create weapon instance.";
                return false;
            }

            if (!buyer.AddWeaponInstance(instance))
            {
                RollbackAddedWeaponInstances(buyer, addedInstanceIds);
                reason = "Could not add weapon to inventory.";
                return false;
            }

            addedInstanceIds.Add(instance.instanceId);
        }

        return true;
    }

    WeaponInstanceData CreateShopWeaponInstance(GunConfig gun, ShopCatalogEntry entry)
    {
        if (!gun || entry == null)
            return null;

        var templatedInstance = entry.CreateWeaponInstanceFromTemplate();
        if (templatedInstance != null)
            return templatedInstance;

        if (entry.ShouldRandomizeWeaponInstance)
            return CreateRandomWeaponInstance(gun);

        WeaponInstanceData instance = entry.rollWeaponAffixes
            ? WeaponInstanceFactory.CreateInstance(gun, entry.weaponRarity, weaponAffixDatabase)
            : WeaponInstanceFactory.CreatePlainInstance(gun, entry.weaponRarity);

        WeaponInstanceFactory.ApplyUpgradeLevel(instance, gun, entry.WeaponUpgradeLevel, fallbackUpgradeCurve);
        return instance;
    }

    WeaponInstanceData CreateRandomWeaponInstance(GunConfig gun)
    {
        if (!gun)
            return null;

        WeaponRarity rarity = RollWeaponRarity();
        var instance = WeaponInstanceFactory.CreateInstance(gun, rarity, weaponAffixDatabase);

        var curve = WeaponUpgradeService.ResolveUpgradeCurve(gun, fallbackUpgradeCurve);
        int maxLevel = curve != null ? curve.GetMaxLevel(rarity) : 10;
        int upgradeLevel = maxLevel > 0 ? UnityEngine.Random.Range(0, maxLevel + 1) : 0;
        WeaponInstanceFactory.ApplyUpgradeLevel(instance, gun, upgradeLevel, fallbackUpgradeCurve);
        return instance;
    }

    static WeaponRarity RollWeaponRarity()
    {
        float roll = UnityEngine.Random.value;
        if (roll >= 0.9f)
            return WeaponRarity.Epic;

        if (roll >= 0.65f)
            return WeaponRarity.Rare;

        return WeaponRarity.Common;
    }

    void RollbackAddedWeaponInstances(PlayerInventory buyer, List<string> instanceIds)
    {
        if (buyer == null || instanceIds == null)
            return;

        for (int i = 0; i < instanceIds.Count; i++)
            buyer.RemoveWeaponInstance(instanceIds[i]);
    }

    void ConsumeStock(ShopCatalogBase catalog, int entryIndex, ShopCatalogEntry entry)
    {
        if (entry == null || !entry.HasLimitedStock)
            return;

        string key = BuildStockKey(catalog, entryIndex, entry);
        int stock = GetRemainingStock(catalog, entryIndex);
        remainingStockByEntry[key] = Mathf.Max(0, stock - 1);
        OnStockChanged?.Invoke(catalog, entryIndex);
    }

    string BuildStockKey(ShopCatalogBase catalog, int entryIndex, ShopCatalogEntry entry)
    {
        int catalogId = catalog != null ? catalog.GetInstanceID() : 0;
        string entryId = catalog != null
            ? catalog.ResolveEntryRuntimeId(entryIndex, entry)
            : entry != null
                ? entry.ResolveRuntimeId(entryIndex)
                : $"entry_{entryIndex:000}";

        return $"{catalogId}:{entryId}";
    }
}
