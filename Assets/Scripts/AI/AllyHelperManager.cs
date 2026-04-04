using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime;

[DefaultExecutionOrder(100)]
public class AllyHelperManager : MonoBehaviour
{
    const int MaxChainTargetColliders = 64;

    enum ChainAttackPhase
    {
        None = 0,
        WaitingForWarpCastMoment = 1,
        WaitingForWarpComplete = 2,
        WaitingForChainStart = 3,
        WaitingForChainCastMoment = 4,
        WaitingForChainComplete = 5,
    }

    sealed class PendingHelperSkill
    {
        public int requestId;
        public int skillLevel;
        public SkillGemDefinition skillDef;
    }

    sealed class PendingChainAttackSequence
    {
        public HelperChainAttackSequenceDef sequenceDef;
        public SkillGemDefinition chainAttackSkillDef;
        public GameObject targetObject;
        public Transform targetTransform;
        public Transform anchorTransform;
        public int requestedSkillLevel;
        public int chainAttackSkillLevel;
        public int warpRequestId;
        public int chainAttackRequestId;
        public ChainAttackPhase phase;
    }

    [SerializeField] private PlayerContext playerContext;
    [SerializeField] private AllyContext allyContext;
    [SerializeField] private GameObject allyHelper;
    [SerializeField] private bool logHelperExecution;

    [Header("Summon")]
    [SerializeField] private float summonRadius = 2.5f;
    [SerializeField] private float minSummonRadius = 1.2f;
    [SerializeField] private float navMeshSampleDistance = 2f;
    [SerializeField] private bool facePlayerForward = true;
    
    CharacterAnimBrain allyAnimBrain;
    ISkillUser allySkillUser;
    ASPHelperDitherFader allyHelperFader;
    NavMeshAgent allyAgent;
    BehaviorTree allyBehaviorTree;
    PendingHelperSkill pendingHelperSkill;
    PendingChainAttackSequence pendingChainAttackSequence;
    bool hideHelperOnSkillComplete;
    int nextHelperSkillRequestId = 1;
    readonly Collider[] _chainTargetBuffer = new Collider[MaxChainTargetColliders];
    readonly HashSet<int> _chainTargetIds = new();
    readonly List<Vector3> _chainCandidatePositions = new();
    readonly List<Quaternion> _chainCandidateRotations = new();

    public bool IsHelperActive => allyHelper != null && allyHelper.activeSelf;
    public bool IsHelperBusy =>
        pendingHelperSkill != null ||
        pendingChainAttackSequence != null ||
        (allyAnimBrain != null && allyAnimBrain.IsSkillActive);

    public bool HasChainAttackTarget(HelperChainAttackSequenceDef sequenceDef)
    {
        return TryResolveChainAttackTarget(
            sequenceDef,
            out _,
            out _,
            out _);
    }

    void Awake()
    {
        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();

        CacheHelperReferences();
    }

    void Start()
    {
        if (allyHelper == null)
        {
            Debug.LogWarning("AllyHelper is null", this);
            return;
        }

        CacheHelperReferences();
        allyHelperFader?.SetHiddenImmediate();

        if (allyHelper.activeSelf)
            allyHelper.SetActive(false);
    }

    void OnDestroy()
    {
        RestoreHelperSkillAutonomy();
        RestoreCollisionMask();
        SubscribeToAnimBrain(null);
    }

    void OnDisable()
    {
        RestoreHelperSkillAutonomy();
        RestoreCollisionMask();
        CancelPendingHelperSkill();
        CancelPendingChainAttackSequence();
        hideHelperOnSkillComplete = false;
    }

    void Update()
    {
        TryStartQueuedChainAttack();
    }

    public void SummonAllyHelper()
    {
        TrySummonAllyHelper(null);
    }

