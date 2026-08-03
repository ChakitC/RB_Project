using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RandomShopCatalog", menuName = "Game/Shop/Random Catalog")]
public class RandomShopCatalog : ShopCatalogBase
{
    [Header("Random Source")]
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private bool refreshRandomOnOpen;
    [SerializeField] private bool excludeGoldItem = true;
    [SerializeField, Min(0)] private int randomEntryCount = 5;
    [SerializeField] private bool allowDuplicateItems;
    [SerializeField] private List<ItemType> allowedItemTypes = new();

    [Header("Random Quantity")]
    [SerializeField, Min(1)] private int minQuantity = 1;
    [SerializeField, Min(1)] private int maxQuantity = 1;

    [Header("Random Price")]
    [SerializeField] private bool useItemBasePrice = true;
    [SerializeField, Min(0)] private int fallbackBuyPrice;
    [SerializeField, Min(0)] private int fallbackSellPrice;

    [Header("Random Stock")]
    [Tooltip("-1 means unlimited. 0 means sold out.")]
    [SerializeField, Min(-1)] private int minStock = -1;
    [Tooltip("-1 means unlimited. 0 means sold out.")]
    [SerializeField, Min(-1)] private int maxStock = -1;

    [Header("Random Weapon Instance")]
    [SerializeField] private WeaponAffixDatabase weaponAffixDatabase;
    [SerializeField] private WeaponUpgradeCurve fallbackUpgradeCurve;
    [SerializeField] private RarityTable weaponRarityTable;
    [SerializeField, Min(0)] private int minWeaponUpgradeLevel = 0;
    [SerializeField, Min(0)] private int maxWeaponUpgradeLevel = 0;
    [SerializeField] private bool randomWeaponRollAffixes = true;

    [NonSerialized] private List<ShopCatalogEntry> runtimeEntries;
    [NonSerialized] private WeaponAffixDatabase runtimeWeaponAffixDatabase;
    [NonSerialized] private WeaponUpgradeCurve runtimeFallbackUpgradeCurve;
    [NonSerialized] private int runtimeGeneration;
    [NonSerialized] private int lastPrepareFrame = -1;
    [NonSerialized] private int testStageIndex;

    public override int EntryCount => RuntimeEntries.Count;
    public int RuntimeGeneration => runtimeGeneration;
    public bool RefreshRandomOnOpen => refreshRandomOnOpen;

    List<ShopCatalogEntry> RuntimeEntries
    {
        get
        {
            runtimeEntries ??= new List<ShopCatalogEntry>();
            return runtimeEntries;
        }
    }

    void OnEnable()
    {
        RuntimeEntries.Clear();
        runtimeGeneration = 0;
        lastPrepareFrame = -1;
    }

    public override void ConfigureWeaponGenerationDefaults(
        WeaponAffixDatabase affixDatabase,
        WeaponUpgradeCurve upgradeCurve)
    {
        runtimeWeaponAffixDatabase = affixDatabase;
        runtimeFallbackUpgradeCurve = upgradeCurve;
    }

    public void ConfigureForTestStage(MapRunConfigSO config)
    {
        int resolvedStageIndex = ResolveTestStageIndex(config);
        if (testStageIndex == resolvedStageIndex)
            return;

        testStageIndex = resolvedStageIndex;
        RuntimeEntries.Clear();
        runtimeGeneration = 0;
    }

    void OnValidate()
    {
        randomEntryCount = Mathf.Max(0, randomEntryCount);
        minQuantity = Mathf.Max(1, minQuantity);
        maxQuantity = Mathf.Max(minQuantity, maxQuantity);
        minStock = Mathf.Max(-1, minStock);
        maxStock = Mathf.Max(minStock, maxStock);
        minWeaponUpgradeLevel = Mathf.Max(0, minWeaponUpgradeLevel);
        maxWeaponUpgradeLevel = Mathf.Max(minWeaponUpgradeLevel, maxWeaponUpgradeLevel);
    }

    public override void PrepareForOpen()
    {
        if (Application.isPlaying)
        {
            int currentFrame = Time.frameCount;
            if (lastPrepareFrame == currentFrame)
                return;

            lastPrepareFrame = currentFrame;
        }

        if (RuntimeEntries.Count == 0 || refreshRandomOnOpen)
            RebuildRandomEntries();
    }

