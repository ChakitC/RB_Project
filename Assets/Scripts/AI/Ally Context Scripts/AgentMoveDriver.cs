using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class AgentMoveDriver : MonoBehaviour
{
    [SerializeField] NavMeshAgent agent;
    [SerializeField] StateHub stateHub;
    [SerializeField] CharacteContext ctx;
    [SerializeField] CharacterController characterController;

    [Header("Ramp")]
    [SerializeField] float rampUpTime = 0.12f;
    [SerializeField] float rampDownTime = 0.18f;

    [Header("IsMoving (stable)")]
    [SerializeField] float move01Start = 0.10f;
    [SerializeField] float move01Stop = 0.05f;
    [SerializeField] float enterDelay = 0.06f;
    [SerializeField] float exitDelay = 0.10f;

    [Header("Player Separation")]
    [SerializeField] bool separateCompanionFromPlayer = true;
    [SerializeField, Min(0f)] float playerKeepoutRadius = 0.65f;
    [SerializeField, Min(0f)] float separationMargin = 0.1f;
    [SerializeField, Min(0.01f)] float separationSpeed = 12f;
    [SerializeField, Min(0f)] float separationPauseDuration = 0.1f;
    [SerializeField, Min(0f)] float separationNavMeshSampleRadius = 0.75f;
    [SerializeField, Min(0.05f)] float playerResolveInterval = 0.5f;

    float _move01;
    float _aboveT;
    float _belowT;
    CharacteContext _playerContext;
    float _nextPlayerResolveTime;
    bool _separationPauseActive;
    bool _restoreAgentStopped;
    float _separationPauseUntil;
    readonly Dictionary<int, float> _moveSpeedOverrides = new();
    int _nextMoveSpeedOverrideToken;
    int _activeMoveSpeedOverrideToken;
    float _activeMoveSpeedOverride;
    Transform ActorTransform => ctx != null ? ctx.transform : transform;

    public bool agentismoving;

    void Awake()
    {
        if (!agent)
        {
            agent = GetComponent<NavMeshAgent>();
            if (!agent)
                agent = GetComponentInParent<NavMeshAgent>();
        }

        if (!stateHub)
        {
            stateHub = GetComponent<StateHub>();
            if (!stateHub)
                stateHub = GetComponentInParent<StateHub>();
        }

        if (!ctx)
        {
            ctx = GetComponent<CharacteContext>();
            if (!ctx)
                ctx = GetComponentInParent<CharacteContext>();
        }

        ctx?.ResolveReferences();

        if (!characterController)
            characterController = ctx != null ? ctx.cc : null;
        if (!characterController)
            characterController = GetComponent<CharacterController>();
        if (!characterController)
            characterController = GetComponentInParent<CharacterController>();
    }

    void Update()
    {
        TickPlayerSeparation();
        UpdateAIMoveAnimFromNavMesh(agent);
        TickSeparationPause();
    }

    public int AcquireMoveSpeedOverride(float moveSpeed)
    {
        if (moveSpeed <= 0f)
            return 0;

        int token = NextMoveSpeedOverrideToken();
        _moveSpeedOverrides[token] = moveSpeed;
        _activeMoveSpeedOverrideToken = token;
        _activeMoveSpeedOverride = moveSpeed;
        ApplyResolvedMoveSpeed();
        return token;
    }

    public void ReleaseMoveSpeedOverride(int token)
    {
        if (token == 0 || !_moveSpeedOverrides.Remove(token))
            return;

        if (token == _activeMoveSpeedOverrideToken)
            ResolveLatestMoveSpeedOverride();

        ApplyResolvedMoveSpeed();
    }

    void UpdateAIMoveAnimFromNavMesh(NavMeshAgent agent)
    {
        if (!agent || !agent.enabled || !agent.isOnNavMesh)
        {
            agentismoving = false;
            _move01 = 0f;
            stateHub?.SetMoveSpeed01(0f);
            return;
        }

        float dt = UsesWorldSlow() ? TimeSlowManager.Instance.WorldDeltaTime : Time.deltaTime;

        ApplyResolvedMoveSpeed();

        Vector3 vel = agent.velocity;
        vel.y = 0f;

        Vector3 desired = agent.desiredVelocity;
        desired.y = 0f;

        bool arrived = !agent.pathPending &&
                       agent.remainingDistance <= agent.stoppingDistance + 0.02f &&
                       (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f);

        Vector3 moveWorldDir;
        float raw01;

        if (arrived)
        {
            moveWorldDir = Vector3.zero;
            raw01 = 0f;
        }
        else
        {
            Vector3 dirSrc = vel.sqrMagnitude > 0.0001f ? vel : desired;
            moveWorldDir = dirSrc.sqrMagnitude > 0.0001f ? dirSrc.normalized : Vector3.zero;

            float speedNow = vel.sqrMagnitude > 0.0001f ? vel.magnitude : desired.magnitude;
            float denom = Mathf.Max(agent.speed, 0.001f);
            raw01 = Mathf.Clamp01(speedNow / denom);
        }

        float upSpeed = rampUpTime <= 0.0001f ? 999f : 1f / rampUpTime;
        float downSpeed = rampDownTime <= 0.0001f ? 999f : 1f / rampDownTime;

        float target01 = raw01;
        float s = target01 > _move01 ? upSpeed : downSpeed;
        _move01 = Mathf.MoveTowards(_move01, target01, s * dt);

        stateHub?.SetMoveSpeed01(_move01);

        bool wantMove = !arrived && _move01 > move01Start && agent.hasPath && !agent.isStopped && !agent.pathPending;
        bool wantStop = arrived || _move01 < move01Stop || agent.isStopped || (!agent.hasPath && !agent.pathPending);

        if (!agentismoving)
        {
            _aboveT = wantMove ? _aboveT + dt : 0f;
            if (_aboveT >= enterDelay)
            {
                agentismoving = true;
                _aboveT = 0f;
            }
        }
        else
        {
            _belowT = wantStop ? _belowT + dt : 0f;
            if (_belowT >= exitDelay)
            {
                agentismoving = false;
                _belowT = 0f;
            }
        }

        if (stateHub != null)
        {
            if (moveWorldDir.sqrMagnitude < 0.0001f)
            {
                stateHub.SetMoveDirLocal(Vector2.zero);
            }
            else
            {
                Vector3 moveLocal3 = transform.InverseTransformDirection(moveWorldDir);
                Vector2 dirLocal = new Vector2(moveLocal3.x, moveLocal3.z);
                if (dirLocal.sqrMagnitude > 1.0001f)
                    dirLocal.Normalize();

                stateHub.SetMoveDirLocal(dirLocal);
            }
        }
    }

    bool UsesWorldSlow()
    {
        return ctx == null || ctx.UsesWorldSlow;
    }

    int NextMoveSpeedOverrideToken()
    {
        do
        {
            _nextMoveSpeedOverrideToken++;
            if (_nextMoveSpeedOverrideToken <= 0)
                _nextMoveSpeedOverrideToken = 1;
        }
        while (_moveSpeedOverrides.ContainsKey(_nextMoveSpeedOverrideToken));

        return _nextMoveSpeedOverrideToken;
    }

    void ResolveLatestMoveSpeedOverride()
    {
        _activeMoveSpeedOverrideToken = 0;
        _activeMoveSpeedOverride = 0f;

        foreach (KeyValuePair<int, float> pair in _moveSpeedOverrides)
        {
            if (pair.Key <= _activeMoveSpeedOverrideToken)
                continue;

            _activeMoveSpeedOverrideToken = pair.Key;
            _activeMoveSpeedOverride = pair.Value;
        }
    }

    void ApplyResolvedMoveSpeed()
    {
        if (agent == null || !agent.enabled)
            return;

        float targetSpeed;
        if (_activeMoveSpeedOverrideToken != 0)
            targetSpeed = _activeMoveSpeedOverride;
        else if (ctx != null)
            targetSpeed = ctx.GetMoveSpeedForCurrentLifeState();
        else
            return;

        if (UsesWorldSlow() && TimeSlowManager.Instance != null)
            targetSpeed *= TimeSlowManager.Instance.WorldTimeScale;

        if (!Mathf.Approximately(agent.speed, targetSpeed))
            agent.speed = targetSpeed;
    }

    void TickPlayerSeparation()
    {
        if (!separateCompanionFromPlayer || ctx == null || ctx.TargetIdentity != AITargetIdentity.Companion)
            return;
        if (ctx.AnimBrain != null && ctx.AnimBrain.RootMotionActive)
            return;

        CharacteContext player = ResolvePlayerContext();
        if (player == null)
            return;

        Transform playerTransform = player.transform;
        Transform actorTransform = ActorTransform;
        Vector3 currentPosition = actorTransform.position;
        Vector3 playerPosition = playerTransform.position;
        Vector3 away = currentPosition - playerPosition;
        away.y = 0f;

        float selfRadius = ResolveSelfRadius();
        float keepoutRadius = ResolvePlayerKeepoutRadius(player) + selfRadius + separationMargin;
        float keepoutSqr = keepoutRadius * keepoutRadius;
        float awaySqr = away.sqrMagnitude;

        if (awaySqr >= keepoutSqr)
            return;

        float distance = Mathf.Sqrt(awaySqr);
        Vector3 direction = distance > 0.001f ? away / distance : ResolveFallbackSeparationDirection(playerTransform);
        float penetration = keepoutRadius - distance;
        float dt = UsesWorldSlow() ? TimeSlowManager.Instance.WorldDeltaTime : Time.deltaTime;
        float moveDistance = Mathf.Min(penetration, separationSpeed * Mathf.Max(dt, 0.0001f));
        Vector3 targetPosition = currentPosition + direction * moveDistance;

        if (agent != null && agent.enabled && NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, separationNavMeshSampleRadius, agent.areaMask))
            targetPosition = hit.position;

        Vector3 delta = targetPosition - currentPosition;
        if (delta.sqrMagnitude <= 0.000001f)
            return;

        BeginSeparationPause();

        if (characterController != null && characterController.enabled)
            characterController.Move(delta);
        else
            actorTransform.position += delta;

        Physics.SyncTransforms();
        SyncAgentToTransform();
    }

    CharacteContext ResolvePlayerContext()
    {
        if (_playerContext != null && _playerContext.isActiveAndEnabled)
            return _playerContext;

        if (Time.time < _nextPlayerResolveTime)
            return null;

        _nextPlayerResolveTime = Time.time + playerResolveInterval;

        CharacteContext[] contexts = FindObjectsByType<CharacteContext>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < contexts.Length; i++)
        {
            CharacteContext candidate = contexts[i];
            if (candidate != null && candidate.TargetIdentity == AITargetIdentity.Player)
            {
                _playerContext = candidate;
                return _playerContext;
            }
        }

        return null;
    }

    float ResolveSelfRadius()
    {
        float radius = agent != null ? agent.radius : 0f;
        if (characterController != null)
            radius = Mathf.Max(radius, characterController.radius);

        return Mathf.Max(radius, 0.01f);
    }

    float ResolvePlayerKeepoutRadius(CharacteContext player)
    {
        float radius = Mathf.Max(playerKeepoutRadius, 0f);
        if (player != null)
        {
            CharacterController playerController = player.cc;
            if (playerController != null)
                radius = Mathf.Max(radius, playerController.radius);

            NavMeshObstacle obstacle = player.GetComponent<NavMeshObstacle>();
            if (obstacle != null)
                radius = Mathf.Max(radius, obstacle.radius);
        }

        return Mathf.Max(radius, 0.01f);
    }

    Vector3 ResolveFallbackSeparationDirection(Transform playerTransform)
    {
        Vector3 direction = playerTransform != null ? -playerTransform.forward : -transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;

        return direction.normalized;
    }

    void BeginSeparationPause()
    {
        if (agent == null || !agent.enabled || separationPauseDuration <= 0f)
            return;

        if (!_separationPauseActive)
        {
            _restoreAgentStopped = agent.isStopped;
            _separationPauseActive = true;
        }

        _separationPauseUntil = Time.time + separationPauseDuration;
        agent.isStopped = true;
    }

    void TickSeparationPause()
    {
        if (!_separationPauseActive || Time.time < _separationPauseUntil)
            return;

        if (agent != null && agent.enabled)
            agent.isStopped = _restoreAgentStopped;

        _separationPauseActive = false;
    }

    void SyncAgentToTransform()
    {
        if (agent == null || !agent.enabled)
            return;

        if (agent.isOnNavMesh)
        {
            agent.nextPosition = ActorTransform.position;
            return;
        }

        if (NavMesh.SamplePosition(ActorTransform.position, out NavMeshHit hit, separationNavMeshSampleRadius, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            agent.nextPosition = hit.position;
        }
    }
}
