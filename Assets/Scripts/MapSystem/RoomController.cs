using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class RoomEntranceSpawnPoint
{
    [SerializeField] private RoomExitDirection direction = RoomExitDirection.Up;
    [SerializeField] private Transform spawnPoint;

    public RoomExitDirection Direction => direction;
    public Transform SpawnPoint => spawnPoint;
}

[DisallowMultipleComponent]
public class RoomController : MonoBehaviour
{
    [Header("Sockets")]
    [Tooltip("ตำแหน่ง spawn หรือ warp ผู้เล่นเมื่อเข้าห้องนี้")]
    [SerializeField] private Transform playerSpawnPoint;

    [Tooltip("Optional player spawn points by the doorway side used to enter this room.")]
    [SerializeField] private RoomEntranceSpawnPoint[] playerSpawnPointsByDirection;

    [Tooltip("ตำแหน่ง spawn ศัตรูในห้องนี้")]
    [SerializeField] private Transform[] enemySpawnPoints;

    [Tooltip("ตำแหน่ง spawn loot หรือ reward หลังเคลียร์ห้อง")]
    [SerializeField] private Transform[] lootSpawnPoints;

    [Tooltip("ประตูหรือ portal ออกจากห้อง เรียงตาม outgoing node ของ graph")]
    [SerializeField] private RoomExitInteractable[] exits;

    [Header("Lockdown")]
    [Tooltip("object ที่จะเปิดเมื่อห้องล็อก เช่น barrier, door block, visual effect")]
    [SerializeField] private GameObject[] lockdownObjects;

    [Tooltip("ซ่อน exit socket ที่ไม่มี node ปลายทางจาก graph")]
    [SerializeField] private bool hideUnusedExits = true;

    private MapRunController runController;
    private MapNode node;
    private bool roomCleared;
    private bool exitsLocked;

    public Transform PlayerSpawnPoint => playerSpawnPoint != null ? playerSpawnPoint : transform;
    public Transform[] EnemySpawnPoints => enemySpawnPoints;
    public Transform[] LootSpawnPoints => lootSpawnPoints;
    public MapNode Node => node;
    public bool RoomCleared => roomCleared;
    public bool ExitsLocked => exitsLocked;

    public void Initialize(MapRunController run, MapNode currentNode)
    {
        runController = run;
        node = currentNode;
        roomCleared = currentNode != null && currentNode.IsCleared;

        ResolveExits();
        ConfigureExits();
        SetExitsLocked(false);
    }

    public void BeginRoom(EncounterDirector encounterDirector)
    {
        if (node == null)
            return;

        node.Visit();

        if (roomCleared)
        {
            SetExitsLocked(false);
            return;
        }

        bool shouldRunEncounter = ShouldRunEncounter();
        if (shouldRunEncounter)
        {
            if (node.RoomDefinition == null || node.RoomDefinition.LockExitsUntilClear)
                SetExitsLocked(true);

            if (encounterDirector != null)
                encounterDirector.StartEncounter(this, node.EncounterDefinition);
            else
                CompleteRoom();

            return;
        }

        CompleteRoom();
    }

    public void CompleteRoom()
    {
        if (roomCleared)
            return;

        roomCleared = true;
        node?.Clear();
        SpawnClearRewards();
        SetExitsLocked(false);
        runController?.NotifyRoomCleared(this);
    }

    public void SetExitsLocked(bool locked)
    {
        exitsLocked = locked;

        if (lockdownObjects == null)
            return;

        for (int i = 0; i < lockdownObjects.Length; i++)
        {
            if (lockdownObjects[i] != null)
                lockdownObjects[i].SetActive(locked);
        }
    }

    public Transform GetEnemySpawnPoint(int index)
    {
        if (enemySpawnPoints == null || enemySpawnPoints.Length == 0)
            return transform;

        Transform point = enemySpawnPoints[Mathf.Abs(index) % enemySpawnPoints.Length];
        return point != null ? point : transform;
    }

    public Transform GetLootSpawnPoint(int index)
    {
        if (lootSpawnPoints == null || lootSpawnPoints.Length == 0)
            return transform;

        Transform point = lootSpawnPoints[Mathf.Abs(index) % lootSpawnPoints.Length];
        return point != null ? point : transform;
    }

    public Transform GetPlayerSpawnPoint(RoomExitDirection? entranceDirection)
    {
        if (entranceDirection.HasValue)
        {
            Transform directionalSpawn = FindPlayerSpawnPoint(entranceDirection.Value);
            if (directionalSpawn != null)
                return directionalSpawn;

            Transform exitSpawn = FindPlayerSpawnPointUnderExit(entranceDirection.Value);
            if (exitSpawn != null)
                return exitSpawn;
        }

        return PlayerSpawnPoint;
    }

    bool ShouldRunEncounter()
    {
        if (node == null || node.Type == MapNodeType.Start)
            return false;

        if (node.RoomDefinition != null && !node.RoomDefinition.StartEncounterOnEnter)
            return false;

        if (node.EncounterDefinition != null)
            return true;

        return MapPitySystem.IsRedNodeType(node.Type) || node.Type == MapNodeType.Boss;
    }

    void ResolveExits()
    {
        if (exits == null || exits.Length == 0)
            exits = GetComponentsInChildren<RoomExitInteractable>(true);
    }

