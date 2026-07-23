using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewActiveSkillTree", menuName = "Game/Active Skill Tree")]
public sealed class SkillUpgradeTreeDefinition : ScriptableObject
{
    public string treeId;
    public string displayName;

    [TextArea(2, 4)]
    public string description;

    public List<SkillUpgradeNodeData> nodes = new();

    public string RuntimeTreeId => string.IsNullOrWhiteSpace(treeId)
        ? name
        : treeId.Trim();

    public bool TryGetNode(string nodeId, out SkillUpgradeNodeData node)
    {
        node = null;
        if (nodes == null || string.IsNullOrWhiteSpace(nodeId))
            return false;

        string resolvedId = nodeId.Trim();
        for (int i = 0; i < nodes.Count; i++)
        {
            SkillUpgradeNodeData candidate = nodes[i];
            if (candidate == null)
                continue;

            if (!string.Equals(candidate.RuntimeNodeId, resolvedId, StringComparison.Ordinal))
                continue;

            node = candidate;
            return true;
        }

        return false;
    }
}
