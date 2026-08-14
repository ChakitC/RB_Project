using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class MapRunController : MonoBehaviour
{
    sealed class CachedRoomEntry
    {
        public readonly MapNode Node;
        public readonly GameObject Instance;
        public readonly RoomController Controller;

        public CachedRoomEntry(MapNode node, GameObject instance, RoomController controller)
        {
            Node = node;
            Instance = instance;
            Controller = controller;
        }

        public RoomRuntimeContent RuntimeContent => Controller != null ? Controller.RuntimeContent : null;
    }

    readonly struct TransformPose
    {
        public readonly Transform Transform;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public TransformPose(Transform transform)
        {
            Transform = transform;
            Position = transform != null ? transform.position : Vector3.zero;
            Rotation = transform != null ? transform.rotation : Quaternion.identity;
        }
    }

    sealed class PartyPoseSnapshot
    {
        public TransformPose RootPose;
        public readonly List<TransformPose> ActorPoses = new();
    }

    private const string DefaultWarpCollisionLayerName = "Terrain";
    private const float CompanionWarpForwardOffset = 1.5f;
    private const float CompanionWarpLateralOffset = 0.9f;
    private const float CompanionWarpRowSpacing = 1.25f;
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
    private CachedRoomEntry currentRoomEntry;
    private readonly Dictionary<string, CachedRoomEntry> roomCache = new();
    private bool isTransitioning;
    private bool warnedMissingWarpCollisionLayer;
    private Transform summonWorldRoot;
    private Transform summonStagingRoot;
    private PartySpawnPoint partySpawnPoint;
    private int stageProgressCount;
    private int stageEnemyLevel;
    private int regularXpRemaining;
    private int bossXpRemaining;
    private int regularEnemiesRemaining;
    private int bossEnemiesRemaining;
    private int completionXpReward;
    private bool bossCleared;
    private bool stageCompletionCommitted;
    private GameObject stageExitInstance;

    public event Action<MapGraph, MapNode> MapChanged;
    public event Action RoomTransitionCommitted;
    public event Action RoomTransitionRolledBack;
    public MapGraph CurrentGraph => graph;
    public MapNode CurrentNode => currentNode;
    public RoomController CurrentRoom => currentRoom;
    public bool IsTransitioning => isTransitioning;
    public int CachedRoomCount => roomCache.Count;
    public MapRunConfigSO RunConfig => runConfig;
    public bool CanCompleteStageRun => runConfig != null && runConfig.IsTestStage && bossCleared && !stageCompletionCommitted;
    public int StageEnemyLevel => stageEnemyLevel;

    public Transform GetOrCreateSummonWorldRoot()
    {
        if (summonWorldRoot != null)
            return summonWorldRoot;

        var rootObject = new GameObject("SummonWorldRoot");
        summonWorldRoot = rootObject.transform;
        summonWorldRoot.SetParent(transform, false);
        return summonWorldRoot;
    }

    public Transform GetOrCreateSummonStagingRoot()
    {
        if (summonStagingRoot != null)
            return summonStagingRoot;

        var rootObject = new GameObject("SummonStagingRoot");
        summonStagingRoot = rootObject.transform;
        summonStagingRoot.SetParent(transform, false);
        rootObject.SetActive(false);
        return summonStagingRoot;
    }

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

        MapRunConfigSO selectedConfig = SceneLoaderSystem.Instance != null
            ? SceneLoaderSystem.Instance.ConsumeSelectedMapRunConfig()
            : null;
        if (selectedConfig != null)
        {
            runConfig = selectedConfig;
        }
        else
        {
            Debug.LogWarning("[MapRunController] No stage selection was supplied by Basement. Using the serialized Run Config fallback.", this);
        }

        if (runConfig == null)
            Debug.LogWarning("[MapRunController] Run Config is missing. Map generation will not be able to assign room definitions.", this);
        else if (!MapRunConfigValidator.Validate(runConfig, out string configError))
        {
            Debug.LogError($"[MapRunController] Run Config is invalid:\n{configError}", this);
            return;
        }

        ResetRoomCache();

        graph = MapGenerator.Generate(runConfig);
        if (graph == null)
        {
            Debug.LogError("[MapRunController] MapGenerator returned null.", this);
            return;
        }

        ConfigureStageRun();
        Log($"Generated map with {graph.Nodes.Count} nodes. Start='{graph.StartNodeId}', Boss='{graph.BossNodeId}', Seed={graph.ResolvedSeed}.");

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

        if (currentRoom != null)
        {
            if (currentRoom.ExitsLocked)
                return false;

            RoomDefinitionSO definition = currentNode.RoomDefinition;
            if (definition != null && definition.RequiresClearBeforeExit && !currentRoom.RoomCleared)
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
        if (runConfig != null && runConfig.IsTestStage && currentNode.Type == MapNodeType.Boss)
        {
            bossCleared = true;
            SpawnStageExit(room);
        }
        NotifyMapChanged();
    }

    public void ConfigureStageEnemy(GameObject enemyObject, bool isBoss)
    {
        if (enemyObject == null || runConfig == null || !runConfig.IsTestStage)
            return;

        EnemyContext enemyContext = enemyObject.GetComponentInChildren<EnemyContext>(true);
        if (enemyContext == null)
        {
            Debug.LogWarning($"[MapRunController] Stage enemy '{enemyObject.name}' has no EnemyContext.", enemyObject);
            return;
        }

        EnemyLevelSystem enemyLevel = enemyContext.EnemyLevelSystem;
        if (enemyLevel == null)
            enemyLevel = enemyContext.GetComponent<EnemyLevelSystem>();
        if (enemyLevel == null)
            enemyLevel = enemyContext.gameObject.AddComponent<EnemyLevelSystem>();
        enemyContext.EnemyLevelSystem = enemyLevel;
        enemyLevel.SetLevel(stageEnemyLevel);

        EnemyHealth health = enemyContext.GetComponentInChildren<EnemyHealth>(true);
        if (health != null)
            health.ConfigureStageXp(this, AllocateEnemyXp(isBoss));
    }

    public void GrantStageEnemyXp(int amount)
    {
        if (runConfig == null || !runConfig.IsTestStage || amount <= 0)
            return;

        GrantXpToDeployedParty(amount);
    }

    public void CompleteStageRunAndReturn()
    {
        if (!CanCompleteStageRun)
            return;

        stageCompletionCommitted = true;
        GrantXpToDeployedParty(completionXpReward);

        int nextProgress = Mathf.Min(runConfig.TargetRunCount, stageProgressCount + 1);
        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveStageProgress(runConfig.StageId, nextProgress);
        else
            Debug.LogWarning("[MapRunController] SaveManager is missing; Stage Progress could not be saved.", this);

        Log($"Completed '{runConfig.StageId}'. Progress {nextProgress}/{runConfig.TargetRunCount}.");
        if (SceneLoaderSystem.Instance != null)
            SceneLoaderSystem.Instance.LoadBasement();
        else
            Debug.LogError("[MapRunController] SceneLoaderSystem is missing; cannot return to Basement.", this);
    }

    void ConfigureStageRun()
    {
        stageProgressCount = 0;
        stageEnemyLevel = 1;
        regularXpRemaining = 0;
        bossXpRemaining = 0;
        regularEnemiesRemaining = 0;
        bossEnemiesRemaining = 0;
        completionXpReward = 0;
        bossCleared = false;
        stageCompletionCommitted = false;
        stageExitInstance = null;

        if (runConfig == null || !runConfig.IsTestStage || graph == null)
            return;

        if (SaveManager.Instance != null)
            stageProgressCount = Mathf.Clamp(SaveManager.Instance.LoadStageProgress(runConfig.StageId), 0, runConfig.TargetRunCount);

        stageEnemyLevel = runConfig.GetEnemyLevel(stageProgressCount);
        int regularSpawnCount = 0;
        int bossSpawnCount = 0;
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            EncounterDefinitionSO encounter = graph.Nodes[i]?.EncounterDefinition;
            if (encounter == null)
                continue;

            if (encounter.BossEncounter)
                bossSpawnCount += encounter.TotalSpawnCount;
            else
                regularSpawnCount += encounter.TotalSpawnCount;
        }

        int budget = runConfig.GetXpBudgetPerRun();
        int regularPool = Mathf.RoundToInt(budget * runConfig.RegularEnemyXpShare);
        int bossPool = Mathf.RoundToInt(budget * runConfig.BossXpShare);
        regularXpRemaining = regularPool;
        bossXpRemaining = bossPool;
        regularEnemiesRemaining = regularSpawnCount;
        bossEnemiesRemaining = bossSpawnCount;
        completionXpReward = Mathf.Max(0, budget - regularPool - bossPool);

        Log($"Stage '{runConfig.StageId}' progress={stageProgressCount}/{runConfig.TargetRunCount}, enemyLv={stageEnemyLevel}, XP budget={budget} (regular pool {regularPool}/{regularSpawnCount}, boss pool {bossPool}/{bossSpawnCount}, completion {completionXpReward}).");
    }

    int AllocateEnemyXp(bool boss)
    {
        int enemiesRemaining = boss ? bossEnemiesRemaining : regularEnemiesRemaining;
        int xpRemaining = boss ? bossXpRemaining : regularXpRemaining;
        if (enemiesRemaining <= 0 || xpRemaining <= 0)
            return 0;

        int reward = Mathf.CeilToInt((float)xpRemaining / enemiesRemaining);
        if (boss)
        {
            bossXpRemaining = Mathf.Max(0, bossXpRemaining - reward);
            bossEnemiesRemaining = Mathf.Max(0, bossEnemiesRemaining - 1);
        }
        else
        {
            regularXpRemaining = Mathf.Max(0, regularXpRemaining - reward);
            regularEnemiesRemaining = Mathf.Max(0, regularEnemiesRemaining - 1);
        }

        return reward;
    }

    void GrantXpToDeployedParty(int amount)
    {
        if (amount <= 0)
            return;

        if (partySpawnPoint == null)
            partySpawnPoint = FindFirstObjectByType<PartySpawnPoint>();
        PartyRuntime party = partySpawnPoint != null ? partySpawnPoint.CurrentParty : null;
        if (party == null)
        {
            Debug.LogWarning("[MapRunController] No deployed PartyRuntime was found for Stage XP.", this);
            return;
        }

        for (int i = 0; i < party.Actors.Count; i++)
        {
            PartyRuntimeActor actor = party.Actors[i];
            LevelSystem levelSystem = actor?.Context != null
                ? actor.Context.GetComponentInChildren<LevelSystem>(true)
                : null;
            if (levelSystem != null)
                levelSystem.AddXp(amount);
        }
    }

    void SpawnStageExit(RoomController room)
    {
        if (stageExitInstance != null || room == null || runConfig.StageExitPrefab == null)
            return;

        Transform spawnPoint = room.GetStageExitSpawnPoint();
        Transform parent = room.RuntimeContent != null ? room.RuntimeContent.PersistentRoot : room.transform;
        stageExitInstance = Instantiate(runConfig.StageExitPrefab, spawnPoint.position, spawnPoint.rotation, parent);
        StageExitInteractable stageExit = stageExitInstance.GetComponentInChildren<StageExitInteractable>(true);
        if (stageExit == null)
            stageExit = stageExitInstance.AddComponent<StageExitInteractable>();
        stageExit.Configure(this);
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

        CachedRoomEntry previousEntry = currentRoomEntry;
        MapNode previousNode = currentNode;
        PartyPoseSnapshot previousPartyPose = CapturePartyPose();

        encounterDirector?.StopEncounter();
        CleanupOutgoingRoom(previousEntry);
        BindItemDropParent(null);
        DeactivateCachedRoom(previousEntry);

        CachedRoomEntry nextEntry = GetOrCreateCachedRoom(nextNode, roomDefinition);
        ActivateCachedRoom(nextEntry);
        nextEntry.Controller.Initialize(this, nextNode);
        BindItemDropParent(nextEntry.RuntimeContent.PersistentRoot);

        if (!MovePartyToRoomSpawn(nextEntry.Controller, entranceDirection))
        {
            Debug.LogError($"[MapRunController] Failed to move the party into node {nextNode.Id}. Rolling back the room transition.", this);
            RollbackTransition(previousEntry, previousNode, previousPartyPose, nextEntry);
            RoomTransitionRolledBack?.Invoke();
            isTransitioning = false;
            return;
        }

        currentRoomEntry = nextEntry;
        currentRoomInstance = nextEntry.Instance;
        currentRoom = nextEntry.Controller;
        currentNode = nextNode;
        currentNode.Visit();
        graph.RevealOutgoing(currentNode);

        RoomTransitionCommitted?.Invoke();
        currentRoom.BeginRoom(encounterDirector);

        isTransitioning = false;
        NotifyMapChanged();
    }

    CachedRoomEntry GetOrCreateCachedRoom(MapNode node, RoomDefinitionSO roomDefinition)
    {
        if (roomCache.TryGetValue(node.Id, out CachedRoomEntry cachedEntry) && cachedEntry.Instance != null)
        {
            Log($"Reusing cached room for node '{node.Id}' (instance {cachedEntry.Instance.GetInstanceID()}).");
            return cachedEntry;
        }

        Transform anchor = roomSpawnAnchor != null ? roomSpawnAnchor : transform;
        Quaternion roomRotation = anchor.rotation * Quaternion.Euler(0f, node.RoomYawDegrees, 0f);
        Log($"Creating room for node '{node.Id}' ({node.Type}) with '{roomDefinition.name}', yaw={node.RoomYawDegrees:0}.");

        GameObject instance = Instantiate(roomDefinition.RoomPrefab, anchor.position, roomRotation, roomParent);
        RoomController controller = instance.GetComponentInChildren<RoomController>(true);
        if (controller == null)
        {
            Debug.LogWarning($"[MapRunController] Spawned room '{roomDefinition.RoomPrefab.name}' has no RoomController. Adding one at runtime.", instance);
            controller = instance.AddComponent<RoomController>();
        }

        var entry = new CachedRoomEntry(node, instance, controller);
        roomCache[node.Id] = entry;
        Log($"Cached room for node '{node.Id}' (instance {instance.GetInstanceID()}).");
        return entry;
    }

    void ActivateCachedRoom(CachedRoomEntry entry)
    {
        if (entry == null || entry.Instance == null)
            return;

        if (!entry.Instance.activeSelf)
            entry.Instance.SetActive(true);

        Physics.SyncTransforms();
    }

    void DeactivateCachedRoom(CachedRoomEntry entry)
    {
        if (entry == null || entry.Instance == null)
            return;

        RemoveRoomNavMeshData(entry.Instance);
        entry.Instance.SetActive(false);
        Log($"Deactivated cached room for node '{entry.Node.Id}'.");
    }

    void CleanupOutgoingRoom(CachedRoomEntry entry)
    {
        RoomTransitionCleanup.ClearTransientWorldObjects();
        if (entry == null || entry.RuntimeContent == null)
            return;

        entry.RuntimeContent.ClearTemporaryContent();
        entry.RuntimeContent.ClearEncounterContent();
    }

    void RollbackTransition(
        CachedRoomEntry previousEntry,
        MapNode previousNode,
        PartyPoseSnapshot previousPartyPose,
        CachedRoomEntry failedEntry)
    {
        BindItemDropParent(null);
        DeactivateCachedRoom(failedEntry);

        if (previousEntry == null || previousEntry.Instance == null)
        {
            ActivateCachedRoom(failedEntry);
            currentRoomEntry = failedEntry;
            currentRoomInstance = failedEntry != null ? failedEntry.Instance : null;
            currentRoom = failedEntry != null ? failedEntry.Controller : null;
            currentNode = failedEntry != null ? failedEntry.Node : previousNode;
            if (failedEntry != null && failedEntry.RuntimeContent != null)
                BindItemDropParent(failedEntry.RuntimeContent.PersistentRoot);
            return;
        }

        ActivateCachedRoom(previousEntry);
        previousEntry.Controller.Initialize(this, previousNode);
        RestorePartyPose(previousPartyPose);
        BindItemDropParent(previousEntry.RuntimeContent.PersistentRoot);

        currentRoomEntry = previousEntry;
        currentRoomInstance = previousEntry.Instance;
        currentRoom = previousEntry.Controller;
        currentNode = previousNode;
    }

    void ResetRoomCache()
    {
        encounterDirector?.StopEncounter();
        RoomTransitionCleanup.ClearTransientWorldObjects();
        CleanupSummonWorldRoot();
        BindItemDropParent(null);

        foreach (CachedRoomEntry entry in roomCache.Values)
        {
            if (entry == null || entry.Instance == null)
                continue;

            RemoveRoomNavMeshData(entry.Instance);
            entry.Instance.SetActive(false);
            Destroy(entry.Instance);
        }

        roomCache.Clear();
        currentRoomEntry = null;
        currentRoomInstance = null;
        currentRoom = null;
        currentNode = null;
        isTransitioning = false;
    }

    void CleanupSummonWorldRoot()
    {
        if (summonWorldRoot != null)
        {
            SummonedEntityRuntime[] summons = summonWorldRoot.GetComponentsInChildren<SummonedEntityRuntime>(true);
            for (int i = 0; i < summons.Length; i++)
            {
                SummonedEntityRuntime summon = summons[i];
                if (summon == null)
                    continue;

                summon.BeginDespawn(SummonDespawnReason.RunEnded);
                summon.ForceDestroy(SummonDespawnReason.RunEnded);
            }

            if (Application.isPlaying)
                Destroy(summonWorldRoot.gameObject);
            else
                DestroyImmediate(summonWorldRoot.gameObject);

            summonWorldRoot = null;
        }

        if (summonStagingRoot != null)
        {
            if (Application.isPlaying)
                Destroy(summonStagingRoot.gameObject);
            else
                DestroyImmediate(summonStagingRoot.gameObject);

            summonStagingRoot = null;
        }
    }

    void OnDestroy()
    {
        if (Application.isPlaying)
            ResetRoomCache();
    }

    void BindItemDropParent(Transform parent)
    {
        if (ItemDropManager.Instance == null)
            return;

        if (parent != null)
            ItemDropManager.Instance.SetSpawnParent(parent);
        else
            ItemDropManager.Instance.ClearSpawnParent();
    }

    PartyPoseSnapshot CapturePartyPose()
    {
        var snapshot = new PartyPoseSnapshot();
        CharacteContext player = ResolvePlayerContext();
        if (player == null)
            return snapshot;

        Transform partyRoot = ResolvePartyWarpRoot(player);
        snapshot.RootPose = new TransformPose(partyRoot != null ? partyRoot : player.transform);

        CharacteContext[] contexts = FindObjectsByType<CharacteContext>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < contexts.Length; i++)
        {
            CharacteContext context = contexts[i];
            if (context == null)
                continue;

            AITargetIdentity identity = context.TargetIdentity;
            if (context == player ||
                (context.ParticipatesInPartyRuntime &&
                 (identity == AITargetIdentity.Player || identity == AITargetIdentity.Companion)))
                snapshot.ActorPoses.Add(new TransformPose(context.transform));
        }

        return snapshot;
    }

    void RestorePartyPose(PartyPoseSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        Transform root = snapshot.RootPose.Transform;
        if (root != null)
        {
            CharacterController[] controllers = root.GetComponentsInChildren<CharacterController>(true);
            bool[] controllerStates = DisableControllers(controllers);
            root.SetPositionAndRotation(snapshot.RootPose.Position, snapshot.RootPose.Rotation);
            RestoreControllers(controllers, controllerStates);
            SyncWarpedAgentsUnderRoot(root);
        }

        for (int i = 0; i < snapshot.ActorPoses.Count; i++)
            RestoreActorPose(snapshot.ActorPoses[i]);

        Physics.SyncTransforms();
    }

    void RestoreActorPose(TransformPose pose)
    {
        Transform actor = pose.Transform;
        if (actor == null)
            return;

        CharacterController controller = actor.GetComponent<CharacterController>();
        if (controller == null)
            controller = actor.GetComponentInChildren<CharacterController>(true);

        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled)
            controller.enabled = false;

        NavMeshAgent agent = actor.GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = actor.GetComponentInChildren<NavMeshAgent>(true);

        if (agent != null && agent.isActiveAndEnabled)
        {
            if (!agent.Warp(pose.Position))
                actor.position = pose.Position;
            else
                SyncWarpedAgent(agent);
        }
        else
        {
            actor.position = pose.Position;
        }

        actor.rotation = pose.Rotation;
        if (controllerWasEnabled)
            controller.enabled = true;
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

        Transform spawn = room.GetPlayerSpawnPoint(entranceDirection);
        Transform partyRoot = ResolvePartyWarpRoot(player);
        if (partyRoot != null && partyRoot != player.transform)
        {
            if (!WarpPartyRootToPlayerSpawn(player, partyRoot, spawn.position, spawn.rotation))
                return false;
        }
        else
        {
            if (!WarpCharacter(player, spawn.position, spawn.rotation))
                return false;
        }

        CharacteContext[] contexts = FindObjectsByType<CharacteContext>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int companionIndex = 0;
        for (int i = 0; i < contexts.Length; i++)
        {
            CharacteContext ctx = contexts[i];
            if (ctx == null ||
                ctx == player ||
                !ctx.ParticipatesInPartyRuntime ||
                ctx.TargetIdentity != AITargetIdentity.Companion ||
                !ctx.gameObject.activeInHierarchy)
                continue;

            if (!WarpCompanionToRoomSpawn(ctx, player.transform, companionIndex))
                return false;

            companionIndex++;
        }

        return true;
    }

    Transform ResolvePartyWarpRoot(CharacteContext player)
    {
        if (player == null)
            return null;

        Transform current = player.transform.parent;
        while (current != null)
        {
            if (IsPartyWarpRootCandidate(current, player))
                return current;

            current = current.parent;
        }

        return player.transform;
    }

    bool IsPartyWarpRootCandidate(Transform candidate, CharacteContext player)
    {
        if (candidate == null || player == null)
            return false;

        if (ContainsIgnoreCase(candidate.name, "Squad") ||
            ContainsIgnoreCase(candidate.name, "Party"))
        {
            return true;
        }

        CharacteContext[] contexts = candidate.GetComponentsInChildren<CharacteContext>(true);
        bool hasPlayer = false;
        bool hasCompanion = false;
        bool hasEnemy = false;

        for (int i = 0; i < contexts.Length; i++)
        {
            CharacteContext ctx = contexts[i];
            if (ctx == null)
                continue;

            AITargetIdentity identity = ctx.TargetIdentity;
            if (ctx == player || (ctx.ParticipatesInPartyRuntime && identity == AITargetIdentity.Player))
                hasPlayer = true;
            else if (ctx.ParticipatesInPartyRuntime && identity == AITargetIdentity.Companion)
                hasCompanion = true;
            else if (identity == AITargetIdentity.Enemy)
                hasEnemy = true;
        }

        return hasPlayer && hasCompanion && !hasEnemy;
    }

    static bool ContainsIgnoreCase(string value, string search)
    {
        return !string.IsNullOrEmpty(value) &&
               !string.IsNullOrEmpty(search) &&
               value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    bool WarpPartyRootToPlayerSpawn(
        CharacteContext player,
        Transform partyRoot,
        Vector3 position,
        Quaternion rotation)
    {
        if (player == null || partyRoot == null)
            return false;

        player.ResolveReferences();

        NavMeshAgent playerAgent = player.GetComponent<NavMeshAgent>();
        if (playerAgent == null)
            playerAgent = player.GetComponentInChildren<NavMeshAgent>(true);

        if (!TryResolveSafeWarpPosition(player, playerAgent, position, rotation, out Vector3 safePosition, out Collider blocker))
        {
            WarnWarpBlocked(player, position, blocker);
            Physics.SyncTransforms();
            return false;
        }

        CharacterController[] controllers = partyRoot.GetComponentsInChildren<CharacterController>(true);
        bool[] controllerEnabledStates = DisableControllers(controllers);

        try
        {
            SetRootPoseForChildPose(partyRoot, player.transform, safePosition, rotation);
            SyncWarpedAgentsUnderRoot(partyRoot);
        }
        finally
        {
            RestoreControllers(controllers, controllerEnabledStates);
            Physics.SyncTransforms();
        }

        return true;
    }

    bool WarpCompanionToRoomSpawn(CharacteContext companion, Transform player, int companionIndex)
    {
        if (companion == null || player == null)
            return false;

        int row = companionIndex / 2;
        float side = companionIndex % 2 == 0 ? -1f : 1f;
        Vector3 formationPosition =
            player.position +
            player.forward * (CompanionWarpForwardOffset + row * CompanionWarpRowSpacing) +
            player.right * (CompanionWarpLateralOffset * side);

        if (WarpCharacter(companion, formationPosition, player.rotation, requireActiveAgentNavMesh: true))
            return true;

        Vector3 centerFallback =
            player.position +
            player.forward * (CompanionWarpForwardOffset + companionIndex * CompanionWarpRowSpacing);

        if (WarpCharacter(companion, centerFallback, player.rotation, requireActiveAgentNavMesh: true))
        {
            Debug.LogWarning(
                $"[MapRunController] Companion '{companion.name}' used the center room-entry fallback at {centerFallback}.",
                this);
            return true;
        }

        Debug.LogError(
            $"[MapRunController] Companion '{companion.name}' could not be placed on the new room NavMesh.",
            this);
        return false;
    }

    static void SetRootPoseForChildPose(
        Transform root,
        Transform child,
        Vector3 childWorldPosition,
        Quaternion childWorldRotation)
    {
        if (root == null || child == null)
            return;

        Quaternion childLocalRotationToRoot = Quaternion.Inverse(root.rotation) * child.rotation;
        root.rotation = childWorldRotation * Quaternion.Inverse(childLocalRotationToRoot);
        root.position += childWorldPosition - child.position;
    }

    bool[] DisableControllers(CharacterController[] controllers)
    {
        if (controllers == null)
            return Array.Empty<bool>();

        bool[] enabledStates = new bool[controllers.Length];
        for (int i = 0; i < controllers.Length; i++)
        {
            CharacterController controller = controllers[i];
            if (controller == null)
                continue;

            enabledStates[i] = controller.enabled;
            if (controller.enabled)
                controller.enabled = false;
        }

        return enabledStates;
    }

    static void RestoreControllers(CharacterController[] controllers, bool[] enabledStates)
    {
        if (controllers == null || enabledStates == null)
            return;

        int count = Mathf.Min(controllers.Length, enabledStates.Length);
        for (int i = 0; i < count; i++)
        {
            if (controllers[i] != null)
                controllers[i].enabled = enabledStates[i];
        }
    }

    void SyncWarpedAgentsUnderRoot(Transform root)
    {
        if (root == null)
            return;

        NavMeshAgent[] agents = root.GetComponentsInChildren<NavMeshAgent>(true);
        for (int i = 0; i < agents.Length; i++)
        {
            NavMeshAgent agent = agents[i];
            if (agent != null && agent.enabled)
                TryWarpAgent(agent, agent.transform.position);
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
            if (ctx != null && ctx.ParticipatesInPartyRuntime && ctx.TargetIdentity == AITargetIdentity.Player)
            {
                playerContext = ctx;
                return playerContext;
            }
        }

        return null;
    }

    bool WarpCharacter(
        CharacteContext ctx,
        Vector3 position,
        Quaternion rotation,
        bool requireActiveAgentNavMesh = false)
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

        bool hasActiveAgent = agent != null && agent.isActiveAndEnabled;
        if (requireActiveAgentNavMesh && !hasActiveAgent)
        {
            if (controllerWasEnabled)
                controller.enabled = true;

            Debug.LogWarning(
                $"[MapRunController] Warp for '{ctx.name}' requires an active NavMeshAgent, but none was available.",
                this);
            return false;
        }

        if (!TryResolveSafeWarpPosition(ctx, agent, position, rotation, out Vector3 safePosition, out Collider blocker))
        {
            if (controllerWasEnabled)
                controller.enabled = true;

            WarnWarpBlocked(ctx, position, blocker);
            Physics.SyncTransforms();
            return false;
        }

        if (hasActiveAgent)
        {
            if (!TryWarpAgent(agent, safePosition))
            {
                if (controllerWasEnabled)
                    controller.enabled = true;

                Debug.LogWarning(
                    $"[MapRunController] NavMesh warp for '{ctx.name}' failed near {safePosition}.",
                    this);
                Physics.SyncTransforms();
                return false;
            }
        }
        else
        {
            ctx.transform.position = safePosition;
        }

        ctx.transform.rotation = rotation;

        if (controllerWasEnabled)
            controller.enabled = true;

        Physics.SyncTransforms();
        return true;
    }

    public bool TryWarpTransientActor(
        CharacteContext actor,
        CharacteContext anchor,
        int formationIndex,
        out Vector3 resolvedPosition)
    {
        resolvedPosition = Vector3.zero;
        if (actor == null || anchor == null || actor == anchor)
            return false;

        int row = Mathf.Max(0, formationIndex) / 2;
        float side = Mathf.Max(0, formationIndex) % 2 == 0 ? -1f : 1f;
        Vector3 position = anchor.transform.position +
                           anchor.transform.forward * (CompanionWarpForwardOffset + row * CompanionWarpRowSpacing) +
                           anchor.transform.right * (CompanionWarpLateralOffset * side);

        bool requireActiveAgent = actor is SummonContext summonContext &&
                                   summonContext.Mobility == SummonMobility.Mobile;
        if (!WarpCharacter(actor, position, anchor.transform.rotation, requireActiveAgent))
            return false;

        resolvedPosition = actor.transform.position;
        return true;
    }

    bool TryWarpAgent(NavMeshAgent agent, Vector3 position, bool allowNavMeshSample = true)
    {
        if (agent == null || !agent.isActiveAndEnabled)
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