    public override ShopCatalogEntry GetEntry(int index)
    {
        var entries = RuntimeEntries;
        if (index < 0 || index >= entries.Count)
            return null;

        return entries[index];
    }

    public override string ResolveEntryRuntimeId(int index, ShopCatalogEntry entry)
    {
        if (entry == null)
            return $"missing_{Mathf.Max(0, index):000}";

        if (!string.IsNullOrWhiteSpace(entry.entryId))
            return entry.entryId.Trim();

        return entry.BuildAutoRuntimeId("random", runtimeGeneration, index);
    }

    void RebuildRandomEntries()
    {
        RuntimeEntries.Clear();
        runtimeGeneration++;

        if (itemDatabase == null || itemDatabase.items == null || randomEntryCount <= 0)
            return;

        List<ItemDefinition> candidates = BuildRandomCandidates();
        int targetCount = allowDuplicateItems ? randomEntryCount : Mathf.Min(randomEntryCount, candidates.Count);

        for (int i = 0; i < targetCount; i++)
        {
            if (candidates.Count == 0)
                break;

            int candidateIndex = UnityEngine.Random.Range(0, candidates.Count);
            ItemDefinition item = candidates[candidateIndex];

            if (!allowDuplicateItems)
                candidates.RemoveAt(candidateIndex);

            RuntimeEntries.Add(CreateRandomEntry(item));
        }
    }

    List<ItemDefinition> BuildRandomCandidates()
    {
        var candidates = new List<ItemDefinition>();

        foreach (var item in itemDatabase.items)
        {
            if (!IsRandomCandidate(item))
                continue;

            candidates.Add(item);
        }

        return candidates;
    }

    bool IsRandomCandidate(ItemDefinition item)
    {
        if (item == null)
            return false;

        if (excludeGoldItem &&
            itemDatabase != null &&
            (item == itemDatabase.goldItem || item == itemDatabase.scrapItem))
        {
            return false;
        }

        return AllowsItemType(item.itemType);
    }

    bool AllowsItemType(ItemType itemType)
    {
        if (allowedItemTypes == null || allowedItemTypes.Count == 0)
            return true;

        return allowedItemTypes.Contains(itemType);
    }

    ShopCatalogEntry CreateRandomEntry(ItemDefinition item)
    {
        var entry = new ShopCatalogEntry
        {
            entryId = string.Empty,
            item = item,
            quantity = RollInclusive(minQuantity, maxQuantity),
            buyPrice = ResolveRandomBuyPrice(item),
            sellPrice = ResolveRandomSellPrice(item),
            stock = RollInclusive(minStock, maxStock)
        };

        ApplyRandomWeaponInstance(entry, item);
        return entry;
    }

    void ApplyRandomWeaponInstance(ShopCatalogEntry entry, ItemDefinition item)
    {
        if (entry == null || !(item is GunConfig))
            return;

        var gun = item as GunConfig;
        var affixDatabase = ResolveWeaponAffixDatabase();
        var upgradeCurve = ResolveFallbackUpgradeCurve();
        WeaponRarity rarity = RollRandomWeaponRarity();
        int rarityCap = upgradeCurve != null ? upgradeCurve.GetMaxLevel(rarity) : int.MaxValue;
        int minimumLevel = Mathf.Min(ResolveMinimumWeaponLevel(), rarityCap);
        int maximumLevel = Mathf.Min(ResolveMaximumWeaponLevel(), rarityCap);
        int upgradeLevel = RollInclusive(minimumLevel, maximumLevel);

        entry.weaponRarity = rarity;
        entry.weaponUpgradeLevel = upgradeLevel;
        entry.rollWeaponAffixes = randomWeaponRollAffixes;

        var instance = randomWeaponRollAffixes
            ? WeaponInstanceFactory.CreateInstance(gun, rarity, affixDatabase)
            : WeaponInstanceFactory.CreatePlainInstance(gun, rarity);

        WeaponInstanceFactory.ApplyUpgradeLevel(instance, gun, upgradeLevel, upgradeCurve);

        string affixSummary = WeaponAffixDisplayUtility.BuildSummary(instance, affixDatabase);
        entry.SetWeaponInstanceTemplate(instance, affixSummary);
        entry.buyPrice = ResolveWeaponBuyPrice(gun, rarity, upgradeLevel, upgradeCurve);
        entry.sellPrice = Mathf.RoundToInt(entry.buyPrice * 0.3f);
    }

