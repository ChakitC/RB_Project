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
        if (!_configurationValid)
            return TaskStatus.Failure;

        if (!CanUseAgent() || target == null || target.Value == null)
            return TaskStatus.Failure;

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
                return TaskStatus.Failure;
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
        bool needsPath = !_agent.hasPath ||
                         _agent.pathStatus == NavMeshPathStatus.PathInvalid ||
                         _repathElapsed >= repathInterval.Value;

        if (!needsPath ||
            (_pathFailureActive && _repathElapsed < repathInterval.Value))
        {
            return TaskStatus.Running;
        }

        _repathElapsed = 0f;
        if (TrySetOrbitPath())
        {
            _pathFailureActive = false;
            _pathFailureElapsed = 0f;
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
        _ctx = gameObject.GetComponent<CharacteContext>();
        if (_ctx == null)
            _ctx = gameObject.GetComponentInParent<CharacteContext>();

        _ctx?.ResolveReferences();

        _actorTransform = _ctx != null ? _ctx.transform : transform;

        _agent = gameObject.GetComponent<NavMeshAgent>();
        if (_agent == null)
            _agent = gameObject.GetComponentInParent<NavMeshAgent>();

        _moveDriver = gameObject.GetComponent<AgentMoveDriver>();
        if (_moveDriver == null)
            _moveDriver = gameObject.GetComponentInParent<AgentMoveDriver>();

        _stateHub = _ctx != null ? _ctx.stateHub : null;
        if (_stateHub == null)
            _stateHub = gameObject.GetComponentInParent<StateHub>();

        _animBrain = _ctx != null ? _ctx.AnimBrain : null;
        if (_animBrain == null)
            _animBrain = gameObject.GetComponentInParent<CharacterAnimBrain>();
    }

    void ResetRuntimeState()
    {
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
                    _agent.areaMask))
            {
                continue;
            }

            _probePath.ClearCorners();
            if (!_agent.CalculatePath(hit.position, _probePath) ||
                _probePath.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            if (_agent.SetPath(_probePath))
                return true;
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
        bool usesWorldSlow = _ctx == null || _ctx.UsesWorldSlow;
        if (usesWorldSlow && TimeSlowManager.Instance != null)
            return TimeSlowManager.Instance.WorldDeltaTime;

        return Time.deltaTime;
    }

    void ApplyDirectSpeedOverride(float configuredMoveSpeed)
    {
        float resolvedSpeed = configuredMoveSpeed;
        bool usesWorldSlow = _ctx == null || _ctx.UsesWorldSlow;
        if (usesWorldSlow && TimeSlowManager.Instance != null)
            resolvedSpeed *= TimeSlowManager.Instance.WorldTimeScale;

        _agent.speed = resolvedSpeed;
    }
}
