using System;
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

    public static AccessoryInstanceData CreateInstance(AccessoryDefinition accessory)
    {
        AccessoryInstanceData instance = CreatePlainInstance(accessory);
        if (instance == null)
            return null;

        if (accessory.rollModifierOnCreate)
        {
            AccessoryModifierDefinition modifier = RollModifier(accessory);
            if (modifier != null)
                instance.modifierId = modifier.RuntimeId;
        }

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

    static AccessoryModifierDefinition RollModifier(AccessoryDefinition accessory)
    {
        if (!accessory || accessory.modifierPool == null || accessory.modifierPool.Count == 0)
            return null;

        float totalWeight = Mathf.Max(0f, accessory.noModifierWeight);
        for (int i = 0; i < accessory.modifierPool.Count; i++)
        {
            AccessoryModifierDefinition modifier = accessory.modifierPool[i];
            if (modifier == null)
                continue;

            totalWeight += Mathf.Max(0f, modifier.weight);
        }

        if (totalWeight <= 0f)
            return null;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        if (roll < Mathf.Max(0f, accessory.noModifierWeight))
            return null;

        roll -= Mathf.Max(0f, accessory.noModifierWeight);

        for (int i = 0; i < accessory.modifierPool.Count; i++)
        {
            AccessoryModifierDefinition modifier = accessory.modifierPool[i];
            if (modifier == null)
                continue;

            roll -= Mathf.Max(0f, modifier.weight);
            if (roll <= 0f)
                return modifier;
        }

        return null;
    }
}

