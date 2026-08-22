using System.Collections.Generic;

public static class MapPathValidator
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

        if (graph.StartNode == null)
        {
            error = "Map graph has no start node.";
            return false;
        }

        if (graph.BossNode == null)
        {
            error = "Map graph has no boss node.";
            return false;
        }

        if (!ValidateEdges(graph, out error))
            return false;

        if (!ValidateReachability(graph, out error))
            return false;

        if (!ValidateCriticalPath(graph, config, out error))
            return false;

        if (!ValidateRoomExitCapacity(graph, out error))
            return false;

        if (!ValidateDeadEnds(graph, out error))
            return false;

        return true;
    }

    static bool ValidateEdges(MapGraph graph, out string error)
    {
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            MapNode node = graph.Nodes[i];
            if (node == null)
                continue;

            for (int j = 0; j < node.OutgoingIds.Count; j++)
            {
                if (graph.GetNode(node.OutgoingIds[j]) == null)
                {
                    error = $"Node {node.Id} points to missing node {node.OutgoingIds[j]}.";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }

    static bool ValidateReachability(MapGraph graph, out string error)
    {
        var visited = new HashSet<string>();
        var queue = new Queue<MapNode>();
        queue.Enqueue(graph.StartNode);
        visited.Add(graph.StartNode.Id);

        while (queue.Count > 0)
        {
            MapNode node = queue.Dequeue();
            for (int i = 0; i < node.OutgoingIds.Count; i++)
            {
                MapNode next = graph.GetNode(node.OutgoingIds[i]);
                if (next == null || visited.Contains(next.Id))
                    continue;

                visited.Add(next.Id);
                queue.Enqueue(next);
            }
        }

        if (!visited.Contains(graph.BossNodeId))
        {
            error = "Boss node is not reachable from start.";
            return false;
        }

        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            MapNode node = graph.Nodes[i];
            if (node != null && !visited.Contains(node.Id))
            {
                error = $"Node {node.Id} is not reachable from start.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    static bool ValidateCriticalPath(MapGraph graph, MapRunConfigSO config, out string error)
    {
        if (graph.CriticalPathIds == null || graph.CriticalPathIds.Count < 2)
        {
            error = "Critical path is missing or too short.";
            return false;
        }

        bool requireBlueBeforeBoss = config == null || config.ForceBlueBeforeBoss;
        bool hasBlueBeforeBoss = false;
        for (int i = 0; i < graph.CriticalPathIds.Count; i++)
        {
            MapNode node = graph.GetNode(graph.CriticalPathIds[i]);
            if (node == null)
            {
                error = $"Critical path contains missing node {graph.CriticalPathIds[i]}.";
                return false;
            }

            if (node.Type == MapNodeType.Boss)
                break;

            if (MapPitySystem.IsBlueNodeType(node.Type))
                hasBlueBeforeBoss = true;
        }

        if (requireBlueBeforeBoss && !hasBlueBeforeBoss)
        {
            error = "Critical path has no blue room before boss.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    static bool ValidateRoomExitCapacity(MapGraph graph, out string error)
    {
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            MapNode node = graph.Nodes[i];
            if (node == null)
                continue;

            if (node.RoomDefinition == null)
            {
                error = $"Node {node.Id} has no room definition.";
                return false;
            }

            if (node.RoomDefinition.RoomPrefab == null)
            {
                error = $"Room {node.RoomDefinition.name} has no room prefab for node {node.Id}.";
                return false;
            }

            if (!ValidateOutgoingExitDirections(node, out error))
                return false;

            if (!ValidateReturnExitDirections(graph, node, out RoomExitMask returnExitMask, out int returnExitCount, out error))
                return false;

            int requiredExitCount = graph.GetRequiredExitCount(node);
            RoomExitMask requiredExitMask = graph.GetRequiredExitMask(node);

            if (requiredExitCount > node.RoomDefinition.MaxExitCount)
            {
                error = $"Room {node.RoomDefinition.name} cannot support {requiredExitCount} exits for node {node.Id}.";
                return false;
            }

            if (!node.RoomDefinition.SupportsExitMask(requiredExitMask, node.RoomRotationSteps))
            {
                RoomExitMask rotatedExitMask = node.RoomDefinition.GetRotatedExitMask(node.RoomRotationSteps);
                error = $"Room {node.RoomDefinition.name} rotated to {rotatedExitMask} does not support exit mask {requiredExitMask} for node {node.Id}.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    static bool ValidateOutgoingExitDirections(MapNode node, out string error)
    {
        IReadOnlyList<MapNodeExit> exits = node.OutgoingExits;
        if (exits.Count != node.OutgoingIds.Count)
        {
            error = $"Node {node.Id} has mismatched outgoing exit data.";
            return false;
        }

        if (RoomExitDirectionUtility.Count(node.OutgoingExitMask) != node.OutgoingIds.Count)
        {
            error = $"Node {node.Id} has duplicate or missing outgoing exit directions.";
            return false;
        }

        var directions = new HashSet<RoomExitDirection>();
        for (int i = 0; i < exits.Count; i++)
        {
            MapNodeExit exit = exits[i];
            if (exit == null || string.IsNullOrWhiteSpace(exit.TargetNodeId))
            {
                error = $"Node {node.Id} has an empty outgoing exit.";
                return false;
            }

            if (!directions.Add(exit.Direction))
            {
                error = $"Node {node.Id} has duplicate exit direction {exit.Direction}.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    static bool ValidateReturnExitDirections(
        MapGraph graph,
        MapNode node,
        out RoomExitMask returnExitMask,
        out int returnExitCount,
        out string error)
    {
        returnExitMask = RoomExitMask.None;
        returnExitCount = 0;

        if (graph == null || node == null || !graph.ShouldCreateReturnExit(node))
        {
            error = string.Empty;
            return true;
        }

        for (int i = 0; i < node.IncomingIds.Count; i++)
        {
            string incomingId = node.IncomingIds[i];
            if (!graph.TryGetReturnExitDirection(node, incomingId, out RoomExitDirection returnDirection))
            {
                error = $"Node {node.Id} cannot resolve return exit from {incomingId}.";
                return false;
            }

            RoomExitMask directionMask = RoomExitDirectionUtility.ToMask(returnDirection);
            if ((returnExitMask & directionMask) != 0)
            {
                error = $"Node {node.Id} has duplicate return exit direction {returnDirection}.";
                return false;
            }

            if ((node.OutgoingExitMask & directionMask) != 0)
            {
                error = $"Node {node.Id} uses {returnDirection} for both outgoing and return exits.";
                return false;
            }

            returnExitMask |= directionMask;
            returnExitCount++;
        }

        error = string.Empty;
        return true;
    }

    static bool ValidateDeadEnds(MapGraph graph, out string error)
    {
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            MapNode node = graph.Nodes[i];
            if (node == null || node.Type == MapNodeType.Boss || node.OutgoingIds.Count > 0)
                continue;

            bool hasReward = node.HasDeadEndReward ||
                             MapPitySystem.IsDeadEndRewardType(node.Type) ||
                             (node.RoomDefinition != null && node.RoomDefinition.HasClearReward);

            if (!hasReward)
            {
                error = $"Dead-end node {node.Id} has no reward.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
