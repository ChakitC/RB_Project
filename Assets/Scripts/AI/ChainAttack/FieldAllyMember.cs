using System;
using Opsive.BehaviorDesigner.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class FieldAllyMember : MonoBehaviour
{
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
    [FoldoutGroup("Chain Attack", Expanded = false), LabelText("Attack Cast Recovery Timeout"), MinValue(0.1f)]
    [SerializeField] private float attackCastRecoveryTimeoutSeconds = 1.25f;
    [SerializeField] private bool logSequenceExecution;

    FieldAllyTransitionController _transitionController;
    FieldAllyAutonomyScope _autonomyScope;
    ChainSkillCastBridge _skillCastBridge;
    FieldAllySequenceRunner _sequenceRunner;

    public ChainActorRole ActorRole => actorRole;
    public bool IsReserved
    {
        get
        {
            EnsureModules();
            return _sequenceRunner.IsReserved;
        }
    }
    public bool HasActiveSequenceExecution
    {
        get
        {
            EnsureModules();
            return _sequenceRunner.HasActiveSequenceExecution;
        }
    }
    public bool HasDeferredSequenceCleanup
    {
        get
        {
            EnsureModules();
            return _sequenceRunner.HasDeferredSequenceCleanup;
        }
    }
    public int ActiveSequenceExecutionId
    {
        get
        {
            EnsureModules();
            return _sequenceRunner.ActiveSequenceExecutionId;
        }
    }
    public bool IsInKnockback => IsActorInKnockback();
    public bool LastExecutionSucceeded
    {
        get
        {
            EnsureModules();
            return _sequenceRunner.LastExecutionSucceeded;
        }
    }
    public CharacteContext ActorContext
    {
        get
        {
            if (actorContext == null)
                actorContext = GetComponent<CharacteContext>();

            return actorContext;
        }
    }
    public CharacterSkillManager SkillManager
    {
        get
        {
            CharacteContext context = ActorContext;
            if (context == null)
                return null;

            if (context.SkillManager == null)
                context.SkillManager = GetComponent<CharacterSkillManager>();

            return context.SkillManager;
        }
    }
    public SkillGemDefinition DefaultChainSkill =>
        SkillManager != null && SkillManager.TryGetChainAttackSkillDefinition(out SkillGemDefinition configuredSkill, out _, out _)
            ? configuredSkill
            : defaultChainSkill != null
                ? defaultChainSkill
                : ActorContext != null && ActorContext.baseStats != null
                    ? ActorContext.baseStats.chainAttackSkill
                    : null;
    public SkillGemDefinition ResolvedActorDefaultChainSkill =>
        useRuntimeChainSkillOverride && runtimeChainSkill != null
            ? runtimeChainSkill
            : DefaultChainSkill;
    public bool IsBusy
    {
        get
        {
            EnsureModules();
            return _sequenceRunner.IsBusy;
        }
    }
    public bool IsAlive
    {
        get
        {
            CharacteContext context = ActorContext;
            return context != null &&
                   context.stateHub != null &&
                   context.stateHub.IsAlive &&
                   !context.stateHub.Isdown;
        }
    }

    [ShowInInspector, ReadOnly, FoldoutGroup("Chain Attack/Runtime Prototype"), PropertyOrder(10), LabelText("Resolved Actor Default Skill")]
    SkillGemDefinition InspectorResolvedActorDefaultChainSkill => ResolvedActorDefaultChainSkill;

    [ShowInInspector, ReadOnly, FoldoutGroup("Chain Attack/Runtime Prototype"), PropertyOrder(11), LabelText("Actor Default Source")]
    string InspectorActorDefaultSkillSource => BuildActorDefaultSkillSourceLabel();

    [ShowInInspector, ReadOnly, FoldoutGroup("Chain Attack/Runtime Prototype"), PropertyOrder(12), LabelText("Last Resolved Attack Skill")]
    SkillGemDefinition InspectorLastResolvedAttackSkill
    {
        get
        {
            EnsureModules();
            return _skillCastBridge.LastResolvedAttackSkill;
        }
    }

    [ShowInInspector, ReadOnly, FoldoutGroup("Chain Attack/Runtime Prototype"), PropertyOrder(13), LabelText("Last Resolved Attack Level")]
    int InspectorLastResolvedAttackLevel
    {
        get
        {
            EnsureModules();
            return _skillCastBridge.LastResolvedAttackLevel;
        }
    }

    [ShowInInspector, ReadOnly, FoldoutGroup("Chain Attack/Debug"), PropertyOrder(20), LabelText("Chain Cast Path")]
    string InspectorChainCastPath
    {
        get
        {
            EnsureModules();
            return _skillCastBridge.ChainCastPathLabel;
        }
    }

    [ShowInInspector, ReadOnly, FoldoutGroup("Chain Attack/Debug"), PropertyOrder(21), LabelText("Resolved Direct Skill User")]
    string InspectorResolvedDirectSkillUser
    {
        get
        {
            EnsureModules();
            return _skillCastBridge.ResolvedDirectSkillUserDescription;
        }
    }

    void Awake()
    {
        EnsureModules();
        CacheReferences();
    }

    void Update()
    {
        _sequenceRunner?.Tick();
    }

    void OnEnable()
    {
        EnsureModules();
        CacheReferences();
        manager?.Register(this);
    }

    void OnDisable()
    {
        manager?.Unregister(this);
        _sequenceRunner?.ResetOnDisable();
        SubscribeToAnimBrain(null);
    }

    public bool TryReserve(object owner)
    {
        EnsureModules();
        return _sequenceRunner.TryReserve(owner);
    }

    public void ReleaseReservation(object owner)
    {
        EnsureModules();
        _sequenceRunner.ReleaseReservation(owner);
    }

    public bool IsSequenceExecutionReadyToContinue(int executionId)
    {
        EnsureModules();
        return _sequenceRunner.IsSequenceExecutionReadyToContinue(executionId);
    }

    public bool TryGetCompletedSequenceExecutionResult(int executionId, out bool success, out bool hasDeferredCleanup)
    {
        EnsureModules();
        return _sequenceRunner.TryGetCompletedSequenceExecutionResult(executionId, out success, out hasDeferredCleanup);
    }

    public bool TryStartSequenceStep(ChainAttackStepDef step, Transform lockedTarget)
    {
        EnsureModules();
        return _sequenceRunner.TryStartSequenceStep(step, lockedTarget);
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

    public void FinalizeSequenceParticipation(object owner, bool interrupted)
    {
        EnsureModules();
        _sequenceRunner.FinalizeSequenceParticipation(owner, interrupted);
    }

    public bool OwnsSequenceWork(object owner)
    {
        EnsureModules();
        return _sequenceRunner.OwnsSequenceWork(owner);
    }

    public bool TryDescribeOwnedSequenceWork(object owner, out string description)
    {
        EnsureModules();
        return _sequenceRunner.TryDescribeOwnedSequenceWork(owner, out description);
    }

    void EnsureModules()
    {
        _transitionController ??= new FieldAllyTransitionController(this);
        _autonomyScope ??= new FieldAllyAutonomyScope(this, _transitionController);
        _skillCastBridge ??= new ChainSkillCastBridge(this);
        _sequenceRunner ??= new FieldAllySequenceRunner(this, _autonomyScope, _transitionController, _skillCastBridge);
    }

    internal void CacheReferences()
    {
        EnsureModules();
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

        if (actorFader == null)
        {
            actorFader = GetComponent<ASPHelperDitherFader>();
            if (actorFader == null)
                actorFader = GetComponentInChildren<ASPHelperDitherFader>(true);
        }

        _skillCastBridge.InvalidateDirectSkillUserCache();
        _skillCastBridge.EnsureInitialized();
    }

    internal void RefreshCollisionReferences()
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

    internal bool IsActorInKnockback()
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
        if (animBrain != null && _sequenceRunner != null)
        {
            animBrain.ChainCastMomentReached -= _sequenceRunner.OnChainCastMomentReached;
            animBrain.ChainAdvanceMomentReached -= _sequenceRunner.OnChainAdvanceMomentReached;
            animBrain.ChainPlaybackInterrupted -= _sequenceRunner.OnChainPlaybackInterrupted;
            animBrain.ChainPlaybackCompleted -= _sequenceRunner.OnChainPlaybackCompleted;
        }

        animBrain = nextAnimBrain;

        if (animBrain != null && _sequenceRunner != null)
        {
            animBrain.ChainCastMomentReached += _sequenceRunner.OnChainCastMomentReached;
            animBrain.ChainAdvanceMomentReached += _sequenceRunner.OnChainAdvanceMomentReached;
            animBrain.ChainPlaybackInterrupted += _sequenceRunner.OnChainPlaybackInterrupted;
            animBrain.ChainPlaybackCompleted += _sequenceRunner.OnChainPlaybackCompleted;
        }
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

        if (SkillManager != null &&
            SkillManager.TryGetChainAttackSkillDefinition(out SkillGemDefinition configuredSkill, out int configuredLevel, out _))
        {
            return $"Skill Manager ({configuredSkill.name}, Lv {Mathf.Max(1, configuredLevel)})";
        }

        if (defaultChainSkill != null)
            return $"Component Default ({defaultChainSkill.name})";

        if (actorContext != null && actorContext.baseStats != null && actorContext.baseStats.chainAttackSkill != null)
            return $"Character Stats Default ({actorContext.baseStats.chainAttackSkill.name})";

        return "None";
    }

    internal void LogExecution(string message)
    {
        if (!logSequenceExecution)
            return;

        Debug.Log($"[FieldAllyMember:{name}] {message}", this);
    }

    internal Transform TransformRef => transform;
    internal GameObject GameObjectRef => gameObject;
    internal string ActorName => name;
    internal ChainActorRole ActorRoleValue => actorRole;
    internal BehaviorTree BehaviorTreeRef => behaviorTree;
    internal NavMeshAgent AgentRef => agent;
    internal CharacterAnimBrain AnimBrainRef => animBrain;
    internal ChainSkillUserProxy SkillUserProxyRef => skillUserProxy;
    internal AIAimTargetDriver AimTargetDriverRef => aimTargetDriver;
    internal ASPHelperDitherFader ActorFaderRef => actorFader;
    internal CharacteContext ActorContextRef => ActorContext;
    internal HealthSystem ActorHealthSystemRef => actorHealthSystem;
    internal AITargetInfo ActorTargetInfoRef => actorTargetInfo;
    internal Rigidbody ActorRigidbodyRef => actorRigidbody;
    internal CharacterController ActorCharacterControllerRef => actorCharacterController;
    internal MonoBehaviour[] ComponentsToDisableDuringSequenceRef => componentsToDisableDuringSequence;
    internal bool AutoDisablePlayerInputAndMovement => autoDisablePlayerInputAndMovement;
    internal bool MakeAllyInvincibleDuringSequence => makeAllyInvincibleDuringSequence;
    internal bool MakeAllyUntargetableDuringSequence => makeAllyUntargetableDuringSequence;
    internal bool IgnoreCollisionDuringSequence => ignoreCollisionDuringSequence;
    internal bool UseRuntimeChainSkillOverride => useRuntimeChainSkillOverride;
    internal SkillGemDefinition RuntimeChainSkillRef => runtimeChainSkill;
    internal bool OverrideRuntimeChainSkillLevel => overrideRuntimeChainSkillLevel;
    internal int RuntimeChainSkillLevel => runtimeChainSkillLevel;
    internal bool UseDirectSkillUserForChainCastDebug => useDirectSkillUserForChainCastDebug;
    internal MonoBehaviour DirectSkillUserSourceRef => directSkillUserSource;
    internal float UtilityRecoveryTimeoutSeconds => utilityRecoveryTimeoutSeconds;
    internal float AttackCastRecoveryTimeoutSeconds => attackCastRecoveryTimeoutSeconds;
    internal SkillGemDefinition ResolvedActorDefaultChainSkillRef => ResolvedActorDefaultChainSkill;
    internal bool TryGetConfiguredChainAttackRuntimeSkill(out SkillInstance runtimeSkill)
    {
        runtimeSkill = null;
        return SkillManager != null && SkillManager.TryGetChainAttackRuntimeSkill(out runtimeSkill);
    }
    internal bool IsAliveActor => IsAlive;
}
