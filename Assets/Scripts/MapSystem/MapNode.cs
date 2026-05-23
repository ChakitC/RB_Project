using System;
using System.Collections.Generic;
using UnityEngine;

public enum MapNodeType
{
    Start,
    Combat,
    Elite,
    Ambush,
    Trap,
    Reward,
    Shop,
    Heal,
    Upgrade,
    Event,
    Boss
}

public enum MapNodeRevealState
{
    Hidden,
    Revealed,
    Visited,
    Cleared
}

public enum RoomExitDirection
{
    Up,
    Right,
    Down,
    Left
}

[Flags]
public enum RoomExitMask
{
    None = 0,
    Up = 1 << 0,
    Right = 1 << 1,
    Down = 1 << 2,
    Left = 1 << 3
}

[Serializable]
public sealed class MapNodeExit
{
    public string targetNodeId;
    public RoomExitDirection direction;

    public MapNodeExit(string targetNodeId, RoomExitDirection direction)
    {
        this.targetNodeId = targetNodeId;
        this.direction = direction;
    }

    public string TargetNodeId => targetNodeId;
    public RoomExitDirection Direction => direction;
    public RoomExitMask Mask => RoomExitDirectionUtility.ToMask(direction);
}

public static class RoomExitDirectionUtility
{
    public static int NormalizeRotationSteps(int rotationSteps)
    {
        rotationSteps %= 4;
        if (rotationSteps < 0)
            rotationSteps += 4;

        return rotationSteps;
    }

    public static RoomExitMask ToMask(RoomExitDirection direction)
    {
        return direction switch
        {
            RoomExitDirection.Right => RoomExitMask.Right,
            RoomExitDirection.Down => RoomExitMask.Down,
            RoomExitDirection.Left => RoomExitMask.Left,
            _ => RoomExitMask.Up
        };
    }

    public static RoomExitDirection Rotate(RoomExitDirection direction, int rotationSteps)
    {
        int directionIndex = (int)direction;
        int rotatedIndex = (directionIndex + NormalizeRotationSteps(rotationSteps)) % 4;
        return (RoomExitDirection)rotatedIndex;
    }

    public static RoomExitMask RotateMask(RoomExitMask mask, int rotationSteps)
    {
        rotationSteps = NormalizeRotationSteps(rotationSteps);
        if (rotationSteps == 0 || mask == RoomExitMask.None)
            return mask;

        RoomExitMask rotatedMask = RoomExitMask.None;
        for (int i = 0; i < 4; i++)
        {
            RoomExitDirection direction = (RoomExitDirection)i;
            if ((mask & ToMask(direction)) == 0)
                continue;

            rotatedMask |= ToMask(Rotate(direction, rotationSteps));
        }

        return rotatedMask;
    }

    public static RoomExitDirection Opposite(RoomExitDirection direction)
    {
        return direction switch
        {
            RoomExitDirection.Up => RoomExitDirection.Down,
            RoomExitDirection.Right => RoomExitDirection.Left,
            RoomExitDirection.Down => RoomExitDirection.Up,
            RoomExitDirection.Left => RoomExitDirection.Right,
            _ => RoomExitDirection.Down
        };
    }

    public static RoomExitDirection FallbackDirection(int index)
    {
        return index switch
        {
            1 => RoomExitDirection.Right,
            2 => RoomExitDirection.Down,
            3 => RoomExitDirection.Left,
            _ => RoomExitDirection.Up
        };
    }

    public static int Count(RoomExitMask mask)
    {
        int value = (int)mask;
        int count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }

        return count;
    }

    public static RoomExitDirection GetDirectionAt(RoomExitMask mask, int index)
    {
        int current = 0;
        for (int i = 0; i < 4; i++)
        {
            RoomExitDirection direction = (RoomExitDirection)i;
            if ((mask & ToMask(direction)) == 0)
                continue;

            if (current == index)
                return direction;

            current++;
        }

        return FallbackDirection(index);
    }
}

[Serializable]
public sealed class MapNode
{
    [SerializeField] private string id;
    [SerializeField] private MapNodeType type;
    [SerializeField] private int depth;
    [SerializeField] private List<string> outgoingIds = new();
    [SerializeField] private List<MapNodeExit> outgoingExits = new();
    [SerializeField] private List<string> incomingIds = new();
    [SerializeField] private MapNodeRevealState state = MapNodeRevealState.Hidden;
    [SerializeField] private bool isCriticalPath;
    [SerializeField] private bool hasDeadEndReward;
    [SerializeField] private RoomDefinitionSO roomDefinition;
    [SerializeField] private int roomRotationSteps;
    [SerializeField] private EncounterDefinitionSO encounterDefinition;

