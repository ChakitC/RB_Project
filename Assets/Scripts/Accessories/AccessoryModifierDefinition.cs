using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Accessories/Modifier", fileName = "AccessoryModifier")]
public sealed class AccessoryModifierDefinition : ScriptableObject
{
    [Header("Identity")]
    public string modifierId;
    public string displayName;

    [TextArea]
    public string description;

    [Header("Roll")]
    [Min(0.01f)] public float weight = 1f;

    [Header("Effects")]
    public List<PassiveStatModifier> statModifiers = new();
    public List<PassiveDefinition> passives = new();

    [Header("Accessory Filter")]
    [Tooltip("Empty = allowed on every accessory. Otherwise the accessory must have at least one of these tags.")]
    public List<string> requiredAnyTags = new();

    [Tooltip("Accessories carrying any of these tags cannot roll this modifier.")]
    public List<string> excludedTags = new();

    public string RuntimeId => string.IsNullOrWhiteSpace(modifierId) ? name : modifierId;

    public bool CanRollOn(AccessoryDefinition accessory)
    {
        if (accessory == null)
            return false;

        List<string> accessoryTags = accessory.tags;

        if (excludedTags != null)
        {
            for (int i = 0; i < excludedTags.Count; i++)
            {
                if (TagListContains(accessoryTags, excludedTags[i]))
                    return false;
            }
        }

        if (requiredAnyTags != null && requiredAnyTags.Count > 0)
        {
            for (int i = 0; i < requiredAnyTags.Count; i++)
            {
                if (TagListContains(accessoryTags, requiredAnyTags[i]))
                    return true;
            }

            return false;
        }

        return true;
    }

    static bool TagListContains(List<string> tags, string tag)
    {
        if (tags == null || string.IsNullOrWhiteSpace(tag))
            return false;

        string trimmedTag = tag.Trim();
        for (int i = 0; i < tags.Count; i++)
        {
            if (string.Equals(tags[i]?.Trim(), trimmedTag, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

