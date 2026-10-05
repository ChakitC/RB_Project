using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;

public enum TargetOrbitDirection
{
    Clockwise,
    CounterClockwise,
    Random
}

[Opsive.Shared.Utility.Category("AI/Movement")]
[Opsive.Shared.Utility.Description("Moves around a target inside a configurable radial band using NavMeshAgent.")]
public class TargetOrbitNavMesh : Action
{
    public SharedVariable<CharacteContext> context;
    [Tooltip("Optional graph-local P0 watchdog. Leave unbound for existing graphs.")]
    public SharedVariable<AICombatState> combatState;
    AICombatNavigation p0Navigation;
    AITargetSensor p0Sensor;
    AITargetHandle p0Target;
    int rotationToken;
    const int PathProbeAttempts = 4;
    const float MinimumProbeAngle = 1f;

    [Header("Target")]
    public SharedVariable<GameObject> target;

    [Header("Orbit Radius")]
    public SharedVariable<float> minRadius;
    public SharedVariable<float> maxRadius;

    [Header("Direction")]
    public TargetOrbitDirection direction = TargetOrbitDirection.Random;

    [Header("Facing")]
    public bool faceTarget = true;
    public SharedVariable<float> turnSpeed;

    [Header("Timing")]
    [Tooltip("0 keeps the task running until it is aborted.")]
    public SharedVariable<float> duration;

    [Tooltip("0 uses the speed already owned by the character movement system.")]
    public SharedVariable<float> moveSpeed;

    [Header("Navigation")]
    public SharedVariable<float> repathInterval;
    public SharedVariable<float> lookAheadAngle;
    public SharedVariable<float> pathFailureTimeout;

    NavMeshAgent _agent;
    AgentMoveDriver _moveDriver;
    CharacteContext _ctx;
    int executionGeneration;
    CharacterMovementMotor executionMotor;
    StateHub _stateHub;
    CharacterAnimBrain _animBrain;
    Transform _actorTransform;
    readonly NavMeshPath _probePath = new();

    bool _configurationValid;
    bool _agentStateCaptured;
    bool _pathFailureActive;
    bool _cachedUpdateRotation;
    float _cachedStoppingDistance;
    float _cachedSpeed;
    bool _directSpeedOverride;
    int _speedOverrideToken;
    float _orbitSign;
    float _activeElapsed;
    float _repathElapsed;
    float _pathFailureElapsed;

    public override void OnStart()
    {
        CacheReferences();
        ResetRuntimeState();

        _configurationValid = ValidateConfiguration();
        if (!_configurationValid)
            return;

        if (combatState != null && combatState.Value != null)
        {
            p0Sensor = _ctx.AITargetSensor;
            p0Target = p0Sensor != null ? p0Sensor.ReadSnapshot.Target : default;
            combatState.Value.ObserveTarget(p0Target);
            combatState.Value.BeginNavigation("Orbit", p0Target, _actorTransform.position);
            p0Navigation = new AICombatNavigation(_ctx, combatState.Value);
            if (!AICombatTaskUtility.CanAct(_ctx)) { _configurationValid = false; return; }
            if (faceTarget && _ctx.MovementMotor != null)
                rotationToken = _ctx.MovementMotor.AcquireAgentSuspension(_agent, false, true);
        }

        _cachedUpdateRotation = _agent.updateRotation;
        _cachedStoppingDistance = _agent.stoppingDistance;
        _cachedSpeed = _agent.speed;
        _agentStateCaptured = true;

        _agent.stoppingDistance = 0f;
        if (faceTarget)
            _agent.updateRotation = false;

        float configuredMoveSpeed = moveSpeed.Value;
        if (configuredMoveSpeed > 0f)
        {
            if (_moveDriver != null)
            {
                _speedOverrideToken = _moveDriver.AcquireMoveSpeedOverride(configuredMoveSpeed);
            }
            else
            {
                _directSpeedOverride = true;
                ApplyDirectSpeedOverride(configuredMoveSpeed);
            }
        }

        _orbitSign = ResolveOrbitSign();
        _repathElapsed = repathInterval.Value;
        ResumeAgent();
    }

