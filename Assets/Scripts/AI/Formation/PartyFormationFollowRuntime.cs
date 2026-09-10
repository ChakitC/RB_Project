using Opsive.BehaviorDesigner.Runtime;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Opsive.GraphDesigner.Runtime.Variables;
using UnityEngine;
using UnityEngine.AI;

internal sealed class PartyFormationFollowRuntime
{
    const float WarpFadeWindowFraction = 0.7f;
    const float MinimumWarpFadeSeconds = 0.06f;

    enum FormationWarpPhase
    {
        None,
        WaitingForWarpOutCastMoment,
        WaitingForWarpOutComplete,
        WaitingForWarpInComplete,
    }

    static int _nextFormationWarpRequestId = 1;

    NavMeshAgent _agent;
    AllyContext _allyContext;
    CharacterContextPartyLoader _partyLoader;
    FieldAllyMember _fieldMember;
    AllyInterruptionController _interruptionController;
    PartyFormationController _formation;
    BehaviorTree _behaviorTree;
    SharedVariable<bool> _inCombat;

    readonly CharacterPlaybackAutoHideSchedule _warpOutAutoHide = new();
    FormationWarpPhase _warpPhase;
    int _warpRequestId;
    Vector3 _pendingTeleportDestination;
    bool _ownsFieldMemberReservation;

    bool _ownsAgent;
    bool _movingToSlot;
    bool _savedStoppingDistance;
    float _previousStoppingDistance;
    float _nextDestinationUpdateTime;
    float _invalidPathSince = -1f;
    Vector3 _lastDestination;

    public bool IsAvailable => _formation != null;

    public bool TryBegin(GameObject ownerObject, GameObject playerObject)
    {
        CacheReferences(ownerObject, playerObject);
        return IsAvailable;
    }

    public bool TryTick(GameObject ownerObject, GameObject playerObject, out TaskStatus status)
    {
        CacheReferences(ownerObject, playerObject);
        if (_formation == null)
        {
            status = TaskStatus.Failure;
            return false;
        }

        if (_agent == null || !_agent.isActiveAndEnabled || _partyLoader == null)
        {
            status = TaskStatus.Failure;
            return true;
        }

        if (_warpPhase != FormationWarpPhase.None)
        {
            TickFormationWarp();
            status = TaskStatus.Running;
            return true;
        }

        if (ReadInCombat() || HasLiveSensorTarget())
        {
            ReleaseControl(stopAgent: true);
            status = TaskStatus.Failure;
            return true;
        }

        if (IsExternallyControlled())
        {
            ReleaseControl(stopAgent: false);
            status = TaskStatus.Running;
            return true;
        }

        if (!_agent.isOnNavMesh && !TryRecoverAgentOnNavMesh())
        {
            ReleaseControl(stopAgent: false);
            status = TaskStatus.Failure;
            return true;
        }

        int partyIndex = _partyLoader.PartyIndex;
        if (!PartyFormationController.IsCompanionPartyIndex(partyIndex) ||
            !_formation.TryGetSlotDestination(partyIndex, _agent, out Vector3 destination))
        {
            StopOwnedAgent();
            status = TaskStatus.Running;
            return true;
        }

        Vector3 actorPosition = _allyContext != null
            ? _allyContext.transform.position
            : _agent.transform.position;
        float distance = PlanarDistance(actorPosition, destination);

        if (ShouldTeleport(distance) &&
            _formation.TryGetTeleportDestination(partyIndex, _agent, out Vector3 teleportDestination))
        {
            if (TryStartAnimatedTeleport(teleportDestination))
            {
                status = TaskStatus.Running;
                return true;
            }

            if (!_agent.Warp(teleportDestination))
            {
                status = TaskStatus.Running;
                return true;
            }

            _agent.nextPosition = teleportDestination;
            _agent.ResetPath();
            _agent.isStopped = true;
            _invalidPathSince = -1f;
            _movingToSlot = false;
            _ownsAgent = false;
            status = TaskStatus.Running;
            return true;
        }

        if (_movingToSlot && distance <= _formation.StopDistance)
        {
            _movingToSlot = false;
            _invalidPathSince = -1f;
            StopOwnedAgent();
            RotateTowardFormation();
            status = TaskStatus.Running;
            return true;
        }

        if (!_movingToSlot)
        {
            if (distance <= _formation.StartMoveDistance)
            {
                StopOwnedAgent();
                RotateTowardFormation();
                status = TaskStatus.Running;
                return true;
            }

            _movingToSlot = true;
            _nextDestinationUpdateTime = 0f;
        }

        if (ShouldUpdateDestination(destination))
            UpdateDestination(destination);

        UpdateInvalidPathWatchdog();
        status = TaskStatus.Running;
        return true;
    }

