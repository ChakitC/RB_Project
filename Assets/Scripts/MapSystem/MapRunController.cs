using System;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class MapRunController : MonoBehaviour
{
    private const string DefaultWarpCollisionLayerName = "Terrain";
    private static readonly Collider[] WarpCollisionHits = new Collider[32];

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
    [SerializeField, Min(0f)] private float partyWarpNavMeshSampleRadius = 3f;

    [Header("Runtime Warp Safety")]
    [Tooltip("0 uses the Terrain layer by name.")]
    [SerializeField] private LayerMask partyWarpCollisionMask;
    [SerializeField, Min(0f)] private float partyWarpCollisionPadding = 0.05f;
    [SerializeField, Min(0f)] private float partyWarpGroundClearance = 0.08f;
    [SerializeField, Min(0f)] private float partyWarpSafeSearchRadius = 2f;
    [SerializeField, Min(4)] private int partyWarpSafeSearchSteps = 12;

    [Header("Debug")]
    [SerializeField] private bool logLifecycle = true;

    private MapGraph graph;
    private MapNode currentNode;
    private GameObject currentRoomInstance;
    private RoomController currentRoom;
    private bool isTransitioning;
    private bool warnedMissingWarpCollisionLayer;

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
        if (!MovePartyToRoomSpawn(currentRoom, entranceDirection))
        {
            Debug.LogError($"[MapRunController] Failed to move player into node {currentNode.Id}. Room encounter will not start.", this);
            isTransitioning = false;
            NotifyMapChanged();
            return;
        }

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

    bool MovePartyToRoomSpawn(RoomController room, RoomExitDirection? entranceDirection)
    {
        if (room == null)
            return false;

        CharacteContext player = ResolvePlayerContext();
        if (player == null)
            return true;

        Vector3 previousPlayerPosition = player.transform.position;
        Transform spawn = room.GetPlayerSpawnPoint(entranceDirection);
        if (!WarpCharacter(player, spawn.position, spawn.rotation))
            return false;

        Vector3 delta = player.transform.position - previousPlayerPosition;
        CharacteContext[] contexts = FindObjectsByType<CharacteContext>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < contexts.Length; i++)
        {
            CharacteContext ctx = contexts[i];
            if (ctx == null || ctx == player || ctx.TargetIdentity != AITargetIdentity.Companion)
                continue;

            WarpCharacter(ctx, ctx.transform.position + delta, spawn.rotation);
        }

        return true;
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

    bool WarpCharacter(CharacteContext ctx, Vector3 position, Quaternion rotation)
    {
        if (ctx == null)
            return false;

        ctx.ResolveReferences();

        CharacterController controller = ctx.cc;
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled)
            controller.enabled = false;

        NavMeshAgent agent = ctx.GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = ctx.GetComponentInChildren<NavMeshAgent>(true);

        if (!TryResolveSafeWarpPosition(ctx, agent, position, rotation, out Vector3 safePosition, out Collider blocker))
        {
            if (controllerWasEnabled)
                controller.enabled = true;

            WarnWarpBlocked(ctx, position, blocker);
            Physics.SyncTransforms();
            return false;
        }

        if (agent == null || !agent.enabled || !TryWarpAgent(agent, safePosition, false))
            ctx.transform.position = safePosition;

        ctx.transform.rotation = rotation;

        if (controllerWasEnabled)
            controller.enabled = true;

        Physics.SyncTransforms();
        return true;
    }

    bool TryWarpAgent(NavMeshAgent agent, Vector3 position, bool allowNavMeshSample = true)
    {
        if (agent == null || !agent.enabled)
            return false;

        if (agent.Warp(position))
        {
            SyncWarpedAgent(agent);
            return true;
        }

        if (!allowNavMeshSample)
            return false;

        if (TrySampleAgentNavMeshPosition(agent, position, out Vector3 navMeshPosition) &&
            agent.Warp(navMeshPosition))
        {
            SyncWarpedAgent(agent);
            return true;
        }

        return false;
    }

    bool TryResolveSafeWarpPosition(
        CharacteContext ctx,
        NavMeshAgent agent,
        Vector3 position,
        Quaternion rotation,
        out Vector3 safePosition,
        out Collider blocker)
    {
        safePosition = position;
        blocker = null;
        Physics.SyncTransforms();

        if (TryResolveSafeWarpCandidate(ctx, agent, position, rotation, out safePosition, out blocker))
            return true;

        float searchRadius = Mathf.Max(partyWarpSafeSearchRadius, partyWarpNavMeshSampleRadius, 0f);
        if (searchRadius <= 0f)
            return false;

        int steps = Mathf.Max(4, partyWarpSafeSearchSteps);
        const int ringCount = 3;
        for (int ring = 1; ring <= ringCount; ring++)
        {
            float radius = searchRadius * ring / ringCount;
            float angleOffset = ring % 2 == 0 ? Mathf.PI / steps : 0f;

            for (int i = 0; i < steps; i++)
            {
                float angle = angleOffset + Mathf.PI * 2f * i / steps;
                Vector3 candidate = position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (TryResolveSafeWarpCandidate(ctx, agent, candidate, rotation, out safePosition, out blocker))
                    return true;
            }
        }

        return false;
    }

    bool TryResolveSafeWarpCandidate(
        CharacteContext ctx,
        NavMeshAgent agent,
        Vector3 candidate,
        Quaternion rotation,
        out Vector3 safePosition,
        out Collider blocker)
    {
        safePosition = candidate;
        blocker = null;

        if (agent != null && agent.enabled &&
            TrySampleAgentNavMeshPosition(agent, candidate, out Vector3 navMeshPosition))
        {
            if (IsWarpPositionClear(ctx, agent, navMeshPosition, rotation, out blocker))
            {
                safePosition = navMeshPosition;
                return true;
            }
        }

        if (IsWarpPositionClear(ctx, agent, candidate, rotation, out blocker))
        {
            safePosition = candidate;
            return true;
        }

        return false;
    }

    bool IsWarpPositionClear(
        CharacteContext ctx,
        NavMeshAgent agent,
        Vector3 position,
        Quaternion rotation,
        out Collider blocker)
    {
        blocker = null;

        int collisionMask = ResolveWarpCollisionMask();
        if (collisionMask == 0)
            return true;

        GetWarpProbeCapsule(ctx, agent, position, rotation, out Vector3 pointA, out Vector3 pointB, out float radius);
        int hitCount = Physics.OverlapCapsuleNonAlloc(
            pointA,
            pointB,
            radius,
            WarpCollisionHits,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = WarpCollisionHits[i];
            WarpCollisionHits[i] = null;

            if (hit == null || IsOwnCollider(ctx, hit))
                continue;

            blocker = hit;
            return false;
        }

        return true;
    }

    void GetWarpProbeCapsule(
        CharacteContext ctx,
        NavMeshAgent agent,
        Vector3 position,
        Quaternion rotation,
        out Vector3 pointA,
        out Vector3 pointB,
        out float radius)
    {
        Vector3 up = rotation * Vector3.up;
        float height;
        Vector3 center;

        CharacterController controller = ctx != null ? ctx.cc : null;
        if (controller != null)
        {
            Vector3 scale = controller.transform.lossyScale;
            Vector3 scaledCenter = Vector3.Scale(controller.center, Abs(scale));
            center = position + rotation * scaledCenter;
            radius = Mathf.Max(0.05f, controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) + partyWarpCollisionPadding);
            height = Mathf.Max(controller.height * Mathf.Abs(scale.y), radius * 2f);
        }
        else if (agent != null)
        {
            radius = Mathf.Max(0.05f, agent.radius + partyWarpCollisionPadding);
            height = Mathf.Max(agent.height, radius * 2f);
            center = position + up * (height * 0.5f);
        }
        else
        {
            radius = Mathf.Max(0.05f, 0.35f + partyWarpCollisionPadding);
            height = Mathf.Max(1.8f, radius * 2f);
            center = position + up * (height * 0.5f);
        }

        float halfLine = Mathf.Max(0f, height * 0.5f - radius);
        float groundClearance = Mathf.Min(Mathf.Max(0f, partyWarpGroundClearance), height * 0.25f);
        pointA = center + up * halfLine;
        pointB = center - up * halfLine + up * groundClearance;
    }

    int ResolveWarpCollisionMask()
    {
        if (partyWarpCollisionMask.value != 0)
            return partyWarpCollisionMask.value;

        int terrainLayer = LayerMask.NameToLayer(DefaultWarpCollisionLayerName);
        if (terrainLayer >= 0)
            return 1 << terrainLayer;

        if (!warnedMissingWarpCollisionLayer)
        {
            warnedMissingWarpCollisionLayer = true;
            Debug.LogWarning($"[MapRunController] Layer '{DefaultWarpCollisionLayerName}' was not found. Warp collision checks are disabled.", this);
        }

        return 0;
    }

    static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    static bool IsOwnCollider(CharacteContext ctx, Collider hit)
    {
        return ctx != null &&
               hit != null &&
               (hit.transform == ctx.transform || hit.transform.IsChildOf(ctx.transform));
    }

    void WarnWarpBlocked(CharacteContext ctx, Vector3 position, Collider blocker)
    {
        string actorName = ctx != null ? ctx.name : "Unknown";
        string blockerName = blocker != null ? $"{blocker.name} ({LayerMask.LayerToName(blocker.gameObject.layer)})" : "unknown Terrain collider";
        Debug.LogWarning($"[MapRunController] Warp for '{actorName}' was blocked near {position} by {blockerName}.", this);
    }

    bool TrySampleAgentNavMeshPosition(NavMeshAgent agent, Vector3 position, out Vector3 navMeshPosition)
    {
        navMeshPosition = position;
        if (agent == null)
            return false;

        float sampleRadius = Mathf.Max(partyWarpNavMeshSampleRadius, agent.radius * 2f, 0.25f);
        int areaMask = agent.areaMask;
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, sampleRadius, areaMask))
        {
            navMeshPosition = hit.position;
            return true;
        }

        if (areaMask != NavMesh.AllAreas &&
            NavMesh.SamplePosition(position, out hit, sampleRadius, NavMesh.AllAreas))
        {
            navMeshPosition = hit.position;
            return true;
        }

        return false;
    }

    static void SyncWarpedAgent(NavMeshAgent agent)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.ResetPath();
        agent.nextPosition = agent.transform.position;
    }

    void ResolveReferences()
    {
        if (encounterDirector == null)
            encounterDirector = GetComponent<EncounterDirector>();
        if (encounterDirector == null)
            encounterDirector = GetComponentInChildren<EncounterDirector>(true);
        if (encounterDirector == null && Application.isPlaying)
        {
            encounterDirector = gameObject.AddComponent<EncounterDirector>();
            Log("EncounterDirector was missing. Added one at runtime.");
        }

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