    void ConfigureExits()
    {
        if (exits == null)
            return;

        for (int i = 0; i < exits.Length; i++)
        {
            RoomExitInteractable exit = exits[i];
            if (exit == null)
                continue;

            exit.ApplyRoomRotation(RoomRotationSteps);
            exit.Configure(runController, this, null);
            if (!hideUnusedExits)
                exit.SetVisible(true);
        }

        if (node == null)
            return;

        IReadOnlyList<MapNodeExit> outgoingExits = node.OutgoingExits;
        for (int i = 0; i < outgoingExits.Count; i++)
        {
            MapNodeExit outgoing = outgoingExits[i];
            if (outgoing == null)
                continue;

            RoomExitInteractable exit = FindExit(outgoing.Direction);
            if (exit == null)
            {
                Debug.LogWarning($"[RoomController] Room for node {node.Id} has no {outgoing.Direction} exit for target {outgoing.TargetNodeId}.", this);
                continue;
            }

            MapNode target = runController != null ? runController.GetNode(outgoing.TargetNodeId) : null;
            exit.Configure(runController, this, target);
        }

        ConfigureReturnExits();
    }

    void ConfigureReturnExits()
    {
        MapGraph graph = runController != null ? runController.CurrentGraph : null;
        if (graph == null || node == null || !graph.ShouldCreateReturnExit(node))
            return;

        RoomExitMask configuredReturnMask = RoomExitMask.None;
        for (int i = 0; i < node.IncomingIds.Count; i++)
        {
            string incomingId = node.IncomingIds[i];
            if (!graph.TryGetReturnExitDirection(node, incomingId, out RoomExitDirection returnDirection))
            {
                Debug.LogWarning($"[RoomController] Node {node.Id} cannot resolve return exit from {incomingId}.", this);
                continue;
            }

            RoomExitMask returnMask = RoomExitDirectionUtility.ToMask(returnDirection);
            if ((configuredReturnMask & returnMask) != 0)
            {
                Debug.LogWarning($"[RoomController] Node {node.Id} has duplicate return exit direction {returnDirection}.", this);
                continue;
            }

            if (HasOutgoingExitAtDirection(returnDirection))
            {
                Debug.LogWarning($"[RoomController] Node {node.Id} cannot use {returnDirection} as a return exit because an outgoing exit already uses it.", this);
                continue;
            }

            RoomExitInteractable exit = FindExit(returnDirection);
            if (exit == null)
            {
                Debug.LogWarning($"[RoomController] Room for node {node.Id} has no {returnDirection} exit for return to {incomingId}.", this);
                continue;
            }

            MapNode target = graph.GetNode(incomingId);
            exit.Configure(runController, this, target, true);
            configuredReturnMask |= returnMask;
        }
    }

    bool HasOutgoingExitAtDirection(RoomExitDirection direction)
    {
        if (node == null)
            return false;

        IReadOnlyList<MapNodeExit> outgoingExits = node.OutgoingExits;
        for (int i = 0; i < outgoingExits.Count; i++)
        {
            MapNodeExit outgoing = outgoingExits[i];
            if (outgoing != null && outgoing.Direction == direction)
                return true;
        }

        return false;
    }

    Transform FindPlayerSpawnPoint(RoomExitDirection direction)
    {
        if (playerSpawnPointsByDirection == null)
            return null;

        for (int i = 0; i < playerSpawnPointsByDirection.Length; i++)
        {
            RoomEntranceSpawnPoint entry = playerSpawnPointsByDirection[i];
            if (entry != null && GetRotatedDirection(entry.Direction) == direction && entry.SpawnPoint != null)
                return entry.SpawnPoint;
        }

        return null;
    }

    Transform FindPlayerSpawnPointUnderExit(RoomExitDirection direction)
    {
        RoomExitInteractable exit = FindExit(direction);
        if (exit == null)
            return null;

        return FindSpawnPointChild(exit.transform);
    }

    static Transform FindSpawnPointChild(Transform root)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (IsSpawnPointName(child.name))
                return child;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == root)
                continue;

            if (IsSpawnPointName(child.name))
                return child;
        }

        return null;
    }

    static bool IsSpawnPointName(string objectName)
    {
        return !string.IsNullOrEmpty(objectName) &&
               objectName.IndexOf("SpawnPoint", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    RoomExitInteractable FindExit(RoomExitDirection direction)
    {
        if (exits == null)
            return null;

        for (int i = 0; i < exits.Length; i++)
        {
            RoomExitInteractable exit = exits[i];
            if (exit != null && exit.Direction == direction)
                return exit;
        }

        return null;
    }

    int RoomRotationSteps => node != null ? node.RoomRotationSteps : 0;

    RoomExitDirection GetRotatedDirection(RoomExitDirection authoredDirection)
    {
        return RoomExitDirectionUtility.Rotate(authoredDirection, RoomRotationSteps);
    }

    void SpawnClearRewards()
    {
        RoomDefinitionSO definition = node != null ? node.RoomDefinition : null;
        if (definition == null || !definition.HasClearReward || ItemDropManager.Instance == null)
            return;

        DropTable table = definition.ClearRewardTable;
        for (int i = 0; i < definition.ClearRewardRolls; i++)
        {
            DropEntry entry = table.GetRandomEntry();
            if (entry == null || !entry.TryResolveItem(out ItemDefinition item))
                continue;

            Transform point = GetLootSpawnPoint(i);
            ItemDropManager.Instance.DropItem(item, entry.ResolveAmount(), point.position, definition.ClearRewardRarity);
        }
    }
}
