using System;
using System.Collections.Generic;
using UnityEngine;

public static class AccessoryInstanceFactory
{
    public static AccessoryInstanceData CreatePlainInstance(AccessoryDefinition accessory)
    {
        if (!accessory)
            return null;

        return new AccessoryInstanceData
        {
            instanceId = CreateInstanceId(),
            accessoryId = ResolveAccessoryId(accessory),
            modifierId = null,
            upgradeLevel = 0
        };
    }

    // The Global Modifier Pool (AccessoryReforgeSettings) is the source of truth for new-instance
    // rolls; `rollModifierOnCreate` and the per-accessory `modifierPool` survive only to resolve
    // legacy modifier ids (see AccessoryDefinition.GetModifierById).
    public static AccessoryInstanceData CreateInstance(AccessoryDefinition accessory)
    {
        AccessoryInstanceData instance = CreatePlainInstance(accessory);
        if (instance == null)
            return null;

        AccessoryReforgeSettings settings = AccessoryReforgeSettings.Resolve();
        List<AccessoryModifierDefinition> candidates = new();
        settings?.BuildCandidates(accessory, excludeModifierId: null, candidates);

        AccessoryModifierDefinition modifier = settings != null ? settings.RollFromCandidates(candidates) : null;
        if (modifier == null)
        {
            Debug.LogError($"[AccessoryInstanceFactory] No reforge modifier candidates for '{accessory.name}'. Check Resources/GameSettings/AccessoryReforgeSettings.");
            return instance;
        }

        instance.modifierId = modifier.RuntimeId;
        return instance;
    }

    public static string CreateInstanceId()
    {
        return $"acc_{Guid.NewGuid():N}";
    }

    public static string ResolveAccessoryId(AccessoryDefinition accessory)
    {
        if (!accessory)
            return null;

        return !string.IsNullOrWhiteSpace(accessory.itemId)
            ? accessory.itemId
            : accessory.name;
    }
}

