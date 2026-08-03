using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MapGraph
{
    [SerializeField] private List<MapNode> nodes = new();
    [SerializeField] private string startNodeId;
    [SerializeField] private string bossNodeId;
    [SerializeField] private List<string> criticalPathIds = new();
    [SerializeField] private int resolvedSeed;

    private readonly Dictionary<string, MapNode> lookup = new();

    public IReadOnlyList<MapNode> Nodes => nodes;
    public string StartNodeId => startNodeId;
    public string BossNodeId => bossNodeId;
    public IReadOnlyList<string> CriticalPathIds => criticalPathIds;
    public int ResolvedSeed => resolvedSeed;

    public void SetResolvedSeed(int seed)
    {
        resolvedSeed = seed;
    }

    public MapNode StartNode => GetNode(startNodeId);
    public MapNode BossNode => GetNode(bossNodeId);

    public void AddNode(MapNode node)
    {
        if (node == null || string.IsNullOrWhiteSpace(node.Id))
            return;

        if (ContainsNode(node.Id))
            return;

        nodes.Add(node);
        lookup[node.Id] = node;

        if (node.Type == MapNodeType.Start && string.IsNullOrEmpty(startNodeId))
            startNodeId = node.Id;

        if (node.Type == MapNodeType.Boss && string.IsNullOrEmpty(bossNodeId))
            bossNodeId = node.Id;

        if (node.IsCriticalPath && !criticalPathIds.Contains(node.Id))
            criticalPathIds.Add(node.Id);
    }

    public bool AddEdge(string fromNodeId, string toNodeId)
    {
        MapNode from = GetNode(fromNodeId);
        MapNode to = GetNode(toNodeId);
        if (from == null || to == null)
            return false;

        from.AddOutgoing(toNodeId);
        to.AddIncoming(fromNodeId);
        return true;
    }

    public MapNode GetNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return null;

        if (lookup.Count != nodes.Count)
            RebuildLookup();

        lookup.TryGetValue(nodeId, out MapNode node);
        return node;
    }

    public bool ContainsNode(string nodeId)
    {
        return GetNode(nodeId) != null;
    }

    public void RebuildLookup()
    {
        lookup.Clear();
        for (int i = 0; i < nodes.Count; i++)
        {
            MapNode node = nodes[i];
            if (node == null || string.IsNullOrWhiteSpace(node.Id))
                continue;

            lookup[node.Id] = node;
        }
    }

    public List<MapNode> GetOutgoingNodes(MapNode node)
    {
        var outgoing = new List<MapNode>();
        if (node == null)
            return outgoing;

        for (int i = 0; i < node.OutgoingIds.Count; i++)
        {
            MapNode next = GetNode(node.OutgoingIds[i]);
            if (next != null)
                outgoing.Add(next);
        }

        return outgoing;
    }

    public bool TryGetOutgoingExit(string fromNodeId, string toNodeId, out MapNodeExit exit)
    {
        exit = null;

        if (string.IsNullOrWhiteSpace(toNodeId))
            return false;

        MapNode from = GetNode(fromNodeId);
        if (from == null)
            return false;

        IReadOnlyList<MapNodeExit> exits = from.OutgoingExits;
        for (int i = 0; i < exits.Count; i++)
        {
            MapNodeExit candidate = exits[i];
            if (candidate != null && candidate.TargetNodeId == toNodeId)
            {
                exit = candidate;
                return true;
            }
        }

        return false;
    }

    public bool ShouldCreateReturnExit(MapNode node)
    {
        return node != null &&
               node.Type != MapNodeType.Start &&
               node.Type != MapNodeType.Boss &&
               node.IncomingIds.Count > 0;
    }

    public bool TryGetReturnExitDirection(MapNode node, string incomingNodeId, out RoomExitDirection direction)
    {
        direction = RoomExitDirection.Down;

        if (!ShouldCreateReturnExit(node) || string.IsNullOrWhiteSpace(incomingNodeId))
            return false;

        return TryGetIncomingExitDirection(node, incomingNodeId, out direction);
    }

    public bool TryGetIncomingExitDirection(MapNode node, string incomingNodeId, out RoomExitDirection direction)
    {
        direction = RoomExitDirection.Down;

        if (node == null || string.IsNullOrWhiteSpace(incomingNodeId))
            return false;

        if (!TryGetOutgoingExit(incomingNodeId, node.Id, out MapNodeExit incomingExit))
            return false;

        direction = RoomExitDirectionUtility.Opposite(incomingExit.Direction);
        return true;
    }

    public RoomExitMask GetReturnExitMask(MapNode node)
    {
        RoomExitMask mask = RoomExitMask.None;
        if (!ShouldCreateReturnExit(node))
            return mask;

        for (int i = 0; i < node.IncomingIds.Count; i++)
        {
            if (TryGetReturnExitDirection(node, node.IncomingIds[i], out RoomExitDirection direction))
                mask |= RoomExitDirectionUtility.ToMask(direction);
        }

        return mask;
    }

    public RoomExitMask GetRequiredExitMask(MapNode node)
    {
        if (node == null)
            return RoomExitMask.None;

        return node.OutgoingExitMask | GetIncomingExitMask(node);
    }

    public int GetRequiredExitCount(MapNode node)
    {
        return RoomExitDirectionUtility.Count(GetRequiredExitMask(node));
    }

    public RoomExitMask GetIncomingExitMask(MapNode node)
    {
        RoomExitMask mask = RoomExitMask.None;
        if (node == null || node.Type == MapNodeType.Start)
            return mask;

        for (int i = 0; i < node.IncomingIds.Count; i++)
        {
            if (TryGetIncomingExitDirection(node, node.IncomingIds[i], out RoomExitDirection direction))
                mask |= RoomExitDirectionUtility.ToMask(direction);
        }

        return mask;
    }

    public void RevealOutgoing(MapNode node)
    {
        if (node == null)
            return;

        for (int i = 0; i < node.OutgoingIds.Count; i++)
            GetNode(node.OutgoingIds[i])?.Reveal();
    }
}
