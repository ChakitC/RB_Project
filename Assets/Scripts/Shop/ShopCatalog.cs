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

    [Header("Weapon Instance")]
    [Tooltip("Only applies when item is a GunConfig. Runtime randomizes rarity, level, and affixes when the shop opens.")]
    public bool randomizeWeaponInstance;
    [HideInInspector]
    public WeaponRarity weaponRarity = WeaponRarity.Common;
    [HideInInspector]
    [Min(0)] public int weaponUpgradeLevel;
    [HideInInspector]
    public bool rollWeaponAffixes;

    [NonSerialized] private WeaponInstanceData weaponInstanceTemplate;
    [NonSerialized] private string weaponAffixSummary;

    public bool HasLimitedStock => stock >= 0;
    public int QuantityPerPurchase => Mathf.Max(1, quantity);
    public int BuyPrice => Mathf.Max(0, buyPrice);
    public int SellPrice => Mathf.Max(0, sellPrice);
    public bool IsWeaponEntry => item is GunConfig;
    public bool ShouldRandomizeWeaponInstance => IsWeaponEntry && randomizeWeaponInstance;
    public int WeaponUpgradeLevel => Mathf.Max(0, weaponUpgradeLevel);
    public bool HasWeaponInstanceTemplate => weaponInstanceTemplate != null;

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

        string displayName = !string.IsNullOrWhiteSpace(item.displayName)
            ? item.displayName.Trim()
            : item.name;

        string weaponLabel = ResolveWeaponInstanceLabel();
        if (!string.IsNullOrWhiteSpace(weaponLabel))
            return $"{displayName} [{weaponLabel}]";

        return displayName;
    }

    public string ResolveWeaponInstanceLabel()
    {
        if (!IsWeaponEntry)
            return string.Empty;

        if (randomizeWeaponInstance && !HasWeaponInstanceTemplate)
            return "Runtime Random";

        int level = WeaponUpgradeLevel;
        return level > 0 ? $"{weaponRarity} +{level}" : weaponRarity.ToString();
    }

    public string ResolveWeaponAffixSummary()
    {
        if (!IsWeaponEntry)
            return string.Empty;

        if (HasWeaponInstanceTemplate)
        {
            return !string.IsNullOrWhiteSpace(weaponAffixSummary)
                ? $"Affinity: {weaponAffixSummary}"
                : "Affinity: None";
        }

        if (randomizeWeaponInstance)
            return "Affinity: Runtime Random";

        return rollWeaponAffixes ? "Affinity: Random" : string.Empty;
    }

    public string ResolveWeaponInlineSummary()
    {
        if (!IsWeaponEntry)
            return string.Empty;

        if (randomizeWeaponInstance && !HasWeaponInstanceTemplate)
            return "Runtime Random";

        WeaponRarity rarity = HasWeaponInstanceTemplate ? weaponInstanceTemplate.rarity : weaponRarity;
        int level = HasWeaponInstanceTemplate ? Mathf.Max(0, weaponInstanceTemplate.upgradeLevel) : WeaponUpgradeLevel;
        int affixCount = HasWeaponInstanceTemplate
            ? CountRolledAffixes(weaponInstanceTemplate)
            : ResolveExpectedAffixCount();

        string levelLabel = level > 0 ? $"{rarity} +{level}" : rarity.ToString();
        string affixLabel = affixCount == 1 ? "1 Affix" : $"{affixCount} Affixes";
        return $"{levelLabel} | {affixLabel}";
    }

    public void SetWeaponInstanceTemplate(WeaponInstanceData template, string affixSummary)
    {
        weaponInstanceTemplate = template != null ? template.DeepClone() : null;
        weaponAffixSummary = affixSummary ?? string.Empty;

        if (weaponInstanceTemplate == null)
            return;

        weaponRarity = weaponInstanceTemplate.rarity;
        weaponUpgradeLevel = Mathf.Max(0, weaponInstanceTemplate.upgradeLevel);
    }

    public WeaponInstanceData CreateWeaponInstanceFromTemplate()
    {
        if (weaponInstanceTemplate == null)
            return null;

        var clone = weaponInstanceTemplate.DeepClone();
        clone.instanceId = WeaponInstanceFactory.CreateInstanceId();
        clone.shotCounter = 0;
        return clone;
    }

    public InventorySlotData CreatePreviewSlotData()
    {
        if (item == null)
            return null;

        var preview = new InventorySlotData();
        if (item is GunConfig gun)
        {
            var instance = weaponInstanceTemplate != null
                ? weaponInstanceTemplate
                : WeaponInstanceFactory.CreatePlainInstance(gun, weaponRarity);

            if (instance == null)
                return null;

            preview.SetWeaponInstance(gun, instance);
            return preview;
        }

        preview.SetItem(item, QuantityPerPurchase);
        return preview;
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

    static int CountRolledAffixes(WeaponInstanceData instance)
    {
        if (instance == null)
            return 0;

        int count = instance.HasMainAffix ? 1 : 0;
        if (instance.subAffixes == null)
            return count;

        for (int i = 0; i < instance.subAffixes.Count; i++)
        {
            var affix = instance.subAffixes[i];
            if (affix != null && !affix.IsEmpty)
                count++;
        }

        return count;
    }

    int ResolveExpectedAffixCount()
    {
        if (!rollWeaponAffixes)
            return 0;

        return weaponRarity switch
        {
            WeaponRarity.Epic => 3,
            WeaponRarity.Rare => 2,
            _ => 1
        };
    }
}

