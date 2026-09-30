using UnityEngine;
using UnityEngine.AI;

public class RootMotionNavMeshDriver : MonoBehaviour
{
    [SerializeField] private CharacterAnimBrain brain;
    [SerializeField] private bool zeroY = false;
    [SerializeField] private bool applyRootRotation = false;

    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;

    [Header("Character Push")]
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private LayerMask pushLayers;

    [Header("Environment-Safe Root Motion")]
    [Tooltip("Blocking geometry for every root-motion playback without a CharacterController. " +
             "CharacterController actors use their controller's collision layers.")]
    [SerializeField] private LayerMask environmentCollisionMask = ~0;

    [Tooltip("Skin width kept between the actor body and blocking geometry.")]
    [Min(0f)][SerializeField] private float environmentCollisionPadding = 0.05f;

    [Tooltip("Search radius used to recover the actor onto the NavMesh when a safe playback ends off-mesh.")]
    [Min(0.1f)][SerializeField] private float navMeshRecoverySearchRadius = 2.5f;

    private System.Action<RootMotionPolicy> _onPolicyChanged;
    private CharacterAnimBrain _subscribedBrain;
    private RootMotionPolicy _policy;

    private bool _prevRM;
    private CharacterMovementMotor _movementMotor;
    private int _agentSuspendToken;
    private bool _environmentSafeRun;
    private Transform _actorRoot;
    private int _characterLayers;

    public bool ZeroY => zeroY;

    Transform ActorRoot => _actorRoot != null
        ? _actorRoot
        : ctx != null
            ? ctx.transform
            : transform;

    void Awake()
    {
        _characterLayers = LayerMask.GetMask("Player", "Enemy", "Ally");
        if (!brain) brain = GetComponent<CharacterAnimBrain>();
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponent<Animator>();
        if (!ctx) ctx = GetComponent<CharacteContext>();
        if (!ctx) ctx = GetComponentInParent<CharacteContext>();
        _actorRoot = ctx != null ? ctx.transform : transform;
        BindMovementMotor();

        if (animator)
            animator.applyRootMotion = false;

        SubscribeToBrain();
        _prevRM = _policy.Active;
    }

    void OnEnable()
    {
        SubscribeToBrain();
    }

    void OnDisable()
    {
        ReleaseRootMotionOwnership();
    }

    void OnDestroy()
    {
        ReleaseRootMotionOwnership();
    }

    /// <summary>
    /// Hands the agent back before dropping the subscription. Skipping this leaves the agent with
    /// isStopped/updatePosition/updateRotation still overridden, which freezes the actor — and a
    /// replacement driver would then cache those broken values as the state to restore later.
    /// A model rebuild disables the old driver exactly this way.
    /// </summary>
    void ReleaseRootMotionOwnership()
    {
        ApplyRootMotionTransition(false);
        UnsubscribeFromBrain();
    }

    /// <summary>
    /// This driver owns <see cref="Animator.applyRootMotion"/> and the agent's motion flags for its
    /// actor. Reacting to the policy event instead of polling closes the one-frame window where
    /// root motion was already active but the agent was still driving the transform too.
    /// </summary>
    void SubscribeToBrain()
    {
        _onPolicyChanged ??= OnRootMotionPolicyChanged;

        if (_subscribedBrain == brain)
            return;

        UnsubscribeFromBrain();

        if (!brain)
            return;

        _subscribedBrain = brain;
        brain.RegisterRootMotionAdapter(_onPolicyChanged);
    }

    void UnsubscribeFromBrain()
    {
        if (!_subscribedBrain)
        {
            _subscribedBrain = null;
            return;
        }

        _subscribedBrain.UnregisterRootMotionAdapter(_onPolicyChanged);
        _subscribedBrain = null;
    }

    void OnRootMotionPolicyChanged(RootMotionPolicy policy)
    {
        _policy = policy;
        LatchEnvironmentSafeRun(policy);
        ApplyRootMotionTransition(policy.Active);
        SyncVerticalOwnership();
    }

    /// <summary>
    /// Remembers that the running playback asked for environment safety.
    ///
    /// It cannot be read once at entry: the Brain publishes <c>Active</c> from
    /// <c>EnterExclusiveLocomotion</c> and the shape flags a moment later, so the first publish of a
    /// playback always still carries the previous shape. Latching on every active publish and
    /// clearing it in <see cref="ExitRootMotion"/> is what makes the exit path see the real request.
    /// </summary>
    void LatchEnvironmentSafeRun(RootMotionPolicy policy)
    {
        if (policy.Active && policy.EnvironmentSafe)
            _environmentSafeRun = true;
    }

