using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PassiveTreeNodeData
{
    public string nodeId = "node";
    public string displayName;

    [TextArea(2, 4)]
    public string description;

    public Sprite icon;
    public PassiveDefinition passive;
    [Min(1)] public int cost = 1;
    [Min(1)] public int requiredLevel = 1;
    public List<string> requiredNodeIds = new();
    public Vector2 uiPosition;

    public string RuntimeNodeId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(nodeId))
                return nodeId.Trim();

            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName.Trim();

            return "node";
        }
    }

    public string ResolvedDisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName;

            if (passive != null)
                return passive.name;

            return RuntimeNodeId;
        }
    }

    public string ResolvedDescription
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(description))
                return description;

            return passive != null ? passive.description : string.Empty;
        }
    }
}