[CreateAssetMenu(fileName = "ShopCatalog", menuName = "Game/Shop/Catalog")]
public class ShopCatalog : ShopCatalogBase
{
    public List<ShopCatalogEntry> entries = new();

    [NonSerialized] private WeaponAffixDatabase runtimeWeaponAffixDatabase;
    [NonSerialized] private WeaponUpgradeCurve runtimeFallbackUpgradeCurve;
    [NonSerialized] private int lastPrepareFrame = -1;

    public override int EntryCount => entries != null ? entries.Count : 0;

    public override void ConfigureWeaponGenerationDefaults(
        WeaponAffixDatabase affixDatabase,
        WeaponUpgradeCurve upgradeCurve)
    {
        runtimeWeaponAffixDatabase = affixDatabase;
        runtimeFallbackUpgradeCurve = upgradeCurve;
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

        if (entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
            RandomizeWeaponEntry(entries[i]);
    }

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

    void RandomizeWeaponEntry(ShopCatalogEntry entry)
    {
        if (entry == null || !entry.ShouldRandomizeWeaponInstance || !(entry.item is GunConfig gun))
            return;

        WeaponRarity rarity = RollWeaponRarity();
        int upgradeLevel = RollWeaponUpgradeLevel(gun, rarity);

        entry.weaponRarity = rarity;
        entry.weaponUpgradeLevel = upgradeLevel;
        entry.rollWeaponAffixes = true;

        var instance = WeaponInstanceFactory.CreateInstance(gun, rarity, runtimeWeaponAffixDatabase);
        WeaponInstanceFactory.ApplyUpgradeLevel(instance, gun, upgradeLevel, runtimeFallbackUpgradeCurve);

        string affixSummary = WeaponAffixDisplayUtility.BuildSummary(instance, runtimeWeaponAffixDatabase);
        entry.SetWeaponInstanceTemplate(instance, affixSummary);
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

    int RollWeaponUpgradeLevel(GunConfig gun, WeaponRarity rarity)
    {
        var curve = WeaponUpgradeService.ResolveUpgradeCurve(gun, runtimeFallbackUpgradeCurve);
        int maxLevel = curve != null ? curve.GetMaxLevel(rarity) : 10;
        return maxLevel > 0 ? UnityEngine.Random.Range(0, maxLevel + 1) : 0;
    }
}