    public void Configure(
        CharacterAnimBrain animBrain,
        NavMeshAgent navMeshAgent,
        Animator sourceAnimator,
        CharacteContext context,
        Transform actorRoot,
        RootMotionNavMeshDriver settingsSource = null)
    {
        _characterLayers = LayerMask.GetMask("Player", "Enemy", "Ally");
        if (settingsSource != null && settingsSource != this)
        {
            zeroY = settingsSource.zeroY;
            applyRootRotation = settingsSource.applyRootRotation;
            pushLayers = settingsSource.pushLayers;
            environmentCollisionMask = settingsSource.environmentCollisionMask;
            environmentCollisionPadding = settingsSource.environmentCollisionPadding;
            navMeshRecoverySearchRadius = settingsSource.navMeshRecoverySearchRadius;
        }

        // Hand the previous agent back BEFORE rebinding. Re-entering root motion without an
        // intervening exit would cache the overrides this driver already applied
        // (isStopped/updatePosition/updateRotation) as if they were the agent's own settings, and
        // the eventual restore would then re-apply them instead of undoing them. Rebinding first
        // would also strand the old agent with no one left to restore it.
        ApplyRootMotionTransition(false);

        if (_movementMotor) _movementMotor.SetRootMotionVerticalOwner(this, false);
        _movementMotor = null;
        brain = animBrain;
        agent = navMeshAgent;
        animator = sourceAnimator;
        ctx = context;
        _actorRoot = actorRoot != null
            ? actorRoot
            : context != null
                ? context.transform
                : transform;
        BindMovementMotor();

        if (animator)
            animator.applyRootMotion = false;

        // Re-subscribing to the same Brain is a no-op, so re-apply the policy explicitly or a
        // rebuilt model keeps the flag we just cleared while root motion is still running.
        SubscribeToBrain();
        ApplyRootMotionTransition(_policy.Active);
        SyncVerticalOwnership();
    }

    void Update()
    {
        // The policy event already drives the transition. This stays as a safety net for the case
        // where the driver is attached to a Brain that is already mid-playback.
        bool rm = _policy.Active;
        LatchEnvironmentSafeRun(_policy);
        ApplyRootMotionTransition(rm);
        SyncVerticalOwnership();

        if (rm) CharacterMovementMotor.SyncAgent(agent, ActorRoot);
    }

    void SyncVerticalOwnership()
    {
        if (!_movementMotor) return;
        _movementMotor.SetRootMotionVerticalOwner(this,
            isActiveAndEnabled && _policy.Active && !zeroY && !_policy.PlanarOnly);
    }

    void BindMovementMotor()
    {
        _movementMotor = ctx ? ctx.EnsureMovementMotor() : CharacterMovementMotor.Resolve(null, ActorRoot);
        if (_movementMotor) _movementMotor.Configure(ctx, ActorRoot, agent: agent);
    }

    private void ApplyRootMotionTransition(bool rootMotionActive)
    {
        if (rootMotionActive == _prevRM)
            return;

        if (rootMotionActive) EnterRootMotion();
        else ExitRootMotion();

        _prevRM = rootMotionActive;
    }

    private void EnterRootMotion()
    {
        if (animator) animator.applyRootMotion = true;
        if (_movementMotor) _agentSuspendToken = _movementMotor.AcquireAgentSuspension(agent);
        CharacterMovementMotor.SyncAgent(agent, ActorRoot);
    }

    private void ExitRootMotion()
    {
        if (animator) animator.applyRootMotion = false;
        CharacterMovementMotor.SyncAgent(agent, ActorRoot);
        if (_movementMotor)
        {
            _movementMotor.SetRootMotionVerticalOwner(this, false);
            _movementMotor.ReleaseAgentSuspension(_agentSuspendToken);
        }
        _agentSuspendToken = 0;
        if (_environmentSafeRun)
        {
            _environmentSafeRun = false;
            // Another owner may still hold the actor in the air. It owns its eventual landing.
            if (!_movementMotor || !_movementMotor.IsAgentTransformSuspended)
                RecoverAgentToNavMesh();
        }
    }

