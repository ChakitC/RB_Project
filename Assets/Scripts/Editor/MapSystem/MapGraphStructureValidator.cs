#if UNITY_EDITOR
using System.Collections.Generic;

/// <summary>
/// Structural invariants of a generated <see cref="MapGraph"/> that <see cref="MapPathValidator"/>
/// does not cover: identity, edge symmetry, critical-path shape, and the branch-count guarantee.
///
/// This runs over generated output rather than authored assets, so it is driven by the seed sweep
/// in <c>MapGeneratorSweepTests</c> rather than by the runtime.
/// </summary>
public static class MapGraphStructureValidator
{
    public static bool Validate(MapGraph graph, MapRunConfigSO config, out string error)
    {
        error = string.Empty;
        if (graph == null)
        {
            error = "Map graph is null.";
            return false;
        }

        graph.RebuildLookup();

        return ValidateUniqueNodeIds(graph, out error) &&
               ValidateEdgeSymmetry(graph, out error) &&
               ValidateCriticalPathShape(graph, out error) &&
               ValidateBranchCount(graph, config, out error);
    }

    static bool ValidateUniqueNodeIds(MapGraph graph, out string error)
    {
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            MapNode node = graph.Nodes[i];
            if (node == null)
            {
                error = $"Node slot {i} is empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(node.Id))
            {
                error = $"Node slot {i} has an empty id.";
                return false;
            }

            if (!seen.Add(node.Id))
            {
                error = $"Node id '{node.Id}' appears more than once.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Return exits and map UI both read the graph backwards, so an edge that exists in only one
    /// direction produces doors that lead nowhere.
    /// </summary>
    static bool ValidateEdgeSymmetry(MapGraph graph, out string error)
    {
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            MapNode node = graph.Nodes[i];
            for (int j = 0; j < node.OutgoingIds.Count; j++)
            {
                MapNode target = graph.GetNode(node.OutgoingIds[j]);
                if (target == null)
                {
                    error = $"Node {node.Id} points to missing node {node.OutgoingIds[j]}.";
                    return false;
                }

                if (!Contains(target.IncomingIds, node.Id))
                {
                    error = $"Node {node.Id} lists {target.Id} as outgoing, but {target.Id} has no matching incoming edge.";
                    return false;
                }
            }

            for (int j = 0; j < node.IncomingIds.Count; j++)
            {
                MapNode source = graph.GetNode(node.IncomingIds[j]);
                if (source == null)
                {
                    error = $"Node {node.Id} lists missing incoming node {node.IncomingIds[j]}.";
                    return false;
                }

                if (!Contains(source.OutgoingIds, node.Id))
                {
                    error = $"Node {node.Id} lists {source.Id} as incoming, but {source.Id} has no matching outgoing edge.";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }

    static bool ValidateCriticalPathShape(MapGraph graph, out string error)
    {
        IReadOnlyList<string> path = graph.CriticalPathIds;
        if (path == null || path.Count < 2)
        {
            error = "Critical path is missing or too short.";
            return false;
        }

        if (path[0] != graph.StartNodeId)
        {
            error = $"Critical path starts at '{path[0]}' instead of the Start node '{graph.StartNodeId}'.";
            return false;
        }

        if (path[path.Count - 1] != graph.BossNodeId)
        {
            error = $"Critical path ends at '{path[path.Count - 1]}' instead of the Boss node '{graph.BossNodeId}'.";
            return false;
        }

        for (int i = 0; i < path.Count - 1; i++)
        {
            MapNode node = graph.GetNode(path[i]);
            if (node == null)
            {
                error = $"Critical path contains missing node '{path[i]}'.";
                return false;
            }

            if (!Contains(node.OutgoingIds, path[i + 1]))
            {
                error = $"Critical path nodes '{path[i]}' and '{path[i + 1]}' are not adjacent.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// MinBranchCount is a guarantee, not a preference. The generator only warns when it runs out
    /// of branch parents, so the shortfall is caught here instead.
    /// </summary>
    static bool ValidateBranchCount(MapGraph graph, MapRunConfigSO config, out string error)
    {
        error = string.Empty;
        if (config == null)
            return true;

        int branches = 0;
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            if (!graph.Nodes[i].IsCriticalPath)
                branches++;
        }

        if (branches < config.MinBranchCount)
        {
            error =
                $"Generated {branches} branch node(s), below MinBranchCount {config.MinBranchCount}. " +
                "Raise CriticalPathNodeCount or MaxOutgoingPerNode, or add room definitions that " +
                "support more exits.";
            return false;
        }

        if (branches > config.MaxBranchCount)
        {
            error = $"Generated {branches} branch node(s), above MaxBranchCount {config.MaxBranchCount}.";
            return false;
        }

        return true;
    }

    static bool Contains(IReadOnlyList<string> ids, string value)
    {
        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] == value)
                return true;
        }

        return false;
    }
}
#endif