    public void End()
    {
        AbortFormationWarp(cancelPlayback: true);

        if (!IsExternallyControlled())
            StopOwnedAgent();
        else
            ReleaseControl(stopAgent: false);

        RestoreStoppingDistance();
        _movingToSlot = false;
        _invalidPathSince = -1f;
    }

    public void Reset()
    {
        End();
        _agent = null;
        _allyContext = null;
        _partyLoader = null;
        _fieldMember = null;
        _interruptionController = null;
        _formation = null;
        _behaviorTree = null;
        _inCombat = null;
    }

    bool TryStartAnimatedTeleport(Vector3 teleportDestination)
    {
        CharacterAnimBrain animBrain = _allyContext != null ? _allyContext.AnimBrain : null;
        CharacterAnimDriver animDriver = _allyContext != null ? _allyContext.AnimDriver : null;
        if (animBrain == null || animDriver == null)
            return false;

        if (_fieldMember != null)
        {
            if (!_fieldMember.TryReserve(this))
                return false;

            _ownsFieldMemberReservation = true;
        }

        _pendingTeleportDestination = teleportDestination;
        _warpRequestId = NextFormationWarpRequestId();
        _warpPhase = FormationWarpPhase.WaitingForWarpOutCastMoment;
        SubscribeToWarpAnimation(animBrain);

        if (!animDriver.TryPlayChainUtilityWarpOut(_warpRequestId))
        {
            ClearFormationWarpState(revealVisual: false);
            return false;
        }

        StopOwnedAgent();

        CharacterVisibilityController visibility = _allyContext.Visibility;
        if (visibility != null)
        {
            visibility.Appear();
            _warpOutAutoHide.Start(_warpRequestId);
        }

        return true;
    }

    void TickFormationWarp()
    {
        if (HasBlockingStateDuringFormationWarp())
        {
            AbortFormationWarp(cancelPlayback: true);
            return;
        }

        CharacterAnimBrain animBrain = _allyContext != null ? _allyContext.AnimBrain : null;
        if (animBrain == null || !animBrain.IsChainPlaybackActive)
        {
            ClearFormationWarpState(revealVisual: true);
            return;
        }

        TickWarpOutAutoHide(animBrain);
    }

    void TickWarpOutAutoHide(CharacterAnimBrain animBrain)
    {
        if (_warpPhase != FormationWarpPhase.WaitingForWarpOutCastMoment ||
            !_warpOutAutoHide.IsPending)
        {
            return;
        }

        CharacterVisibilityController visibility = _allyContext != null ? _allyContext.Visibility : null;
        if (visibility == null)
        {
            _warpOutAutoHide.Cancel();
            return;
        }

        _warpOutAutoHide.Advance(visibility);
        if (!animBrain.TryGetActiveChainPlaybackTiming(
                _warpRequestId,
                out float remainingDuration,
                out float totalDuration))
        {
            return;
        }

        float timeUntilTeleport = remainingDuration;
        float fadeDuration = -1f;
        if (animBrain.TryGetActiveChainCastPointNormalized(
                _warpRequestId,
                out float castPointNormalized) &&
            float.IsFinite(totalDuration) &&
            totalDuration > 0f)
        {
            timeUntilTeleport = Mathf.Max(
                0f,
                remainingDuration - (1f - castPointNormalized) * totalDuration);
            float preWarpWindow = Mathf.Max(0f, castPointNormalized * totalDuration);
            fadeDuration = Mathf.Clamp(
                preWarpWindow * WarpFadeWindowFraction,
                MinimumWarpFadeSeconds,
                Mathf.Max(MinimumWarpFadeSeconds, preWarpWindow));
        }

        _warpOutAutoHide.TryBeginHide(
            visibility,
            timeUntilTeleport,
            totalDuration,
            fadeDuration);
    }

