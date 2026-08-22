using UnityEngine;

/// <summary>
/// The state of one run: which map was generated, where the party stands, and whether a transition
/// is in flight. It also answers whether a given travel is legal, which is the one rule the room
/// exits, the map UI, and the controller all have to agree on.
/// </summary>
public sealed class MapRunSession
{
    public MapGraph Graph { get; private set; }
    public MapNode CurrentNode { get; private set; }
    public RoomRuntimeCache.Entry CurrentEntry { get; private set; }
    public bool IsTransitioning { get; set; }

    public RoomController CurrentRoom => CurrentEntry != null ? CurrentEntry.Controller : null;
    public GameObject CurrentRoomInstance => CurrentEntry != null ? CurrentEntry.Instance : null;
    public bool HasActiveRoom => CurrentRoom != null;

    public void SetGraph(MapGraph graph)
    {
        Graph = graph;
        CurrentNode = null;
        CurrentEntry = null;
    }

    public void ClearRun()
    {
        Graph = null;
        CurrentNode = null;
        CurrentEntry = null;
        IsTransitioning = false;
    }

    /// <summary>Binds the party to a room. Only a committed transition may call this.</summary>
    public void Commit(RoomRuntimeCache.Entry entry, MapNode node)
    {
        CurrentEntry = entry;
        CurrentNode = node;
    }

    /// <summary>Leaves the run with no room, which is where a failed first entry lands.</summary>
    public void ClearCurrentRoom(MapNode node)
    {
        CurrentEntry = null;
        CurrentNode = node;
    }

    public MapNode GetNode(string nodeId)
    {
        return Graph != null ? Graph.GetNode(nodeId) : null;
    }

    public bool CanTravelTo(string targetNodeId)
    {
        if (IsTransitioning || Graph == null || CurrentNode == null || string.IsNullOrWhiteSpace(targetNodeId))
            return false;

        RoomController room = CurrentRoom;
        if (room != null)
        {
            if (room.ExitsLocked)
                return false;

            RoomDefinitionSO definition = CurrentNode.RoomDefinition;
            if (definition != null && definition.RequiresClearBeforeExit && !room.RoomCleared)
                return false;
        }

        return IsOutgoingTravelTarget(targetNodeId) || IsReturnTravelTarget(targetNodeId);
    }

    public bool CanTravelTo(string targetNodeId, RoomExitDirection exitDirection)
    {
        if (!CanTravelTo(targetNodeId))
            return false;

        return IsOutgoingTravelTarget(targetNodeId, exitDirection) || IsReturnTravelTarget(targetNodeId, exitDirection);
    }

    public bool TryResolveTravelExitDirection(string targetNodeId, out RoomExitDirection exitDirection)
    {
        exitDirection = RoomExitDirection.Up;

        if (Graph == null || CurrentNode == null)
            return false;

        if (Graph.TryGetOutgoingExit(CurrentNode.Id, targetNodeId, out MapNodeExit outgoingExit))
        {
            exitDirection = outgoingExit.Direction;
            return true;
        }

        return Graph.TryGetReturnExitDirection(CurrentNode, targetNodeId, out exitDirection);
    }

    bool IsOutgoingTravelTarget(string targetNodeId)
    {
        for (int i = 0; i < CurrentNode.OutgoingIds.Count; i++)
        {
            if (CurrentNode.OutgoingIds[i] == targetNodeId)
                return true;
        }

        return false;
    }

    bool IsOutgoingTravelTarget(string targetNodeId, RoomExitDirection exitDirection)
    {
        return Graph != null &&
               CurrentNode != null &&
               Graph.TryGetOutgoingExit(CurrentNode.Id, targetNodeId, out MapNodeExit exit) &&
               exit.Direction == exitDirection;
    }

    bool IsReturnTravelTarget(string targetNodeId)
    {
        if (Graph == null || CurrentNode == null || !Graph.ShouldCreateReturnExit(CurrentNode))
            return false;

        MapNode target = Graph.GetNode(targetNodeId);
        if (target == null || !target.IsVisited)
            return false;

        return Graph.TryGetReturnExitDirection(CurrentNode, targetNodeId, out _);
    }

    bool IsReturnTravelTarget(string targetNodeId, RoomExitDirection exitDirection)
    {
        return IsReturnTravelTarget(targetNodeId) &&
               Graph.TryGetReturnExitDirection(CurrentNode, targetNodeId, out RoomExitDirection returnDirection) &&
               returnDirection == exitDirection;
    }
}
