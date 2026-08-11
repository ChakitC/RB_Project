using System;
using System.Collections.Generic;

/// <summary>
/// Editor-only "what if I take exactly this path" preview: walks a node's requiredNodeIds
/// backward, folds every ancestor plus the node itself into a SkillUpgradeStatSnapshot using the
/// exact runtime formula, then asks SkillInstance for the resulting FinalSkillStats. This is not
/// the player's eventual final stats -- an optional sibling node unlocked later can still change
/// them -- so callers must label it "Required Path Preview", never "Final".
/// </summary>
public static class RequiredPathPreviewResolver
{
    public static FinalSkillStats Resolve(
        SkillUpgradeTreeDefinition tree,
        SkillUpgradeNodeData selectedNode,
        SkillGemDefinition owner)
    {
        if (tree == null || selectedNode == null || owner == null)
            return null;

        Dictionary<string, SkillUpgradeNodeData> nodesById = BuildNodesById(tree);
        var included = new HashSet<string>(StringComparer.Ordinal);
        CollectRequiredPath(selectedNode.RuntimeNodeId, nodesById, included);

        var snapshot = new SkillUpgradeStatSnapshot();
        foreach (string nodeId in included)
        {
            if (nodesById.TryGetValue(nodeId, out SkillUpgradeNodeData node))
                snapshot.AddNode(node);
        }

        var instance = new SkillInstance { def = owner, upgradeSnapshot = snapshot };
        return instance.GetFinalStats(null);
    }

    static Dictionary<string, SkillUpgradeNodeData> BuildNodesById(SkillUpgradeTreeDefinition tree)
    {
        var byId = new Dictionary<string, SkillUpgradeNodeData>(StringComparer.Ordinal);
        if (tree.nodes == null)
            return byId;

        for (int i = 0; i < tree.nodes.Count; i++)
        {
            SkillUpgradeNodeData node = tree.nodes[i];
            string id = node?.RuntimeNodeId;
            if (!string.IsNullOrEmpty(id) && !byId.ContainsKey(id))
                byId[id] = node;
        }

        return byId;
    }

    // included.Add returning false both dedupes a shared ancestor and breaks a prerequisite cycle --
    // a node already on the walk stack is never re-entered. A dangling/missing prerequisite id is
    // simply skipped instead of throwing.
    static void CollectRequiredPath(
        string nodeId,
        Dictionary<string, SkillUpgradeNodeData> nodesById,
        HashSet<string> included)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return;

        string trimmed = nodeId.Trim();
        if (!included.Add(trimmed))
            return;

        if (!nodesById.TryGetValue(trimmed, out SkillUpgradeNodeData node) || node.requiredNodeIds == null)
            return;

        for (int i = 0; i < node.requiredNodeIds.Count; i++)
            CollectRequiredPath(node.requiredNodeIds[i], nodesById, included);
    }
}