    void OnWarpCastMomentReached(int requestId)
    {
        if (requestId != _warpRequestId ||
            _warpPhase != FormationWarpPhase.WaitingForWarpOutCastMoment)
        {
            return;
        }

        _warpOutAutoHide.Cancel();
        _allyContext?.Visibility?.ConcealForTeleport();

        if (_agent == null ||
            !_agent.isActiveAndEnabled ||
            !_agent.isOnNavMesh ||
            !_agent.Warp(_pendingTeleportDestination))
        {
            AbortFormationWarp(cancelPlayback: true);
            return;
        }

        _agent.nextPosition = _pendingTeleportDestination;
        _agent.ResetPath();
        _agent.isStopped = true;
        _invalidPathSince = -1f;
        _movingToSlot = false;
        _ownsAgent = false;
        _warpPhase = FormationWarpPhase.WaitingForWarpOutComplete;
    }

    void OnWarpPlaybackCompleted(int requestId)
    {
        if (requestId != _warpRequestId)
            return;

        if (_warpPhase == FormationWarpPhase.WaitingForWarpOutComplete)
        {
            StartWarpInOrComplete();
            return;
        }

        if (_warpPhase == FormationWarpPhase.WaitingForWarpInComplete)
            ClearFormationWarpState(revealVisual: false);
        else
            AbortFormationWarp(cancelPlayback: false);
    }

    void OnWarpPlaybackInterrupted(int requestId)
    {
        if (requestId == _warpRequestId)
            ClearFormationWarpState(revealVisual: true);
    }

    void StartWarpInOrComplete()
    {
        CharacterAnimDriver animDriver = _allyContext != null ? _allyContext.AnimDriver : null;
        if (animDriver == null)
        {
            ClearFormationWarpState(revealVisual: true);
            return;
        }

        _warpRequestId = NextFormationWarpRequestId();
        _warpPhase = FormationWarpPhase.WaitingForWarpInComplete;
        if (!animDriver.TryPlayChainUtilityWarpIn(_warpRequestId))
        {
            ClearFormationWarpState(revealVisual: true);
            return;
        }

        _allyContext?.Visibility?.Appear();
    }

    void AbortFormationWarp(bool cancelPlayback)
    {
        if (_warpPhase == FormationWarpPhase.None)
            return;

        int requestId = _warpRequestId;
        CharacterAnimDriver animDriver = _allyContext != null ? _allyContext.AnimDriver : null;
        ClearFormationWarpState(revealVisual: true);

        if (cancelPlayback && requestId > 0)
            animDriver?.CancelChainPlaybackRequest(requestId);
    }

    void ClearFormationWarpState(bool revealVisual)
    {
        CharacterAnimBrain animBrain = _allyContext != null ? _allyContext.AnimBrain : null;
        UnsubscribeFromWarpAnimation(animBrain);
        _warpOutAutoHide.Cancel();
        _warpPhase = FormationWarpPhase.None;
        _warpRequestId = 0;
        _pendingTeleportDestination = Vector3.zero;

        if (_ownsFieldMemberReservation)
        {
            _fieldMember?.ReleaseReservation(this);
            _ownsFieldMemberReservation = false;
        }

        if (revealVisual)
            _allyContext?.Visibility?.Appear();
    }

