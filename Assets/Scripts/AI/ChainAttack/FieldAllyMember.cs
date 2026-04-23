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
        WaitingForAttackStart = 3,
        WaitingForAttackCastMoment = 4,
        WaitingForAttackComplete = 5,
        WaitingForExitStart = 6,
        WaitingForExitCastMoment = 7,
        WaitingForExitComplete = 8,
    }

    sealed class PendingSequenceExecution
    {
        public int executionId;
        public object owner;
        public ChainAttackStepDef step;
        public Transform lockedTarget;
        public ISkillUser attackSkillUser;
        public SkillGemDefinition attackSkillDef;
        public int attackSkillLevel;
        public bool ignoreResourceCosts;
        public bool attackPayloadReleased;
        public bool continueReleased;
        public bool releaseReservationOnComplete;
        public int enterRequestId;
        public int attackRequestId;
        public int exitRequestId;
        public SequenceExecutionPhase phase;
        public float phaseStartedAt;
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
    [SerializeField] private HealthSystem actorHealthSystem;
    [SerializeField] private AITargetInfo actorTargetInfo;
    [SerializeField] private Rigidbody actorRigidbody;
    [SerializeField] private CharacterController actorCharacterController;
    [SerializeField] private BehaviorTree behaviorTree;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private CharacterAnimBrain animBrain;
    [SerializeField] private ChainSkillUserProxy skillUserProxy;
    [SerializeField] private AIAimTargetDriver aimTargetDriver;
    [SerializeField] private ASPHelperDitherFader actorFader;
    [FoldoutGroup("Chain Attack", Expanded = false), LabelText("Disable Components During Sequence")]
    [SerializeField] private MonoBehaviour[] componentsToDisableDuringSequence;
    [FoldoutGroup("Chain Attack", Expanded = false), LabelText("Auto Disable Player Input/Move")]
    [SerializeField] private bool autoDisablePlayerInputAndMovement = true;
    [FoldoutGroup("Chain Attack", Expanded = false), LabelText("Make Ally Invincible During Sequence")]
    [SerializeField] private bool makeAllyInvincibleDuringSequence = true;
    [FoldoutGroup("Chain Attack", Expanded = false), LabelText("Make Ally Untargetable During Sequence")]
    [SerializeField] private bool makeAllyUntargetableDuringSequence = true;
    [FoldoutGroup("Chain Attack", Expanded = false), LabelText("Ignore Collision During Sequence")]
    [SerializeField] private bool ignoreCollisionDuringSequence = true;
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
    [FoldoutGroup("Chain Attack/Debug"), LabelText("Use Direct Skill User For Chain Cast")]
    [SerializeField] private bool useDirectSkillUserForChainCastDebug;
    [FoldoutGroup("Chain Attack/Debug"), ShowIf(nameof(useDirectSkillUserForChainCastDebug)), LabelText("Direct Skill User Source")]
    [SerializeField] private MonoBehaviour directSkillUserSource;
    [FoldoutGroup("Chain Attack", Expanded = false), LabelText("Utility Recovery Timeout"), MinValue(0.1f)]
    [SerializeField] private float utilityRecoveryTimeoutSeconds = 1.25f;
    [SerializeField] private bool logSequenceExecution;

    PendingSequenceExecution _pendingExecution;
    DeferredSequenceCleanup _deferredCleanup;
    object _reservationOwner;
    int _nextRequestId = 1;
    int _nextExecutionId = 1;
    int _lastCompletedExecutionId;
    bool _lastCompletedExecutionSucceeded;
    bool _lastCompletedExecutionHadDeferredCleanup;
    ISkillUser _directSkillUser;
    SkillGemDefinition _lastResolvedAttackSkill;
    int _lastResolvedAttackLevel = 1;
    MonoBehaviour[] _capturedDisabledComponents = Array.Empty<MonoBehaviour>();
    bool[] _capturedDisabledComponentStates = Array.Empty<bool>();

    bool _autonomyCaptured;
    bool _defaultBehaviorTreeEnabled;
    bool _defaultAgentEnabled;
    bool _defaultAgentIsStopped;
    bool _defaultAgentUpdatePosition;
    bool _defaultAgentUpdateRotation;
    bool _defaultAgentHadPath;
    bool _actorProtectionApplied;
    bool _collisionMaskCaptured;
    bool _visualHiddenForChainTransition;
    int _actorInvincibilityToken;
    int _actorUntargetableToken;
    LayerMask _defaultRigidbodyExcludeLayers;
    LayerMask _defaultCharacterControllerExcludeLayers;
    Vector3 _defaultAgentDestination;

    public ChainActorRole ActorRole => actorRole;
    public bool IsReserved => _reservationOwner != null;
    public bool HasActiveSequenceExecution => _pendingExecution != null;
    public bool HasDeferredSequenceCleanup => _deferredCleanup != null;
    public int ActiveSequenceExecutionId => _pendingExecution != null ? _pendingExecution.executionId : 0;
    public bool IsInKnockback => IsActorInKnockback();
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
        (animBrain != null && animBrain.IsExclusiveLocomotionActive);
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

    [ShowInInspector, ReadOnly, FoldoutGroup("Chain Attack/Debug"), PropertyOrder(20), LabelText("Chain Cast Path")]
    string InspectorChainCastPath => useDirectSkillUserForChainCastDebug
        ? "Direct ISkillUser"
        : "ChainSkillUserProxy";

    [ShowInInspector, ReadOnly, FoldoutGroup("Chain Attack/Debug"), PropertyOrder(21), LabelText("Resolved Direct Skill User")]
    string InspectorResolvedDirectSkillUser => DescribeSkillUser(ResolveDirectSkillUser());

    void Awake()
    {
        CacheReferences();
    }

    void Update()
    {
        ProcessDeferredExecutionPhase();
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
        _visualHiddenForChainTransition = false;
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

    public bool IsSequenceExecutionReadyToContinue(int executionId)
    {
        if (executionId <= 0)
            return false;

        if (_pendingExecution != null && _pendingExecution.executionId == executionId)
            return _pendingExecution.continueReleased;

        return _lastCompletedExecutionId == executionId;
    }

    public bool TryGetCompletedSequenceExecutionResult(int executionId, out bool success, out bool hasDeferredCleanup)
    {
        if (executionId > 0 && _lastCompletedExecutionId == executionId)
        {
            success = _lastCompletedExecutionSucceeded;
            hasDeferredCleanup = _lastCompletedExecutionHadDeferredCleanup;
            return true;
        }

        success = false;
        hasDeferredCleanup = false;
        return false;
    }

    public bool TryStartSequenceStep(ChainAttackStepDef step, Transform lockedTarget)
    {
        if (step == null)
            return false;

        CacheReferences();

        if (animBrain == null || _reservationOwner == null)
            return false;

        if (step.requireActorAlive && !IsAlive)
            return false;

        if (IsActorInKnockback())
        {
            Log($"Step '{step.RuntimeId}' cannot start because actor '{name}' is in knockback.");
            return false;
        }

        if (IsBusy)
            return false;

        if (lockedTarget == null && step.skipIfTargetMissing)
            return false;

        LastExecutionSucceeded = false;
        ApplyTemporaryAutonomy();
        skillUserProxy.ClearAimOverrides();
        aimTargetDriver?.ClearOverride();

        if (!TryResolveAttackSkill(step, out SkillGemDefinition resolvedSkillDef, out int resolvedSkillLevel))
        {
            Log($"Step '{step.RuntimeId}' cannot start because no chain skill is configured for actor '{name}'.");
            RestoreTemporaryAutonomy();
            return false;
        }

        PendingSequenceExecution execution = new()
        {
            executionId = NextExecutionId(),
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
        ApplyChainAimTargetOverride(execution.lockedTarget);

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

            case ChainActorExitMode.ReturnToRecordedOriginThenWarpInOnSequenceEnd:
                if (TryStartReturnWarpInAtRecordedOrigin(
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

    public bool TryDescribeOwnedSequenceWork(object owner, out string description)
    {
        description = null;

        if (!OwnsSequenceWork(owner))
            return false;

        List<string> states = new();

        if (_pendingExecution != null && ReferenceEquals(_pendingExecution.owner, owner))
        {
            string stepId = _pendingExecution.step != null
                ? _pendingExecution.step.RuntimeId
                : "unknown-step";
            states.Add($"pending phase={_pendingExecution.phase} step={stepId}");
        }

        if (_deferredCleanup != null && ReferenceEquals(_deferredCleanup.owner, owner))
            states.Add($"deferred exit={_deferredCleanup.exitMode}");

        if (_reservationOwner != null && ReferenceEquals(_reservationOwner, owner))
            states.Add("reserved");

        description = $"{actorRole}:{name} [{string.Join(", ", states)}]";
        return true;
    }

    void CacheReferences()
    {
        RefreshCollisionReferences();

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

        if (aimTargetDriver == null)
            aimTargetDriver = GetComponent<AIAimTargetDriver>();

        if (aimTargetDriver == null && Application.isPlaying)
            aimTargetDriver = gameObject.AddComponent<AIAimTargetDriver>();

        if (directSkillUserSource is not ISkillUser)
            directSkillUserSource = null;

        if (_directSkillUser == null ||
            (directSkillUserSource is ISkillUser explicitSource && !ReferenceEquals(_directSkillUser, explicitSource)))
        {
            _directSkillUser = null;
        }

        if (actorFader == null)
        {
            actorFader = GetComponent<ASPHelperDitherFader>();
            if (actorFader == null)
                actorFader = GetComponentInChildren<ASPHelperDitherFader>(true);
        }
    }

    void RefreshCollisionReferences()
    {
        if (actorContext == null)
            actorContext = GetComponent<CharacteContext>();

        if (actorContext != null && actorContext.HealthSystem != null)
            actorHealthSystem = actorContext.HealthSystem;
        else if (actorHealthSystem == null)
            actorHealthSystem = GetComponent<HealthSystem>();

        if (actorContext != null && actorContext.HealthSystem == null)
            actorContext.HealthSystem = actorHealthSystem;

        if (actorTargetInfo == null)
        {
            actorTargetInfo = GetComponent<AITargetInfo>();
            if (actorTargetInfo == null)
                actorTargetInfo = GetComponentInChildren<AITargetInfo>(true);
        }

        if (actorContext != null && actorContext.rb != null)
            actorRigidbody = actorContext.rb;
        else if (actorRigidbody == null)
            actorRigidbody = GetComponent<Rigidbody>();

        if (actorContext != null && actorContext.rb == null)
            actorContext.rb = actorRigidbody;

        if (actorContext != null && actorContext.cc != null)
            actorCharacterController = actorContext.cc;
        else if (actorCharacterController == null)
            actorCharacterController = GetComponent<CharacterController>();

        if (actorContext != null && actorContext.cc == null)
            actorContext.cc = actorCharacterController;

        if (actorContext != null && actorContext.KnockbackMotor == null)
            actorContext.KnockbackMotor = GetComponent<CharacterKnockbackMotor>();
    }

    bool IsActorInKnockback()
    {
        CharacterKnockbackMotor knockbackMotor = null;
        if (actorContext != null)
            knockbackMotor = actorContext.KnockbackMotor;

        if (knockbackMotor == null)
            knockbackMotor = GetComponent<CharacterKnockbackMotor>();

        if (actorContext != null && actorContext.KnockbackMotor == null)
            actorContext.KnockbackMotor = knockbackMotor;

        if (knockbackMotor != null && knockbackMotor.IsActive)
            return true;

        return actorContext != null &&
               actorContext.stateHub != null &&
               actorContext.stateHub.MoveSM != null &&
               actorContext.stateHub.MoveSM.CurrentId == MoveStateId.Knockback;
    }

    void SubscribeToAnimBrain(CharacterAnimBrain nextAnimBrain)
    {
        if (animBrain != null)
        {
            animBrain.ChainCastMomentReached -= OnChainCastMomentReached;
            animBrain.ChainAdvanceMomentReached -= OnChainAdvanceMomentReached;
            animBrain.ChainPlaybackInterrupted -= OnChainPlaybackInterrupted;
            animBrain.ChainPlaybackCompleted -= OnChainPlaybackCompleted;
        }

        animBrain = nextAnimBrain;

        if (animBrain != null)
        {
            animBrain.ChainCastMomentReached += OnChainCastMomentReached;
            animBrain.ChainAdvanceMomentReached += OnChainAdvanceMomentReached;
            animBrain.ChainPlaybackInterrupted += OnChainPlaybackInterrupted;
            animBrain.ChainPlaybackCompleted += OnChainPlaybackCompleted;
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
            _defaultAgentEnabled = true;
            _defaultAgentIsStopped = agent.isStopped;
            _defaultAgentUpdatePosition = agent.updatePosition;
            _defaultAgentUpdateRotation = agent.updateRotation;
            _defaultAgentHadPath = agent.hasPath || agent.pathPending;
            _defaultAgentDestination = agent.isOnNavMesh ? agent.destination : transform.position;

            agent.isStopped = true;
            agent.updatePosition = false;
            agent.updateRotation = false;

            if (agent.isOnNavMesh)
                agent.nextPosition = transform.position;

            agent.enabled = false;
            capturedAny = true;
        }
        else
        {
            _defaultAgentEnabled = false;
            _defaultAgentHadPath = false;
            _defaultAgentDestination = transform.position;
        }

        if (ApplyTemporaryComponentDisables())
            capturedAny = true;

        if (ApplyTemporaryActorProtection())
            capturedAny = true;

        if (ApplyTemporaryNoCollision())
            capturedAny = true;

        if (ApplyPlayerChainLock())
            capturedAny = true;

        _autonomyCaptured = capturedAny;
    }

    void RestoreTemporaryAutonomy()
    {
        if (!_autonomyCaptured)
            return;

        bool restoreAgentToDefaultAutonomy = _defaultBehaviorTreeEnabled;

        if (behaviorTree != null)
            behaviorTree.enabled = _defaultBehaviorTreeEnabled;

        if (agent != null && _defaultAgentEnabled)
        {
            if (!agent.enabled)
                agent.enabled = true;

            SyncAgentToTransform();

            agent.updatePosition = restoreAgentToDefaultAutonomy ? true : _defaultAgentUpdatePosition;
            agent.updateRotation = restoreAgentToDefaultAutonomy ? true : _defaultAgentUpdateRotation;
            agent.isStopped = restoreAgentToDefaultAutonomy ? false : _defaultAgentIsStopped;

            if ((!restoreAgentToDefaultAutonomy && !_defaultAgentIsStopped || restoreAgentToDefaultAutonomy) &&
                _defaultAgentHadPath &&
                agent.isOnNavMesh &&
                !agent.pathPending &&
                !agent.hasPath)
            {
                agent.SetDestination(_defaultAgentDestination);
            }
        }

        _defaultAgentEnabled = false;
        _defaultAgentHadPath = false;
        _defaultAgentDestination = transform.position;

        RestoreTemporaryComponentDisables();
        RestoreTemporaryActorProtection();
        RestoreTemporaryNoCollision();
        RestorePlayerChainLock();
        _autonomyCaptured = false;
    }

    bool ApplyTemporaryActorProtection()
    {
        if (actorRole == ChainActorRole.Player || _actorProtectionApplied)
            return false;

        if (!makeAllyInvincibleDuringSequence && !makeAllyUntargetableDuringSequence)
            return false;

        RefreshCollisionReferences();

        bool applied = false;

        if (makeAllyInvincibleDuringSequence && actorHealthSystem != null)
        {
            _actorInvincibilityToken = actorHealthSystem.AcquireInvincibilityToken();
            applied = true;
        }

        if (makeAllyUntargetableDuringSequence && actorTargetInfo != null)
        {
            _actorUntargetableToken = actorTargetInfo.AcquireUntargetableToken();
            applied = true;
        }

        _actorProtectionApplied = applied;
        return applied;
    }

    void RestoreTemporaryActorProtection()
    {
        if (_actorUntargetableToken != 0 && actorTargetInfo != null)
            actorTargetInfo.ReleaseUntargetableToken(_actorUntargetableToken);

        if (_actorInvincibilityToken != 0 && actorHealthSystem != null)
            actorHealthSystem.ReleaseInvincibilityToken(_actorInvincibilityToken);

        _actorUntargetableToken = 0;
        _actorInvincibilityToken = 0;
        _actorProtectionApplied = false;
    }

    bool ApplyTemporaryNoCollision()
    {
        if (!ignoreCollisionDuringSequence)
            return false;

        RefreshCollisionReferences();

        if (actorRigidbody == null && actorCharacterController == null)
            return false;

        if (!_collisionMaskCaptured)
        {
            _defaultRigidbodyExcludeLayers = actorRigidbody != null
                ? actorRigidbody.excludeLayers
                : 0;
            _defaultCharacterControllerExcludeLayers = actorCharacterController != null
                ? actorCharacterController.excludeLayers
                : 0;
            _collisionMaskCaptured = true;
        }

        if (actorRigidbody != null)
            actorRigidbody.excludeLayers = Physics.AllLayers;

        if (actorCharacterController != null)
            actorCharacterController.excludeLayers = Physics.AllLayers;

        return true;
    }

    void RestoreTemporaryNoCollision()
    {
        if (!_collisionMaskCaptured)
            return;

        if (actorRigidbody != null)
            actorRigidbody.excludeLayers = _defaultRigidbodyExcludeLayers;

        if (actorCharacterController != null)
            actorCharacterController.excludeLayers = _defaultCharacterControllerExcludeLayers;

        _collisionMaskCaptured = false;
    }

    bool TryStartEnterUtility(PendingSequenceExecution execution)
    {
        if (execution == null || animBrain == null)
            return false;

        execution.enterRequestId = NextRequestId();
        SetExecutionPhase(execution, SequenceExecutionPhase.WaitingForEnterCastMoment);

        bool started = animBrain.TryPlayChainUtilityWarpOut(execution.enterRequestId);
        if (started)
        {
            StartChainVisualLifecycle(hideOnAnimationComplete: true);
            Log($"Started utility warp-out for step '{execution.step.RuntimeId}' (request {execution.enterRequestId}).");
            return true;
        }

        if (!execution.step.allowFallbackToInstantTeleportIfUtilityUnavailable)
        {
            CleanupActiveExecution(success: false);
            return false;
        }

        Log($"Utility warp-out is unavailable for '{execution.step.RuntimeId}'. Falling back to instant teleport.");

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

        if (!TryResolveAttackSkillUser(execution, out ISkillUser attackSkillUser))
        {
            CleanupActiveExecution(success: false);
            return false;
        }

        execution.attackSkillUser = attackSkillUser;

        if (execution.step.faceLockedTargetOnStart)
            FaceTarget(execution.lockedTarget);

        execution.attackPayloadReleased = false;
        execution.attackRequestId = NextRequestId();
        SetExecutionPhase(execution, SequenceExecutionPhase.WaitingForAttackCastMoment);

        bool started = animBrain.TryPlayChainSkill(
            execution.attackRequestId,
            execution.attackSkillDef,
            execution.attackSkillDef.GetCastPointNormalized(),
            ShouldRequestAttackAdvanceMoment(execution),
            ResolveAttackContinueNormalizedTime(execution));

        if (started)
        {
            StartChainVisualLifecycle(ShouldAutoHideNearAttackEnd(execution.step.exitMode));
            Log(
                $"Started attack for step '{execution.step.RuntimeId}' with skill '{execution.attackSkillDef.name}' " +
                $"(request {execution.attackRequestId}, castUser={DescribeSkillUser(execution.attackSkillUser)}).");
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

        HideVisualForTeleport();
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

    static bool ShouldRequestAttackAdvanceMoment(PendingSequenceExecution execution)
    {
        return ResolveAttackContinueMode(execution) == ChainStepContinueMode.OnAttackNormalizedTime;
    }

    static ChainStepContinueMode ResolveAttackContinueMode(PendingSequenceExecution execution)
    {
        if (execution == null)
            return ChainStepContinueMode.OnStepComplete;

        if (execution.step != null && execution.step.UsesStepContinueOverride)
            return execution.step.continueMode;

        return execution.attackSkillDef != null && execution.attackSkillDef.payload != null
            ? execution.attackSkillDef.payload.GetChainContinueMode()
            : ChainStepContinueMode.OnStepComplete;
    }

    static float ResolveConfiguredContinueNormalizedTime(PendingSequenceExecution execution)
    {
        if (execution == null)
            return 1f;

        if (execution.step != null && execution.step.UsesStepContinueOverride)
            return execution.step.ClampedContinueNormalizedTime;

        return execution.attackSkillDef != null && execution.attackSkillDef.payload != null
            ? execution.attackSkillDef.payload.GetChainContinueNormalizedTime()
            : 1f;
    }

    static float ResolveAttackContinueNormalizedTime(PendingSequenceExecution execution)
    {
        return ResolveAttackContinueMode(execution) switch
        {
            ChainStepContinueMode.OnAttackCastMoment => execution?.attackSkillDef != null
                ? execution.attackSkillDef.GetCastPointNormalized()
                : ResolveConfiguredContinueNormalizedTime(execution),
            ChainStepContinueMode.OnAttackNormalizedTime => ResolveConfiguredContinueNormalizedTime(execution),
            _ => 1f,
        };
    }

    void ReleaseContinueSignalIfNeeded(PendingSequenceExecution execution, string reason)
    {
        if (execution == null || execution.continueReleased)
            return;

        if (ResolveAttackContinueMode(execution) == ChainStepContinueMode.OnStepComplete)
            return;

        execution.continueReleased = true;
        Log($"Released chain continue signal for step '{execution.step.RuntimeId}' at {reason}.");
    }

    void OnChainCastMomentReached(int requestId)
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

    void OnChainAdvanceMomentReached(int requestId)
    {
        if (_pendingExecution == null ||
            requestId != _pendingExecution.attackRequestId ||
            _pendingExecution.phase != SequenceExecutionPhase.WaitingForAttackComplete ||
            !_pendingExecution.attackPayloadReleased)
        {
            return;
        }

        ReleaseContinueSignalIfNeeded(_pendingExecution, "attack normalized time");
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
                animBrain.CancelChainPlaybackRequest(requestId);
                return;
            }

            CleanupActiveExecution(success: false);
            return;
        }

        if (_pendingExecution.step.faceLockedTargetOnStart)
            FaceTarget(_pendingExecution.lockedTarget);

        SetExecutionPhase(_pendingExecution, SequenceExecutionPhase.WaitingForEnterComplete);
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

        ISkillUser attackSkillUser = _pendingExecution.attackSkillUser;
        if (attackSkillUser == null && !TryResolveAttackSkillUser(_pendingExecution, out attackSkillUser))
        {
            Log($"Skill '{_pendingExecution.attackSkillDef.name}' failed because no cast user could be resolved.");
            CleanupActiveExecution(success: false);
            return;
        }

        _pendingExecution.attackSkillUser = attackSkillUser;

        bool executed = _pendingExecution.ignoreResourceCosts
            ? runtimeSkill.TryCastIgnoringResourceCosts(attackSkillUser)
            : ExecutePaidCast(runtimeSkill, attackSkillUser);

        _pendingExecution.attackPayloadReleased = executed;

        if (!executed)
        {
            Log(
                $"Skill '{_pendingExecution.attackSkillDef.name}' failed at cast moment " +
                $"with castUser={DescribeSkillUser(attackSkillUser)}.");
            if (animBrain != null)
            {
                animBrain.CancelChainPlaybackRequest(requestId);
                return;
            }

            CleanupActiveExecution(success: false);
            return;
        }

        SetExecutionPhase(_pendingExecution, SequenceExecutionPhase.WaitingForAttackComplete);

        if (ResolveAttackContinueMode(_pendingExecution) == ChainStepContinueMode.OnAttackCastMoment)
            ReleaseContinueSignalIfNeeded(_pendingExecution, "attack cast moment");
    }

    void HandleExitCastMoment(int requestId)
    {
        if (_pendingExecution == null || _pendingExecution.phase != SequenceExecutionPhase.WaitingForExitCastMoment)
            return;

        HideVisualForTeleport();
        TeleportActorTo(_pendingExecution.recordedOriginPosition, _pendingExecution.recordedOriginRotation);
        RevealVisualAfterTeleportIfNeeded();
        SetExecutionPhase(_pendingExecution, SequenceExecutionPhase.WaitingForExitComplete);
        Log($"Returned '{name}' to its recorded origin for step '{_pendingExecution.step.RuntimeId}'.");
    }

    void OnChainPlaybackInterrupted(int requestId)
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

    void OnChainPlaybackCompleted(int requestId)
    {
        if (_pendingExecution == null)
            return;

        if (requestId != _pendingExecution.enterRequestId &&
            requestId != _pendingExecution.attackRequestId &&
            requestId != _pendingExecution.exitRequestId)
        {
            return;
        }

        switch (_pendingExecution.phase)
        {
            case SequenceExecutionPhase.WaitingForEnterComplete:
                QueueAttackStart("utility animation completed");
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
                HideVisualForTeleport();
                TeleportActorTo(_pendingExecution.recordedOriginPosition, _pendingExecution.recordedOriginRotation);
                RevealVisualAfterTeleportIfNeeded();
                CleanupActiveExecution(success: true);
                return;

            case ChainActorExitMode.ReturnToRecordedOriginThenWarpIn:
                QueueImmediateReturnUtilityStart();
                return;

            case ChainActorExitMode.ReturnToRecordedOriginOnSequenceEnd:
            case ChainActorExitMode.ReturnToRecordedOriginViaUtilityOnSequenceEnd:
            case ChainActorExitMode.FadeOutAndDeactivateOnSequenceEnd:
            case ChainActorExitMode.ReturnToRecordedOriginThenWarpInOnSequenceEnd:
                QueueDeferredCleanup(_pendingExecution);
                CleanupActiveExecution(success: true);
                return;

            case ChainActorExitMode.ReturnToRecordedOriginViaUtility:
                QueueImmediateReturnUtilityStart();
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

    void ProcessDeferredExecutionPhase()
    {
        if (_pendingExecution == null || animBrain == null)
            return;

        switch (_pendingExecution.phase)
        {
            case SequenceExecutionPhase.WaitingForEnterCastMoment:
                if (HasPhaseTimedOut(_pendingExecution, utilityRecoveryTimeoutSeconds))
                {
                    Log($"Utility warp-out cast moment timed out for step '{_pendingExecution.step.RuntimeId}'. Forcing teleport recovery.");
                    HandleEnterCastMoment(_pendingExecution.enterRequestId);
                }
                return;

            case SequenceExecutionPhase.WaitingForEnterComplete:
                if (!animBrain.IsUtilityActive)
                {
                    QueueAttackStart("utility animation state exited without completion callback");
                    return;
                }

                if (HasPhaseTimedOut(_pendingExecution, utilityRecoveryTimeoutSeconds))
                {
                    Log($"Utility warp-out completion timed out for step '{_pendingExecution.step.RuntimeId}'. Forcing attack handoff.");
                    animBrain.CancelChainPlaybackRequest(_pendingExecution.enterRequestId);
                    QueueAttackStart("utility completion timeout recovery");
                }
                return;

            case SequenceExecutionPhase.WaitingForAttackStart:
                TryStartAttack(_pendingExecution);
                return;

            case SequenceExecutionPhase.WaitingForAttackCastMoment:
                if (!animBrain.IsChainPlaybackActive)
                {
                    Log($"Attack cast moment for step '{_pendingExecution.step.RuntimeId}' was missed. Forcing cast recovery.");
                    HandleAttackCastMoment(_pendingExecution.attackRequestId);
                    return;
                }

                if (HasPhaseTimedOut(_pendingExecution, utilityRecoveryTimeoutSeconds))
                {
                    Log($"Attack cast moment timed out for step '{_pendingExecution.step.RuntimeId}'. Forcing skill payload release.");
                    HandleAttackCastMoment(_pendingExecution.attackRequestId);
                }
                return;

            case SequenceExecutionPhase.WaitingForAttackComplete:
                if (!animBrain.IsChainPlaybackActive)
                {
                    Log($"Attack for step '{_pendingExecution.step.RuntimeId}' finished via animation state exit recovery.");
                    HandleAttackCompleted();
                }
                return;

            case SequenceExecutionPhase.WaitingForExitStart:
                bool startedExit = _pendingExecution.step != null &&
                                   _pendingExecution.step.exitMode == ChainActorExitMode.ReturnToRecordedOriginThenWarpIn
                    ? TryStartReturnWarpInAtRecordedOrigin(
                        _pendingExecution.owner,
                        _pendingExecution.recordedOriginPosition,
                        _pendingExecution.recordedOriginRotation,
                        releaseReservationOnComplete: false)
                    : TryStartReturnUtility(
                        _pendingExecution.owner,
                        _pendingExecution.recordedOriginPosition,
                        _pendingExecution.recordedOriginRotation,
                        releaseReservationOnComplete: false);

                if (!startedExit)
                {
                    CleanupActiveExecution(success: false);
                }
                return;

            case SequenceExecutionPhase.WaitingForExitCastMoment:
                if (HasPhaseTimedOut(_pendingExecution, utilityRecoveryTimeoutSeconds))
                {
                    Log($"Return utility cast moment timed out for '{name}'. Forcing return teleport recovery.");
                    HandleExitCastMoment(_pendingExecution.exitRequestId);
                }
                return;

            case SequenceExecutionPhase.WaitingForExitComplete:
                if (!animBrain.IsUtilityActive)
                {
                    Log($"Return utility for '{name}' finished via animation state exit recovery.");
                    CleanupActiveExecution(success: true);
                    return;
                }

                if (HasPhaseTimedOut(_pendingExecution, utilityRecoveryTimeoutSeconds))
                {
                    Log($"Return utility completion timed out for '{name}'. Forcing return cleanup.");
                    animBrain.CancelChainPlaybackRequest(_pendingExecution.exitRequestId);
                    CleanupActiveExecution(success: true);
                }
                return;
        }
    }

    void QueueAttackStart(string reason)
    {
        if (_pendingExecution == null)
            return;

        _pendingExecution.enterRequestId = 0;
        SetExecutionPhase(_pendingExecution, SequenceExecutionPhase.WaitingForAttackStart);
        Log($"Queued attack start for step '{_pendingExecution.step.RuntimeId}' after {reason}.");
    }

    void QueueImmediateReturnUtilityStart()
    {
        if (_pendingExecution == null)
            return;

        _pendingExecution.attackRequestId = 0;
        SetExecutionPhase(_pendingExecution, SequenceExecutionPhase.WaitingForExitStart);
        Log($"Queued return utility for step '{_pendingExecution.step.RuntimeId}'.");
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
        SetExecutionPhase(_pendingExecution, SequenceExecutionPhase.WaitingForExitCastMoment);

        bool started = animBrain.TryPlayChainUtilityWarpOut(_pendingExecution.exitRequestId);
        if (started)
        {
            StartChainVisualLifecycle(hideOnAnimationComplete: true);
            Log($"Started return utility for '{name}' (request {_pendingExecution.exitRequestId}).");
            return true;
        }

        HideVisualForTeleport();
        TeleportActorTo(returnPosition, returnRotation);
        RevealVisualAfterTeleportIfNeeded();
        CleanupActiveExecution(success: true);
        return true;
    }

    bool TryStartReturnWarpInAtRecordedOrigin(
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
                step = new ChainAttackStepDef { stepId = "deferred_return_warp_in" },
                recordedOriginPosition = returnPosition,
                recordedOriginRotation = returnRotation,
            };
        }

        _pendingExecution.owner = owner;
        _pendingExecution.recordedOriginPosition = returnPosition;
        _pendingExecution.recordedOriginRotation = returnRotation;
        _pendingExecution.releaseReservationOnComplete = releaseReservationOnComplete;
        _pendingExecution.attackRequestId = 0;
        _pendingExecution.exitRequestId = NextRequestId();

        HideVisualForTeleport();
        TeleportActorTo(returnPosition, returnRotation);

        bool started = animBrain.TryPlayChainUtilityWarpIn(_pendingExecution.exitRequestId);
        if (!started)
        {
            _pendingExecution.exitRequestId = 0;
            RevealVisualAfterTeleportIfNeeded();
            Log($"Return warp-in could not start for '{name}'. Falling back to immediate reveal at the recorded origin.");
            CleanupActiveExecution(success: true);
            return true;
        }

        SetExecutionPhase(_pendingExecution, SequenceExecutionPhase.WaitingForExitComplete);
        StartChainVisualLifecycle(hideOnAnimationComplete: false);
        Log($"Started return warp-in for '{name}' (request {_pendingExecution.exitRequestId}).");
        return true;
    }

    void CompleteDeferredReturn(DeferredSequenceCleanup cleanup)
    {
        if (cleanup == null)
            return;

        HideVisualForTeleport();
        TeleportActorTo(cleanup.returnPosition, cleanup.returnRotation);
        RevealVisualAfterTeleportIfNeeded();
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

    bool ExecutePaidCast(SkillInstance runtimeSkill, ISkillUser skillUser)
    {
        if (runtimeSkill == null || skillUser == null)
            return false;

        if (!runtimeSkill.CanCast(skillUser))
            return false;

        runtimeSkill.Cast(skillUser);
        return true;
    }

    bool TryResolveAttackSkillUser(PendingSequenceExecution execution, out ISkillUser attackSkillUser)
    {
        attackSkillUser = null;

        if (execution != null)
            ApplyChainAimTargetOverride(execution.lockedTarget);

        if (useDirectSkillUserForChainCastDebug)
        {
            skillUserProxy?.ClearAimOverrides();
            attackSkillUser = ResolveDirectSkillUser();
            if (attackSkillUser == null)
            {
                Log($"Direct chain cast debug path is enabled but no direct ISkillUser was found on '{name}'.");
                return false;
            }

            return true;
        }

        if (skillUserProxy == null)
            return false;

        skillUserProxy.ClearAimOverrides();

        if (execution != null)
        {
            if (ChainAttackTargetingUtility.TryResolveTargetAnchor(execution.lockedTarget, out Transform aimAnchor))
                skillUserProxy.SetAimTargetOverride(aimAnchor);
            else if (execution.lockedTarget != null)
                skillUserProxy.SetAimTargetOverride(execution.lockedTarget);
        }

        attackSkillUser = skillUserProxy;
        return true;
    }

    void CleanupActiveExecution(bool success)
    {
        bool shouldReleaseReservation =
            _pendingExecution != null &&
            _pendingExecution.releaseReservationOnComplete &&
            _pendingExecution.owner != null;
        bool hasDeferredCleanup =
            _pendingExecution != null &&
            _deferredCleanup != null &&
            _pendingExecution.owner != null &&
            ReferenceEquals(_deferredCleanup.owner, _pendingExecution.owner);

        object ownerToRelease = shouldReleaseReservation ? _pendingExecution.owner : null;

        if (_pendingExecution != null)
        {
            _lastCompletedExecutionId = _pendingExecution.executionId;
            _lastCompletedExecutionSucceeded = success;
            _lastCompletedExecutionHadDeferredCleanup = hasDeferredCleanup;
        }

        _pendingExecution = null;
        LastExecutionSucceeded = success;
        skillUserProxy?.ClearAimOverrides();
        aimTargetDriver?.ClearOverride();

        if (!success)
            RecoverVisibleStateAfterInterruptedExecution();

        if (_deferredCleanup == null)
            RestoreTemporaryAutonomy();

        if (ownerToRelease != null)
            ReleaseReservation(ownerToRelease);
    }

    void StartChainVisualLifecycle(bool hideOnAnimationComplete)
    {
        if (actorFader == null || !gameObject.activeInHierarchy)
            return;

        actorFader.BeginAnimationLifecycle(hideOnAnimationComplete);
        _visualHiddenForChainTransition = false;
    }

    void HideVisualForTeleport()
    {
        if (actorFader == null || !gameObject.activeInHierarchy)
            return;

        actorFader.SetHiddenImmediate();
        _visualHiddenForChainTransition = true;
    }

    void RevealVisualAfterTeleportIfNeeded()
    {
        if (!_visualHiddenForChainTransition)
            return;

        _visualHiddenForChainTransition = false;

        if (actorFader == null || !gameObject.activeInHierarchy)
            return;

        actorFader.BeginAnimationLifecycle(hideOnAnimationComplete: false);
    }

    void RecoverVisibleStateAfterInterruptedExecution()
    {
        _visualHiddenForChainTransition = false;

        if (actorFader == null || !gameObject.activeInHierarchy)
            return;

        actorFader.BeginAnimationLifecycle(hideOnAnimationComplete: false);
    }

    static bool ShouldAutoHideNearAttackEnd(ChainActorExitMode exitMode)
    {
        switch (exitMode)
        {
            case ChainActorExitMode.ReturnToRecordedOrigin:
            case ChainActorExitMode.ReturnToRecordedOriginViaUtility:
            case ChainActorExitMode.ReturnToRecordedOriginThenWarpIn:
            case ChainActorExitMode.ReturnToRecordedOriginThenWarpInOnSequenceEnd:
            case ChainActorExitMode.FadeOutAndDeactivate:
                return true;

            default:
                return false;
        }
    }

    void SetExecutionPhase(PendingSequenceExecution execution, SequenceExecutionPhase phase)
    {
        if (execution == null)
            return;

        execution.phase = phase;
        execution.phaseStartedAt = Time.time;
    }

    bool HasPhaseTimedOut(PendingSequenceExecution execution, float timeoutSeconds)
    {
        if (execution == null)
            return false;

        float timeout = Mathf.Max(0.1f, timeoutSeconds);
        return execution.phaseStartedAt > 0f && Time.time - execution.phaseStartedAt >= timeout;
    }

    int ResolveActorDefaultSkillLevel(ChainAttackStepDef step)
    {
        if (useRuntimeChainSkillOverride && runtimeChainSkill != null && overrideRuntimeChainSkillLevel)
            return Mathf.Max(1, runtimeChainSkillLevel);

        return step != null ? step.ClampedSkillLevel : 1;
    }

    ISkillUser ResolveDirectSkillUser()
    {
        if (directSkillUserSource is ISkillUser typedSource)
        {
            _directSkillUser = typedSource;
            return _directSkillUser;
        }

        if (_directSkillUser != null)
            return _directSkillUser;

        if (actorContext == null)
            actorContext = GetComponent<CharacteContext>();

        if (actorContext != null && actorContext.EnegySystem != null)
        {
            _directSkillUser = actorContext.EnegySystem;
            return _directSkillUser;
        }

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == this || behaviour == skillUserProxy)
                continue;

            if (behaviour is ISkillUser skillUser)
            {
                _directSkillUser = skillUser;
                return _directSkillUser;
            }
        }

        return null;
    }

    void ApplyChainAimTargetOverride(Transform lockedTarget)
    {
        if (aimTargetDriver == null)
            return;

        if (lockedTarget != null)
            aimTargetDriver.SetOverrideTarget(lockedTarget, preferChainAttackPoint: true);
        else
            aimTargetDriver.ClearOverride();
    }

    string DescribeSkillUser(ISkillUser skillUser)
    {
        if (skillUser == null)
            return "null";

        if (skillUser is Component component)
            return $"{component.GetType().Name}:{component.name}";

        return skillUser.GetType().Name;
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

        HideVisualForTeleport();
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

    void SyncAgentToTransform()
    {
        if (agent == null || !agent.enabled)
            return;

        Vector3 syncPosition = transform.position;

        if (agent.isOnNavMesh)
        {
            agent.Warp(syncPosition);
            agent.nextPosition = syncPosition;
            return;
        }

        if (NavMesh.SamplePosition(syncPosition, out NavMeshHit navHit, 1f, NavMesh.AllAreas))
        {
            syncPosition = navHit.position;
            transform.position = navHit.position;
            agent.Warp(navHit.position);
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

    int NextExecutionId()
    {
        if (_nextExecutionId == int.MaxValue)
            _nextExecutionId = 1;

        return _nextExecutionId++;
    }

    void Log(string message)
    {
        if (!logSequenceExecution)
            return;

        Debug.Log($"[FieldAllyMember:{name}] {message}", this);
    }
}