    public bool TrySummonAllyHelper(
        SkillGemDefinition skillDef,
        int skillLevel = 1,
        bool hideOnSkillComplete = true)
    {
        if (!TryPrepareHelperForSummon(out bool activatedNow))
            return false;

        hideHelperOnSkillComplete = hideOnSkillComplete;
        CancelPendingHelperSkill();
        CancelPendingChainAttackSequence();

        if (skillDef == null)
        {
            allyAnimBrain.PlaySkill();

            if (!allyAnimBrain.IsSkillActive)
            {
                hideHelperOnSkillComplete = false;
                if (activatedNow)
                    HideHelperImmediate();
                return false;
            }

            allyHelperFader?.BeginAnimationLifecycle(hideOnSkillComplete);
            return true;
        }

        int requestId = NextHelperSkillRequestId();
        pendingHelperSkill = new PendingHelperSkill
        {
            requestId = requestId,
            skillDef = skillDef,
            skillLevel = Mathf.Max(1, skillLevel),
        };

        if (logHelperExecution)
            Debug.Log($"[AllyHelperManager] Starting helper skill '{skillDef.name}' with request {requestId}.", this);

        ApplyTemporaryHelperSkillAutonomy();
        ApplyTemporaryNoCollision();
        bool started = allyAnimBrain.TryPlaySkill(
            requestId,
            skillDef,
            skillDef.GetCastPointNormalized());

        if (started)
        {
            allyHelperFader?.BeginAnimationLifecycle(hideOnSkillComplete);
            return true;
        }

        RestoreHelperSkillAutonomy();
        RestoreCollisionMask();
        CancelPendingHelperSkill();
        hideHelperOnSkillComplete = false;

        if (activatedNow)
            HideHelperImmediate();

        return false;
    }

    public bool TryStartChainAttackHelper(
        HelperChainAttackSequenceDef sequenceDef,
        SkillGemDefinition chainAttackSkillDef,
        int requestedSkillLevel = 1,
        bool hideOnSkillComplete = true)
    {
        if (sequenceDef == null || chainAttackSkillDef == null)
        {
            Log(sequenceDef, "Chain attack start failed: sequence config is incomplete.");
            return false;
        }

        if (!TryResolveChainAttackTarget(sequenceDef, out GameObject targetObject, out Transform targetTransform, out Transform anchorTransform))
        {
            Log(sequenceDef, "Chain attack start failed: no valid target near the player's aim target.");
            return false;
        }

        if (!TryPrepareHelperForSummon(out bool activatedNow))
            return false;

        hideHelperOnSkillComplete = hideOnSkillComplete;
        CancelPendingHelperSkill();
        CancelPendingChainAttackSequence();

        pendingChainAttackSequence = new PendingChainAttackSequence
        {
            sequenceDef = sequenceDef,
            chainAttackSkillDef = chainAttackSkillDef,
            targetObject = targetObject,
            targetTransform = targetTransform,
            anchorTransform = anchorTransform,
            requestedSkillLevel = Mathf.Max(1, requestedSkillLevel),
            chainAttackSkillLevel = Mathf.Max(1, requestedSkillLevel),
            warpRequestId = NextHelperSkillRequestId(),
            phase = ChainAttackPhase.WaitingForWarpCastMoment,
        };

        if (logHelperExecution || sequenceDef.debugLogging)
        {
            Debug.Log(
                $"[AllyHelperManager] Starting chain attack helper on target '{targetObject.name}' using utility warp-in.",
                this);
        }

        ApplyTemporaryHelperSkillAutonomy();
        ApplyTemporaryNoCollision();

        bool started = allyAnimBrain.TryPlayUtilityWarpIn(
            pendingChainAttackSequence.warpRequestId);

        if (started)
        {
            allyHelperFader?.BeginAnimationLifecycle(sequenceDef.hideHelperAtWarpCastMoment);
            return true;
        }

        RestoreHelperSkillAutonomy();
        RestoreCollisionMask();
        CancelPendingChainAttackSequence();
        hideHelperOnSkillComplete = false;

        if (activatedNow)
            HideHelperImmediate();

        return false;
    }

    public void AllyHelperOut()
    {
        RestoreHelperSkillAutonomy();
        RestoreCollisionMask();
        CancelPendingHelperSkill();
        CancelPendingChainAttackSequence();
        hideHelperOnSkillComplete = false;

        if (allyHelper == null || !allyHelper.activeSelf)
            return;

        if (allyHelperFader != null)
            allyHelperFader.FadeOutThenDeactivate();
        else
            allyHelper.SetActive(false);
    }

    void CacheHelperReferences()
    {
        if (allyHelper == null)
            return;

        allyContext = allyHelper.GetComponent<AllyContext>();
        allyBehaviorTree = allyHelper.GetComponent<BehaviorTree>();

        if (allyContext != null && allyContext.AITargetSensor == null)
            allyContext.AITargetSensor = allyHelper.GetComponent<AITargetSensor>();

        allyAgent = allyContext != null ? allyContext.agent : null;
        if (allyAgent == null)
            allyAgent = allyHelper.GetComponent<NavMeshAgent>();

        if (allyContext != null && allyContext.agent == null)
            allyContext.agent = allyAgent;

        CharacterAnimBrain nextAnimBrain = allyContext != null ? allyContext.AnimBrain : null;
        if (nextAnimBrain == null)
            nextAnimBrain = allyHelper.GetComponent<CharacterAnimBrain>();

        if (allyContext != null && allyContext.AnimBrain == null)
            allyContext.AnimBrain = nextAnimBrain;

        SubscribeToAnimBrain(nextAnimBrain);

        allySkillUser = allyHelper.GetComponent<ISkillUser>();
        if (allySkillUser == null && allyContext != null && allyContext.EnegySystem != null)
            allySkillUser = allyContext.EnegySystem;

        allyHelperFader = allyHelper.GetComponent<ASPHelperDitherFader>();
        if (allyHelperFader == null)
            allyHelperFader = allyHelper.GetComponentInChildren<ASPHelperDitherFader>(true);
    }