    public override TaskStatus OnUpdate()
    {
        if (!AIContextBinding.IsCurrent(context, _ctx, executionGeneration) || _ctx.NavigationAgent != _agent) return TaskStatus.Failure;
        if (!_configurationValid)
            return TaskStatus.Failure;

        if (!CanUseAgent() || target == null || target.Value == null)
            return TaskStatus.Failure;

        if (p0Navigation != null)
        {
            AITargetSnapshot s = p0Sensor != null ? p0Sensor.ReadSnapshot : AITargetSnapshot.Empty;
            if (s.Target != p0Target || !s.IsCurrentVisible || !AICombatTaskUtility.IsEligible(_ctx, p0Target))
                return TaskStatus.Failure;
            if (!AICombatTaskUtility.CanAct(_ctx)) return TaskStatus.Failure;
            if (AICombatTaskUtility.IsPaused || !AICombatTaskUtility.CanMove(_ctx))
            {
                p0Navigation.Tick(combatState.Value.Destination, .1f, false);
                return TaskStatus.Running;
            }
            if (combatState.Value.NavigationFailure != null) return TaskStatus.Failure;
            if (_agent.hasPath)
            {
                p0Navigation.TrackExisting(_agent.destination);
                if (p0Navigation.Tick(_agent.destination, .1f, false) == TaskStatus.Failure)
                    return TaskStatus.Failure;
            }
        }

        float deltaTime = GetActorDeltaTime();
        if (!CanControlMovement())
        {
            PauseAgent();
            return TaskStatus.Running;
        }

        if (_pathFailureActive)
        {
            PauseAgent();
            _pathFailureElapsed += deltaTime;
            if (_pathFailureElapsed >= pathFailureTimeout.Value)
            {
                combatState?.Value?.FailNavigation("Orbit path timeout");
                return TaskStatus.Failure;
            }
        }
        else
        {
            ResumeAgent();
        }

        if (_directSpeedOverride)
            ApplyDirectSpeedOverride(moveSpeed.Value);

        _activeElapsed += deltaTime;
        if (duration.Value > 0f && _activeElapsed >= duration.Value)
            return TaskStatus.Success;

        RotateTowardTarget(deltaTime);

        _repathElapsed += deltaTime;
        float effectiveRepathInterval = p0Navigation != null ? Mathf.Max(.25f, repathInterval.Value) : repathInterval.Value;
        bool needsPath = !_agent.hasPath ||
                         _agent.pathStatus == NavMeshPathStatus.PathInvalid ||
                         _repathElapsed >= effectiveRepathInterval;

        if (!needsPath ||
            (_pathFailureActive && _repathElapsed < effectiveRepathInterval))
        {
            return TaskStatus.Running;
        }

        bool hasUsablePath = _agent.hasPath &&
                             _agent.pathStatus != NavMeshPathStatus.PathInvalid;

        _repathElapsed = 0f;
        if (TrySetOrbitPath())
        {
            _pathFailureActive = false;
            _pathFailureElapsed = 0f;
            ResumeAgent();
            return TaskStatus.Running;
        }

        if (hasUsablePath)
        {
            ResumeAgent();
            return TaskStatus.Running;
        }

        _pathFailureActive = true;
        PauseAgent();
        if (pathFailureTimeout.Value <= 0f)
            return TaskStatus.Failure;

        return TaskStatus.Running;
    }

    public override void OnEnd()
    {
        if (_ctx == null || _ctx.LifeGeneration != executionGeneration) return;
        if (p0Navigation != null)
        {
            p0Navigation.Cleanup();
            if (rotationToken != 0 && executionMotor != null)
                executionMotor.ReleaseAgentSuspension(rotationToken);
            rotationToken = 0;
            if (_agent != null && _agentStateCaptured && AICombatTaskUtility.CanAct(_ctx))
                _agent.stoppingDistance = _cachedStoppingDistance;
            if (_moveDriver != null && _speedOverrideToken != 0)
                _moveDriver.ReleaseMoveSpeedOverride(_speedOverrideToken);
            _speedOverrideToken = 0;
            _agentStateCaptured = false;
            p0Navigation = null;
            return;
        }
        if (_moveDriver != null && _speedOverrideToken != 0)
            _moveDriver.ReleaseMoveSpeedOverride(_speedOverrideToken);

        _speedOverrideToken = 0;

        if (_agent != null && _agentStateCaptured)
        {
            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }

            _agent.updateRotation = _cachedUpdateRotation;
            _agent.stoppingDistance = _cachedStoppingDistance;

            if (_directSpeedOverride)
                _agent.speed = _cachedSpeed;
        }

