using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WeaponUpgradeService : MonoBehaviour
{
    [SerializeField] private WeaponUpgradeCurve defaultUpgradeCurve;

    public WeaponUpgradeCurve DefaultUpgradeCurve => defaultUpgradeCurve;

    public bool CanUpgrade(PlayerInventory inventory, string instanceId, out string reason)
    {
        return CanUpgrade(inventory, instanceId, defaultUpgradeCurve, out _, out reason);
    }

    public bool TryUpgrade(PlayerInventory inventory, string instanceId, out string reason)
    {
        return TryUpgrade(inventory, instanceId, defaultUpgradeCurve, out reason);
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

        if (!RemoveMaterials(inventory, cost.materials, removedMaterials))
        {
            RollbackCosts(inventory, spentGold, removedMaterials);
            reason = "Not enough materials.";
            return false;
        }

        int maxLevel = curve.GetMaxLevel(instance.rarity);
        instance.upgradeLevel = Mathf.Clamp(instance.upgradeLevel + 1, 0, maxLevel);
        curve.SyncUnlockedMilestones(instance, weapon);

        inventory.NotifyWeaponInstanceChanged(instance.instanceId);
        WeaponUpgradeRuntimeRefresh.NotifyWeaponInstanceChanged(instance.instanceId);

        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();

        reason = null;
        return true;
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
        List<WeaponUpgradeMaterialCost> removedMaterials)
    {
        if (inventory == null)
            return;

        if (spentGold > 0)
            inventory.AddGold(spentGold);

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