    void SubscribeToAnimBrain(CharacterAnimBrain nextAnimBrain)
    {
        if (allyAnimBrain == nextAnimBrain)
            return;

        if (allyAnimBrain != null)
        {
            allyAnimBrain.SkillCastMomentReached -= OnAllySkillCastMomentReached;
            allyAnimBrain.SkillCastInterrupted -= OnAllySkillCastInterrupted;
            allyAnimBrain.SkillCompleted -= OnAllySkillCompleted;
        }

        allyAnimBrain = nextAnimBrain;

        if (allyAnimBrain != null)
        {
            allyAnimBrain.SkillCastMomentReached += OnAllySkillCastMomentReached;
            allyAnimBrain.SkillCastInterrupted += OnAllySkillCastInterrupted;
            allyAnimBrain.SkillCompleted += OnAllySkillCompleted;
        }
    }

    bool TryPrepareHelperForSummon(out bool activatedNow)
    {
        activatedNow = false;

        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();

        if (playerContext == null || allyHelper == null)
        {
            Debug.LogWarning("Summon failed: playerContext or allyHelper is null", this);
            return false;
        }

        CacheHelperReferences();
        if (allyAnimBrain == null)
        {
            Debug.LogWarning("Summon failed: CharacterAnimBrain is null", this);
            return false;
        }

        Vector3 playerPos = playerContext.transform.position;
        Vector3 finalSpawnPos = ResolveSummonPosition(playerPos);

        allyHelper.transform.position = finalSpawnPos;
        
     
        ApplySummonRotation(finalSpawnPos, playerPos);

        if (!allyHelper.activeSelf)
        {
            allyHelper.SetActive(true);
            activatedNow = true;
            allyHelperFader?.SetHiddenImmediate();
        }

        return true;
    }

    void HideHelperImmediate()
    {
        RestoreHelperSkillAutonomy();
        RestoreCollisionMask();
        allyHelperFader?.SetHiddenImmediate();

        if (allyHelper != null && allyHelper.activeSelf)
            allyHelper.SetActive(false);
    }

