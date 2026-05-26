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

    public string RuntimeId => string.IsNullOrWhiteSpace(modifierId) ? name : modifierId;
}