    void SubscribeToWarpAnimation(CharacterAnimBrain animBrain)
    {
        if (animBrain == null)
            return;

        animBrain.ChainCastMomentReached += OnWarpCastMomentReached;
        animBrain.ChainPlaybackInterrupted += OnWarpPlaybackInterrupted;
        animBrain.ChainPlaybackCompleted += OnWarpPlaybackCompleted;
    }

    void UnsubscribeFromWarpAnimation(CharacterAnimBrain animBrain)
    {
        if (animBrain == null)
            return;

        animBrain.ChainCastMomentReached -= OnWarpCastMomentReached;
        animBrain.ChainPlaybackInterrupted -= OnWarpPlaybackInterrupted;
        animBrain.ChainPlaybackCompleted -= OnWarpPlaybackCompleted;
    }

    bool HasBlockingStateDuringFormationWarp()
    {
        if (ReadInCombat() || HasLiveSensorTarget() || _allyContext == null)
            return true;

        StateHub stateHub = _allyContext.stateHub;
        if (stateHub != null && (!stateHub.IsAlive || stateHub.Isdown || !stateHub.CanMove()))
            return true;

        if (_allyContext.SkillManager != null && _allyContext.SkillManager.TryGetActiveCast(out _))
            return true;

        if (_fieldMember != null && _fieldMember.IsInKnockback)
            return true;

        return _interruptionController != null && _interruptionController.IsExecuting;
    }

    static int NextFormationWarpRequestId()
    {
        if (_nextFormationWarpRequestId == int.MaxValue)
            _nextFormationWarpRequestId = 1;

        return _nextFormationWarpRequestId++;
    }

    void CacheReferences(GameObject ownerObject, GameObject playerObject)
    {
        if (ownerObject == null)
            return;

        if (_allyContext == null)
            _allyContext = ownerObject.GetComponentInParent<AllyContext>();
        if (_allyContext == null)
            _allyContext = ownerObject.GetComponentInChildren<AllyContext>(true);

        _allyContext?.ResolveReferences();

        if (_agent == null)
            _agent = _allyContext != null ? _allyContext.agent : ownerObject.GetComponentInParent<NavMeshAgent>();
        if (_partyLoader == null)
            _partyLoader = _allyContext != null ? _allyContext.CharacterLoad : ownerObject.GetComponentInParent<CharacterContextPartyLoader>();
        if (_fieldMember == null)
            _fieldMember = ownerObject.GetComponentInParent<FieldAllyMember>();
        if (_interruptionController == null)
            _interruptionController = ownerObject.GetComponentInParent<AllyInterruptionController>();
        if (_behaviorTree == null)
            _behaviorTree = _allyContext != null ? _allyContext.BehaviorTree : ownerObject.GetComponentInParent<BehaviorTree>();

        if (_inCombat == null && _behaviorTree != null)
        {
            _inCombat = _behaviorTree.GetVariable<bool>(
                "InCombat",
                SharedVariable.SharingScope.GameObject);
        }

        if (_formation == null && _allyContext != null && playerObject != null)
        {
            PlayerContext playerContext = playerObject.GetComponentInParent<PlayerContext>();
            if (playerContext == null)
                playerContext = playerObject.GetComponentInChildren<PlayerContext>(true);

            playerContext?.ResolveReferences();
            _formation = playerContext != null ? playerContext.partyFormation : null;
        }
    }

    bool ReadInCombat()
    {
        return _inCombat != null && _inCombat.Value;
    }

    bool HasLiveSensorTarget()
    {
        AITargetSensor sensor = _allyContext != null ? _allyContext.AITargetSensor : null;
        return sensor != null && sensor.HasLiveTarget;
    }

    bool IsExternallyControlled()
    {
        if (_allyContext == null)
            return true;

        StateHub stateHub = _allyContext.stateHub;
        if (stateHub != null && (!stateHub.IsAlive || stateHub.Isdown || !stateHub.CanMove()))
            return true;

        CharacterAnimBrain animBrain = _allyContext.AnimBrain;
        if (animBrain != null && (animBrain.RootMotionActive || animBrain.IsSkillPlaybackActive))
            return true;

        if (_allyContext.SkillManager != null && _allyContext.SkillManager.TryGetActiveCast(out _))
            return true;

        if (_fieldMember != null && (_fieldMember.IsBusy || _fieldMember.IsReserved || _fieldMember.IsInKnockback))
            return true;

        return _interruptionController != null && _interruptionController.IsExecuting;
    }