    void OnAnimatorMove()
    {
        if (!_policy.Active) return;
        if (!animator) return;

        Vector3 delta = RootMotionDeltaUtility.GetPositionDelta(
            animator,
            zeroY || _policy.PlanarOnly);

        Transform actorRoot = ActorRoot;

        if (ctx != null && ctx.DefensiveBlockAttack != null)
            delta = ctx.DefensiveBlockAttack.ConstrainContactRootMotion(actorRoot.position, delta);

        ApplyTranslation(delta);

        CharacterMovementMotor.SyncAgent(agent, actorRoot);

        if (applyRootRotation || _policy.ApplyYaw)
        {
            float yawDelta = RootMotionDeltaUtility.GetYawDelta(animator);
            actorRoot.rotation *= Quaternion.AngleAxis(yawDelta, Vector3.up);
        }

        if (pushLayers != 0 && !_policy.IgnoreCharacterCollision)
            PushOverlappingCharacters();
    }

    private void ApplyTranslation(Vector3 delta)
    {
        var settings = CharacterMovementSettings.Default;
        settings.CollisionMask = environmentCollisionMask;
        settings.Padding = environmentCollisionPadding;
        settings.Slide = true;
        settings.IgnoreLayers = _policy.IgnoreCharacterCollision ? _characterLayers : 0;
        if (_movementMotor) _movementMotor.Move(delta, settings);
    }
    /// <summary>
    /// Puts the actor back on the NavMesh after an environment-safe playback.
    ///
    /// <see cref="ResyncAgent"/> only ever syncs or warps an agent that is <em>already</em> on the
    /// mesh, so it cannot recover an actor that animation carried off it. This samples a nearby
    /// valid position and warps only when the agent actually needs it.
    /// </summary>
    private void RecoverAgentToNavMesh()
    {
        if (!agent || !agent.enabled)
            return;

        Vector3 position = ActorRoot.position;

        if (agent.isOnNavMesh)
        {
            ResyncAgent();
            return;
        }

        if (NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshRecoverySearchRadius, NavMesh.AllAreas))
        {
            if (_movementMotor) _movementMotor.PlaceOnNavMesh(hit.position);
            return;
        }

        // Nothing valid nearby. Leaving the agent off-mesh is still better than teleporting the
        // actor somewhere arbitrary, so only the warning is worth emitting.
        Debug.LogWarning(
            $"[{nameof(RootMotionNavMeshDriver)}] Environment-safe root motion ended off the NavMesh and no " +
            $"valid position was found within {navMeshRecoverySearchRadius:0.##}m.",
            this);
    }

    private void PushOverlappingCharacters()
    {
        if (ctx == null || ctx.ColliderRefs == null) return;
        Collider aiCol = ctx.ColliderRefs.CharacterPositionCollider;
        if (aiCol == null) return;

        Bounds b = aiCol.bounds;
        Collider[] hits = Physics.OverlapSphere(b.center, b.extents.magnitude, pushLayers, QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (hit == aiCol) continue;

            if (!Physics.ComputePenetration(
                aiCol, aiCol.transform.position, aiCol.transform.rotation,
                hit, hit.transform.position, hit.transform.rotation,
                out Vector3 dir, out float dist))
                continue;

            var cc = hit.GetComponentInParent<CharacterController>();
            if (!cc) continue;

            Vector3 planarDirection = ResolvePlanarPushDirection(aiCol, hit, -dir, cc.transform);
            if (planarDirection.sqrMagnitude > 0.0001f)
            {
                var target = cc.GetComponentInParent<CharacteContext>();
                Transform root = target ? target.transform : cc.transform;
                var motor = CharacterMovementMotor.Resolve(target, root);
                if (motor) motor.Move(planarDirection * dist, CharacterMovementSettings.Default);
            }
        }
    }

    static Vector3 ResolvePlanarPushDirection(
        Collider source,
        Collider hit,
        Vector3 penetrationDirection,
        Transform fallbackRoot)
    {
        Vector3 planarDirection = Vector3.ProjectOnPlane(penetrationDirection, Vector3.up);
        if (planarDirection.sqrMagnitude <= 0.0001f)
        {
            planarDirection = hit.bounds.center - source.bounds.center;
            planarDirection.y = 0f;
        }

        if (planarDirection.sqrMagnitude <= 0.0001f && fallbackRoot != null)
            planarDirection = Vector3.ProjectOnPlane(fallbackRoot.forward, Vector3.up);

        return planarDirection.sqrMagnitude > 0.0001f
            ? planarDirection.normalized
            : Vector3.zero;
    }

    public void ResyncAgent(float warpIfDistanceGreaterThan = 0.5f)
    {
        if (!agent || !agent.enabled) return;
        if (!agent.isOnNavMesh) return;

        float d = Vector3.Distance(agent.nextPosition, ActorRoot.position);
        if (d > warpIfDistanceGreaterThan)
            agent.Warp(ActorRoot.position);
        else
            agent.nextPosition = ActorRoot.position;
    }
}
