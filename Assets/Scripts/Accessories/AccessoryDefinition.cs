using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAccessory", menuName = "Game/Accessory")]
public class AccessoryDefinition : ItemDefinition
{
    [Header("Accessory Tags")]
    public List<string> tags = new();

    [Header("Effects")]
    public List<PassiveStatModifier> statModifiers = new();
    public List<PassiveDefinition> passives = new();

    [Header("Modifier Roll")]
    public bool rollModifierOnCreate;
    [Min(0f)] public float noModifierWeight = 1f;
    public List<AccessoryModifierDefinition> modifierPool = new();

    public string RuntimeId => string.IsNullOrWhiteSpace(itemId) ? name : itemId;

    void OnValidate()
    {
        itemType = ItemType.Accessory;
        stackable = false;
        maxStack = 1;
    }

    public bool CanEquip(CharacteContext ctx)
    {
        return true;
    }

    public AccessoryModifierDefinition GetModifierById(string modifierId)
    {
        if (string.IsNullOrWhiteSpace(modifierId) || modifierPool == null)
            return null;

        for (int i = 0; i < modifierPool.Count; i++)
        {
            AccessoryModifierDefinition modifier = modifierPool[i];
            if (modifier == null)
                continue;

            if (string.Equals(modifier.RuntimeId, modifierId, StringComparison.Ordinal))
                return modifier;
        }

        return null;
    }
}