    bool ShouldTeleport(float distance)
    {
        if (IsExternallyControlled())
            return false;

        if (distance > _formation.TeleportDistance)
            return true;

        return _invalidPathSince >= 0f &&
               Time.time - _invalidPathSince >= _formation.InvalidPathTimeout;
    }

    bool ShouldUpdateDestination(Vector3 destination)
    {
        if (Time.time < _nextDestinationUpdateTime)
            return false;

        float threshold = _formation.DestinationChangeThreshold;
        return !_ownsAgent ||
               (_lastDestination - destination).sqrMagnitude >= threshold * threshold ||
               !_agent.hasPath;
    }

    void UpdateDestination(Vector3 destination)
    {
        CaptureStoppingDistance();
        _agent.stoppingDistance = _formation.StopDistance;
        _agent.isStopped = false;

        _ownsAgent = _agent.SetDestination(destination);
        _lastDestination = destination;
        _nextDestinationUpdateTime = Time.time + _formation.DestinationUpdateInterval;

        if (!_ownsAgent && _invalidPathSince < 0f)
            _invalidPathSince = Time.time;
    }

    void UpdateInvalidPathWatchdog()
    {
        if (!_ownsAgent || _agent.pathPending)
            return;

        if (_agent.hasPath && _agent.pathStatus == NavMeshPathStatus.PathComplete)
        {
            _invalidPathSince = -1f;
            return;
        }

        if (_invalidPathSince < 0f)
            _invalidPathSince = Time.time;
    }

    void StopOwnedAgent()
    {
        if (_ownsAgent && _agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
            _agent.isStopped = true;

        _ownsAgent = false;
    }

    void ReleaseControl(bool stopAgent)
    {
        if (stopAgent)
        {
            StopOwnedAgent();
            RestoreStoppingDistance();
        }
        else
        {
            _ownsAgent = false;
            _savedStoppingDistance = false;
        }

        _movingToSlot = false;
        _invalidPathSince = -1f;
    }

    void CaptureStoppingDistance()
    {
        if (_savedStoppingDistance || _agent == null)
            return;

        _previousStoppingDistance = _agent.stoppingDistance;
        _savedStoppingDistance = true;
    }

    void RestoreStoppingDistance()
    {
        if (!_savedStoppingDistance || _agent == null || !_agent.isActiveAndEnabled)
            return;

        _agent.stoppingDistance = _previousStoppingDistance;
        _savedStoppingDistance = false;
    }

    bool TryRecoverAgentOnNavMesh()
    {
        if (_agent == null || !_agent.isActiveAndEnabled)
            return false;

        if (!NavMesh.SamplePosition(_agent.transform.position, out NavMeshHit hit, 2f, _agent.areaMask))
            return false;

        return _agent.Warp(hit.position);
    }

    void RotateTowardFormation()
    {
        if (_allyContext == null ||
            _allyContext.stateHub == null ||
            !_allyContext.stateHub.CanRotate() ||
            (_allyContext.AimTargetDriver != null && _allyContext.AimTargetDriver.HasOverride))
        {
            return;
        }

        Vector3 forward = _formation.FormationForward;
        if (forward.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(forward, Vector3.up);
        float deltaTime = _allyContext.UsesWorldSlow && TimeSlowManager.Instance != null
            ? TimeSlowManager.Instance.WorldDeltaTime
            : Time.deltaTime;
        _allyContext.transform.rotation = Quaternion.RotateTowards(
            _allyContext.transform.rotation,
            targetRotation,
            _formation.HeadingTurnSpeed * deltaTime);
    }

    static float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