    WeaponRarity RollRandomWeaponRarity()
    {
        if (testStageIndex > 0)
        {
            float roll = UnityEngine.Random.value;
            return testStageIndex switch
            {
                1 => roll < 0.8f ? WeaponRarity.Common : WeaponRarity.Rare,
                2 => roll < 0.45f ? WeaponRarity.Common : roll < 0.9f ? WeaponRarity.Rare : WeaponRarity.Epic,
                _ => roll < 0.2f ? WeaponRarity.Common : roll < 0.7f ? WeaponRarity.Rare : WeaponRarity.Epic,
            };
        }

        if (weaponRarityTable == null ||
            weaponRarityTable.entries == null ||
            weaponRarityTable.entries.Count == 0)
        {
            return WeaponRarity.Common;
        }

        ItemRarity itemRarity = weaponRarityTable != null
            ? weaponRarityTable.RollRarity()
            : ItemRarity.Common;

        return WeaponRarityUtility.FromItemRarity(itemRarity);
    }

    int ResolveMinimumWeaponLevel()
    {
        return testStageIndex switch
        {
            1 => 0,
            2 => 5,
            3 => 10,
            _ => minWeaponUpgradeLevel,
        };
    }

    int ResolveMaximumWeaponLevel()
    {
        return testStageIndex switch
        {
            1 => 5,
            2 => 15,
            3 => 25,
            _ => maxWeaponUpgradeLevel,
        };
    }

    static int ResolveTestStageIndex(MapRunConfigSO config)
    {
        if (config == null || !config.IsTestStage)
            return 0;

        if (config.StartLevel <= 1)
            return 1;
        if (config.StartLevel <= 11)
            return 2;
        return 3;
    }

    static int ResolveWeaponBuyPrice(
        GunConfig weapon,
        WeaponRarity rarity,
        int upgradeLevel,
        WeaponUpgradeCurve upgradeCurve)
    {
        int basePrice = weapon != null && weapon.baseBuyPrice > 0 ? weapon.baseBuyPrice : 300;
        int rarityPremium = rarity switch
        {
            WeaponRarity.Rare => 150,
            WeaponRarity.Epic => 350,
            _ => 0,
        };

        int upgradeGold = 0;
        if (upgradeCurve != null)
        {
            for (int level = 1; level <= Mathf.Max(0, upgradeLevel); level++)
                upgradeGold += upgradeCurve.GetCostForTargetLevel(level).goldCost;
        }

        return Mathf.Max(0, basePrice + rarityPremium + Mathf.RoundToInt(upgradeGold * 0.5f));
    }

    WeaponAffixDatabase ResolveWeaponAffixDatabase()
    {
        return weaponAffixDatabase != null ? weaponAffixDatabase : runtimeWeaponAffixDatabase;
    }

    WeaponUpgradeCurve ResolveFallbackUpgradeCurve()
    {
        return fallbackUpgradeCurve != null ? fallbackUpgradeCurve : runtimeFallbackUpgradeCurve;
    }

    int ResolveRandomBuyPrice(ItemDefinition item)
    {
        if (useItemBasePrice && item != null && item.baseBuyPrice > 0)
            return item.baseBuyPrice;

        return Mathf.Max(0, fallbackBuyPrice);
    }

    int ResolveRandomSellPrice(ItemDefinition item)
    {
        if (useItemBasePrice && item != null && item.baseSellPrice > 0)
            return item.baseSellPrice;

        return Mathf.Max(0, fallbackSellPrice);
    }

    int RollInclusive(int minValue, int maxValue)
    {
        if (maxValue < minValue)
            maxValue = minValue;

        return UnityEngine.Random.Range(minValue, maxValue + 1);
    }
}
