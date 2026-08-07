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
    [SerializeField] private Transform actorRoot;
    [SerializeField] private CharacteContext actorContext;
    [SerializeField] private ChainSkillUserProxy skillUserProxy;
    [SerializeField] private ASPHelperDitherFader actorFader;
    [FoldoutGroup("Chain Attack", Expanded = false), LabelText("Teleport Probe Collider")]
    [SerializeField] private Collider chainTeleportProbeCollider;
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
    CharacterAnimBrain _subscribedAnimBrain;

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
            if (actorContext == null && actorRoot != null)
                actorContext = actorRoot.GetComponent<CharacteContext>();

            if (actorContext == null)
                actorContext = GetComponent<CharacteContext>();

            if (actorContext == null)
                actorContext = GetComponentInParent<CharacteContext>();

            if (actorContext == null)
                actorContext = GetComponentInChildren<CharacteContext>(true);

            if (actorRoot == null && actorContext != null)
                actorRoot = actorContext.transform;

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

            context.ResolveReferences();
            return context.SkillManager;
        }
    }
    public SkillGemDefinition DefaultChainSkill =>
        SkillManager != null && SkillManager.TryGetChainAttackSkillDefinition(out SkillGemDefinition configuredSkill, out _)
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
            context?.ResolveReferences();
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

    public void ConfigureRuntime(ChainActorRole role, FieldAllyManager runtimeManager)
    {
        if (role == ChainActorRole.None)
            throw new ArgumentOutOfRangeException(nameof(role));
        if (runtimeManager == null)
            throw new ArgumentNullException(nameof(runtimeManager));

        if (isActiveAndEnabled)
            manager?.Unregister(this);

        actorRole = role;
        manager = runtimeManager;

        if (isActiveAndEnabled)
            manager.Register(this);
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

    public bool TryStartSequenceStep(ChainAttackStepDef step, Transform lockedTarget, Transform lockedTargetAnchor = null)
    {
        EnsureModules();
        return _sequenceRunner.TryStartSequenceStep(step, lockedTarget, lockedTargetAnchor);
    }

    public void SetRuntimeChainSkillOverride(SkillGemDefinition skill)
    {
        runtimeChainSkill = skill;
        useRuntimeChainSkillOverride = skill != null;
    }

    public void ClearRuntimeChainSkillOverride()
    {
        useRuntimeChainSkillOverride = false;
        runtimeChainSkill = null;
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
        CharacteContext context = ActorContext;
        context?.ResolveReferences();

        if (actorRoot == null && context != null)
            actorRoot = context.transform;

        RefreshCollisionReferences();
        Collider contextPositionCollider = ResolveCharacterPositionCollider(context);
        if (contextPositionCollider != null)
            chainTeleportProbeCollider = contextPositionCollider;

        if (manager == null)
            manager = FindFirstObjectByType<FieldAllyManager>();

        CharacterAnimBrain nextAnimBrain = context != null ? context.AnimBrain : null;
        SubscribeToAnimBrain(nextAnimBrain);

        if (skillUserProxy == null)
            skillUserProxy = GetComponent<ChainSkillUserProxy>();

        if (skillUserProxy == null)
            skillUserProxy = gameObject.AddComponent<ChainSkillUserProxy>();

        if (directSkillUserSource is not ISkillUser)
            directSkillUserSource = null;

        if (actorFader == null)
        {
            if (context != null)
                actorFader = context.GetComponentInChildren<ASPHelperDitherFader>(true);
        }

        _skillCastBridge.InvalidateDirectSkillUserCache();
        _skillCastBridge.EnsureInitialized();
    }

    internal void RefreshCollisionReferences()
    {
        CharacteContext context = ActorContext;
        context?.ResolveReferences();
    }

    Collider ResolveChainTeleportProbeCollider()
    {
        Collider contextPositionCollider = ResolveCharacterPositionCollider(ActorContext);
        if (contextPositionCollider != null)
            return contextPositionCollider;

        if (chainTeleportProbeCollider != null)
            return chainTeleportProbeCollider;

        Transform root = ResolveActorTransform();
        if (root != null)
        {
            Collider rootCollider = root.GetComponent<Collider>();
            if (rootCollider != null)
                return rootCollider;
        }

        CharacteContext context = ActorContext;
        if (context != null)
        {
            Collider contextCollider = context.GetComponent<Collider>();
            if (contextCollider != null)
                return contextCollider;
        }

        return null;
    }

    static Collider ResolveCharacterPositionCollider(CharacteContext context)
    {
        if (context == null)
            return null;

        context.ResolveReferences();

        return context.ColliderRefs != null
            ? context.ColliderRefs.CharacterPositionCollider
            : null;
    }

    internal bool IsActorInKnockback()
    {
        CharacteContext context = ActorContext;
        context?.ResolveReferences();
        CharacterKnockbackMotor knockbackMotor = context != null ? context.KnockbackMotor : null;

        if (knockbackMotor != null && knockbackMotor.IsActive)
            return true;

        return context != null &&
               context.stateHub != null &&
               context.stateHub.MoveSM != null &&
               context.stateHub.MoveSM.CurrentId == MoveStateId.Knockback;
    }

    void SubscribeToAnimBrain(CharacterAnimBrain nextAnimBrain)
    {
        if (_subscribedAnimBrain != null && _sequenceRunner != null)
        {
            _subscribedAnimBrain.ChainCastMomentReached -= _sequenceRunner.OnChainCastMomentReached;
            _subscribedAnimBrain.ChainAdvanceMomentReached -= _sequenceRunner.OnChainAdvanceMomentReached;
            _subscribedAnimBrain.ChainPlaybackInterrupted -= _sequenceRunner.OnChainPlaybackInterrupted;
            _subscribedAnimBrain.ChainPlaybackCompleted -= _sequenceRunner.OnChainPlaybackCompleted;
        }

        _subscribedAnimBrain = nextAnimBrain;

        if (_subscribedAnimBrain != null && _sequenceRunner != null)
        {
            _subscribedAnimBrain.ChainCastMomentReached += _sequenceRunner.OnChainCastMomentReached;
            _subscribedAnimBrain.ChainAdvanceMomentReached += _sequenceRunner.OnChainAdvanceMomentReached;
            _subscribedAnimBrain.ChainPlaybackInterrupted += _sequenceRunner.OnChainPlaybackInterrupted;
            _subscribedAnimBrain.ChainPlaybackCompleted += _sequenceRunner.OnChainPlaybackCompleted;
        }
    }

    string BuildActorDefaultSkillSourceLabel()
    {
        if (useRuntimeChainSkillOverride && runtimeChainSkill != null)
        {
            return $"Runtime Override ({runtimeChainSkill.name})";
        }

        if (SkillManager != null &&
            SkillManager.TryGetChainAttackSkillDefinition(out SkillGemDefinition configuredSkill, out _))
        {
            return $"Skill Manager ({configuredSkill.name})";
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

        Debug.Log($"[FieldAllyMember:{ActorName}/{actorRole}:{name}] {message}", this);
    }

    internal Transform TransformRef => ResolveActorTransform();
    internal GameObject GameObjectRef => TransformRef != null ? TransformRef.gameObject : gameObject;
    internal string ActorName => TransformRef != null ? TransformRef.name : name;
    internal ChainActorRole ActorRoleValue => actorRole;
    internal BehaviorTree BehaviorTreeRef => ActorContext is AllyContext allyContext ? allyContext.BehaviorTree : null;
    internal NavMeshAgent AgentRef => ActorContext is AllyContext allyContext ? allyContext.agent : null;
    internal CharacterAnimBrain AnimBrainRef => ActorContext != null ? ActorContext.AnimBrain : null;
    internal CharacterAnimDriver AnimDriverRef => ActorContext != null ? ActorContext.AnimDriver : null;
    internal ChainSkillUserProxy SkillUserProxyRef => skillUserProxy;
    internal AIAimTargetDriver AimTargetDriverRef => ActorContext is AllyContext allyContext ? allyContext.AimTargetDriver : null;
    internal ASPHelperDitherFader ActorFaderRef => actorFader;
    internal Collider ChainTeleportProbeColliderRef => ResolveChainTeleportProbeCollider();
    internal CharacteContext ActorContextRef => ActorContext;
    internal HealthSystem ActorHealthSystemRef => ActorContext != null ? ActorContext.HealthSystem : null;
    internal AITargetInfo ActorTargetInfoRef => ActorContext != null ? ActorContext.TargetInfo : null;
    internal Rigidbody ActorRigidbodyRef => ActorContext != null ? ActorContext.rb : null;
    internal CharacterController ActorCharacterControllerRef => ActorContext != null ? ActorContext.cc : null;
    internal MonoBehaviour[] ComponentsToDisableDuringSequenceRef => componentsToDisableDuringSequence;
    internal bool AutoDisablePlayerInputAndMovement => autoDisablePlayerInputAndMovement;
    internal bool MakeAllyInvincibleDuringSequence => makeAllyInvincibleDuringSequence;
    internal bool MakeAllyUntargetableDuringSequence => makeAllyUntargetableDuringSequence;
    internal bool IgnoreCollisionDuringSequence => ignoreCollisionDuringSequence;
    internal bool UseRuntimeChainSkillOverride => useRuntimeChainSkillOverride;
    internal SkillGemDefinition RuntimeChainSkillRef => runtimeChainSkill;
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

    Transform ResolveActorTransform()
    {
        if (actorRoot != null)
            return actorRoot;

        CharacteContext context = ActorContext;
        if (context != null)
            return context.transform;

        return transform;
    }
}