    Vector3 ResolveSummonPosition(Vector3 playerPos)
    {
        Vector2 random2D = Random.insideUnitCircle.normalized * Random.Range(minSummonRadius, summonRadius);
        Vector3 rawSpawnPos = playerPos + new Vector3(random2D.x, 0f, random2D.y);

        if (NavMesh.SamplePosition(rawSpawnPos, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
            return hit.position;

        return rawSpawnPos;
    }

    void ApplySummonRotation(Vector3 spawnPos, Vector3 playerPos)
    {
        if (facePlayerForward)
        {
            allyHelper.transform.rotation = playerContext.transform.rotation;
            return;
        }

        Vector3 lookDir = playerPos - spawnPos;
        lookDir.y = 0f;

        if (lookDir.sqrMagnitude > 0.001f)
            allyHelper.transform.rotation = Quaternion.LookRotation(lookDir);
    }

    void ExecutePendingHelperSkill(int requestId)
    {
        if (pendingHelperSkill == null || pendingHelperSkill.requestId != requestId)
            return;

        PendingHelperSkill helperSkill = pendingHelperSkill;
        pendingHelperSkill = null;

        if (helperSkill.skillDef == null)
            return;

        ExecuteHelperSkill(helperSkill.skillDef, helperSkill.skillLevel, applyFacing: true);
    }

    void CancelPendingHelperSkill()
    {
        pendingHelperSkill = null;
    }

    void CancelPendingChainAttackSequence()
    {
        pendingChainAttackSequence = null;
    }

    int NextHelperSkillRequestId()
    {
        if (nextHelperSkillRequestId == int.MaxValue)
            nextHelperSkillRequestId = 1;

        return nextHelperSkillRequestId++;
    }

    void OnAllySkillCastMomentReached(int requestId)
    {
        if (HandlePendingChainAttackCastMoment(requestId))
            return;

        ExecutePendingHelperSkill(requestId);
    }

    void OnAllySkillCastInterrupted(int requestId)
    {
        if (HandlePendingChainAttackInterrupted(requestId))
            return;

        if (pendingHelperSkill == null || pendingHelperSkill.requestId != requestId)
            return;

        if (logHelperExecution && pendingHelperSkill.skillDef != null)
        {
            Debug.Log(
                $"[AllyHelperManager] Helper skill '{pendingHelperSkill.skillDef.name}' was interrupted before release.",
                this);
        }

        RestoreHelperSkillAutonomy();
        RestoreCollisionMask();
        CancelPendingHelperSkill();

        if (hideHelperOnSkillComplete)
            AllyHelperOut();
    }

    void OnAllySkillCompleted()
    {
        if (HandlePendingChainAttackCompleted())
            return;

        RestoreHelperSkillAutonomy();
        RestoreCollisionMask();
        CancelPendingHelperSkill();

        if (!hideHelperOnSkillComplete)
            return;

        hideHelperOnSkillComplete = false;

        if (allyHelperFader != null && allyHelper != null && allyHelper.activeSelf)
            allyHelperFader.FinalizeAfterAnimation();
        else
            AllyHelperOut();
    }

    bool HandlePendingChainAttackCastMoment(int requestId)
    {
        if (pendingChainAttackSequence == null)
            return false;

        if (requestId == pendingChainAttackSequence.warpRequestId)
        {
            if (pendingChainAttackSequence.phase != ChainAttackPhase.WaitingForWarpCastMoment)
                return true;

            if (!IsChainAttackTargetAlive(pendingChainAttackSequence.targetTransform))
            {
                Log(pendingChainAttackSequence.sequenceDef, "Chain attack cancelled: target died before teleport.");
                CancelActiveChainAttackSequence(interrupted: false);
                return true;
            }

            if (!TryResolveChainAttackTeleportPose(
                    pendingChainAttackSequence.sequenceDef,
                    pendingChainAttackSequence.anchorTransform,
                    out Vector3 teleportPosition,
                    out Quaternion teleportRotation))
            {
                Log(pendingChainAttackSequence.sequenceDef, "Chain attack cancelled: no safe teleport pose was found.");
                CancelActiveChainAttackSequence(interrupted: false);
                return true;
            }

            if (pendingChainAttackSequence.sequenceDef.hideHelperAtWarpCastMoment)
                allyHelperFader?.SetHiddenImmediate();

            TeleportHelperTo(teleportPosition, teleportRotation);
            pendingChainAttackSequence.phase = ChainAttackPhase.WaitingForWarpComplete;
            Log(pendingChainAttackSequence.sequenceDef, $"Teleported helper to chain attack pose at {teleportPosition}.");
            return true;
        }

        if (requestId == pendingChainAttackSequence.chainAttackRequestId)
        {
            if (pendingChainAttackSequence.phase != ChainAttackPhase.WaitingForChainCastMoment)
                return true;

            ExecuteHelperSkill(
                pendingChainAttackSequence.chainAttackSkillDef,
                pendingChainAttackSequence.chainAttackSkillLevel,
                applyFacing: false);

            pendingChainAttackSequence.phase = ChainAttackPhase.WaitingForChainComplete;
            return true;
        }

        return false;
    }

    bool HandlePendingChainAttackInterrupted(int requestId)
    {
        if (pendingChainAttackSequence == null)
            return false;

        if (requestId != pendingChainAttackSequence.warpRequestId &&
            requestId != pendingChainAttackSequence.chainAttackRequestId)
        {
            return false;
        }

        Log(
            pendingChainAttackSequence.sequenceDef,
            $"Chain attack skill request {requestId} was interrupted during phase '{pendingChainAttackSequence.phase}'.");

        CancelActiveChainAttackSequence(interrupted: true);
        return true;
    }

    bool HandlePendingChainAttackCompleted()
    {
        if (pendingChainAttackSequence == null)
            return false;

        if (pendingChainAttackSequence.phase == ChainAttackPhase.WaitingForWarpComplete)
        {
            if (!IsChainAttackTargetAlive(pendingChainAttackSequence.targetTransform))
            {
                Log(pendingChainAttackSequence.sequenceDef, "Chain attack cancelled: target died before the follow-up attack started.");
                CancelActiveChainAttackSequence(interrupted: false);
                return true;
            }

            pendingChainAttackSequence.phase = ChainAttackPhase.WaitingForChainStart;
            Log(
                pendingChainAttackSequence.sequenceDef,
                $"Queued follow-up chain attack skill '{pendingChainAttackSequence.chainAttackSkillDef.name}'.");
            return true;
        }

        if (pendingChainAttackSequence.phase == ChainAttackPhase.WaitingForChainComplete)
        {
            RestoreHelperSkillAutonomy();
            RestoreCollisionMask();
            CancelPendingChainAttackSequence();

            if (!hideHelperOnSkillComplete)
                return true;

            hideHelperOnSkillComplete = false;

            if (allyHelperFader != null && allyHelper != null && allyHelper.activeSelf)
                allyHelperFader.FinalizeAfterAnimation();
            else
                AllyHelperOut();

            return true;
        }

        return true;
    }

    void CancelActiveChainAttackSequence(bool interrupted)
    {
        RestoreHelperSkillAutonomy();
        RestoreCollisionMask();
        CancelPendingChainAttackSequence();

        if (!hideHelperOnSkillComplete)
            return;

        hideHelperOnSkillComplete = false;

        if (interrupted)
            AllyHelperOut();
        else if (allyHelperFader != null && allyHelper != null && allyHelper.activeSelf)
            allyHelperFader.FinalizeAfterAnimation();
        else
            AllyHelperOut();
    }

    bool _helperAutonomyCaptured;
    bool _defaultHelperBehaviorTreeEnabled;
    bool _defaultHelperAgentIsStopped;
    bool _defaultHelperAgentUpdatePosition;
    bool _defaultHelperAgentUpdateRotation;

    void ApplyTemporaryHelperSkillAutonomy()
    {
        if (_helperAutonomyCaptured || allyHelper == null)
            return;

        CacheHelperReferences();

        bool capturedAny = false;

        if (allyBehaviorTree != null)
        {
            _defaultHelperBehaviorTreeEnabled = allyBehaviorTree.enabled;
            allyBehaviorTree.enabled = false;
            capturedAny = true;
        }

        if (allyAgent != null && allyAgent.enabled)
        {
            _defaultHelperAgentIsStopped = allyAgent.isStopped;
            _defaultHelperAgentUpdatePosition = allyAgent.updatePosition;
            _defaultHelperAgentUpdateRotation = allyAgent.updateRotation;

            allyAgent.isStopped = true;
            allyAgent.updatePosition = false;
            allyAgent.updateRotation = false;

            if (allyAgent.isOnNavMesh)
                allyAgent.nextPosition = allyHelper.transform.position;

            capturedAny = true;
        }

        _helperAutonomyCaptured = capturedAny;
    }

    void RestoreHelperSkillAutonomy()
    {
        if (!_helperAutonomyCaptured)
            return;

        if (allyBehaviorTree != null)
            allyBehaviorTree.enabled = _defaultHelperBehaviorTreeEnabled;

        if (allyAgent != null && allyAgent.enabled)
        {
            Vector3 helperPosition = allyHelper != null ? allyHelper.transform.position : allyAgent.transform.position;
            if (allyAgent.isOnNavMesh)
                allyAgent.nextPosition = helperPosition;

            allyAgent.updatePosition = _defaultHelperAgentUpdatePosition;
            allyAgent.updateRotation = _defaultHelperAgentUpdateRotation;
            allyAgent.isStopped = _defaultHelperAgentIsStopped;
        }

        _helperAutonomyCaptured = false;
    }

    void ApplyHelperSkillFacing(SkillGemDefinition skillDef)
    {
        if (skillDef == null ||
            skillDef.helperFacingMode != SkillGemDefinition.HelperFacingMode.FaceDetectedTargetOnCast ||
            allyHelper == null)
        {
            return;
        }

        if (!TryResolveHelperSkillAimPoint(out Vector3 aimPoint))
            return;

        Transform facingOrigin = allySkillUser != null && allySkillUser.CastOrigin != null
            ? allySkillUser.CastOrigin
            : allyHelper.transform;

        Vector3 lookDir = aimPoint - facingOrigin.position;
        lookDir.y = 0f;

        if (lookDir.sqrMagnitude <= 0.001f)
            return;

        allyHelper.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

        if (allyAgent != null && allyAgent.enabled && allyAgent.isOnNavMesh)
            allyAgent.nextPosition = allyHelper.transform.position;
    }

    bool ExecuteHelperSkill(SkillGemDefinition skillDef, int skillLevel, bool applyFacing)
    {
        if (skillDef == null)
            return false;

        if (allySkillUser == null)
        {
            Debug.LogWarning($"Helper skill '{skillDef.name}' requires an ISkillUser on the helper actor.", this);
            return false;
        }

        var runtimeSkill = new SkillInstance
        {
            def = skillDef,
            level = Mathf.Max(1, skillLevel),
        };

        if (applyFacing)
            ApplyHelperSkillFacing(skillDef);

        if (logHelperExecution)
            Debug.Log($"[AllyHelperManager] Executing helper skill '{skillDef.name}'.", this);

        if (runtimeSkill.TryCastIgnoringResourceCosts(allySkillUser))
            return true;

        Debug.LogWarning($"Helper skill '{skillDef.name}' could not execute. Check helper payload or legacy projectile setup.", this);
        return false;
    }

    void TryStartQueuedChainAttack()
    {
        if (pendingChainAttackSequence == null ||
            pendingChainAttackSequence.phase != ChainAttackPhase.WaitingForChainStart)
        {
            return;
        }

        if (!IsChainAttackTargetAlive(pendingChainAttackSequence.targetTransform))
        {
            Log(pendingChainAttackSequence.sequenceDef, "Chain attack cancelled: target died before the follow-up attack could start.");
            CancelActiveChainAttackSequence(interrupted: false);
            return;
        }

        pendingChainAttackSequence.chainAttackRequestId = NextHelperSkillRequestId();
        pendingChainAttackSequence.phase = ChainAttackPhase.WaitingForChainCastMoment;

        bool started = allyAnimBrain.TryPlaySkill(
            pendingChainAttackSequence.chainAttackRequestId,
            pendingChainAttackSequence.chainAttackSkillDef,
            pendingChainAttackSequence.chainAttackSkillDef.GetCastPointNormalized());

        if (!started)
        {
            Log(pendingChainAttackSequence.sequenceDef, "Chain attack cancelled: follow-up attack clip could not start.");
            CancelActiveChainAttackSequence(interrupted: false);
            return;
        }

        allyHelperFader?.BeginAnimationLifecycle(hideHelperOnSkillComplete);
        Log(
            pendingChainAttackSequence.sequenceDef,
            $"Started follow-up chain attack skill '{pendingChainAttackSequence.chainAttackSkillDef.name}'.");
    }

    bool TryResolveHelperSkillAimPoint(out Vector3 aimPoint)
    {
        CacheHelperReferences();

        if (allyContext != null && allyContext.AITargetSensor != null)
        {
            allyContext.AITargetSensor.ForceScan();

            Transform currentTarget = allyContext.AITargetSensor.CurrentTarget;
            if (currentTarget != null)
            {
                aimPoint = currentTarget.position;
                return true;
            }

            if (allyContext.AITargetSensor.HasAnyTarget)
            {
                aimPoint = allyContext.AITargetSensor.LastSeenPosition;
                return true;
            }
        }

        aimPoint = Vector3.zero;
        return false;
    }

    bool TryResolveChainAttackTarget(
        HelperChainAttackSequenceDef sequenceDef,
        out GameObject targetObject,
        out Transform targetTransform,
        out Transform anchorTransform)
    {
        targetObject = null;
        targetTransform = null;
        anchorTransform = null;

        if (sequenceDef == null)
            return false;

        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();

        if (playerContext == null || playerContext.aimTarget == null)
            return false;

        Vector3 aimPoint = playerContext.aimTarget.position;
        int hitCount = Physics.OverlapSphereNonAlloc(
            aimPoint,
            Mathf.Max(0.1f, sequenceDef.aimSearchRadius),
            _chainTargetBuffer,
            sequenceDef.targetLayers,
            sequenceDef.targetTriggerInteraction);

        _chainTargetIds.Clear();

        float bestDistanceSqr = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _chainTargetBuffer[i];
            if (hit == null)
                continue;

            if (!TryResolveChainAttackCandidate(hit, out GameObject candidateObject, out Transform candidateTransform, out Transform candidateAnchor))
                continue;

            int targetId = candidateObject.GetInstanceID();
            if (!_chainTargetIds.Add(targetId))
                continue;

            Vector3 candidatePoint = candidateAnchor != null ? candidateAnchor.position : candidateTransform.position;
            if (sequenceDef.requireAimLineOfSight &&
                !HasAimLineOfSight(aimPoint, candidatePoint, sequenceDef))
            {
                continue;
            }

            float distSqr = (candidatePoint - aimPoint).sqrMagnitude;
            if (distSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = distSqr;
            targetObject = candidateObject;
            targetTransform = candidateTransform;
            anchorTransform = candidateAnchor;
        }

        return targetObject != null && anchorTransform != null;
    }

    bool TryResolveChainAttackCandidate(
        Collider hit,
        out GameObject candidateObject,
        out Transform candidateTransform,
        out Transform candidateAnchor)
    {
        candidateObject = null;
        candidateTransform = null;
        candidateAnchor = null;

        if (hit == null)
            return false;

        CharacteContext targetContext = hit.GetComponentInParent<CharacteContext>();
        AITargetInfo targetInfo = hit.GetComponentInParent<AITargetInfo>();
        IAITargetable aiTargetable = FindInterfaceInParents<IAITargetable>(hit.transform);
        IDamageable damageable = FindInterfaceInParents<IDamageable>(hit.transform);

        bool hasCombatIdentity =
            targetContext != null ||
            targetInfo != null ||
            aiTargetable != null ||
            damageable != null;

        if (!hasCombatIdentity)
            return false;

        Transform rootTransform = targetContext != null
            ? targetContext.transform
            : hit.attachedRigidbody != null
                ? hit.attachedRigidbody.transform
                : hit.transform.root != null ? hit.transform.root : hit.transform;

        if (rootTransform == null)
            return false;

        if (playerContext != null && rootTransform == playerContext.transform.root)
            return false;

        if (allyHelper != null && rootTransform == allyHelper.transform.root)
            return false;

        if (!IsResolvedTargetAlive(rootTransform, targetContext, aiTargetable, damageable))
            return false;

        candidateTransform = rootTransform;
        candidateObject = rootTransform.gameObject;
        candidateAnchor = targetInfo != null && targetInfo.ChainAttackPoint != null
            ? targetInfo.ChainAttackPoint
            : aiTargetable?.AimPoint != null
                ? aiTargetable.AimPoint
                : rootTransform;

        return candidateAnchor != null;
    }

    bool IsResolvedTargetAlive(
        Transform rootTransform,
        CharacteContext targetContext,
        IAITargetable aiTargetable,
        IDamageable damageable)
    {
        if (targetContext != null && targetContext.stateHub != null)
            return targetContext.stateHub.IsAlive && !targetContext.stateHub.Isdown;

        if (aiTargetable != null)
            return aiTargetable.IsAlive;

        if (damageable != null)
            return damageable.IsAlive;

        return rootTransform != null;
    }

    bool IsChainAttackTargetAlive(Transform targetTransform)
    {
        if (targetTransform == null)
            return false;

        CharacteContext targetContext = targetTransform.GetComponentInParent<CharacteContext>();
        IAITargetable aiTargetable = FindInterfaceInParents<IAITargetable>(targetTransform);
        IDamageable damageable = FindInterfaceInParents<IDamageable>(targetTransform);

        return IsResolvedTargetAlive(targetTransform.root != null ? targetTransform.root : targetTransform, targetContext, aiTargetable, damageable);
    }

    bool HasAimLineOfSight(Vector3 origin, Vector3 targetPoint, HelperChainAttackSequenceDef sequenceDef)
    {
        if (sequenceDef == null || sequenceDef.aimObstacleLayers == 0)
            return true;

        Vector3 dir = targetPoint - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f)
            return true;

        return !Physics.Raycast(
            origin,
            dir / dist,
            dist,
            sequenceDef.aimObstacleLayers,
            sequenceDef.targetTriggerInteraction);
    }

    bool TryResolveChainAttackTeleportPose(
        HelperChainAttackSequenceDef sequenceDef,
        Transform anchorTransform,
        out Vector3 teleportPosition,
        out Quaternion teleportRotation)
    {
        teleportPosition = Vector3.zero;
        teleportRotation = Quaternion.identity;

        if (sequenceDef == null || anchorTransform == null)
            return false;

        Quaternion baseRotation = sequenceDef.useAnchorRotationAsBase
            ? anchorTransform.rotation
            : playerContext != null ? playerContext.transform.rotation : allyHelper.transform.rotation;

        float[] angles = sequenceDef.GetOrientationAngles();
        bool shouldResolveCandidates =
            sequenceDef.probeOrientation &&
            angles != null &&
            angles.Length > 0;

        if (!shouldResolveCandidates)
        {
            return TryResolveChainAttackPoseCandidate(
                sequenceDef,
                anchorTransform,
                baseRotation,
                0f,
                requireClearance: sequenceDef.HasClearanceProbe,
                out teleportPosition,
                out teleportRotation);
        }

        _chainCandidatePositions.Clear();
        _chainCandidateRotations.Clear();

        for (int i = 0; i < angles.Length; i++)
        {
            if (!TryResolveChainAttackPoseCandidate(
                    sequenceDef,
                    anchorTransform,
                    baseRotation,
                    angles[i],
                    requireClearance: sequenceDef.HasClearanceProbe,
                    out Vector3 candidatePosition,
                    out Quaternion candidateRotation))
            {
                continue;
            }

            _chainCandidatePositions.Add(candidatePosition);
            _chainCandidateRotations.Add(candidateRotation);
        }

        if (_chainCandidatePositions.Count > 0)
        {
            int selectedIndex = Random.Range(0, _chainCandidatePositions.Count);
            teleportPosition = _chainCandidatePositions[selectedIndex];
            teleportRotation = _chainCandidateRotations[selectedIndex];
            _chainCandidatePositions.Clear();
            _chainCandidateRotations.Clear();
            return true;
        }

        _chainCandidatePositions.Clear();
        _chainCandidateRotations.Clear();

        if (!sequenceDef.allowFallbackToBaseRotation)
            return false;

        return TryResolveChainAttackPoseCandidate(
            sequenceDef,
            anchorTransform,
            baseRotation,
            0f,
            requireClearance: false,
            out teleportPosition,
            out teleportRotation);
    }

    bool TryResolveChainAttackPoseCandidate(
        HelperChainAttackSequenceDef sequenceDef,
        Transform anchorTransform,
        Quaternion baseRotation,
        float yawAngle,
        bool requireClearance,
        out Vector3 teleportPosition,
        out Quaternion teleportRotation)
    {
        teleportPosition = Vector3.zero;
        teleportRotation = baseRotation;

        if (sequenceDef == null || anchorTransform == null)
            return false;

        Quaternion yawRotation = Quaternion.AngleAxis(yawAngle, Vector3.up);
        teleportRotation = yawRotation * baseRotation;

        Vector3 localOffset = yawRotation * sequenceDef.anchorPositionOffset;
        teleportPosition = anchorTransform.TransformPoint(localOffset);

        if (sequenceDef.requireNavMeshAtAnchor)
        {
            if (!NavMesh.SamplePosition(
                    teleportPosition,
                    out NavMeshHit navHit,
                    Mathf.Max(0.05f, sequenceDef.navMeshSampleDistance),
                    NavMesh.AllAreas))
            {
                return false;
            }

            teleportPosition = navHit.position;
        }

        if (requireClearance && !IsChainAttackPoseClear(sequenceDef, teleportPosition, teleportRotation))
            return false;

        return true;
    }

    bool IsChainAttackPoseClear(
        HelperChainAttackSequenceDef sequenceDef,
        Vector3 teleportPosition,
        Quaternion rotation)
    {
        if (sequenceDef == null || !sequenceDef.HasClearanceProbe)
            return true;

        Vector3 center = teleportPosition + rotation * sequenceDef.clearanceCenterOffset;
        return !Physics.CheckBox(
            center,
            sequenceDef.clearanceHalfExtents,
            rotation,
            sequenceDef.obstacleLayers,
            sequenceDef.obstacleTriggerInteraction);
    }

    void TeleportHelperTo(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (allyHelper == null)
            return;

        allyHelper.transform.SetPositionAndRotation(worldPosition, worldRotation);

        if (allyAgent == null || !allyAgent.enabled)
            return;

        if (allyAgent.isOnNavMesh)
        {
            allyAgent.nextPosition = allyHelper.transform.position;
            return;
        }

        if (NavMesh.SamplePosition(allyHelper.transform.position, out NavMeshHit navHit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            allyAgent.Warp(navHit.position);
            allyHelper.transform.position = navHit.position;
            allyAgent.nextPosition = navHit.position;
        }
    }

    static T FindInterfaceInParents<T>(Transform start) where T : class
    {
        if (start == null)
            return null;

        MonoBehaviour[] behaviours = start.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is T match)
                return match;
        }

        return null;
    }

    void Log(HelperChainAttackSequenceDef sequenceDef, string message)
    {
        if (!logHelperExecution && (sequenceDef == null || !sequenceDef.debugLogging))
            return;

        Debug.Log($"[AllyHelperManager] {message}", this);
    }

    private bool _excludeCaptured;
    private LayerMask _defaultExcludeLayers;

    void ApplyTemporaryNoCollision()
    {
        if (allyContext == null || allyContext.rb == null) return;

        if (!_excludeCaptured)
        {
            _defaultExcludeLayers = allyContext.rb.excludeLayers;
            _excludeCaptured = true;
        }

        allyContext.rb.excludeLayers = Physics.AllLayers;
    }

    void RestoreCollisionMask()
    {
        if (allyContext == null || allyContext.rb == null) return;
        if (!_excludeCaptured) return;

        allyContext.rb.excludeLayers = _defaultExcludeLayers;
        _excludeCaptured = false;
    }
}
