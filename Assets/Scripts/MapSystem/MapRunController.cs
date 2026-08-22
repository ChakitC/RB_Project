using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The scene-facing entry point for a map run. It owns the serialized setup and the run lifecycle,
/// and delegates the work to four collaborators:
///
/// <list type="bullet">
/// <item><see cref="MapRunSession"/> — run state and travel rules.</item>
/// <item><see cref="RoomRuntimeCache"/> — room instances and their activation.</item>
/// <item><see cref="PartyRoomTransitionService"/> — moving the party and putting it back.</item>
/// <item><see cref="StageRunProgressionService"/> — Test Stage progress, XP, and completion.</item>
/// </list>
///
/// Everything the shop, summon, room, and enemy systems already call still lives here, so prefabs
/// and scenes keep the same contract.
/// </summary>
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

    [Tooltip("context ของผู้เล่น ถ้าไม่ใส่ ระบบจะใช้ player actor ของ PartyRuntime")]
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

    private MapRunSession session;
    private RoomRuntimeCache roomCache;
    private PartyRoomTransitionService partyTransitions;
    private StageRunProgressionService stageProgression;

    private Transform summonWorldRoot;
    private Transform summonStagingRoot;
    private bool stageIntroAttempted;
    private readonly List<Transform> cleanupRoomRoots = new();

    public event Action<MapGraph, MapNode> MapChanged;
    public event Action RoomTransitionCommitted;
    public event Action RoomTransitionRolledBack;

    public MapGraph CurrentGraph => Session.Graph;
    public MapNode CurrentNode => Session.CurrentNode;
    public RoomController CurrentRoom => Session.CurrentRoom;
    public bool IsTransitioning => Session.IsTransitioning;
    public bool HasActiveRoom => Session.HasActiveRoom;
    public int CachedRoomCount => RoomCache.Count;
    public MapRunConfigSO RunConfig => runConfig;
    public bool CanCompleteStageRun => StageProgression.CanCompleteStageRun;
    public int StageEnemyLevel => StageProgression.StageEnemyLevel;

    /// <summary>
    /// Test seam. When set, it replaces the party warp step so the commit and rollback halves of
    /// a room transition can be exercised without a live party, NavMesh, or physics scene.
    /// Always null during normal play.
    /// </summary>
    public Func<RoomController, RoomExitDirection?, bool> PartyWarpOverride { get; set; }

    MapRunSession Session => session ??= new MapRunSession();

    RoomRuntimeCache RoomCache => roomCache ??= new RoomRuntimeCache(Log);

    PartyRoomTransitionService PartyTransitions
    {
        get
        {
            if (partyTransitions == null)
                partyTransitions = new PartyRoomTransitionService(this);

            partyTransitions.Configure(BuildWarpSettings(), playerContext);
            return partyTransitions;
        }
    }

    StageRunProgressionService StageProgression =>
        stageProgression ??= new StageRunProgressionService(this, () => PartyTransitions.ResolveParty(), Log);

    PartyWarpSettings BuildWarpSettings()
    {
        return new PartyWarpSettings(
            partyWarpNavMeshSampleRadius,
            partyWarpCollisionMask,
            partyWarpCollisionPadding,
            partyWarpGroundClearance,
            partyWarpSafeSearchRadius,
            partyWarpSafeSearchSteps);
    }

    // ------------------------------------------------------------------ summon hosting

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

    public bool TryWarpTransientActor(
        CharacteContext actor,
        CharacteContext anchor,
        int formationIndex,
        out Vector3 resolvedPosition)
    {
        return PartyTransitions.TryWarpTransientActor(actor, anchor, formationIndex, out resolvedPosition);
    }

    // ------------------------------------------------------------------ run lifecycle

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
        stageIntroAttempted = false;

        MapGraph graph = MapGenerator.Generate(runConfig);
        if (graph == null)
        {
            Debug.LogError("[MapRunController] MapGenerator returned null.", this);
            return;
        }

        Session.SetGraph(graph);
        StageProgression.BeginRun(runConfig, graph);
        Log($"Generated map with {graph.Nodes.Count} nodes. Start='{graph.StartNodeId}', Boss='{graph.BossNodeId}', Seed={graph.ResolvedSeed}.");

        if (!MapPathValidator.Validate(graph, runConfig, out string error))
        {
            Debug.LogError($"[MapRunController] Generated map is invalid: {error}", this);
            return;
        }

        if (!TryEnterNode(graph.StartNodeId, null))
        {
            Debug.LogError(
                "[MapRunController] The party could not be moved into the Start room. " +
                "The run has no current room; call TryEnterStartRoom to retry or AbortRun to end it.",
                this);
        }
    }

    /// <summary>
    /// Retries the Start room after a failed first entry. Returns false when there is nothing to
    /// retry, either because no map is generated or because a room is already committed.
    /// </summary>
    public bool TryEnterStartRoom()
    {
        if (Session.Graph == null || Session.IsTransitioning || Session.CurrentNode != null)
            return false;

        return TryEnterNode(Session.Graph.StartNodeId, null);
    }

    /// <summary>
    /// Ends the run and releases every cached room. Use it when the Start room cannot be entered.
    /// </summary>
    public void AbortRun()
    {
        ResetRoomCache();
        Session.ClearRun();
        NotifyMapChanged();
    }

    // ------------------------------------------------------------------ travel

    public MapNode GetNode(string nodeId)
    {
        return Session.GetNode(nodeId);
    }

    public bool CanTravelTo(string targetNodeId)
    {
        return Session.CanTravelTo(targetNodeId);
    }

    public bool CanTravelTo(string targetNodeId, RoomExitDirection exitDirection)
    {
        return Session.CanTravelTo(targetNodeId, exitDirection);
    }

    public void RequestTravelTo(string targetNodeId)
    {
        if (!Session.CanTravelTo(targetNodeId))
            return;

        RoomExitDirection? entranceDirection = null;
        if (Session.TryResolveTravelExitDirection(targetNodeId, out RoomExitDirection exitDirection))
            entranceDirection = RoomExitDirectionUtility.Opposite(exitDirection);

        TryEnterNode(targetNodeId, entranceDirection);
    }

    public void RequestTravelTo(string targetNodeId, RoomExitDirection exitDirection)
    {
        if (!Session.CanTravelTo(targetNodeId, exitDirection))
            return;

        TryEnterNode(targetNodeId, RoomExitDirectionUtility.Opposite(exitDirection));
    }

    /// <summary>
    /// Runs a room transition as a transaction. Nothing about the previous room is torn down and
    /// no map state is advanced until the party is known to stand in the destination room.
    /// </summary>
    bool TryEnterNode(string nodeId, RoomExitDirection? entranceDirection)
    {
        MapNode nextNode = Session.GetNode(nodeId);
        if (nextNode == null)
        {
            Debug.LogError($"[MapRunController] Cannot enter missing node {nodeId}.", this);
            return false;
        }

        RoomDefinitionSO roomDefinition = nextNode.RoomDefinition;
        if (roomDefinition == null || roomDefinition.RoomPrefab == null)
        {
            Debug.LogError($"[MapRunController] Node {nextNode.Id} has no room prefab.", this);
            return false;
        }

        Session.IsTransitioning = true;

        RoomRuntimeCache.Entry previousEntry = Session.CurrentEntry;
        MapNode previousNode = Session.CurrentNode;
        PartyRoomTransitionService.PartyPoseSnapshot previousPartyPose = PartyTransitions.CapturePartyPose();

        // The previous room is only hidden here. Its encounter, its spawned enemies, and the
        // transient world objects all stay alive until the warp succeeds, so a rollback can put
        // the party back into the exact state it left.
        RoomCache.Deactivate(previousEntry);

        Transform anchor = roomSpawnAnchor != null ? roomSpawnAnchor : transform;
        RoomRuntimeCache.Entry nextEntry = RoomCache.GetOrCreate(nextNode, roomDefinition, anchor, roomParent);
        RoomCache.Activate(nextEntry);
        nextEntry.Controller.Initialize(this, nextNode);

        if (!TryMovePartyToRoomSpawn(nextEntry.Controller, entranceDirection))
        {
            Debug.LogError($"[MapRunController] Failed to move the party into node {nextNode.Id}. Rolling back the room transition.", this);
            RollbackTransition(previousEntry, previousNode, previousPartyPose, nextEntry);
            Session.IsTransitioning = false;
            RoomTransitionRolledBack?.Invoke();
            return false;
        }

        // Commit. Only now may the previous room lose its encounter and its content.
        encounterDirector?.StopEncounter();
        ClearTransientWorldObjects();
        RoomCache.ClearTransientContent(previousEntry);
        BindItemDropParent(nextEntry.PersistentRoot);

        // The warp faces the party into the new room, and each room carries its own yaw, so the
        // camera has to be re-aligned or it keeps pointing the way the previous room faced.
        GameplayCameraController.Instance?.SnapYawToPlayer();

        Session.Commit(nextEntry, nextNode);
        nextNode.Visit();
        Session.Graph.RevealOutgoing(nextNode);

        RoomTransitionCommitted?.Invoke();

        // The stage intro owns the frame between the room warp and BeginRoom. `IsTransitioning`
        // stays true for its whole duration so the party cannot travel out mid-intro.
        if (TryPlayStageIntro(nextEntry, nextNode))
            return true;

        BeginRoomAndFinishTransition();
        return true;
    }

    bool TryMovePartyToRoomSpawn(RoomController room, RoomExitDirection? entranceDirection)
    {
        Func<RoomController, RoomExitDirection?, bool> warpOverride = PartyWarpOverride;
        return warpOverride != null
            ? warpOverride(room, entranceDirection)
            : PartyTransitions.MovePartyToRoomSpawn(room, entranceDirection);
    }

    void RollbackTransition(
        RoomRuntimeCache.Entry previousEntry,
        MapNode previousNode,
        PartyRoomTransitionService.PartyPoseSnapshot previousPartyPose,
        RoomRuntimeCache.Entry failedEntry)
    {
        RoomCache.Deactivate(failedEntry);

        if (previousEntry == null || previousEntry.Instance == null)
        {
            // First room of the run. There is nothing to return to, so the controller stays
            // roomless rather than committing a room the party never reached.
            BindItemDropParent(null);
            Session.ClearCurrentRoom(previousNode);
            return;
        }

        // The previous room is restored, not re-initialized: Initialize would unlock a room that
        // was locked down for a running encounter and would rebuild its exit wiring.
        RoomCache.Activate(previousEntry);
        PartyTransitions.RestorePartyPose(previousPartyPose);
        BindItemDropParent(previousEntry.PersistentRoot);

        Session.Commit(previousEntry, previousNode);
    }

    bool TryPlayStageIntro(RoomRuntimeCache.Entry entry, MapNode node)
    {
        if (stageIntroAttempted)
            return false;

        MapGraph graph = Session.Graph;
        if (graph == null || node == null ||
            !string.Equals(node.Id, graph.StartNodeId, StringComparison.Ordinal))
        {
            return false;
        }

        // Once per StartRun, even if the player walks back into the Start room later in the run.
        stageIntroAttempted = true;

        StageIntroRig rig = entry != null && entry.Instance != null
            ? entry.Instance.GetComponentInChildren<StageIntroRig>(true)
            : null;
        if (rig == null)
        {
            Log("Start room has no StageIntroRig. Starting gameplay without the stage intro.");
            return false;
        }

        PartyRuntime party = PartyTransitions.ResolveParty();
        if (party == null)
        {
            Log("No deployed PartyRuntime was found for the stage intro. Starting gameplay without it.");
            return false;
        }

        return rig.TryPlay(party, BeginRoomAndFinishTransition);
    }

    void BeginRoomAndFinishTransition()
    {
        if (this == null)
            return;

        Session.CurrentRoom?.BeginRoom(encounterDirector);

        Session.IsTransitioning = false;
        NotifyMapChanged();
    }

    // ------------------------------------------------------------------ room and stage callbacks

    public void NotifyRoomCleared(RoomController room)
    {
        if (room == null || room != Session.CurrentRoom || Session.CurrentNode == null)
            return;

        Session.Graph.RevealOutgoing(Session.CurrentNode);
        StageProgression.NotifyRoomCleared(this, room, Session.CurrentNode);
        NotifyMapChanged();
    }

    public void ConfigureStageEnemy(GameObject enemyObject, bool isBoss)
    {
        StageProgression.ConfigureStageEnemy(this, enemyObject, isBoss);
    }

    public void GrantStageEnemyXp(int amount)
    {
        StageProgression.GrantStageEnemyXp(amount);
    }

    public void CompleteStageRunAndReturn()
    {
        TryCompleteStageRunAndReturn();
    }

    /// <summary>
    /// Commits the stage completion only when both the save and the scene-load dependency are
    /// available. When either is missing nothing is granted, spent, or locked, so the Stage Exit
    /// stays interactable and the player can try again.
    /// </summary>
    public bool TryCompleteStageRunAndReturn()
    {
        return StageProgression.TryCompleteStageRunAndReturn();
    }

    // ------------------------------------------------------------------ teardown and plumbing

    void ResetRoomCache()
    {
        encounterDirector?.StopEncounter();
        ClearTransientWorldObjects();
        CleanupSummonWorldRoot();
        BindItemDropParent(null);

        RoomCache.DestroyAll();
        Session.ClearRun();
    }

    void ClearTransientWorldObjects()
    {
        cleanupRoomRoots.Clear();
        foreach (Transform roomRoot in RoomCache.RoomRoots())
            cleanupRoomRoots.Add(roomRoot);

        CharacteContext player = PartyTransitions.ResolvePlayerContext();
        Transform partyRoot = PartyTransitions.ResolvePartyRoot(player);
        RoomTransitionCleanup.ClearTransientWorldObjects(new RoomTransitionCleanupScope(partyRoot, cleanupRoomRoots));
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
        MapChanged?.Invoke(Session.Graph, Session.CurrentNode);
        mapView?.Refresh(this);
    }

    void Log(string message)
    {
        if (logLifecycle)
            Debug.Log($"[MapRunController] {message}", this);
    }
}
