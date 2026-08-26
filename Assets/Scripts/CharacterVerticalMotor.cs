using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Single owner of a character's Y axis.
///
/// Every planar mover in the project (PlayerMovementCC, DashSystem, CharacterKnockbackMotor,
/// AgentMoveDriver) is deliberately planar-only, and each of them sits behind state gates that
/// can stop running at any time. This motor runs independently of all of them so an actor that
/// leaves the ground always comes back down, including while stunned, dashing, or dead.
/// </summary>
[DefaultExecutionOrder(120)]
[DisallowMultipleComponent]
public sealed class CharacterVerticalMotor : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private CapsuleCollider capsuleCollider;
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private AgentMoveDriver agentMoveDriver;
    [SerializeField] private CharacterAnimBrain animBrain;
    [SerializeField] private RootMotionCCDriver rootMotionCCDriver;
    [SerializeField] private RootMotionNavMeshDriver rootMotionNavMeshDriver;

    [Header("Mode")]
    [SerializeField] private CharacterVerticalMode mode = CharacterVerticalMode.Always;

    [Header("Gravity")]
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float terminalVelocity = -50f;
    [SerializeField] private float groundedStickSpeed = -2f;

    [Header("Ground Probe")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Min(0f)] private float groundProbeDistance = 0.15f;

    [Header("Collision (no CharacterController)")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private QueryTriggerInteraction queryTriggers = QueryTriggerInteraction.Ignore;
    [SerializeField, Min(0f)] private float collisionPadding = 0.02f;

    [Header("NavMesh")]
    [SerializeField, Min(0.05f)] private float navMeshLandSampleRadius = 1.5f;
    [SerializeField, Min(0f)] private float navMeshLandRecoveryTimeout = 1f;

    private const int ProbeBufferSize = 8;

    private readonly RaycastHit[] _probeHits = new RaycastHit[ProbeBufferSize];
    private readonly HashSet<int> _gravitySuspendTokens = new();

    private float _verticalVelocity;
    private float _airTime;
    private bool _grounded = true;
    private bool _airborne;
    private int _nextGravitySuspendToken;
    private int _agentSuspendToken;
    private bool _directAgentSuspendActive;
    private bool _cachedAgentIsStopped;
    private bool _cachedAgentUpdatePosition;
    private float _landRecoveryElapsed;

    Transform ActorTransform => ctx != null ? ctx.transform : transform;

    public CharacterVerticalMode Mode => mode;
    public bool IsGrounded => _grounded;
    public float VerticalVelocity => _verticalVelocity;
    public float AirTime => _airTime;
    public bool GravitySuspended => _gravitySuspendTokens.Count > 0;

    /// <summary>
    /// In <see cref="CharacterVerticalMode.Always"/> this is simply "not grounded".
    /// In <see cref="CharacterVerticalMode.AgentDriven"/> it is true only while the actor has been
    /// launched and the motor holds the transform away from the NavMeshAgent.
    /// </summary>
    public bool IsAirborne => mode == CharacterVerticalMode.Always ? !_grounded : _airborne;

    void Awake()
    {
        ResolveRefs();
    }

    void OnEnable()
    {
        ResolveRefs();
    }

    void OnDisable()
    {
        // Never leave the agent suspended: an actor disabled mid-flight would otherwise come back
        // with its NavMeshAgent permanently off transform duty.
        ReleaseAgentSuspend();
        _airborne = false;
        _verticalVelocity = 0f;
        _airTime = 0f;
        _landRecoveryElapsed = 0f;
    }

    void OnDestroy()
    {
        ReleaseAgentSuspend();
    }

    void ResolveRefs()
    {
        if (!ctx)
            ctx = GetComponent<CharacteContext>();
        if (!ctx)
            ctx = GetComponentInParent<CharacteContext>();

        ctx?.ResolveReferences();

        if (!characterController)
            characterController = ctx != null ? ctx.cc : null;
        if (!characterController)
            characterController = ResolveActorComponent(characterController);

        if (!capsuleCollider)
            capsuleCollider = ResolveActorComponent(capsuleCollider);

        if (!animBrain)
            animBrain = ctx != null ? ctx.AnimBrain : null;
        if (!animBrain)
            animBrain = ResolveActorComponent(animBrain);

        // NavMeshAgent and AgentMoveDriver live on the ally/enemy subtypes, not on CharacteContext,
        // so they are resolved by hierarchy search rather than through the shared context.
        if (!navMeshAgent)
            navMeshAgent = ResolveActorComponent(navMeshAgent);
        if (!agentMoveDriver)
            agentMoveDriver = ResolveActorComponent(agentMoveDriver);

        if (!rootMotionCCDriver)
            rootMotionCCDriver = ResolveActorComponent(rootMotionCCDriver);
        if (!rootMotionNavMeshDriver)
            rootMotionNavMeshDriver = ResolveActorComponent(rootMotionNavMeshDriver);

        if (ctx != null && ctx.VerticalMotor == null)
            ctx.VerticalMotor = this;
    }

    /// <summary>
    /// Binds the runtime root-motion driver created for the active character model. The visual
    /// controller owns model rebuilds, so it can update this reference without a hierarchy lookup.
    /// </summary>
    public void BindRuntimeRootMotionDriver(RootMotionCCDriver driver)
    {
        rootMotionCCDriver = driver;
        rootMotionNavMeshDriver = null;
    }

    /// <summary>
    /// Binds the runtime root-motion driver created for the active character model. The visual
    /// controller owns model rebuilds, so it can update this reference without a hierarchy lookup.
    /// </summary>
    public void BindRuntimeRootMotionDriver(RootMotionNavMeshDriver driver)
    {
        rootMotionNavMeshDriver = driver;
        rootMotionCCDriver = null;
    }

    T ResolveActorComponent<T>(T current) where T : Component
    {
        if (current != null)
            return current;

        Transform root = ActorTransform;
        if (root == null)
            return null;

        if (root.TryGetComponent(out T localComponent))
            return localComponent;

        T childComponent = root.GetComponentInChildren<T>(true);
        if (childComponent != null)
            return childComponent;

        return root.GetComponentInParent<T>();
    }

    void LateUpdate()
    {
        float dt = ResolveDeltaTime();

        // Hitlag and pause freeze the world. Integrating gravity through a freeze makes actors sink.
        if (dt <= 0f)
            return;

        // Dormant path for NavMesh actors: one bool, before any probe or component access.
        if (mode == CharacterVerticalMode.AgentDriven && !_airborne)
            return;

        if (GravitySuspended || RootMotionOwnsY)
        {
            _verticalVelocity = 0f;
            return;
        }

        _grounded = ProbeGround();

        if (_grounded && _verticalVelocity <= 0f)
        {
            _verticalVelocity = mode == CharacterVerticalMode.Always ? groundedStickSpeed : 0f;
        }
        else
        {
            _verticalVelocity = Mathf.Max(
                _verticalVelocity + gravity * dt,
                terminalVelocity);
        }

        ApplyVerticalDelta(_verticalVelocity * dt);

        _airTime = _grounded ? 0f : _airTime + dt;

        if (mode == CharacterVerticalMode.AgentDriven && _grounded && _verticalVelocity <= 0f)
            TryLand(dt);
    }

    float ResolveDeltaTime()
    {
        bool usesWorldSlow = ctx == null || ctx.UsesWorldSlow;

        if (usesWorldSlow && TimeSlowManager.Instance != null)
            return TimeSlowManager.Instance.WorldDeltaTime;

        return Time.deltaTime;
    }

    /// <summary>
    /// True while a root motion driver is writing the clip's own Y this frame. Gravity and root
    /// motion must not both write Y, so the motor stands down and lets the clip win.
    /// </summary>
    bool RootMotionOwnsY
    {
        get
        {
            if (animBrain == null || !animBrain.RootMotionActive)
                return false;

            if (animBrain.RootMotionPlanarOnly)
                return false;

            if (rootMotionCCDriver != null && rootMotionCCDriver.isActiveAndEnabled && !rootMotionCCDriver.ZeroY)
                return true;

            if (rootMotionNavMeshDriver != null && rootMotionNavMeshDriver.isActiveAndEnabled && !rootMotionNavMeshDriver.ZeroY)
                return true;

            return false;
        }
    }

    // ---------------------------------------------------------------- launch / suspend API

    /// <summary>Sends the actor upward at <paramref name="upwardSpeed"/> m/s.</summary>
    public void Launch(float upwardSpeed)
    {
        SetVerticalVelocity(Mathf.Max(0f, upwardSpeed));
    }

    public void AddVerticalVelocity(float delta)
    {
        SetVerticalVelocity(_verticalVelocity + delta);
    }

    public void SetVerticalVelocity(float value)
    {
        _verticalVelocity = value;

        if (mode == CharacterVerticalMode.AgentDriven && value > 0f)
            BeginAirborne();
    }

    /// <summary>
    /// Holds gravity off for levitate statuses, cutscenes, and scripted lifts. Reference counted so
    /// overlapping owners cannot cancel each other.
    /// </summary>
    public int AcquireGravitySuspendToken()
    {
        int token = NextGravitySuspendToken();
        _gravitySuspendTokens.Add(token);
        _verticalVelocity = 0f;
        return token;
    }

    public void ReleaseGravitySuspendToken(int token)
    {
        if (token == 0 || !_gravitySuspendTokens.Remove(token))
            return;

        if (_gravitySuspendTokens.Count == 0)
            _verticalVelocity = 0f;
    }

    int NextGravitySuspendToken()
    {
        do
        {
            _nextGravitySuspendToken++;
            if (_nextGravitySuspendToken <= 0)
                _nextGravitySuspendToken = 1;
        }
        while (_gravitySuspendTokens.Contains(_nextGravitySuspendToken));

        return _nextGravitySuspendToken;
    }

    // ---------------------------------------------------------------- agent handover

    void BeginAirborne()
    {
        if (_airborne)
            return;

        _airborne = true;
        _grounded = false;
        _landRecoveryElapsed = 0f;

        SuspendAgent();
    }

    /// <summary>
    /// NavMeshAgent.updatePosition rewrites the transform from its NavMesh-projected position every
    /// frame, which would erase the vertical motion outright. The agent has to be taken off
    /// transform duty for the whole airborne window.
    /// </summary>
    void SuspendAgent()
    {
        if (_agentSuspendToken != 0 || _directAgentSuspendActive)
            return;

        // Reference-counted path, so overlapping owners (airborne + knockback) cannot restore each
        // other's cached state.
        if (agentMoveDriver != null)
        {
            _agentSuspendToken = agentMoveDriver.AcquireAgentTransformSuspendToken();
            if (_agentSuspendToken != 0)
                return;
        }

        // Some actor prefabs carry a NavMeshAgent without an AgentMoveDriver, so fall back to
        // suspending the agent directly.
        if (navMeshAgent == null || !navMeshAgent.enabled)
            return;

        _cachedAgentIsStopped = navMeshAgent.isStopped;
        _cachedAgentUpdatePosition = navMeshAgent.updatePosition;
        navMeshAgent.isStopped = true;
        navMeshAgent.updatePosition = false;
        navMeshAgent.nextPosition = ActorTransform.position;
        _directAgentSuspendActive = true;
    }

    void TryLand(float dt)
    {
        if (navMeshAgent == null || !navMeshAgent.enabled)
        {
            FinishLanding();
            return;
        }

        Vector3 position = ActorTransform.position;

        // Warp rather than nextPosition: an actor launched over a ledge can land somewhere the
        // agent has no valid mesh position for, and nextPosition would leave it desynced forever.
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshLandSampleRadius, navMeshAgent.areaMask))
        {
            navMeshAgent.Warp(hit.position);
            FinishLanding();
            return;
        }

        _landRecoveryElapsed += dt;
        if (_landRecoveryElapsed < navMeshLandRecoveryTimeout)
            return;

        // Widen once, then give up and hand the agent back anyway. An actor that snaps a couple of
        // metres is a far better outcome than one left with a permanently suspended agent.
        if (NavMesh.SamplePosition(position, out hit, navMeshLandSampleRadius * 4f, navMeshAgent.areaMask))
        {
            navMeshAgent.Warp(hit.position);
        }
        else
        {
            Debug.LogWarning(
                $"[CharacterVerticalMotor] '{name}' landed off the NavMesh and could not be resampled. " +
                "Releasing the agent in place.",
                this);
        }

        FinishLanding();
    }

    void FinishLanding()
    {
        _verticalVelocity = 0f;
        _airborne = false;
        _grounded = true;
        _landRecoveryElapsed = 0f;
        ReleaseAgentSuspend();
    }

    void ReleaseAgentSuspend()
    {
        if (_agentSuspendToken != 0)
        {
            if (agentMoveDriver != null)
                agentMoveDriver.ReleaseAgentTransformSuspendToken(_agentSuspendToken);

            _agentSuspendToken = 0;
        }

        if (!_directAgentSuspendActive)
            return;

        if (navMeshAgent != null && navMeshAgent.enabled)
        {
            navMeshAgent.nextPosition = ActorTransform.position;
            navMeshAgent.updatePosition = _cachedAgentUpdatePosition;
            navMeshAgent.isStopped = _cachedAgentIsStopped;
        }

        _directAgentSuspendActive = false;
    }

    // ---------------------------------------------------------------- movement / probing

    void ApplyVerticalDelta(float delta)
    {
        if (Mathf.Abs(delta) <= 0.000001f)
            return;

        Vector3 motion = new Vector3(0f, delta, 0f);

        if (characterController != null && characterController.enabled)
        {
            characterController.Move(motion);
        }
        else
        {
            Vector3 resolved = ResolveManualDelta(motion);
            if (resolved.sqrMagnitude <= 0.000001f)
                return;

            ActorTransform.position += resolved;
        }

        if (navMeshAgent != null && navMeshAgent.enabled)
            navMeshAgent.nextPosition = ActorTransform.position;
    }

    /// <summary>
    /// Swept fallback for actors with no CharacterController, mirroring
    /// CharacterKnockbackMotor.ResolveManualDelta.
    /// </summary>
    Vector3 ResolveManualDelta(Vector3 desiredDelta)
    {
        float desiredDistance = desiredDelta.magnitude;
        if (desiredDistance <= 0.0001f)
            return Vector3.zero;

        Vector3 direction = desiredDelta / desiredDistance;

        if (!TryResolveCapsule(out Vector3 p1, out Vector3 p2, out float radius))
            return desiredDelta;

        int count = Physics.CapsuleCastNonAlloc(
            p1,
            p2,
            radius,
            direction,
            _probeHits,
            desiredDistance + collisionPadding,
            collisionMask,
            queryTriggers);

        float allowedDistance = desiredDistance;
        Transform selfRoot = transform.root;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _probeHits[i];
            if (hit.collider == null)
                continue;

            if (hit.transform.root == selfRoot)
                continue;

            allowedDistance = Mathf.Min(
                allowedDistance,
                Mathf.Max(0f, hit.distance - collisionPadding));
        }

        return direction * allowedDistance;
    }

    bool ProbeGround()
    {
        if (characterController != null && characterController.enabled && characterController.isGrounded)
            return true;

        // cc.isGrounded is unreliable until something has moved the capsule downward, so it can
        // report false for a frame after a teleport, a state change, or a scripted lift. The
        // sweep below is also the only ground source for actors with no CharacterController.
        if (!TryResolveFootSphere(out Vector3 origin, out float radius))
            return ProbeGroundByRay();

        int count = Physics.SphereCastNonAlloc(
            origin,
            radius,
            Vector3.down,
            _probeHits,
            groundProbeDistance,
            groundMask,
            queryTriggers);

        Transform selfRoot = transform.root;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _probeHits[i];
            if (hit.collider == null)
                continue;

            if (hit.transform.root == selfRoot)
                continue;

            return true;
        }

        return false;
    }

    bool ProbeGroundByRay()
    {
        Vector3 origin = ActorTransform.position + Vector3.up * 0.1f;

        int count = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            _probeHits,
            groundProbeDistance + 0.1f,
            groundMask,
            queryTriggers);

        Transform selfRoot = transform.root;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _probeHits[i];
            if (hit.collider == null)
                continue;

            if (hit.transform.root == selfRoot)
                continue;

            return true;
        }

        return false;
    }

    bool TryResolveBounds(out Bounds bounds)
    {
        if (characterController != null && characterController.enabled)
        {
            bounds = characterController.bounds;
            return true;
        }

        if (capsuleCollider != null && capsuleCollider.enabled)
        {
            bounds = capsuleCollider.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    bool TryResolveFootSphere(out Vector3 origin, out float radius)
    {
        if (!TryResolveBounds(out Bounds bounds))
        {
            origin = default;
            radius = 0f;
            return false;
        }

        radius = Mathf.Max(0.05f, Mathf.Min(bounds.extents.x, bounds.extents.z) - 0.01f);
        origin = new Vector3(bounds.center.x, bounds.min.y + radius, bounds.center.z);
        return true;
    }

    bool TryResolveCapsule(out Vector3 p1, out Vector3 p2, out float radius)
    {
        if (!TryResolveBounds(out Bounds bounds))
        {
            p1 = default;
            p2 = default;
            radius = 0f;
            return false;
        }

        radius = Mathf.Max(0.05f, Mathf.Min(bounds.extents.x, bounds.extents.z));
        float half = Mathf.Max(0f, bounds.extents.y - radius);

        p1 = bounds.center + Vector3.up * half;
        p2 = bounds.center - Vector3.up * half;
        return true;
    }
}
