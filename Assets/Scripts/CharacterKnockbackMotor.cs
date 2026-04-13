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
    [SerializeField] private FieldAllyMember fieldAllyMember;
    [SerializeField] private BehaviorTree behaviorTree;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private QueryTriggerInteraction queryTriggers = QueryTriggerInteraction.Ignore;
    [SerializeField, Min(0f)] private float collisionPadding = 0.02f;
    [SerializeField, Min(0f)] private float replaceDistanceThreshold = 0.05f;
    [SerializeField, Min(0.05f)] private float navMeshResyncDistance = 1f;

    private Vector3 _targetDisplacement;
    private Vector3 _appliedDisplacement;
    private float _elapsedTime;
    private float _curveProgress;
    private KnockbackData _activeKnockback;
    private bool _agentOverrideActive;
    private bool _resumeAgentStopped;
    private bool _resumeAgentUpdatePosition;
    private bool _resumeAgentUpdateRotation;
    private bool _resumeAgentHadPath;
    private Vector3 _resumeAgentDestination;
    private bool _behaviorTreeWasEnabled;
    private bool _behaviorTreeSuspended;
    private bool _restoreAgentToDefaultAutonomy;

    Vector3 RemainingDisplacement => _targetDisplacement - _appliedDisplacement;

    public bool IsActive =>
        _activeKnockback.IsValid &&
        _elapsedTime < _activeKnockback.Duration &&
        RemainingDisplacement.sqrMagnitude > 0.0001f;

    public KnockbackData ActiveKnockback => _activeKnockback;

    void Awake()
    {
        ResolveRefs();
    }

    void OnEnable()
    {
        ResolveRefs();

        if (healthSystem != null)
        {
            healthSystem.CharacterDown += OnCharacterDisabled;
            healthSystem.CharacterDead += OnCharacterDisabled;
        }
    }

    void OnDisable()
    {
        if (healthSystem != null)
        {
            healthSystem.CharacterDown -= OnCharacterDisabled;
            healthSystem.CharacterDead -= OnCharacterDisabled;
        }

        StopKnockback(preserveMoveState: false);
    }

    void LateUpdate()
    {
        Tick(Time.deltaTime);
    }

    public bool ApplyKnockback(KnockbackData knockback)
    {
        ResolveRefs();

        if (!CanApply(knockback))
            return false;

        if (IsActive && !ShouldReplace(knockback))
            return false;

        if (knockback.InterruptActions)
            InterruptGameplayActions();

        BeginKnockback(knockback);
        return true;
    }

    public void StopKnockback(bool preserveMoveState = true)
    {
        _targetDisplacement = Vector3.zero;
        _appliedDisplacement = Vector3.zero;
        _elapsedTime = 0f;
        _curveProgress = 0f;
        _activeKnockback = default(KnockbackData);

        RestoreAgentOverride(preserveMoveState);
        RestoreBehaviorTreeOverride(preserveMoveState);

        if (preserveMoveState)
            RestoreMoveState();

        animBrain?.StopKnockbackPlayback();
    }

    void ResolveRefs()
    {
        if (!ctx)
            TryGetComponent(out ctx);
        if (!stateHub)
            TryGetComponent(out stateHub);
        if (!healthSystem)
            TryGetComponent(out healthSystem);
        if (!characterController)
            TryGetComponent(out characterController);
        if (!navMeshAgent)
            TryGetComponent(out navMeshAgent);
        if (!capsuleCollider)
            TryGetComponent(out capsuleCollider);
        if (!animBrain)
            TryGetComponent(out animBrain);
        if (!fieldAllyMember)
            TryGetComponent(out fieldAllyMember);
        if (!behaviorTree)
            TryGetComponent(out behaviorTree);

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

        return next.Distance >= RemainingDisplacement.magnitude + replaceDistanceThreshold;
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

    void BeginKnockback(KnockbackData knockback)
    {
        _activeKnockback = knockback;
        _targetDisplacement = knockback.Direction * knockback.Distance;
        _appliedDisplacement = Vector3.zero;
        _elapsedTime = 0f;
        _curveProgress = 0f;

        EnableAgentOverride();
        FaceTowardKnockbackPoint(knockback);
        animBrain?.PlayKnockback(knockback);

        if (stateHub != null && stateHub.MoveSM != null)
            stateHub.MoveSM.TryChange(MoveStateId.Knockback);
    }

    void FaceTowardKnockbackPoint(KnockbackData knockback)
    {
        Vector3 lookDirection = knockback.HitPoint - transform.position;
        lookDirection = Vector3.ProjectOnPlane(lookDirection, Vector3.up);

        if (lookDirection.sqrMagnitude <= 0.0001f)
            lookDirection = Vector3.ProjectOnPlane(-knockback.Direction, Vector3.up);

        if (lookDirection.sqrMagnitude <= 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
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
            weaponSystem = GetComponent<WeaponSystem>();

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
        animBrain?.InterruptActivePlaybackForExternalControlLoss();
        SuspendBehaviorTreeOverride();

        if (navMeshAgent != null && navMeshAgent.enabled)
            _restoreAgentToDefaultAutonomy = true;
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

        if (navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.nextPosition = restorePosition;
        }
        else if (TrySampleNavMeshPosition(restorePosition, out Vector3 sampledPosition))
        {
            restorePosition = sampledPosition;
            transform.position = sampledPosition;
            navMeshAgent.Warp(sampledPosition);
            navMeshAgent.nextPosition = sampledPosition;
        }

        bool restoreDefaults = resumeAutonomy && _restoreAgentToDefaultAutonomy;

        navMeshAgent.updatePosition = restoreDefaults ? true : _resumeAgentUpdatePosition;
        navMeshAgent.updateRotation = restoreDefaults ? true : _resumeAgentUpdateRotation;
        navMeshAgent.isStopped = restoreDefaults ? false : _resumeAgentStopped;

        if (!restoreDefaults &&
            _resumeAgentHadPath &&
            !_resumeAgentStopped &&
            navMeshAgent.isOnNavMesh &&
            !navMeshAgent.pathPending &&
            !navMeshAgent.hasPath)
        {
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

    void OnCharacterDisabled()
    {
        StopKnockback(preserveMoveState: false);
    }
}