        _directSpeedOverride = false;
        _agentStateCaptured = false;
    }

    public override void Reset()
    {
        target = null;
        minRadius = 2f;
        maxRadius = 4f;
        direction = TargetOrbitDirection.Random;
        faceTarget = true;
        turnSpeed = 720f;
        duration = 0f;
        moveSpeed = 0f;
        repathInterval = 0.1f;
        lookAheadAngle = 25f;
        pathFailureTimeout = 0.5f;
    }

    void CacheReferences()
    {
        _ctx = AIContextBinding.Read(context);
        executionGeneration = _ctx != null ? _ctx.LifeGeneration : 0;
        executionMotor = _ctx != null ? _ctx.MovementMotor : null;
        _actorTransform = _ctx != null ? _ctx.transform : transform;
        _agent = _ctx != null ? _ctx.NavigationAgent : null;
        _moveDriver = _ctx != null ? _ctx.AgentMoveDriver : null;
        _stateHub = _ctx != null ? _ctx.stateHub : null;
        _animBrain = _ctx != null ? _ctx.AnimBrain : null;
    }

    void ResetRuntimeState()
    {
        p0Navigation = null;
        rotationToken = 0;
        _configurationValid = false;
        _agentStateCaptured = false;
        _pathFailureActive = false;
        _directSpeedOverride = false;
        _speedOverrideToken = 0;
        _activeElapsed = 0f;
        _repathElapsed = 0f;
        _pathFailureElapsed = 0f;
    }

    bool ValidateConfiguration()
    {
        if (!CanUseAgent())
            return FailConfiguration("A valid NavMeshAgent on a NavMesh is required.");

        if (target == null || target.Value == null)
            return FailConfiguration("Target is not assigned.");

        if (minRadius == null || maxRadius == null ||
            minRadius.Value < 0f ||
            maxRadius.Value <= 0f ||
            maxRadius.Value < minRadius.Value)
        {
            return FailConfiguration("Radius requires 0 <= minRadius <= maxRadius and maxRadius > 0.");
        }

        if (turnSpeed == null || turnSpeed.Value < 0f)
            return FailConfiguration("turnSpeed must be 0 or greater.");

        if (duration == null || duration.Value < 0f)
            return FailConfiguration("duration must be 0 or greater.");

        if (moveSpeed == null || moveSpeed.Value < 0f)
            return FailConfiguration("moveSpeed must be 0 or greater.");

        if (repathInterval == null || repathInterval.Value < 0f)
            return FailConfiguration("repathInterval must be 0 or greater.");

        if (lookAheadAngle == null ||
            lookAheadAngle.Value <= 0f ||
            lookAheadAngle.Value > 180f)
        {
            return FailConfiguration("lookAheadAngle must be greater than 0 and at most 180.");
        }

        if (pathFailureTimeout == null || pathFailureTimeout.Value < 0f)
            return FailConfiguration("pathFailureTimeout must be 0 or greater.");

        return true;
    }

    bool FailConfiguration(string reason)
    {
        Debug.LogWarning($"[TargetOrbitNavMesh] {reason}", gameObject);
        return false;
    }

    bool CanUseAgent()
    {
        return _agent != null &&
               _agent.enabled &&
               _agent.isActiveAndEnabled &&
               _agent.isOnNavMesh;
    }

    bool CanControlMovement()
    {
        if (_animBrain != null && _animBrain.RootMotionActive)
            return false;

        return _stateHub == null || _stateHub.CanMove();
    }

    void ResumeAgent()
    {
        if (CanUseAgent())
            _agent.isStopped = false;
    }

    void PauseAgent()
    {
        if (CanUseAgent())
            _agent.isStopped = true;
    }

    float ResolveOrbitSign()
    {
        switch (direction)
        {
            case TargetOrbitDirection.Clockwise:
                return 1f;
            case TargetOrbitDirection.CounterClockwise:
                return -1f;
            default:
                return Random.value < 0.5f ? 1f : -1f;
        }
    }

    bool TrySetOrbitPath()
    {
        Transform targetTransform = target.Value.transform;
        Vector3 targetPosition = targetTransform.position;
        Vector3 actorPosition = _actorTransform.position;
        Vector3 radial = actorPosition - targetPosition;
        radial.y = 0f;

        if (radial.sqrMagnitude <= 0.0001f)
        {
            radial = -targetTransform.forward;
            radial.y = 0f;
            if (radial.sqrMagnitude <= 0.0001f)
                radial = Vector3.forward;
        }

        float currentRadius = radial.magnitude;
        float desiredRadius = Mathf.Clamp(
            currentRadius,
            minRadius.Value,
            maxRadius.Value);
        float sampleRadius = Mathf.Max(0.5f, _agent.radius * 2f);

        for (int attempt = 0; attempt < PathProbeAttempts; attempt++)
        {
            float angleScale = Mathf.Pow(0.5f, attempt);
            float probeAngle = Mathf.Max(
                MinimumProbeAngle,
                lookAheadAngle.Value * angleScale);
            Vector3 orbitDirection =
                Quaternion.AngleAxis(_orbitSign * probeAngle, Vector3.up) *
                radial.normalized;
            Vector3 candidate = targetPosition + orbitDirection * desiredRadius;

            if (!NavMesh.SamplePosition(
                    candidate,
                    out NavMeshHit hit,
                    sampleRadius,
                    new NavMeshQueryFilter { agentTypeID = _agent.agentTypeID, areaMask = _agent.areaMask }))
            {
                continue;
            }

            _probePath.ClearCorners();
            if (!_agent.CalculatePath(hit.position, _probePath) ||
                _probePath.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            if (p0Navigation != null && NavMesh.FindClosestEdge(hit.position, out NavMeshHit edge,
                    new NavMeshQueryFilter { agentTypeID = _agent.agentTypeID, areaMask = _agent.areaMask }) && edge.distance < _agent.radius)
                continue;
            if (_agent.SetPath(_probePath))
            {
                p0Navigation?.TrackExisting(hit.position);
                return true;
            }
        }

        return false;
    }

    void RotateTowardTarget(float deltaTime)
    {
        if (!faceTarget ||
            turnSpeed.Value <= 0f ||
            (_stateHub != null && !_stateHub.CanRotate()))
        {
            return;
        }

        Vector3 directionToTarget =
            target.Value.transform.position - _actorTransform.position;
        directionToTarget.y = 0f;
        if (directionToTarget.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(directionToTarget.normalized, Vector3.up);
        _actorTransform.rotation = Quaternion.RotateTowards(
            _actorTransform.rotation,
            targetRotation,
            turnSpeed.Value * deltaTime);
    }

    float GetActorDeltaTime()
    {
        if (p0Navigation != null && AICombatTaskUtility.IsPaused) return 0f;
        bool usesWorldSlow = _ctx == null || _ctx.UsesWorldSlow;
        if (usesWorldSlow && TimeSlowManager.Instance != null)
            return TimeSlowManager.Instance.WorldDeltaTime;

        return Time.deltaTime;
    }

    public override void OnBehaviorTreeStopped(bool paused) { if (!paused) OnEnd(); }
    public override void OnDestroy() => OnEnd();

    void ApplyDirectSpeedOverride(float configuredMoveSpeed)
    {
        float resolvedSpeed = configuredMoveSpeed;
        bool usesWorldSlow = _ctx == null || _ctx.UsesWorldSlow;
        if (usesWorldSlow && TimeSlowManager.Instance != null)
            resolvedSpeed *= TimeSlowManager.Instance.WorldTimeScale;

        _agent.speed = resolvedSpeed;
    }
}
