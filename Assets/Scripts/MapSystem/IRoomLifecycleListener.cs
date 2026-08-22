/// <summary>
/// Hook for behaviour that belongs to one kind of room rather than to rooms in general. Implement
/// it on a component under the room prefab and <see cref="RoomController"/> will drive it through
/// the room's lifecycle, so the generic controller never has to know which stage or node type it
/// is running.
/// </summary>
public interface IRoomLifecycleListener
{
    /// <summary>The room has been bound to a node and its runtime content roots exist.</summary>
    void OnRoomInitialized(RoomController room, MapNode node);

    /// <summary>The party is in the room and gameplay is starting.</summary>
    void OnRoomBegan(RoomController room, MapNode node);

    /// <summary>The room's encounter is finished, or the room never had one.</summary>
    void OnRoomCleared(RoomController room, MapNode node);
}
