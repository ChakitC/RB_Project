using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WeaponUpgradeService : MonoBehaviour
{
    [SerializeField] private WeaponUpgradeCurve defaultUpgradeCurve;

    [Header("Dismantle Reward")]
    [SerializeField, Min(0)] private int commonBaseScrap = 10;
    [SerializeField, Min(0)] private int rareBaseScrap = 25;
    [SerializeField, Min(0)] private int epicBaseScrap = 60;
    [SerializeField, Min(0)] private int commonScrapPerLevel = 3;
    [SerializeField, Min(0)] private int rareScrapPerLevel = 6;
    [SerializeField, Min(0)] private int epicScrapPerLevel = 12;
    [SerializeField, Min(0)] private int scrapPerTier = 15;

    [Header("Dismantle Rules")]
    [SerializeField] private bool allowAssignedWeaponDismantle = false;
    [SerializeField] private bool saveAfterDismantle = true;

    public WeaponUpgradeCurve DefaultUpgradeCurve => defaultUpgradeCurve;

    public bool CanUpgrade(PlayerInventory inventory, string instanceId, out string reason)
    {
        return CanUpgrade(inventory, instanceId, defaultUpgradeCurve, out _, out reason);
    }

    public bool TryUpgrade(PlayerInventory inventory, string instanceId, out string reason)
    {
        return TryUpgrade(inventory, instanceId, defaultUpgradeCurve, out reason);
    }

    public bool CanDismantle(
        PlayerInventory inventory,
        string instanceId,
        out int scrapReward,
        out string reason)
    {
        return CanDismantle(
            inventory,
            instanceId,
            allowAssignedWeaponDismantle,
            commonBaseScrap,
            rareBaseScrap,
            epicBaseScrap,
            commonScrapPerLevel,
            rareScrapPerLevel,
            epicScrapPerLevel,
            scrapPerTier,
            out scrapReward,
            out reason);
    }

    public bool TryDismantle(
        PlayerInventory inventory,
        string instanceId,
        out int scrapGranted,
        out string reason)
    {
        return TryDismantle(
            inventory,
            instanceId,
            allowAssignedWeaponDismantle,
            saveAfterDismantle,
            commonBaseScrap,
            rareBaseScrap,
            epicBaseScrap,
            commonScrapPerLevel,
            rareScrapPerLevel,
            epicScrapPerLevel,
            scrapPerTier,
            out scrapGranted,
            out reason);
    }

    public static bool CanUpgrade(
        PlayerInventory inventory,
        string instanceId,
        WeaponUpgradeCurve fallbackCurve,
        out WeaponUpgradeResolvedCost cost,
        out string reason)
    {
        return TryBuildContext(inventory, instanceId, fallbackCurve, out _, out _, out _, out cost, out reason);
    }

    public static bool TryUpgrade(
        PlayerInventory inventory,
        string instanceId,
        WeaponUpgradeCurve fallbackCurve,
        out string reason)
    {
        if (!TryBuildContext(
                inventory,
                instanceId,
                fallbackCurve,
                out var weapon,
                out var instance,
                out var curve,
                out var cost,
                out reason))
        {
            return false;
        }

        int spentGold = 0;
        int spentScrap = 0;
        var removedMaterials = new List<WeaponUpgradeMaterialCost>();

        if (cost.goldCost > 0)
        {
            if (!inventory.SpendGold(cost.goldCost))
            {
                reason = "Not enough gold.";
                return false;
            }

            spentGold = cost.goldCost;
        }

        if (cost.scrapCost > 0)
        {
            if (!inventory.SpendScrap(cost.scrapCost))
            {
                RollbackCosts(inventory, spentGold, spentScrap, removedMaterials);
                reason = "Not enough scrap.";
                return false;
            }

            spentScrap = cost.scrapCost;
        }

        if (!RemoveMaterials(inventory, cost.materials, removedMaterials))
        {
            RollbackCosts(inventory, spentGold, spentScrap, removedMaterials);
            reason = "Not enough materials.";
            return false;
        }

        int maxLevel = curve.GetMaxLevel(instance.rarity);
        instance.upgradeLevel = Mathf.Clamp(instance.upgradeLevel + 1, 0, maxLevel);
        curve.SyncUnlockedMilestones(instance, weapon);

        inventory.NotifyWeaponInstanceChanged(instance.instanceId);
        WeaponUpgradeRuntimeRefresh.NotifyWeaponInstanceChanged(instance.instanceId);

        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveInventoryOnly();

        reason = null;
        return true;
    }

    public static bool CanDismantleWithDefaultReward(
        PlayerInventory inventory,
        string instanceId,
        out int scrapReward,
        out string reason)
    {
        return CanDismantle(
            inventory,
            instanceId,
            allowAssignedWeaponDismantle: false,
            commonBaseScrap: 10,
            rareBaseScrap: 25,
            epicBaseScrap: 60,
            commonScrapPerLevel: 3,
            rareScrapPerLevel: 6,
            epicScrapPerLevel: 12,
            scrapPerTier: 15,
            out scrapReward,
            out reason);
    }

    public static bool TryDismantleWithDefaultReward(
        PlayerInventory inventory,
        string instanceId,
        out int scrapGranted,
        out string reason)
    {
        return TryDismantle(
            inventory,
            instanceId,
            allowAssignedWeaponDismantle: false,
            saveAfterDismantle: true,
            commonBaseScrap: 10,
            rareBaseScrap: 25,
            epicBaseScrap: 60,
            commonScrapPerLevel: 3,
            rareScrapPerLevel: 6,
            epicScrapPerLevel: 12,
            scrapPerTier: 15,
            out scrapGranted,
            out reason);
    }

    static bool TryBuildContext(
        PlayerInventory inventory,
        string instanceId,
        WeaponUpgradeCurve fallbackCurve,
        out GunConfig weapon,
        out WeaponInstanceData instance,
        out WeaponUpgradeCurve curve,
        out WeaponUpgradeResolvedCost cost,
        out string reason)
    {
        weapon = null;
        instance = null;
        curve = null;
        cost = null;
        reason = null;

        if (inventory == null)
        {
            reason = "Missing inventory.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(instanceId))
        {
            reason = "Missing weapon instance id.";
            return false;
        }

        if (!inventory.TryGetWeaponInstanceWithDefinition(instanceId, out weapon, out instance) || instance == null || !weapon)
        {
            reason = "Weapon instance not found.";
            return false;
        }

        curve = ResolveUpgradeCurve(weapon, fallbackCurve);
        if (curve == null)
        {
            reason = "Missing weapon upgrade curve.";
            return false;
        }

        int maxLevel = curve.GetMaxLevel(instance.rarity);
        if (instance.upgradeLevel >= maxLevel)
        {
            reason = "Weapon upgrade level is already maxed.";
            return false;
        }

        cost = curve.GetCostForNextLevel(instance);

        if (cost.goldCost > 0 && inventory.Gold < cost.goldCost)
        {
            reason = "Not enough gold.";
            return false;
        }

        if (cost.scrapCost > 0 && inventory.Scrap < cost.scrapCost)
        {
            reason = "Not enough scrap.";
            return false;
        }

        if (!HasMaterials(inventory, cost.materials))
        {
            reason = "Not enough materials.";
            return false;
        }

        return true;
    }

    public static WeaponUpgradeCurve ResolveUpgradeCurve(GunConfig weapon, WeaponUpgradeCurve fallbackCurve)
    {
        if (weapon != null && weapon.upgradeCurve != null)
            return weapon.upgradeCurve;

        return fallbackCurve;
    }

    static bool CanDismantle(
        PlayerInventory inventory,
        string instanceId,
        bool allowAssignedWeaponDismantle,
        int commonBaseScrap,
        int rareBaseScrap,
        int epicBaseScrap,
        int commonScrapPerLevel,
        int rareScrapPerLevel,
        int epicScrapPerLevel,
        int scrapPerTier,
        out int scrapReward,
        out string reason)
    {
        scrapReward = 0;
        reason = null;

        if (inventory == null)
        {
            reason = "Missing inventory.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(instanceId))
        {
            reason = "Missing weapon instance id.";
            return false;
        }

        if (!inventory.TryGetWeaponInstanceWithDefinition(instanceId, out _, out WeaponInstanceData instance) ||
            instance == null)
        {
            reason = "Weapon instance not found.";
            return false;
        }

        if (!allowAssignedWeaponDismantle)
        {
            string assignedOwnerId = EquipmentAssignmentService.FindAssignedOwnerId(
                inventory,
                EquipmentItemKind.Weapon,
                instanceId);

            if (!string.IsNullOrWhiteSpace(assignedOwnerId))
            {
                reason = "Cannot dismantle equipped weapon.";
                return false;
            }
        }

        scrapReward = CalculateScrapReward(
            instance,
            commonBaseScrap,
            rareBaseScrap,
            epicBaseScrap,
            commonScrapPerLevel,
            rareScrapPerLevel,
            epicScrapPerLevel,
            scrapPerTier);

        return true;
    }

    static bool TryDismantle(
        PlayerInventory inventory,
        string instanceId,
        bool allowAssignedWeaponDismantle,
        bool saveAfterDismantle,
        int commonBaseScrap,
        int rareBaseScrap,
        int epicBaseScrap,
        int commonScrapPerLevel,
        int rareScrapPerLevel,
        int epicScrapPerLevel,
        int scrapPerTier,
        out int scrapGranted,
        out string reason)
    {
        scrapGranted = 0;

        if (!CanDismantle(
                inventory,
                instanceId,
                allowAssignedWeaponDismantle,
                commonBaseScrap,
                rareBaseScrap,
                epicBaseScrap,
                commonScrapPerLevel,
                rareScrapPerLevel,
                epicScrapPerLevel,
                scrapPerTier,
                out int scrapReward,
                out reason))
        {
            return false;
        }

        if (!inventory.RemoveWeaponInstance(instanceId))
        {
            reason = "Could not remove weapon instance.";
            return false;
        }

        if (scrapReward > 0)
            inventory.AddScrap(scrapReward);

        scrapGranted = scrapReward;

        if (saveAfterDismantle && SaveManager.Instance != null)
            SaveManager.Instance.SaveInventoryOnly();

        reason = null;
        return true;
    }

    static int CalculateScrapReward(
        WeaponInstanceData instance,
        int commonBaseScrap,
        int rareBaseScrap,
        int epicBaseScrap,
        int commonScrapPerLevel,
        int rareScrapPerLevel,
        int epicScrapPerLevel,
        int scrapPerTier)
    {
        if (instance == null)
            return 0;

        int baseScrap;
        int scrapPerLevel;
        switch (instance.rarity)
        {
            case WeaponRarity.Epic:
                baseScrap = epicBaseScrap;
                scrapPerLevel = epicScrapPerLevel;
                break;

            case WeaponRarity.Rare:
                baseScrap = rareBaseScrap;
                scrapPerLevel = rareScrapPerLevel;
                break;

            default:
                baseScrap = commonBaseScrap;
                scrapPerLevel = commonScrapPerLevel;
                break;
        }

        int levelBonus = Mathf.Max(0, instance.upgradeLevel) * Mathf.Max(0, scrapPerLevel);
        int tierBonus = Mathf.Max(0, instance.upgradeTier) * Mathf.Max(0, scrapPerTier);
        return Mathf.Max(0, baseScrap + levelBonus + tierBonus);
    }

    static bool HasMaterials(PlayerInventory inventory, List<WeaponUpgradeMaterialCost> materials)
    {
        if (materials == null)
            return true;

        for (int i = 0; i < materials.Count; i++)
        {
            var material = materials[i];
            if (material == null || !material.IsValid)
                continue;

            if (!inventory.HasItem(material.item, material.amount))
                return false;
        }

        return true;
    }

    static bool RemoveMaterials(
        PlayerInventory inventory,
        List<WeaponUpgradeMaterialCost> materials,
        List<WeaponUpgradeMaterialCost> removedMaterials)
    {
        if (materials == null)
            return true;

        for (int i = 0; i < materials.Count; i++)
        {
            var material = materials[i];
            if (material == null || !material.IsValid)
                continue;

            if (!inventory.RemoveItem(material.item, material.amount))
                return false;

            removedMaterials.Add(new WeaponUpgradeMaterialCost
            {
                item = material.item,
                amount = material.amount
            });
        }

        return true;
    }

    static void RollbackCosts(
        PlayerInventory inventory,
        int spentGold,
        int spentScrap,
        List<WeaponUpgradeMaterialCost> removedMaterials)
    {
        if (inventory == null)
            return;

        if (spentGold > 0)
            inventory.AddGold(spentGold);

        if (spentScrap > 0)
            inventory.AddScrap(spentScrap);

        if (removedMaterials == null)
            return;

        for (int i = 0; i < removedMaterials.Count; i++)
        {
            var material = removedMaterials[i];
            if (material == null || !material.IsValid)
                continue;

            inventory.AddItem(material.item, material.amount);
        }
    }
}

public static class WeaponUpgradeRuntimeRefresh
{
    public static void NotifyWeaponInstanceChanged(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return;

        var weaponSystems = Object.FindObjectsByType<WeaponSystem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < weaponSystems.Length; i++)
        {
            var weaponSystem = weaponSystems[i];
            if (weaponSystem == null ||
                weaponSystem.CurrentWeaponInstance == null ||
                !string.Equals(weaponSystem.CurrentWeaponInstance.instanceId, instanceId, System.StringComparison.Ordinal))
            {
                continue;
            }

            weaponSystem.NotifyWeaponInstanceChanged();
        }
    }
}
