using System;
using System.Collections.Generic;
using Opsive.BehaviorDesigner.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class FieldAllyMember : MonoBehaviour
{
    enum SequenceExecutionPhase
    {
        None = 0,
        WaitingForEnterCastMoment = 1,
        WaitingForEnterComplete = 2,
        WaitingForAttackCastMoment = 3,
        WaitingForAttackComplete = 4,
        WaitingForExitCastMoment = 5,
        WaitingForExitComplete = 6,
    }

    sealed class PendingSequenceExecution
    {
        public object owner;
        public ChainAttackStepDef step;
        public Transform lockedTarget;
        public SkillGemDefinition attackSkillDef;
        public int attackSkillLevel;
        public bool ignoreResourceCosts;
        public bool attackPayloadReleased;
        public bool releaseReservationOnComplete;
        public int enterRequestId;
        public int attackRequestId;
        public int exitRequestId;
        public SequenceExecutionPhase phase;
        public Vector3 recordedOriginPosition;
        public Quaternion recordedOriginRotation;
    }

    sealed class DeferredSequenceCleanup
    {
        public object owner;
        public ChainActorExitMode exitMode;
        public Vector3 returnPosition;
        public Quaternion returnRotation;
    }

    [SerializeField] private ChainActorRole actorRole = ChainActorRole.PartySlot1;
    [SerializeField] private FieldAllyManager manager;
    [SerializeField] private CharacteContext actorContext;
    [SerializeField] private BehaviorTree behaviorTree;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private CharacterAnimBrain animBrain;
    [SerializeField] private ChainSkillUserProxy skillUserProxy;
    [SerializeField] private ASPHelperDitherFader actorFader;
    [FoldoutGroup("Chain Attack", Expanded = false), LabelText("Disable Components During Sequence")]
    [SerializeField] private MonoBehaviour[] componentsToDisableDuringSequence;
    [FoldoutGroup("Chain Attack", Expanded = false), LabelText("Auto Disable Player Input/Move")]
    [SerializeField] private bool autoDisablePlayerInputAndMovement = true;
    [FoldoutGroup("Chain Attack", Expanded = false), LabelText("Default Chain Skill"), AssetsOnly]
    [SerializeField] private SkillGemDefinition defaultChainSkill;
    [FoldoutGroup("Chain Attack/Runtime Prototype"), LabelText("Use Runtime Skill Override")]
    [SerializeField] private bool useRuntimeChainSkillOverride;
    [FoldoutGroup("Chain Attack/Runtime Prototype"), ShowIf(nameof(useRuntimeChainSkillOverride)), LabelText("Runtime Chain Skill"), AssetsOnly]
    [SerializeField] private SkillGemDefinition runtimeChainSkill;
    [FoldoutGroup("Chain Attack/Runtime Prototype"), ShowIf(nameof(useRuntimeChainSkillOverride)), LabelText("Override Skill Level")]
    [SerializeField] private bool overrideRuntimeChainSkillLevel = true;
    [FoldoutGroup("Chain Attack/Runtime Prototype"), ShowIf("@useRuntimeChainSkillOverride && overrideRuntimeChainSkillLevel"), LabelText("Runtime Skill Level"), MinValue(1)]
    [SerializeField] private int runtimeChainSkillLevel = 1;
    [SerializeField] private bool logSequenceExecution;

    PendingSequenceExecution _pendingExecution;
    DeferredSequenceCleanup _deferredCleanup;
    object _reservationOwner;
    int _nextRequestId = 1;
    SkillGemDefinition _lastResolvedAttackSkill;
    int _lastResolvedAttackLevel = 1;
    MonoBehaviour[] _capturedDisabledComponents = Array.Empty<MonoBehaviour>();
    bool[] _capturedDisabledComponentStates = Array.Empty<bool>();

    bool _autonomyCaptured;
    bool _defaultBehaviorTreeEnabled;
    bool _defaultAgentIsStopped;
    bool _defaultAgentUpdatePosition;
    bool _defaultAgentUpdateRotation;

    public ChainActorRole ActorRole => actorRole;
    public bool IsReserved => _reservationOwner != null;
    public bool HasActiveSequenceExecution => _pendingExecution != null;
    public bool HasDeferredSequenceCleanup => _deferredCleanup != null;
    public bool LastExecutionSucceeded { get; private set; }
    public SkillGemDefinition DefaultChainSkill =>
        defaultChainSkill != null
            ? defaultChainSkill
            : actorContext != null && actorContext.baseStats != null
                ? actorContext.baseStats.chainAttackSkill
                : null;
    public SkillGemDefinition ResolvedActorDefaultChainSkill =>
        useRuntimeChainSkillOverride && runtimeChainSkill != null
            ? runtimeChainSkill
            : DefaultChainSkill;
    public bool IsBusy =>
        _pendingExecution != null ||
        _deferredCleanup != null ||
        (animBrain != null && animBrain.IsSkillActive);
    public bool IsAlive =>
        actorContext != null &&
        actorContext.stateHub != null &&
        actorContext.stateHub.IsAlive &&
        !actorContext.stateHub.Isdown;

    [ShowInInspector, ReadOnly, FoldoutGroup("Chain Attack/Runtime Prototype"), PropertyOrder(10), LabelText("Resolved Actor Default Skill")]
    SkillGemDefinition InspectorResolvedActorDefaultChainSkill => ResolvedActorDefaultChainSkill;

    [ShowInInspector, ReadOnly, FoldoutGroup("Chain Attack/Runtime Prototype"), PropertyOrder(11), LabelText("Actor Default Source")]
    string InspectorActorDefaultSkillSource => BuildActorDefaultSkillSourceLabel();

    [ShowInInspector, ReadOnly, FoldoutGroup("Chain Attack/Runtime Prototype"), PropertyOrder(12), LabelText("Last Resolved Attack Skill")]
    SkillGemDefinition InspectorLastResolvedAttackSkill => _lastResolvedAttackSkill;

    [ShowInInspector, ReadOnly, FoldoutGroup("Chain Attack/Runtime Prototype"), PropertyOrder(13), LabelText("Last Resolved Attack Level")]
    int InspectorLastResolvedAttackLevel => Mathf.Max(1, _lastResolvedAttackLevel);

    void Awake()
    {
        CacheReferences();
    }

    void OnEnable()
    {
        CacheReferences();
        manager?.Register(this);
    }

    void OnDisable()
    {
        manager?.Unregister(this);
        CancelDeferredSequenceCleanup();
        CleanupActiveExecution(success: false);
        SubscribeToAnimBrain(null);
        _reservationOwner = null;
    }

    public bool TryReserve(object owner)
    {
        if (owner == null)
            return false;

        if (_reservationOwner != null && !ReferenceEquals(_reservationOwner, owner))
            return false;

        _reservationOwner = owner;
        return true;
    }

    public void ReleaseReservation(object owner)
    {
        if (owner == null || !ReferenceEquals(_reservationOwner, owner))
            return;

        _reservationOwner = null;
    }

    public bool TryStartSequenceStep(ChainAttackStepDef step, Transform lockedTarget)
    {
        if (step == null)
            return false;

        CacheReferences();

        if (animBrain == null || skillUserProxy == null || _reservationOwner == null)
            return false;

        if (step.requireActorAlive && !IsAlive)
            return false;

        if (IsBusy)
            return false;

        if (lockedTarget == null && step.skipIfTargetMissing)
            return false;

        LastExecutionSucceeded = false;
        ApplyTemporaryAutonomy();
        skillUserProxy.ClearAimOverrides();

        if (!TryResolveAttackSkill(step, out SkillGemDefinition resolvedSkillDef, out int resolvedSkillLevel))
        {
            Log($"Step '{step.RuntimeId}' cannot start because no chain skill is configured for actor '{name}'.");
            RestoreTemporaryAutonomy();
            return false;
        }

        PendingSequenceExecution execution = new()
        {
            owner = _reservationOwner,
            step = step,
            lockedTarget = lockedTarget,
            attackSkillDef = resolvedSkillDef,
            attackSkillLevel = resolvedSkillLevel,
            ignoreResourceCosts = step.ignoreResourceCosts,
            recordedOriginPosition = transform.position,
            recordedOriginRotation = transform.rotation,
            phase = SequenceExecutionPhase.None,
        };

        _pendingExecution = execution;

        if (step.enterMode == ChainActorEnterMode.UtilityWarpInToTarget)
            return TryStartEnterUtility(execution);

        if (!TryApplyEntryMovement(execution))
        {
            CleanupActiveExecution(success: false);
            return false;
        }

        return TryStartAttack(execution);
    }

    public void SetRuntimeChainSkillOverride(SkillGemDefinition skill, int level = 1, bool overrideSkillLevel = true)
    {
        runtimeChainSkill = skill;
        runtimeChainSkillLevel = Mathf.Max(1, level);
        this.overrideRuntimeChainSkillLevel = overrideSkillLevel;
        useRuntimeChainSkillOverride = skill != null;
    }

    public void ClearRuntimeChainSkillOverride()
    {
        useRuntimeChainSkillOverride = false;
        runtimeChainSkill = null;
        runtimeChainSkillLevel = 1;
        overrideRuntimeChainSkillLevel = true;
    }

    bool TryResolveAttackSkill(ChainAttackStepDef step, out SkillGemDefinition skillDef, out int skillLevel)
    {
        skillDef = null;
        skillLevel = 1;

        if (step == null)
            return false;

        if (step.skillSource == ChainStepSkillSource.ActorDefault)
        {
            skillDef = ResolvedActorDefaultChainSkill;
            if (skillDef == null)
                return false;

            skillLevel = ResolveActorDefaultSkillLevel(step);
            skillLevel = skillDef.ClampLevel(skillLevel);
            _lastResolvedAttackSkill = skillDef;
            _lastResolvedAttackLevel = skillLevel;
            return true;
        }

        skillDef = step.skillDef;
        if (skillDef == null)
            return false;

        skillLevel = skillDef.ClampLevel(step.ClampedSkillLevel);
        _lastResolvedAttackSkill = skillDef;
        _lastResolvedAttackLevel = skillLevel;
        return true;
    }

    public void FinalizeSequenceParticipation(object owner, bool interrupted)
    {
        if (owner == null || _deferredCleanup == null || !ReferenceEquals(_deferredCleanup.owner, owner))
            return;

        DeferredSequenceCleanup cleanup = _deferredCleanup;
        _deferredCleanup = null;

        switch (cleanup.exitMode)
        {
            case ChainActorExitMode.ReturnToRecordedOriginOnSequenceEnd:
                CompleteDeferredReturn(cleanup);
                return;

            case ChainActorExitMode.ReturnToRecordedOriginViaUtilityOnSequenceEnd:
                if (TryStartReturnUtility(
                        cleanup.owner,
                        cleanup.returnPosition,
                        cleanup.returnRotation,
                        releaseReservationOnComplete: true))
                {
                    return;
                }

                LastExecutionSucceeded = false;
                ReleaseReservation(cleanup.owner);
                RestoreTemporaryAutonomy();
                return;

            case ChainActorExitMode.FadeOutAndDeactivateOnSequenceEnd:
                ExecuteFadeOutCleanup(cleanup.owner);
                return;

            default:
                if (interrupted)
                    ReleaseReservation(cleanup.owner);
                RestoreTemporaryAutonomy();
                return;
        }
    }

    public bool OwnsSequenceWork(object owner)
    {
        if (owner == null)
            return false;

        if (_pendingExecution != null && ReferenceEquals(_pendingExecution.owner, owner))
            return true;

        if (_deferredCleanup != null && ReferenceEquals(_deferredCleanup.owner, owner))
            return true;

        return _reservationOwner != null && ReferenceEquals(_reservationOwner, owner);
    }

    void CacheReferences()
    {
        if (actorContext == null)
            actorContext = GetComponent<CharacteContext>();

        if (manager == null)
            manager = FindFirstObjectByType<FieldAllyManager>();

        if (behaviorTree == null)
            behaviorTree = GetComponent<BehaviorTree>();

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        CharacterAnimBrain nextAnimBrain = actorContext != null ? actorContext.AnimBrain : null;
        if (nextAnimBrain == null)
            nextAnimBrain = GetComponent<CharacterAnimBrain>();

        if (actorContext != null && actorContext.AnimBrain == null)
            actorContext.AnimBrain = nextAnimBrain;

        SubscribeToAnimBrain(nextAnimBrain);

        if (skillUserProxy == null)
            skillUserProxy = GetComponent<ChainSkillUserProxy>();

        if (skillUserProxy == null)
            skillUserProxy = gameObject.AddComponent<ChainSkillUserProxy>();

        if (actorFader == null)
        {
            actorFader = GetComponent<ASPHelperDitherFader>();
            if (actorFader == null)
                actorFader = GetComponentInChildren<ASPHelperDitherFader>(true);
        }
    }

    void SubscribeToAnimBrain(CharacterAnimBrain nextAnimBrain)
    {
        if (animBrain == nextAnimBrain)
            return;

        if (animBrain != null)
        {
            animBrain.SkillCastMomentReached -= OnSkillCastMomentReached;
            animBrain.SkillCastInterrupted -= OnSkillCastInterrupted;
            animBrain.SkillCompleted -= OnSkillCompleted;
        }

        animBrain = nextAnimBrain;

        if (animBrain != null)
        {
            animBrain.SkillCastMomentReached += OnSkillCastMomentReached;
            animBrain.SkillCastInterrupted += OnSkillCastInterrupted;
            animBrain.SkillCompleted += OnSkillCompleted;
        }
    }

    void ApplyTemporaryAutonomy()
    {
        if (_autonomyCaptured)
            return;

        bool capturedAny = false;

        if (behaviorTree != null)
        {
            _defaultBehaviorTreeEnabled = behaviorTree.enabled;
            behaviorTree.enabled = false;
            capturedAny = true;
        }

        if (agent != null && agent.enabled)
        {
            _defaultAgentIsStopped = agent.isStopped;
            _defaultAgentUpdatePosition = agent.updatePosition;
            _defaultAgentUpdateRotation = agent.updateRotation;

            agent.isStopped = true;
            agent.updatePosition = false;
            agent.updateRotation = false;

            if (agent.isOnNavMesh)
                agent.nextPosition = transform.position;

            capturedAny = true;
        }

        if (ApplyTemporaryComponentDisables())
            capturedAny = true;

        if (ApplyPlayerChainLock())
            capturedAny = true;

        _autonomyCaptured = capturedAny;
    }

    void RestoreTemporaryAutonomy()
    {
        if (!_autonomyCaptured)
            return;

        if (behaviorTree != null)
            behaviorTree.enabled = _defaultBehaviorTreeEnabled;

        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh)
                agent.nextPosition = transform.position;

            agent.updatePosition = _defaultAgentUpdatePosition;
            agent.updateRotation = _defaultAgentUpdateRotation;
            agent.isStopped = _defaultAgentIsStopped;
        }

        RestoreTemporaryComponentDisables();
        RestorePlayerChainLock();
        _autonomyCaptured = false;
    }

    bool TryStartEnterUtility(PendingSequenceExecution execution)
    {
        if (execution == null || animBrain == null)
            return false;

        execution.enterRequestId = NextRequestId();
        execution.phase = SequenceExecutionPhase.WaitingForEnterCastMoment;

        bool started = animBrain.TryPlayUtilityWarpIn(execution.enterRequestId);
        if (started)
        {
            Log($"Started utility warp-in for step '{execution.step.RuntimeId}' (request {execution.enterRequestId}).");
            return true;
        }

        if (!execution.step.allowFallbackToInstantTeleportIfUtilityUnavailable)
        {
            CleanupActiveExecution(success: false);
            return false;
        }

        Log($"Utility warp-in is unavailable for '{execution.step.RuntimeId}'. Falling back to instant teleport.");

        if (!TryApplyEntryMovement(execution))
        {
            CleanupActiveExecution(success: false);
            return false;
        }

        return TryStartAttack(execution);
    }

    bool TryStartAttack(PendingSequenceExecution execution)
    {
        if (execution == null || execution.step == null || execution.attackSkillDef == null || animBrain == null)
            return false;

        if (!CanAttackLockedTarget(execution))
        {
            CleanupActiveExecution(success: false);
            return false;
        }

        skillUserProxy.ClearAimOverrides();

        if (ChainAttackTargetingUtility.TryResolveTargetAnchor(execution.lockedTarget, out Transform aimAnchor))
            skillUserProxy.SetAimTargetOverride(aimAnchor);
        else if (execution.lockedTarget != null)
            skillUserProxy.SetAimTargetOverride(execution.lockedTarget);

        if (execution.step.faceLockedTargetOnStart)
            FaceTarget(execution.lockedTarget);

        execution.attackPayloadReleased = false;
        execution.attackRequestId = NextRequestId();
        execution.phase = SequenceExecutionPhase.WaitingForAttackCastMoment;

        bool started = animBrain.TryPlaySkill(
            execution.attackRequestId,
            execution.attackSkillDef,
            execution.attackSkillDef.GetCastPointNormalized());

        if (started)
        {
            Log($"Started attack for step '{execution.step.RuntimeId}' with skill '{execution.attackSkillDef.name}' (request {execution.attackRequestId}).");
            return true;
        }

        CleanupActiveExecution(success: false);
        return false;
    }

    bool TryApplyEntryMovement(PendingSequenceExecution execution)
    {
        if (execution == null || execution.step == null)
            return false;

        ChainAttackStepDef step = execution.step;

        if (step.enterMode == ChainActorEnterMode.InstantTeleportToTarget)
            return TryTeleportToEntryPose(execution);

        if (step.moveMode == ChainActorMoveMode.WarpToLockedTargetAnchor)
            return TryWarpToLockedTargetAnchor(step, execution.lockedTarget);

        return true;
    }

    bool TryTeleportToEntryPose(PendingSequenceExecution execution)
    {
        if (execution == null || execution.step == null || execution.lockedTarget == null)
            return false;

        if (!TryResolveEntryTeleportPose(execution.step, execution.lockedTarget, out Vector3 teleportPosition, out Quaternion teleportRotation))
            return false;

        TeleportActorTo(teleportPosition, teleportRotation);
        return true;
    }

    bool TryResolveEntryTeleportPose(
        ChainAttackStepDef step,
        Transform lockedTarget,
        out Vector3 teleportPosition,
        out Quaternion teleportRotation)
    {
        teleportPosition = Vector3.zero;
        teleportRotation = Quaternion.identity;

        if (step == null || lockedTarget == null)
            return false;

        if (step.teleportProfile != null)
        {
            if (!ChainAttackTargetingUtility.TryResolveTargetAnchor(lockedTarget, out Transform anchorTransform))
                return false;

            return ChainAttackTeleportUtility.TryResolveTeleportPose(
                step.teleportProfile,
                anchorTransform,
                transform.rotation,
                out teleportPosition,
                out teleportRotation);
        }

        return TryResolveLegacyWarpPose(step, lockedTarget, out teleportPosition, out teleportRotation);
    }

    bool TryResolveLegacyWarpPose(
        ChainAttackStepDef step,
        Transform lockedTarget,
        out Vector3 finalPosition,
        out Quaternion finalRotation)
    {
        finalPosition = Vector3.zero;
        finalRotation = Quaternion.identity;

        if (step == null || lockedTarget == null)
            return false;

        if (!ChainAttackTargetingUtility.TryResolveTargetAnchor(lockedTarget, out Transform anchorTransform))
            return false;

        Quaternion baseRotation = step.useTargetAnchorRotation
            ? anchorTransform.rotation
            : transform.rotation;
        finalRotation = Quaternion.AngleAxis(step.warpYawOffset, Vector3.up) * baseRotation;
        finalPosition = anchorTransform.TransformPoint(step.warpOffset);

        if (step.requireNavMeshAtWarpPoint)
        {
            if (!NavMesh.SamplePosition(
                    finalPosition,
                    out NavMeshHit navHit,
                    Mathf.Max(0.05f, step.warpNavMeshSampleDistance),
                    NavMesh.AllAreas))
            {
                return false;
            }

            finalPosition = navHit.position;
        }

        return true;
    }

    bool CanAttackLockedTarget(PendingSequenceExecution execution)
    {
        if (execution == null || execution.step == null)
            return false;

        if (execution.lockedTarget == null)
            return !execution.step.skipIfTargetMissing;

        return ChainAttackTargetingUtility.IsTargetAlive(execution.lockedTarget);
    }

    void OnSkillCastMomentReached(int requestId)
    {
        if (_pendingExecution == null)
            return;

        if (requestId == _pendingExecution.enterRequestId)
        {
            HandleEnterCastMoment(requestId);
            return;
        }

        if (requestId == _pendingExecution.attackRequestId)
        {
            HandleAttackCastMoment(requestId);
            return;
        }

        if (requestId == _pendingExecution.exitRequestId)
            HandleExitCastMoment(requestId);
    }

    void HandleEnterCastMoment(int requestId)
    {
        if (_pendingExecution == null || _pendingExecution.phase != SequenceExecutionPhase.WaitingForEnterCastMoment)
            return;

        if (!TryTeleportToEntryPose(_pendingExecution))
        {
            Log($"Step '{_pendingExecution.step.RuntimeId}' failed to resolve a warp-in pose.");
            if (animBrain != null)
            {
                animBrain.CancelUtilityCastRequest(requestId);
                return;
            }

            CleanupActiveExecution(success: false);
            return;
        }

        if (_pendingExecution.step.faceLockedTargetOnStart)
            FaceTarget(_pendingExecution.lockedTarget);

        _pendingExecution.phase = SequenceExecutionPhase.WaitingForEnterComplete;
        Log($"Teleported '{name}' into chain attack pose for step '{_pendingExecution.step.RuntimeId}'.");
    }

    void HandleAttackCastMoment(int requestId)
    {
        if (_pendingExecution == null || _pendingExecution.phase != SequenceExecutionPhase.WaitingForAttackCastMoment)
            return;

        if (_pendingExecution.step.faceLockedTargetOnCast)
            FaceTarget(_pendingExecution.lockedTarget);

        SkillInstance runtimeSkill = new SkillInstance
        {
            def = _pendingExecution.attackSkillDef,
            level = Mathf.Max(1, _pendingExecution.attackSkillLevel),
        };

        bool executed = _pendingExecution.ignoreResourceCosts
            ? runtimeSkill.TryCastIgnoringResourceCosts(skillUserProxy)
            : ExecutePaidCast(runtimeSkill);

        _pendingExecution.attackPayloadReleased = executed;

        if (!executed)
        {
            Log($"Skill '{_pendingExecution.attackSkillDef.name}' failed at cast moment.");
            if (animBrain != null)
            {
                animBrain.CancelSkillCastRequest(requestId);
                return;
            }

            CleanupActiveExecution(success: false);
            return;
        }

        _pendingExecution.phase = SequenceExecutionPhase.WaitingForAttackComplete;
    }

    void HandleExitCastMoment(int requestId)
    {
        if (_pendingExecution == null || _pendingExecution.phase != SequenceExecutionPhase.WaitingForExitCastMoment)
            return;

        TeleportActorTo(_pendingExecution.recordedOriginPosition, _pendingExecution.recordedOriginRotation);
        _pendingExecution.phase = SequenceExecutionPhase.WaitingForExitComplete;
        Log($"Returned '{name}' to its recorded origin for step '{_pendingExecution.step.RuntimeId}'.");
    }

    void OnSkillCastInterrupted(int requestId)
    {
        if (_pendingExecution == null)
            return;

        if (requestId != _pendingExecution.enterRequestId &&
            requestId != _pendingExecution.attackRequestId &&
            requestId != _pendingExecution.exitRequestId)
        {
            return;
        }

        Log($"Sequence request {requestId} was interrupted during phase '{_pendingExecution.phase}'.");
        CleanupActiveExecution(success: false);
    }

    void OnSkillCompleted()
    {
        if (_pendingExecution == null)
            return;

        switch (_pendingExecution.phase)
        {
            case SequenceExecutionPhase.WaitingForEnterComplete:
                TryStartAttack(_pendingExecution);
                return;

            case SequenceExecutionPhase.WaitingForAttackComplete:
                HandleAttackCompleted();
                return;

            case SequenceExecutionPhase.WaitingForExitComplete:
                CleanupActiveExecution(success: true);
                return;
        }
    }

    void HandleAttackCompleted()
    {
        if (_pendingExecution == null)
            return;

        if (!_pendingExecution.attackPayloadReleased)
        {
            CleanupActiveExecution(success: false);
            return;
        }

        switch (_pendingExecution.step.exitMode)
        {
            case ChainActorExitMode.KeepAtCurrentPosition:
                CleanupActiveExecution(success: true);
                return;

            case ChainActorExitMode.ReturnToRecordedOrigin:
                TeleportActorTo(_pendingExecution.recordedOriginPosition, _pendingExecution.recordedOriginRotation);
                CleanupActiveExecution(success: true);
                return;

            case ChainActorExitMode.ReturnToRecordedOriginOnSequenceEnd:
            case ChainActorExitMode.ReturnToRecordedOriginViaUtilityOnSequenceEnd:
            case ChainActorExitMode.FadeOutAndDeactivateOnSequenceEnd:
                QueueDeferredCleanup(_pendingExecution);
                CleanupActiveExecution(success: true);
                return;

            case ChainActorExitMode.ReturnToRecordedOriginViaUtility:
                if (!TryStartReturnUtility(
                        _pendingExecution.owner,
                        _pendingExecution.recordedOriginPosition,
                        _pendingExecution.recordedOriginRotation,
                        releaseReservationOnComplete: false))
                {
                    CleanupActiveExecution(success: false);
                }
                return;

            case ChainActorExitMode.FadeOutAndDeactivate:
                ExecuteFadeOutCleanup(_pendingExecution.owner);
                return;
        }
    }

    void QueueDeferredCleanup(PendingSequenceExecution execution)
    {
        if (execution == null)
            return;

        _deferredCleanup = new DeferredSequenceCleanup
        {
            owner = execution.owner,
            exitMode = execution.step.exitMode,
            returnPosition = execution.recordedOriginPosition,
            returnRotation = execution.recordedOriginRotation,
        };
    }

    bool TryStartReturnUtility(
        object owner,
        Vector3 returnPosition,
        Quaternion returnRotation,
        bool releaseReservationOnComplete)
    {
        if (animBrain == null)
            return false;

        if (_pendingExecution == null)
        {
            _pendingExecution = new PendingSequenceExecution
            {
                owner = owner,
                step = new ChainAttackStepDef { stepId = "deferred_return" },
                recordedOriginPosition = returnPosition,
                recordedOriginRotation = returnRotation,
            };
        }

        _pendingExecution.owner = owner;
        _pendingExecution.recordedOriginPosition = returnPosition;
        _pendingExecution.recordedOriginRotation = returnRotation;
        _pendingExecution.releaseReservationOnComplete = releaseReservationOnComplete;
        _pendingExecution.exitRequestId = NextRequestId();
        _pendingExecution.phase = SequenceExecutionPhase.WaitingForExitCastMoment;

        bool started = animBrain.TryPlayUtilityWarpIn(_pendingExecution.exitRequestId);
        if (started)
        {
            Log($"Started return utility for '{name}' (request {_pendingExecution.exitRequestId}).");
            return true;
        }

        TeleportActorTo(returnPosition, returnRotation);
        CleanupActiveExecution(success: true);
        return true;
    }

    void CompleteDeferredReturn(DeferredSequenceCleanup cleanup)
    {
        if (cleanup == null)
            return;

        TeleportActorTo(cleanup.returnPosition, cleanup.returnRotation);
        ReleaseReservation(cleanup.owner);
        RestoreTemporaryAutonomy();
    }

    void ExecuteFadeOutCleanup(object owner)
    {
        ReleaseReservation(owner);
        RestoreTemporaryAutonomy();

        if (gameObject == null || !gameObject.activeSelf)
            return;

        if (actorFader != null)
            actorFader.FadeOutThenDeactivate();
        else
            gameObject.SetActive(false);
    }

    bool ExecutePaidCast(SkillInstance runtimeSkill)
    {
        if (runtimeSkill == null || skillUserProxy == null)
            return false;

        if (!runtimeSkill.CanCast(skillUserProxy))
            return false;

        runtimeSkill.Cast(skillUserProxy);
        return true;
    }

    void CleanupActiveExecution(bool success)
    {
        bool shouldReleaseReservation =
            _pendingExecution != null &&
            _pendingExecution.releaseReservationOnComplete &&
            _pendingExecution.owner != null;

        object ownerToRelease = shouldReleaseReservation ? _pendingExecution.owner : null;

        _pendingExecution = null;
        LastExecutionSucceeded = success;
        skillUserProxy?.ClearAimOverrides();

        if (_deferredCleanup == null)
            RestoreTemporaryAutonomy();

        if (ownerToRelease != null)
            ReleaseReservation(ownerToRelease);
    }

    int ResolveActorDefaultSkillLevel(ChainAttackStepDef step)
    {
        if (useRuntimeChainSkillOverride && runtimeChainSkill != null && overrideRuntimeChainSkillLevel)
            return Mathf.Max(1, runtimeChainSkillLevel);

        return step != null ? step.ClampedSkillLevel : 1;
    }

    bool ApplyTemporaryComponentDisables()
    {
        MonoBehaviour[] components = ResolveComponentsToDisableDuringSequence();
        if (components == null || components.Length == 0)
        {
            _capturedDisabledComponents = Array.Empty<MonoBehaviour>();
            _capturedDisabledComponentStates = Array.Empty<bool>();
            return false;
        }

        _capturedDisabledComponents = components;
        _capturedDisabledComponentStates = new bool[components.Length];

        for (int i = 0; i < components.Length; i++)
        {
            MonoBehaviour component = components[i];
            if (component == null)
                continue;

            _capturedDisabledComponentStates[i] = component.enabled;
            component.enabled = false;
        }

        return true;
    }

    void RestoreTemporaryComponentDisables()
    {
        if (_capturedDisabledComponents == null || _capturedDisabledComponents.Length == 0)
            return;

        int count = Mathf.Min(_capturedDisabledComponents.Length, _capturedDisabledComponentStates.Length);
        for (int i = 0; i < count; i++)
        {
            MonoBehaviour component = _capturedDisabledComponents[i];
            if (component == null)
                continue;

            component.enabled = _capturedDisabledComponentStates[i];
        }

        _capturedDisabledComponents = Array.Empty<MonoBehaviour>();
        _capturedDisabledComponentStates = Array.Empty<bool>();
    }

    MonoBehaviour[] ResolveComponentsToDisableDuringSequence()
    {
        List<MonoBehaviour> resolved = null;

        if (componentsToDisableDuringSequence != null && componentsToDisableDuringSequence.Length > 0)
        {
            resolved = new List<MonoBehaviour>(componentsToDisableDuringSequence.Length);
            for (int i = 0; i < componentsToDisableDuringSequence.Length; i++)
            {
                MonoBehaviour component = componentsToDisableDuringSequence[i];
                if (component != null && !resolved.Contains(component))
                    resolved.Add(component);
            }
        }

        if (actorRole == ChainActorRole.Player && autoDisablePlayerInputAndMovement)
        {
            resolved ??= new List<MonoBehaviour>(2);

            PlayerInputHandler inputHandler = GetComponent<PlayerInputHandler>();
            if (inputHandler != null && !resolved.Contains(inputHandler))
                resolved.Add(inputHandler);

            PlayerMovementCC playerMovement = GetComponent<PlayerMovementCC>();
            if (playerMovement != null && !resolved.Contains(playerMovement))
                resolved.Add(playerMovement);
        }

        return resolved != null && resolved.Count > 0
            ? resolved.ToArray()
            : Array.Empty<MonoBehaviour>();
    }

    bool ApplyPlayerChainLock()
    {
        if (actorRole != ChainActorRole.Player || actorContext == null)
            return false;

        actorContext.moveInput = Vector2.zero;
        actorContext.lookInput = Vector2.zero;
        actorContext.WeaponSystem?.SetFiring(false);
        actorContext.stateHub?.RequestCanceledFire();
        actorContext.WeaponSystem?.OnAim(false);

        if (actorContext.DashSystem != null && actorContext.DashSystem.IsDashing)
            actorContext.DashSystem.CancelDash();

        return true;
    }

    void RestorePlayerChainLock()
    {
        if (actorRole != ChainActorRole.Player || actorContext == null)
            return;

        actorContext.moveInput = Vector2.zero;
        actorContext.lookInput = Vector2.zero;
        actorContext.WeaponSystem?.SetFiring(false);
        actorContext.stateHub?.RequestCanceledFire();
        actorContext.WeaponSystem?.OnAim(false);
    }

    string BuildActorDefaultSkillSourceLabel()
    {
        if (useRuntimeChainSkillOverride && runtimeChainSkill != null)
        {
            string levelLabel = overrideRuntimeChainSkillLevel
                ? $"Lv {Mathf.Max(1, runtimeChainSkillLevel)}"
                : "Step Level";
            return $"Runtime Override ({runtimeChainSkill.name}, {levelLabel})";
        }

        if (defaultChainSkill != null)
            return $"Component Default ({defaultChainSkill.name})";

        if (actorContext != null && actorContext.baseStats != null && actorContext.baseStats.chainAttackSkill != null)
            return $"Character Stats Default ({actorContext.baseStats.chainAttackSkill.name})";

        return "None";
    }

    void CancelDeferredSequenceCleanup()
    {
        _deferredCleanup = null;
    }

    bool TryWarpToLockedTargetAnchor(ChainAttackStepDef step, Transform lockedTarget)
    {
        if (!TryResolveLegacyWarpPose(step, lockedTarget, out Vector3 finalPosition, out Quaternion finalRotation))
            return false;

        TeleportActorTo(finalPosition, finalRotation);
        return true;
    }

    void TeleportActorTo(Vector3 worldPosition, Quaternion worldRotation)
    {
        transform.SetPositionAndRotation(worldPosition, worldRotation);

        if (agent == null || !agent.enabled)
            return;

        if (agent.isOnNavMesh)
        {
            agent.nextPosition = worldPosition;
            return;
        }

        if (NavMesh.SamplePosition(worldPosition, out NavMeshHit navHit, 1f, NavMesh.AllAreas))
        {
            agent.Warp(navHit.position);
            transform.position = navHit.position;
            agent.nextPosition = navHit.position;
        }
    }

    void FaceTarget(Transform lockedTarget)
    {
        if (lockedTarget == null)
            return;

        Transform targetAnchor = lockedTarget;
        if (ChainAttackTargetingUtility.TryResolveTargetAnchor(lockedTarget, out Transform anchorTransform))
            targetAnchor = anchorTransform;

        Transform origin = skillUserProxy != null && skillUserProxy.CastOrigin != null
            ? skillUserProxy.CastOrigin
            : transform;
        Vector3 lookDirection = targetAnchor.position - origin.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude <= 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.nextPosition = transform.position;
    }

    int NextRequestId()
    {
        if (_nextRequestId == int.MaxValue)
            _nextRequestId = 1;

        return _nextRequestId++;
    }

    void Log(string message)
    {
        if (!logSequenceExecution)
            return;

        Debug.Log($"[FieldAllyMember:{name}] {message}", this);
    }
}
