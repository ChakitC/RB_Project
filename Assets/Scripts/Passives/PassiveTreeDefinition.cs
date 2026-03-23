using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Combat/Passives/Passive Tree")]
public sealed class PassiveTreeDefinition : ScriptableObject
{
    public string treeId;
    public string displayName;

    [TextArea(2, 4)]
    public string description;

    public List<PassiveTreeNodeData> nodes = new();

    public string RuntimeTreeId => string.IsNullOrWhiteSpace(treeId) ? name : treeId;

    public bool TryGetNode(string nodeId, out PassiveTreeNodeData node)
    {
        node = null;
        if (nodes == null || string.IsNullOrWhiteSpace(nodeId))
            return false;

        string resolvedId = nodeId.Trim();
        for (int i = 0; i < nodes.Count; i++)
        {
            var candidate = nodes[i];
            if (candidate == null)
                continue;

            if (!string.Equals(candidate.RuntimeNodeId, resolvedId, System.StringComparison.Ordinal))
                continue;

            node = candidate;
            return true;
        }

        return false;
    }
}
