using System;
using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime;

[DefaultExecutionOrder(110)]
[DisallowMultipleComponent]
public sealed class CharacterKnockbackMotor : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private StateHub stateHub;
    [SerializeField] private HealthSystem healthSystem;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private CapsuleCollider capsuleCollider;
    [SerializeField] private CharacterAnimBrain animBrain;
    private CharacterAnimDriver animDriver;
    [SerializeField] private FieldAllyMember fieldAllyMember;
    [SerializeField] private BehaviorTree behaviorTree;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private QueryTriggerInteraction queryTriggers = QueryTriggerInteraction.Ignore;
    [SerializeField, Min(0f)] private float collisionPadding = 0.02f;
    [SerializeField, Min(0f)] private float replaceDistanceThreshold = 0.05f;
    [SerializeField, Min(0.05f)] private float navMeshResyncDistance = 1f;

    [Header("Reaction")]
    [SerializeField, Min(0f)] private float rootRecoveryDuration = 0.1f;
    [SerializeField, Min(0f)] private float miniStunRecoveryDuration = 0.1f;
    [SerializeField, Min(0f)] private float stunRecoveryDuration = 0.1f;

    private Vector3 _targetDisplacement;
    private Vector3 _appliedDisplacement;
    private float _elapsedTime;
    private float _curveProgress;
    private KnockbackData _activeKnockback;
    private ImpactReactionKind _activeReaction;
    private float _reactionTimeRemaining;
    private bool _agentOverrideActive;
    private bool _resumeAgentStopped;
    private bool _resumeAgentUpdatePosition;
    private bool _resumeAgentUpdateRotation;
    private bool _resumeAgentHadPath;
    private Vector3 _resumeAgentDestination;
    private bool _behaviorTreeWasEnabled;
    private bool _behaviorTreeSuspended;
    private bool _restoreAgentToDefaultAutonomy;
    private bool _clearReactionOnKnockbackEnd;

    Vector3 RemainingDisplacement => _targetDisplacement - _appliedDisplacement;

    public bool IsActive =>
        _activeKnockback.IsValid &&
        _elapsedTime < _activeKnockback.Duration &&
        RemainingDisplacement.sqrMagnitude > 0.0001f;

    public KnockbackData ActiveKnockback => _activeKnockback;
    public event Action<KnockbackData> KnockbackStarted;

    void Awake()
    {
        ResolveRefs();
    }

    void OnEnable()
    {
        ResolveRefs();

        if (healthSystem != null)
        {
            healthSystem.CharacterDown += OnCharacterDown;
            healthSystem.CharacterDead += OnCharacterDead;
        }
    }

    void OnDisable()
    {
        if (healthSystem != null)
        {
            healthSystem.CharacterDown -= OnCharacterDown;
            healthSystem.CharacterDead -= OnCharacterDead;
        }

        StopKnockback(preserveMoveState: false, clearReactionState: true);
    }

    void LateUpdate()
    {
        Tick(Time.deltaTime);
        TickReaction(Time.deltaTime);
    }

    public bool ApplyKnockback(KnockbackData knockback)
    {
        ResolveRefs();

        if (!CanApply(knockback))
            return false;

        if (IsActive && !ShouldReplace(knockback))
            return false;

        CancelReloadForKnockback();

        if (knockback.InterruptActions)
            InterruptGameplayActions();

        BeginKnockback(knockback);
        BeginReaction(knockback);
        return true;
    }

    public bool ApplyKnockback(KnockbackData knockback, bool forceReplace)
    {
        if (!forceReplace) return ApplyKnockback(knockback);

        ResolveRefs();

        if (!CanApply(knockback))
            return false;

        CancelReloadForKnockback();

        if (knockback.InterruptActions)
            InterruptGameplayActions();

        BeginKnockback(knockback);
        BeginReaction(knockback);
        return true;
    }

    public void StopKnockback(bool preserveMoveState = true, bool clearReactionState = false)
    {
        _targetDisplacement = Vector3.zero;
        _appliedDisplacement = Vector3.zero;
        _elapsedTime = 0f;
        _curveProgress = 0f;
        _activeKnockback = default(KnockbackData);

        RestoreAgentOverride(preserveMoveState);

        if (preserveMoveState)
            RestoreMoveState();

        RestoreBehaviorTreeOverride(preserveMoveState);

        animDriver?.StopKnockbackPlayback();

        if (clearReactionState || _clearReactionOnKnockbackEnd)
            ClearReactionState();
    }

    void ResolveRefs()
    {
        if (!ctx)
        {
            TryGetComponent(out ctx);
            if (!ctx)
                ctx = GetComponentInParent<CharacteContext>();
        }

        ctx?.ResolveReferences();

        Transform actorRoot = ctx ? ctx.transform : transform.root;

        if (!stateHub)
            stateHub = ctx != null ? ctx.stateHub : null;
        if (!stateHub)
            stateHub = ResolveActorComponent<StateHub>(actorRoot);
        if (!healthSystem)
            healthSystem = ctx != null ? ctx.HealthSystem : null;
        if (!healthSystem)
            healthSystem = ResolveActorComponent<HealthSystem>(actorRoot);
        if (!characterController)
            characterController = ctx != null ? ctx.cc : null;
        if (!characterController)
            characterController = ResolveActorComponent<CharacterController>(actorRoot);
        if (!navMeshAgent)
            navMeshAgent = ResolveActorComponent<NavMeshAgent>(actorRoot);
        if (!capsuleCollider)
            capsuleCollider = ResolveActorComponent<CapsuleCollider>(actorRoot);
        if (!animBrain)
            animBrain = ctx != null ? ctx.AnimBrain : null;
        if (!animBrain)
            animBrain = ResolveActorComponent<CharacterAnimBrain>(actorRoot);
        if (!animDriver)
            animDriver = ctx != null ? ctx.AnimDriver : null;
        if (!animDriver)
            animDriver = ResolveActorComponent<CharacterAnimDriver>(actorRoot);
        if (!fieldAllyMember)
            fieldAllyMember = ResolveActorComponent<FieldAllyMember>(actorRoot);
        if (!behaviorTree)
            behaviorTree = ResolveActorComponent<BehaviorTree>(actorRoot);

        if (ctx != null)
        {
            if (ctx.stateHub == null)
                ctx.stateHub = stateHub;
            if (ctx.HealthSystem == null)
                ctx.HealthSystem = healthSystem;
            if (ctx.cc == null)
                ctx.cc = characterController;
            if (ctx.AnimBrain == null)
                ctx.AnimBrain = animBrain;
            if (ctx.KnockbackMotor == null)
                ctx.KnockbackMotor = this;
        }
    }

    T ResolveActorComponent<T>(Transform actorRoot) where T : Component
    {
        if (actorRoot != null && actorRoot.TryGetComponent(out T rootComponent))
            return rootComponent;

        if (TryGetComponent(out T localComponent))
            return localComponent;

        T parentComponent = GetComponentInParent<T>();
        if (parentComponent != null)
            return parentComponent;

        return actorRoot != null
            ? actorRoot.GetComponentInChildren<T>(true)
            : GetComponentInChildren<T>(true);
    }

    bool CanApply(KnockbackData knockback)
    {
        if (!knockback.IsValid)
            return false;

        if (IsBlockedByChainExecution())
            return false;

        if (healthSystem != null && !healthSystem.IsAlive)
            return false;

        if (stateHub == null || stateHub.LifeSM == null)
            return true;

        return stateHub.LifeSM.CurrentId == LifeStateId.Alive;
    }

    bool IsBlockedByChainExecution()
    {
        if (fieldAllyMember != null &&
            (fieldAllyMember.HasActiveSequenceExecution ||
             fieldAllyMember.HasDeferredSequenceCleanup))
        {
            return true;
        }

        return animBrain != null && animBrain.IsChainPlaybackActive;
    }

    bool ShouldReplace(KnockbackData next)
    {
        if (!IsActive)
            return true;

        if (GetReactionPriority(next.Reaction) > GetReactionPriority(_activeKnockback.Reaction))
            return true;

        float remainingDistance = RemainingDisplacement.magnitude;
        return next.Distance + replaceDistanceThreshold >= remainingDistance;
    }

    static int GetReactionPriority(ImpactReactionKind reaction)
    {
        return reaction switch
        {
            ImpactReactionKind.Stun => 30,
            ImpactReactionKind.MiniStun => 20,
            ImpactReactionKind.Root => 10,
            _ => 0,
        };
    }

    void BeginReaction(KnockbackData knockback)
    {
        float totalReactionDuration = knockback.Duration + GetReactionRecoveryDuration(knockback.Reaction);
        if (knockback.Reaction == ImpactReactionKind.None || totalReactionDuration <= 0f)
            return;

        if (!ShouldReplaceReaction(knockback.Reaction, totalReactionDuration))
            return;

        _activeReaction = knockback.Reaction;
        _reactionTimeRemaining = totalReactionDuration;
        _clearReactionOnKnockbackEnd = false;
        ApplyReactionState();
    }

    bool ShouldReplaceReaction(ImpactReactionKind nextReaction, float nextDuration)
    {
        if (_activeReaction == ImpactReactionKind.None || _reactionTimeRemaining <= 0f)
            return true;

        int nextPriority = GetReactionPriority(nextReaction);
        int activePriority = GetReactionPriority(_activeReaction);
        if (nextPriority > activePriority)
            return true;
        if (nextPriority < activePriority)
            return false;

        return nextDuration + 0.001f >= _reactionTimeRemaining;
    }

    float GetReactionRecoveryDuration(ImpactReactionKind reaction)
    {
        return reaction switch
        {
            ImpactReactionKind.Root => rootRecoveryDuration,
            ImpactReactionKind.MiniStun => miniStunRecoveryDuration,
            ImpactReactionKind.Stun => stunRecoveryDuration,
            _ => 0f,
        };
    }

    static ControlBlockFlags GetReactionControlBlocks(ImpactReactionKind reaction)
    {
        return reaction switch
        {
            ImpactReactionKind.Root => ControlBlockFlags.Move,
            ImpactReactionKind.MiniStun => ControlBlockFlags.Move | ControlBlockFlags.Shoot | ControlBlockFlags.Skill,
            ImpactReactionKind.Stun => ControlBlockFlags.Move | ControlBlockFlags.Shoot | ControlBlockFlags.Skill,
            _ => ControlBlockFlags.None,
        };
    }

    void ApplyReactionState()
    {
        ControlBlockFlags blocks = GetReactionControlBlocks(_activeReaction);
        bool stunned = _activeReaction == ImpactReactionKind.Stun;
        stateHub?.SetExternalControlState(blocks, stunned);
        animDriver?.SetExternalStatusLocomotion(_activeReaction);
    }

    void TickReaction(float dt)
    {
        if (_reactionTimeRemaining <= 0f || dt <= 0f)
            return;

        _reactionTimeRemaining = Mathf.Max(0f, _reactionTimeRemaining - dt);
        if (_reactionTimeRemaining > 0f)
            return;

        ClearReactionState();
    }

    void ClearReactionState()
    {
        _activeReaction = ImpactReactionKind.None;
        _reactionTimeRemaining = 0f;
        _clearReactionOnKnockbackEnd = false;
        stateHub?.SetExternalControlState(ControlBlockFlags.None, false);
        animDriver?.SetExternalStatusLocomotion(ImpactReactionKind.None);
    }

    void BeginKnockback(KnockbackData knockback)
    {
        _activeKnockback = knockback;
        _targetDisplacement = knockback.Direction * knockback.Distance;
        _appliedDisplacement = Vector3.zero;
        _elapsedTime = 0f;
        _curveProgress = 0f;

        EnableAgentOverride();
        FaceTowardKnockbackPoint(knockback);
        animDriver?.PlayKnockback(knockback);

        // This motor stays planar-only. Vertical launch is handed to the component that owns Y.
        if (knockback.VerticalImpulse > 0f && ctx != null && ctx.VerticalMotor != null)
            ctx.VerticalMotor.Launch(knockback.VerticalImpulse);

        if (stateHub != null && stateHub.MoveSM != null)
            stateHub.MoveSM.TryChange(MoveStateId.Knockback);

        KnockbackStarted?.Invoke(knockback);
    }

    void FaceTowardKnockbackPoint(KnockbackData knockback)
    {
        Transform facingTransform = ResolveFacingTransform();
        Vector3 lookDirection = knockback.HitPoint - facingTransform.position;
        lookDirection = Vector3.ProjectOnPlane(lookDirection, Vector3.up);

        if (lookDirection.sqrMagnitude <= 0.0001f)
            lookDirection = Vector3.ProjectOnPlane(-knockback.Direction, Vector3.up);

        if (lookDirection.sqrMagnitude <= 0.0001f)
            return;

        facingTransform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    Transform ResolveFacingTransform()
    {
        if (ctx != null)
            return ctx.transform;

        if (characterController != null)
            return characterController.transform;

        if (navMeshAgent != null)
            return navMeshAgent.transform;

        return transform;
    }

    void Tick(float dt)
    {
        if (!IsActive || dt <= 0f)
            return;

        float duration = Mathf.Max(0.0001f, _activeKnockback.Duration);
        float stepTime = Mathf.Min(dt, duration - _elapsedTime);
        if (stepTime <= 0f)
        {
            StopKnockback();
            return;
        }

        _elapsedTime = Mathf.Min(duration, _elapsedTime + stepTime);

        float normalizedTime = Mathf.Clamp01(_elapsedTime / duration);
        _curveProgress = Mathf.Max(_curveProgress, _activeKnockback.EvaluateProgress(normalizedTime));

        Vector3 desiredDisplacement = _targetDisplacement * _curveProgress;
        Vector3 desiredDelta = desiredDisplacement - _appliedDisplacement;
        Vector3 appliedDelta = ApplyDelta(desiredDelta);

        _appliedDisplacement += appliedDelta;

        bool blockedByObstacle =
            desiredDelta.sqrMagnitude > 0.0001f &&
            appliedDelta.sqrMagnitude + 0.000001f < desiredDelta.sqrMagnitude * 0.25f;

        if (_elapsedTime >= duration ||
            RemainingDisplacement.sqrMagnitude <= 0.0001f ||
            blockedByObstacle)
        {
            StopKnockback();
        }
    }

    Vector3 ApplyDelta(Vector3 desiredDelta)
    {
        if (desiredDelta.sqrMagnitude <= 0.0000001f)
            return Vector3.zero;

        Vector3 before = transform.position;

        if (characterController != null && characterController.enabled)
        {
            characterController.Move(desiredDelta);
        }
        else
        {
            Vector3 resolvedDelta = ResolveManualDelta(desiredDelta);
            transform.position += resolvedDelta;
        }

        if (navMeshAgent != null && navMeshAgent.enabled)
            navMeshAgent.nextPosition = transform.position;

        return transform.position - before;
    }

    Vector3 ResolveManualDelta(Vector3 desiredDelta)
    {
        float desiredDistance = desiredDelta.magnitude;
        if (desiredDistance <= 0.0001f)
            return Vector3.zero;

        Vector3 direction = desiredDelta / desiredDistance;
        if (!TryBuildCastCapsule(out var p1, out var p2, out float radius))
            return desiredDelta;

        float allowedDistance = desiredDistance;
        var hits = Physics.CapsuleCastAll(
            p1,
            p2,
            radius,
            direction,
            desiredDistance + collisionPadding,
            collisionMask,
            queryTriggers);

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit.collider == null)
                continue;

            if (hit.transform.root == transform.root)
                continue;

            allowedDistance = Mathf.Min(
                allowedDistance,
                Mathf.Max(0f, hit.distance - collisionPadding));
        }

        return direction * allowedDistance;
    }

    bool TryBuildCastCapsule(out Vector3 p1, out Vector3 p2, out float radius)
    {
        if (capsuleCollider != null && capsuleCollider.enabled)
        {
            Vector3 lossyScale = transform.lossyScale;
            float scaleY = Mathf.Abs(lossyScale.y);
            float scaleXZ = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z));

            radius = Mathf.Max(0.001f, capsuleCollider.radius * scaleXZ);
            float height = Mathf.Max(capsuleCollider.height * scaleY, radius * 2f + 0.001f);
            Vector3 center = transform.TransformPoint(capsuleCollider.center);
            Vector3 up = transform.up;
            float half = Mathf.Max(0f, height * 0.5f - radius);

            p1 = center + up * half;
            p2 = center - up * half;
            return true;
        }

        if (navMeshAgent != null && navMeshAgent.enabled)
        {
            radius = Mathf.Max(0.001f, navMeshAgent.radius);
            float height = Mathf.Max(navMeshAgent.height, radius * 2f + 0.001f);
            Vector3 center = transform.position + Vector3.up * (height * 0.5f);
            float half = Mathf.Max(0f, height * 0.5f - radius);

            p1 = center + Vector3.up * half;
            p2 = center - Vector3.up * half;
            return true;
        }

        p1 = transform.position;
        p2 = transform.position;
        radius = 0f;
        return false;
    }

    void InterruptGameplayActions()
    {
        if (ctx == null)
            return;

        ctx.DashSystem?.CancelDash();

        var weaponSystem = ctx.WeaponSystem;
        if (!weaponSystem)
            weaponSystem = ctx.GetComponentInChildren<WeaponSystem>(true);

        if (weaponSystem != null)
        {
            weaponSystem.SetFiring(false);
            if (weaponSystem.IsReloading)
                weaponSystem.CancelReload();
        }

        if (stateHub != null)
        {
            stateHub.SetDesiredFireHeld(false);
            stateHub.SetFireHeld(false);
        }

        var meleeController = ctx.MeleeController;
        if (!meleeController)
            meleeController = GetComponent<MeleeController>();

        meleeController?.InterruptMelee();
        animDriver?.InterruptActivePlaybackForExternalControlLoss();
        SuspendBehaviorTreeOverride();

        if (navMeshAgent != null && navMeshAgent.enabled)
            _restoreAgentToDefaultAutonomy = true;
    }

    void CancelReloadForKnockback()
    {
        var weaponSystem = ctx != null ? ctx.WeaponSystem : null;
        if (!weaponSystem)
            weaponSystem = ctx != null ? ctx.GetComponentInChildren<WeaponSystem>(true) : GetComponent<WeaponSystem>();

        if (weaponSystem != null && weaponSystem.IsReloading)
            weaponSystem.CancelReload();
    }

    void EnableAgentOverride()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled)
            return;

        if (_agentOverrideActive)
        {
            navMeshAgent.nextPosition = transform.position;
            return;
        }

        _resumeAgentStopped = navMeshAgent.isStopped;
        _resumeAgentUpdatePosition = navMeshAgent.updatePosition;
        _resumeAgentUpdateRotation = navMeshAgent.updateRotation;
        _resumeAgentHadPath = navMeshAgent.hasPath || navMeshAgent.pathPending;
        _resumeAgentDestination = navMeshAgent.isOnNavMesh ? navMeshAgent.destination : transform.position;
        navMeshAgent.isStopped = true;
        navMeshAgent.updatePosition = false;
        navMeshAgent.updateRotation = false;
        navMeshAgent.nextPosition = transform.position;
        _agentOverrideActive = true;
    }

    void RestoreAgentOverride(bool resumeAutonomy)
    {
        if (!_agentOverrideActive)
            return;

        _agentOverrideActive = false;

        if (navMeshAgent == null || !navMeshAgent.enabled)
            return;

        Vector3 restorePosition = transform.position;
        bool sampledRestorePosition = false;

        if (navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.nextPosition = restorePosition;
        }
        else if (TrySampleNavMeshPosition(restorePosition, out Vector3 sampledPosition))
        {
            sampledRestorePosition = true;
            restorePosition = sampledPosition;
            transform.position = sampledPosition;
            navMeshAgent.Warp(sampledPosition);
            navMeshAgent.nextPosition = sampledPosition;
        }

        bool restoreDefaults = resumeAutonomy && _restoreAgentToDefaultAutonomy;

        navMeshAgent.updatePosition = restoreDefaults ? true : _resumeAgentUpdatePosition;
        navMeshAgent.updateRotation = restoreDefaults ? true : _resumeAgentUpdateRotation;
        navMeshAgent.isStopped = restoreDefaults ? false : _resumeAgentStopped;

        bool behaviorTreeOwnsNavigation =
            _behaviorTreeSuspended ||
            (behaviorTree != null && behaviorTree.enabled);

        if (!restoreDefaults &&
            !sampledRestorePosition &&
            !behaviorTreeOwnsNavigation &&
            _resumeAgentHadPath &&
            !_resumeAgentStopped &&
            navMeshAgent.isOnNavMesh &&
            !navMeshAgent.pathPending &&
            !navMeshAgent.hasPath)
        {
            // Let the active AI controller reacquire a route instead of reviving a stale cached destination.
            navMeshAgent.SetDestination(_resumeAgentDestination);
        }

        _restoreAgentToDefaultAutonomy = false;
    }

    void SuspendBehaviorTreeOverride()
    {
        if (_behaviorTreeSuspended || behaviorTree == null)
            return;

        _behaviorTreeWasEnabled = behaviorTree.enabled;
        if (!_behaviorTreeWasEnabled)
            return;

        behaviorTree.enabled = false;
        _behaviorTreeSuspended = true;
    }

    void RestoreBehaviorTreeOverride(bool resumeAutonomy)
    {
        if (!_behaviorTreeSuspended)
            return;

        bool shouldResume = resumeAutonomy && _behaviorTreeWasEnabled;
        if (shouldResume && behaviorTree != null)
            behaviorTree.enabled = true;

        _behaviorTreeSuspended = false;
        _behaviorTreeWasEnabled = false;
    }

    bool TrySampleNavMeshPosition(Vector3 worldPosition, out Vector3 sampledPosition)
    {
        sampledPosition = worldPosition;

        if (NavMesh.SamplePosition(worldPosition, out NavMeshHit navHit, navMeshResyncDistance, NavMesh.AllAreas))
        {
            sampledPosition = navHit.position;
            return true;
        }

        return false;
    }

    void RestoreMoveState()
    {
        if (stateHub == null || stateHub.MoveSM == null)
            return;

        if (stateHub.MoveSM.CurrentId != MoveStateId.Knockback)
            return;

        MoveStateId nextState = MoveStateId.Stand;

        if (ctx != null && ctx.DashSystem != null && ctx.DashSystem.IsDashing)
            nextState = MoveStateId.Dash;
        else if (ctx != null && ctx.ShouldBeInMoveState())
            nextState = MoveStateId.Moveing;

        stateHub.MoveSM.TryChange(nextState);
    }

    void OnCharacterDown()
    {
        _clearReactionOnKnockbackEnd = true;
        if (!IsActive)
            ClearReactionState();
    }

    void OnCharacterDead()
    {
        StopKnockback(preserveMoveState: false, clearReactionState: true);
    }
}
