using System.Collections.Generic;
using UnityEngine;

public sealed class AccessoryReforgeResult
{
    public string instanceId;
    public string oldModifierId;
    public string newModifierId;
    public AccessoryModifierDefinition oldModifier;
    public AccessoryModifierDefinition newModifier;
    public int goldSpent;
}

[DisallowMultipleComponent]
public class AccessoryReforgeService : MonoBehaviour
{
    [SerializeField] private AccessoryReforgeSettings settingsOverride;
    [SerializeField] private bool saveAfterReforge = true;

    public bool CanReforge(PlayerInventory inventory, string instanceId, out int goldCost, out string reason)
    {
        return CanReforge(inventory, instanceId, settingsOverride, out goldCost, out reason);
    }

    public int GetReforgeCost(PlayerInventory inventory, string instanceId)
    {
        CanReforge(inventory, instanceId, out int goldCost, out _);
        return goldCost;
    }

    public bool TryReforge(PlayerInventory inventory, string instanceId, out AccessoryReforgeResult result, out string reason)
    {
        return TryReforge(inventory, instanceId, settingsOverride, saveAfterReforge, out result, out reason);
    }

    public static bool CanReforgeWithDefaults(PlayerInventory inventory, string instanceId, out int goldCost, out string reason)
    {
        return CanReforge(inventory, instanceId, settingsOverride: null, out goldCost, out reason);
    }

    public static bool TryReforgeWithDefaults(PlayerInventory inventory, string instanceId, out AccessoryReforgeResult result, out string reason)
    {
        return TryReforge(inventory, instanceId, settingsOverride: null, saveAfterReforge: true, out result, out reason);
    }

    static bool CanReforge(
        PlayerInventory inventory,
        string instanceId,
        AccessoryReforgeSettings settingsOverride,
        out int goldCost,
        out string reason)
    {
        goldCost = 0;
        reason = null;

        if (inventory == null)
        {
            reason = "Missing inventory.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(instanceId))
        {
            reason = "Missing accessory instance id.";
            return false;
        }

        if (!inventory.TryGetAccessoryInstanceWithDefinition(instanceId, out AccessoryDefinition def, out AccessoryInstanceData instance) ||
            def == null || instance == null)
        {
            reason = "Accessory instance not found.";
            return false;
        }

        AccessoryReforgeSettings settings = settingsOverride != null ? settingsOverride : AccessoryReforgeSettings.Resolve();
        if (settings == null)
        {
            Debug.LogError("[AccessoryReforgeService] Reforge settings are missing. Check Resources/GameSettings/AccessoryReforgeSettings.");
            reason = "Reforge settings are missing.";
            return false;
        }

        List<AccessoryModifierDefinition> candidates = new();
        settings.BuildCandidates(def, instance.modifierId, candidates);
        if (candidates.Count == 0)
        {
            reason = "No other modifier available.";
            return false;
        }

        goldCost = settings.CalculateReforgeCost(def);
        if (inventory.Gold < goldCost)
        {
            reason = "Not enough gold.";
            return false;
        }

        return true;
    }

    static bool TryReforge(
        PlayerInventory inventory,
        string instanceId,
        AccessoryReforgeSettings settingsOverride,
        bool saveAfterReforge,
        out AccessoryReforgeResult result,
        out string reason)
    {
        result = null;

        if (inventory == null)
        {
            reason = "Missing inventory.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(instanceId))
        {
            reason = "Missing accessory instance id.";
            return false;
        }

        if (!inventory.TryGetAccessoryInstanceWithDefinition(instanceId, out AccessoryDefinition def, out AccessoryInstanceData instance) ||
            def == null || instance == null)
        {
            reason = "Accessory instance not found.";
            return false;
        }

        AccessoryReforgeSettings settings = settingsOverride != null ? settingsOverride : AccessoryReforgeSettings.Resolve();
        if (settings == null)
        {
            Debug.LogError("[AccessoryReforgeService] Reforge settings are missing. Check Resources/GameSettings/AccessoryReforgeSettings.");
            reason = "Reforge settings are missing.";
            return false;
        }

        List<AccessoryModifierDefinition> candidates = new();
        settings.BuildCandidates(def, instance.modifierId, candidates);
        if (candidates.Count == 0)
        {
            reason = "No other modifier available.";
            return false;
        }

        int goldCost = settings.CalculateReforgeCost(def);
        if (inventory.Gold < goldCost)
        {
            reason = "Not enough gold.";
            return false;
        }

        AccessoryModifierDefinition newModifier = settings.RollFromCandidates(candidates);
        if (newModifier == null)
        {
            reason = "No other modifier available.";
            return false;
        }

        if (goldCost > 0 && !inventory.SpendGold(goldCost))
        {
            reason = "Not enough gold.";
            return false;
        }

        string oldModifierId = instance.modifierId;
        AccessoryModifierDefinition oldModifier = def.GetModifierById(oldModifierId);
        instance.modifierId = newModifier.RuntimeId;

        inventory.NotifyAccessoryInstanceChanged(instanceId);
        AccessoryLoadout.SyncPersistedInstanceModifier(instanceId, newModifier.RuntimeId);

        if (saveAfterReforge)
            SaveManager.Instance?.SaveInventoryAndAccessories();

        result = new AccessoryReforgeResult
        {
            instanceId = instanceId,
            oldModifierId = oldModifierId,
            newModifierId = newModifier.RuntimeId,
            oldModifier = oldModifier,
            newModifier = newModifier,
            goldSpent = goldCost
        };
        reason = null;
        return true;
    }
}
