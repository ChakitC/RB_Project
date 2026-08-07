using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class UpgradeIdPickerAttribute : PropertyAttribute
{
}

[Serializable]
public sealed class SkillUpgradeNodeData
{
    public const float MinVisualScale = 1f;
    public const float MaxVisualScale = 2f;
    public const float BaseRuntimeSize = 96f;

    public string nodeId = "node";
    public string displayName;

    [TextArea(2, 4)]
    public string description;

    public Sprite icon;
    public Vector2 uiPosition;

    [Range(MinVisualScale, MaxVisualScale)]
    public float visualScale = MinVisualScale;

    [Min(1)] public int cost = 1;
    [Min(1)] public int requiredCharacterLevel = 1;
    public List<string> requiredNodeIds = new();
    public List<string> mutuallyExclusiveNodeIds = new();

    [Header("Skill Effects")]
    public int skillLevelDelta;
    public List<StatModifier> statModifiers = new();

    [Tooltip("Behavior flags this node grants. Steps and payloads query these through SkillCastContext.")]
    [UpgradeIdPicker]
    public List<string> grantedUpgradeIds = new();

    public string RuntimeNodeId => string.IsNullOrWhiteSpace(nodeId)
        ? string.Empty
        : nodeId.Trim();

    public string ResolvedDisplayName => string.IsNullOrWhiteSpace(displayName)
        ? RuntimeNodeId
        : displayName.Trim();

    public float ResolvedVisualScale => float.IsFinite(visualScale)
        ? Mathf.Clamp(visualScale, MinVisualScale, MaxVisualScale)
        : MinVisualScale;

    public float ResolvedRuntimeSize => BaseRuntimeSize * ResolvedVisualScale;

    public Rect RuntimeUiBounds
    {
        get
        {
            Vector2 size = Vector2.one * ResolvedRuntimeSize;
            return new Rect(uiPosition - size * 0.5f, size);
        }
    }
}
