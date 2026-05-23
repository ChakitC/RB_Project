using System;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class MapRunController : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("config หลักสำหรับสร้าง graph, เลือก room และเลือก encounter ของ run นี้")]
    [SerializeField] private MapRunConfigSO runConfig;

    [Tooltip("เริ่ม run อัตโนมัติเมื่อ scene เริ่มเล่น")]
    [SerializeField] private bool startRunOnStart = true;

    [Header("Runtime Scene")]
    [Tooltip("parent สำหรับห้องที่ถูก spawn ใน run ปัจจุบัน")]
    [SerializeField] private Transform roomParent;

    [Tooltip("ตำแหน่งอ้างอิงสำหรับ spawn room prefab")]
    [SerializeField] private Transform roomSpawnAnchor;

    [Tooltip("context ของผู้เล่น ถ้าไม่ใส่ ระบบจะหา object ที่ TargetIdentity เป็น Player")]
    [SerializeField] private CharacteContext playerContext;

    [Tooltip("ตัวคุมการ spawn enemy encounter ในห้อง")]
    [SerializeField] private EncounterDirector encounterDirector;

    [Tooltip("UI map แบบเบื้องต้นที่จะ refresh เมื่อ graph หรือห้องปัจจุบันเปลี่ยน")]
    [SerializeField] private MapView mapView;

    [Header("Runtime NavMesh")]
    [SerializeField] private bool rebuildRoomNavMeshAfterSpawn = true;

    [Header("Debug")]
    [SerializeField] private bool logLifecycle = true;

    private MapGraph graph;
    private MapNode currentNode;
    private GameObject currentRoomInstance;
    private RoomController currentRoom;
    private bool isTransitioning;

    public event Action<MapGraph, MapNode> MapChanged;
    public MapGraph CurrentGraph => graph;
    public MapNode CurrentNode => currentNode;
    public RoomController CurrentRoom => currentRoom;
    public bool IsTransitioning => isTransitioning;

    void Start()
    {
        ResolveReferences();

        if (!startRunOnStart)
        {
            Log("StartRunOnStart is disabled. Call StartRun manually or enable it on the controller.");
            return;
        }

        StartRun();
    }

    [ContextMenu("Start Run")]
    public void StartRun()
    {
        ResolveReferences();
        Log("StartRun requested.");

        if (runConfig == null)
            Debug.LogWarning("[MapRunController] Run Config is missing. Map generation will not be able to assign room definitions.", this);
        else if (!MapRunConfigValidator.Validate(runConfig, out string configError))
        {
            Debug.LogError($"[MapRunController] Run Config is invalid:\n{configError}", this);
            return;
        }

        graph = MapGenerator.Generate(runConfig);
        if (graph == null)
        {
            Debug.LogError("[MapRunController] MapGenerator returned null.", this);
            return;
        }

        Log($"Generated map with {graph.Nodes.Count} nodes. Start='{graph.StartNodeId}', Boss='{graph.BossNodeId}'.");

        if (!MapPathValidator.Validate(graph, out string error))
        {
            Debug.LogError($"[MapRunController] Generated map is invalid: {error}", this);
            return;
        }

        EnterNode(graph.StartNodeId);
    }

    public MapNode GetNode(string nodeId)
    {
        return graph != null ? graph.GetNode(nodeId) : null;
    }

    public bool CanTravelTo(string targetNodeId)
    {
        if (isTransitioning || graph == null || currentNode == null || string.IsNullOrWhiteSpace(targetNodeId))
            return false;

        if (currentRoom != null && currentRoom.ExitsLocked)
            return false;

        return IsOutgoingTravelTarget(targetNodeId) || IsReturnTravelTarget(targetNodeId);
    }

    public bool CanTravelTo(string targetNodeId, RoomExitDirection exitDirection)
    {
        if (!CanTravelTo(targetNodeId))
            return false;

        return IsOutgoingTravelTarget(targetNodeId, exitDirection) || IsReturnTravelTarget(targetNodeId, exitDirection);
    }

    public void RequestTravelTo(string targetNodeId)
    {
        if (!CanTravelTo(targetNodeId))
            return;

        RoomExitDirection? entranceDirection = null;
        if (TryResolveTravelExitDirection(targetNodeId, out RoomExitDirection exitDirection))
            entranceDirection = RoomExitDirectionUtility.Opposite(exitDirection);

        EnterNode(targetNodeId, entranceDirection);
    }

    public void RequestTravelTo(string targetNodeId, RoomExitDirection exitDirection)
    {
        if (!CanTravelTo(targetNodeId, exitDirection))
            return;

        EnterNode(targetNodeId, RoomExitDirectionUtility.Opposite(exitDirection));
    }

    public void NotifyRoomCleared(RoomController room)
    {
        if (room == null || room != currentRoom || currentNode == null)
            return;

        graph.RevealOutgoing(currentNode);
        NotifyMapChanged();
    }

    bool IsOutgoingTravelTarget(string targetNodeId)
    {
        for (int i = 0; i < currentNode.OutgoingIds.Count; i++)
        {
            if (currentNode.OutgoingIds[i] == targetNodeId)
                return true;
        }

        return false;
    }

    bool IsOutgoingTravelTarget(string targetNodeId, RoomExitDirection exitDirection)
    {
        return graph != null &&
               currentNode != null &&
               graph.TryGetOutgoingExit(currentNode.Id, targetNodeId, out MapNodeExit exit) &&
               exit.Direction == exitDirection;
    }

    bool IsReturnTravelTarget(string targetNodeId)
    {
        if (graph == null || currentNode == null || !graph.ShouldCreateReturnExit(currentNode))
            return false;

        MapNode target = graph.GetNode(targetNodeId);
        if (target == null || !target.IsVisited)
            return false;

        return graph.TryGetReturnExitDirection(currentNode, targetNodeId, out _);
    }

    bool IsReturnTravelTarget(string targetNodeId, RoomExitDirection exitDirection)
    {
        return IsReturnTravelTarget(targetNodeId) &&
               graph.TryGetReturnExitDirection(currentNode, targetNodeId, out RoomExitDirection returnDirection) &&
               returnDirection == exitDirection;
    }

    bool TryResolveTravelExitDirection(string targetNodeId, out RoomExitDirection exitDirection)
    {
        exitDirection = RoomExitDirection.Up;

        if (graph == null || currentNode == null)
            return false;

        if (graph.TryGetOutgoingExit(currentNode.Id, targetNodeId, out MapNodeExit outgoingExit))
        {
            exitDirection = outgoingExit.Direction;
            return true;
        }

        return graph.TryGetReturnExitDirection(currentNode, targetNodeId, out exitDirection);
    }

    void EnterNode(string nodeId, RoomExitDirection? entranceDirection = null)
    {
        MapNode nextNode = graph != null ? graph.GetNode(nodeId) : null;
        if (nextNode == null)
        {
            Debug.LogError($"[MapRunController] Cannot enter missing node {nodeId}.", this);
            return;
        }

        RoomDefinitionSO roomDefinition = nextNode.RoomDefinition;
        if (roomDefinition == null || roomDefinition.RoomPrefab == null)
        {
            Debug.LogError($"[MapRunController] Node {nextNode.Id} has no room prefab.", this);
            return;
        }

        isTransitioning = true;
        encounterDirector?.StopEncounter();
        DestroyCurrentRoom();

        Transform anchor = roomSpawnAnchor != null ? roomSpawnAnchor : transform;
        Quaternion roomRotation = anchor.rotation * Quaternion.Euler(0f, nextNode.RoomYawDegrees, 0f);
        Log($"Entering node '{nextNode.Id}' ({nextNode.Type}) with room '{roomDefinition.name}', yaw={nextNode.RoomYawDegrees:0}.");
        currentRoomInstance = Instantiate(roomDefinition.RoomPrefab, anchor.position, roomRotation, roomParent);
        currentRoom = currentRoomInstance.GetComponentInChildren<RoomController>(true);
        if (currentRoom == null)
        {
            Debug.LogWarning($"[MapRunController] Spawned room '{roomDefinition.RoomPrefab.name}' has no RoomController. Adding one at runtime.", currentRoomInstance);
            currentRoom = currentRoomInstance.AddComponent<RoomController>();
        }

        currentNode = nextNode;
        currentNode.Visit();
        graph.RevealOutgoing(currentNode);

        currentRoom.Initialize(this, currentNode);
        RebuildSpawnedRoomNavMesh(currentRoomInstance);
        MovePartyToRoomSpawn(currentRoom, entranceDirection);
        currentRoom.BeginRoom(encounterDirector);

        isTransitioning = false;
        NotifyMapChanged();
    }

    void DestroyCurrentRoom()
    {
        if (currentRoomInstance != null)
        {
            RemoveRoomNavMeshData(currentRoomInstance);
            currentRoomInstance.SetActive(false);
            Destroy(currentRoomInstance);
        }

        currentRoomInstance = null;
        currentRoom = null;
    }

    void RebuildSpawnedRoomNavMesh(GameObject roomInstance)
    {
        if (!rebuildRoomNavMeshAfterSpawn || roomInstance == null)
            return;

        NavMeshSurface[] surfaces = roomInstance.GetComponentsInChildren<NavMeshSurface>(true);
        if (surfaces == null || surfaces.Length == 0)
            return;

        int rebuiltCount = 0;
        for (int i = 0; i < surfaces.Length; i++)
        {
            NavMeshSurface surface = surfaces[i];
            if (surface == null)
                continue;

            surface.RemoveData();
            if (!surface.isActiveAndEnabled)
                continue;

            surface.BuildNavMesh();
            rebuiltCount++;
        }

        Log($"Rebuilt {rebuiltCount} NavMeshSurface(s) for spawned room '{roomInstance.name}'.");
    }

    void RemoveRoomNavMeshData(GameObject roomInstance)
    {
        if (roomInstance == null)
            return;

        NavMeshSurface[] surfaces = roomInstance.GetComponentsInChildren<NavMeshSurface>(true);
        for (int i = 0; i < surfaces.Length; i++)
        {
            NavMeshSurface surface = surfaces[i];
            if (surface != null)
                surface.RemoveData();
        }
    }

    void MovePartyToRoomSpawn(RoomController room, RoomExitDirection? entranceDirection)
    {
        if (room == null)
            return;

        CharacteContext player = ResolvePlayerContext();
        if (player == null)
            return;

        Vector3 previousPlayerPosition = player.transform.position;
        Transform spawn = room.GetPlayerSpawnPoint(entranceDirection);
        WarpCharacter(player, spawn.position, spawn.rotation);

        Vector3 delta = spawn.position - previousPlayerPosition;
        CharacteContext[] contexts = FindObjectsByType<CharacteContext>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < contexts.Length; i++)
        {
            CharacteContext ctx = contexts[i];
            if (ctx == null || ctx == player || ctx.TargetIdentity != AITargetIdentity.Companion)
                continue;

            WarpCharacter(ctx, ctx.transform.position + delta, spawn.rotation);
        }
    }

    CharacteContext ResolvePlayerContext()
    {
        if (playerContext != null)
            return playerContext;

        CharacteContext[] contexts = FindObjectsByType<CharacteContext>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < contexts.Length; i++)
        {
            CharacteContext ctx = contexts[i];
            if (ctx != null && ctx.TargetIdentity == AITargetIdentity.Player)
            {
                playerContext = ctx;
                return playerContext;
            }
        }

        return null;
    }

    void WarpCharacter(CharacteContext ctx, Vector3 position, Quaternion rotation)
    {
        if (ctx == null)
            return;

        ctx.ResolveReferences();

        CharacterController controller = ctx.cc;
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled)
            controller.enabled = false;

        NavMeshAgent agent = ctx.GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = ctx.GetComponentInChildren<NavMeshAgent>(true);

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.Warp(position);
        else
            ctx.transform.position = position;

        ctx.transform.rotation = rotation;

        if (controllerWasEnabled)
            controller.enabled = true;
    }

    void ResolveReferences()
    {
        if (encounterDirector == null)
            encounterDirector = GetComponent<EncounterDirector>();
        if (encounterDirector == null)
            encounterDirector = GetComponentInChildren<EncounterDirector>(true);

        if (mapView == null)
            mapView = FindFirstObjectByType<MapView>(FindObjectsInactive.Include);
    }

    void NotifyMapChanged()
    {
        MapChanged?.Invoke(graph, currentNode);
        mapView?.Refresh(this);
    }

    void Log(string message)
    {
        if (logLifecycle)
            Debug.Log($"[MapRunController] {message}", this);
    }
}