    public string Id => id;
    public MapNodeType Type => type;
    public int Depth => depth;
    public IReadOnlyList<string> OutgoingIds => outgoingIds;
    public IReadOnlyList<MapNodeExit> OutgoingExits
    {
        get
        {
            EnsureOutgoingExits();
            return outgoingExits;
        }
    }

    public RoomExitMask OutgoingExitMask
    {
        get
        {
            EnsureOutgoingExits();

            RoomExitMask mask = RoomExitMask.None;
            for (int i = 0; i < outgoingExits.Count; i++)
            {
                MapNodeExit exit = outgoingExits[i];
                if (exit != null)
                    mask |= exit.Mask;
            }

            return mask;
        }
    }

    public IReadOnlyList<string> IncomingIds => incomingIds;
    public MapNodeRevealState State => state;
    public bool IsCriticalPath => isCriticalPath;
    public bool HasDeadEndReward => hasDeadEndReward;
    public RoomDefinitionSO RoomDefinition => roomDefinition;
    public int RoomRotationSteps => RoomExitDirectionUtility.NormalizeRotationSteps(roomRotationSteps);
    public float RoomYawDegrees => RoomRotationSteps * 90f;
    public EncounterDefinitionSO EncounterDefinition => encounterDefinition;
    public bool IsCleared => state == MapNodeRevealState.Cleared;
    public bool IsVisited => state == MapNodeRevealState.Visited || state == MapNodeRevealState.Cleared;

    public MapNode(string id, MapNodeType type, int depth, bool isCriticalPath)
    {
        this.id = id;
        this.type = type;
        this.depth = depth;
        this.isCriticalPath = isCriticalPath;
    }

    public void SetRoomDefinition(RoomDefinitionSO definition)
    {
        SetRoomDefinition(definition, 0);
    }

    public void SetRoomDefinition(RoomDefinitionSO definition, int rotationSteps)
    {
        roomDefinition = definition;
        roomRotationSteps = RoomExitDirectionUtility.NormalizeRotationSteps(rotationSteps);
    }

    public void SetEncounterDefinition(EncounterDefinitionSO definition)
    {
        encounterDefinition = definition;
    }

    public void SetDeadEndReward(bool value)
    {
        hasDeadEndReward = value;
    }

    public void AddOutgoing(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || outgoingIds.Contains(nodeId))
            return;

        outgoingIds.Add(nodeId);
        outgoingExits.Add(new MapNodeExit(nodeId, RoomExitDirectionUtility.FallbackDirection(outgoingIds.Count - 1)));
    }

    public void SetOutgoingDirections(RoomExitMask exitMask)
    {
        EnsureOutgoingExits();

        int expectedCount = outgoingIds.Count;
        int maskCount = RoomExitDirectionUtility.Count(exitMask);
        if (maskCount != expectedCount)
            exitMask = ResolveFallbackMask(expectedCount);

        outgoingExits.Clear();
        for (int i = 0; i < outgoingIds.Count; i++)
            outgoingExits.Add(new MapNodeExit(outgoingIds[i], RoomExitDirectionUtility.GetDirectionAt(exitMask, i)));
    }

    public void AddIncoming(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || incomingIds.Contains(nodeId))
            return;

        incomingIds.Add(nodeId);
    }

    public void Reveal()
    {
        if (state == MapNodeRevealState.Hidden)
            state = MapNodeRevealState.Revealed;
    }

    public void Visit()
    {
        if (state != MapNodeRevealState.Cleared)
            state = MapNodeRevealState.Visited;
    }

    public void Clear()
    {
        state = MapNodeRevealState.Cleared;
    }

    public string GetDisplayName()
    {
        if (roomDefinition != null && !string.IsNullOrWhiteSpace(roomDefinition.DisplayName))
            return roomDefinition.DisplayName;

        return type.ToString();
    }

    void EnsureOutgoingExits()
    {
        if (outgoingExits == null)
            outgoingExits = new List<MapNodeExit>();

        if (outgoingExits.Count == outgoingIds.Count)
        {
            bool valid = true;
            for (int i = 0; i < outgoingIds.Count; i++)
            {
                MapNodeExit exit = outgoingExits[i];
                if (exit == null || exit.TargetNodeId != outgoingIds[i])
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
                return;
        }

        outgoingExits.Clear();
        for (int i = 0; i < outgoingIds.Count; i++)
            outgoingExits.Add(new MapNodeExit(outgoingIds[i], RoomExitDirectionUtility.FallbackDirection(i)));
    }

    static RoomExitMask ResolveFallbackMask(int count)
    {
        return count switch
        {
            0 => RoomExitMask.None,
            1 => RoomExitMask.Up,
            2 => RoomExitMask.Up | RoomExitMask.Right,
            3 => RoomExitMask.Left | RoomExitMask.Right | RoomExitMask.Up,
            _ => RoomExitMask.Up | RoomExitMask.Right | RoomExitMask.Down | RoomExitMask.Left
        };
    }
}
