using System;
using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInterruptionController : MonoBehaviour
{
    enum State { Idle, Reserved, Warping, Casting, Impact, Completed, Cancelled }

    const float RootMotionImpactPositionTolerance = 0.05f;
    const float RootMotionImpactYawTolerance = 2f;

    [Header("Refs")]
    [SerializeField] private PlayerContext playerContext;

    [Header("Skill")]
    [SerializeField] private CharacterSkillEntry interruptionSkill;
    [SerializeField, AssetsOnly] private ChainAttackTeleportProfileDef teleportProfile;

    [Header("Knockback")]
    [SerializeField, Min(0.01f)] private float knockbackDistance = 2f;
    [SerializeField, Min(0.01f)] private float knockbackDuration = 0.4f;
    [SerializeField] private ImpactReactionKind knockbackReaction = ImpactReactionKind.None;
    [SerializeField] private bool knockbackInterruptActions;
    [SerializeField] private AnimationCurve knockbackProgressCurve;

    [Header("Timeout")]
    [SerializeField, Min(0.1f)] private float impactTimeoutSeconds = 2f;

    [Header("Debug")]
    [SerializeField] private bool logInterruptionFlow;

    State _state;
    InterruptionTargetContext _target;
    PreCastBlockReservation _blockReservation;
    int _activeRequestId;
    float _timeoutTimer;

    CharacterAnimBrain _animBrain;
    CharacterAnimDriver _animDriver;
    CharacterSkillManager _skillManager;
    PlayerMovementCC _movement;
    Transform _actorTransform;
    ASPHelperDitherFader _actorFader;

    bool _movementWasEnabled;
    bool _movementSuspended;
    bool _visualHiddenForSnap;
    bool _blockCompletedSuccessfully;
    bool _guaranteeCommitted;
    bool _blockCompletionAttempted;
    bool _impactReached;
    bool _hitLagFired;
    bool _hitLagPending;
    bool _cleanupRunning;
    ReservedBlockResult _blockCompletionResult;
    TargetedSkillPlacementResult _placementResult;
    Vector3 _originalActorPosition;
    Quaternion _originalActorRotation;
    bool _hasOriginalActorPose;

    public bool IsExecuting => _state != State.Idle;

    void Awake()
    {
        ResolveRefs();
    }

    void OnDisable()
    {
        if (_state == State.Impact)
        {
            LogFlow("disabled after impact; completing");
            CompleteAndRestore();
        }
        else if (_state != State.Idle && _guaranteeCommitted)
            RunGuaranteedFallback("Disabled");
        else if (_state != State.Idle)
            RollbackBeforeGuarantee("Disabled");
    }

    void Update()
    {
        if (_state != State.Casting) return;

        _timeoutTimer -= Time.deltaTime;
        if (_timeoutTimer <= 0f)
            RunGuaranteedFallback("ImpactTimeout");
    }

    public bool IsReadyForInterruption()
    {
        ResolveRefs();
        if (playerContext == null) return false;
        if (playerContext.HealthSystem == null || !playerContext.HealthSystem.IsAlive) return false;
        if (_state != State.Idle) return false;
        if (interruptionSkill == null || teleportProfile == null) return false;
        if (_skillManager == null) return false;
        return _skillManager.CanStartExternalSkill(interruptionSkill, ignoreResourceCosts: true);
    }

    public bool TryResolvePlacement(
        Transform targetAnchor,
        Transform targetRoot,
        out TargetedSkillPlacementResult result)
    {
        return TryResolvePlacement(targetAnchor, targetRoot, 0f, 0f, out result);
    }

    public bool TryResolvePlacement(
        Transform targetAnchor,
        Transform targetRoot,
        float noWarpStartDistance,
        float noWarpTargetDistance,
        out TargetedSkillPlacementResult result)
    {
        ResolveRefs();
        if (teleportProfile == null)
        {
            result = TargetedSkillPlacementResult.Failed("Teleport profile is missing.");
            return false;
        }

        SkillGemDefinition skillDef = interruptionSkill != null ? interruptionSkill.skillAsset : null;
        if (skillDef == null)
        {
            result = TargetedSkillPlacementResult.Failed("Interruption skill asset is missing.");
            return false;
        }

        if (_animBrain == null)
        {
            result = TargetedSkillPlacementResult.Failed("CharacterAnimBrain is missing.");
            return false;
        }

        if (!_animBrain.TryResolveSkillAnimationClip(skillDef, out AnimationClip attackClip) ||
            attackClip == null)
        {
            result = TargetedSkillPlacementResult.Failed(
                $"Skill '{skillDef.name}' has no resolvable animation clip.");
            return false;
        }

        if (!_animBrain.TryGetRootMotionSamplingAnimator(out Animator samplingAnimator))
        {
            result = TargetedSkillPlacementResult.Failed("Root-motion sampling Animator is missing.");
            return false;
        }

        float impactNormalized = ResolveInterruptionImpactNormalized(skillDef);
        Collider probeCollider = playerContext != null && playerContext.ColliderRefs != null
            ? playerContext.ColliderRefs.CharacterPositionCollider
            : null;

        bool resolved = TargetedSkillPlacementResolver.TryResolve(
            teleportProfile,
            targetAnchor,
            _actorTransform != null ? _actorTransform.rotation : Quaternion.identity,
            attackClip,
            samplingAnimator,
            impactNormalized,
            faceTarget: true,
            teleportProfile.requireNavMeshAtAnchor,
            teleportProfile.navMeshSampleDistance,
            probeCollider,
            _actorTransform,
            targetRoot,
            out result,
            preferredActorPosition: _actorTransform != null ? _actorTransform.position : (Vector3?)null,
            noWarpStartDistance: noWarpStartDistance,
            noWarpTargetDistance: noWarpTargetDistance,
            reservations: CharacterPlacementReservationRegistry.Shared);

        if (resolved)
        {
            LogFlow(
                $"placement accepted mode={result.Mode} yaw={result.AcceptedYaw:0.###} " +
                $"start={result.StartPosition} impact={result.ImpactPosition} impactN={impactNormalized:0.###}");
        }
        else
            LogFlow($"placement failed reason='{result.FailureReason}'", warning: true);

        return resolved;
    }

    static float ResolveInterruptionImpactNormalized(SkillGemDefinition skillDef)
    {
        if (skillDef == null)
            return 0.35f;

        AnimancerEvent.Sequence events = skillDef.skillClip != null ? skillDef.skillClip.Events : null;
        StringReference hitStartName = CombatTimelineEventNames.ToStringReference(CombatTimelineEventName.HitStart);
        if (events != null && hitStartName != null)
        {
            int index = events.IndexOf(hitStartName);
            if (index >= 0)
                return Mathf.Clamp(events[index].normalizedTime, 0f, 0.999f);
        }

        return skillDef.GetCastPointNormalized();
    }

    public bool BeginInterruption(
        InterruptionTargetContext target,
        PreCastBlockReservation blockReservation,
        TargetedSkillPlacementResult placementResult)
    {
        ResolveRefs();
        if (_state != State.Idle)
        {
            LogFlow($"begin rejected reason=StateNotIdle state={_state}", warning: true);
            return false;
        }

        if (!blockReservation.IsValid || !placementResult.IsValid)
        {
            LogFlow("begin rejected reason=InvalidReservationOrPlacement", warning: true);
            return false;
        }

        _target = target;
        _blockReservation = blockReservation;
        _placementResult = placementResult;
        _blockCompletedSuccessfully = false;
        _guaranteeCommitted = false;
        _blockCompletionAttempted = false;
        _blockCompletionResult = default;
        _impactReached = false;
        _hitLagFired = false;
        _hitLagPending = false;
        CacheOriginalActorPose();
        _state = State.Reserved;
        LogFlow(
            $"begin target='{ResolveName(target.Transform)}' targetRequestId={blockReservation.RequestId} " +
            $"reservationId={blockReservation.ReservationId} mode={placementResult.Mode} " +
            $"startPosition={placementResult.StartPosition}");

        SuspendPlayerMovement();
        if (placementResult.RequiresPositionSnap)
            HideVisualForSnap();
        ApplyActorPose(placementResult.StartPosition, placementResult.StartRotation);
        if (!placementResult.UsesRootMotion)
            FaceTarget();
        _state = State.Warping;
        LogFlow(
            $"snap applied targetRequestId={blockReservation.RequestId} " +
            $"reservationId={blockReservation.ReservationId} position={placementResult.StartPosition}");

        if (_skillManager == null)
        {
            LogFlow("skill start rejected reason=MissingSkillManager", warning: true);
            RollbackBeforeGuarantee("MissingSkillManager");
            return false;
        }

        var result = _skillManager.TryStartExternalSkill(
            interruptionSkill,
            "interruption-player",
            CombatTimelineEventName.HitStart,
            usePlanarRootMotion: placementResult.UsesRootMotion,
            ignoreResourceCosts: true,
            stampCooldown: false);
        if (!result.Started)
        {
            LogFlow($"skill start rejected kind={result.Kind}", warning: true);
            RollbackBeforeGuarantee($"SkillStart:{result.Kind}");
            return false;
        }

        _activeRequestId = result.RequestId;
        _guaranteeCommitted = true;
        _state = State.Casting;
        _timeoutTimer = impactTimeoutSeconds;
        LogFlow(
            $"skill started kind={result.Kind} playerRequestId={result.RequestId} targetRequestId={blockReservation.RequestId} reservationId={blockReservation.ReservationId}");
        BeginVisualFadeIn();

        if (_animBrain != null)
        {
            _animBrain.SkillTimelineEventRaised += OnTimelineEvent;
            _animBrain.PlaybackEvent += OnPlaybackEvent;
        }

        return true;
    }

    void OnTimelineEvent(int requestId, CombatTimelineEventName eventName)
    {
        if (_state != State.Casting && _state != State.Impact) return;
        if (requestId != _activeRequestId) return;

        if (eventName == CombatTimelineEventName.HitStart && _state == State.Casting)
        {
            LogFlow(
                $"timeline event={eventName} playerRequestId={requestId} targetRequestId={_blockReservation.RequestId} reservationId={_blockReservation.ReservationId}");
            ValidateRootMotionImpactPose();
            OnImpact();
            return;
        }

        if (eventName == CombatTimelineEventName.HitLag)
        {
            if (_impactReached && _blockCompletedSuccessfully && !_hitLagFired)
                TriggerHitLag();
            else if (!_hitLagFired)
                _hitLagPending = true;
        }
    }

    void ValidateRootMotionImpactPose()
    {
        if (!_placementResult.UsesRootMotion || _actorTransform == null)
            return;

        Vector3 actualPosition = _actorTransform.position;
        Vector3 expectedPosition = _placementResult.ImpactPosition;
        actualPosition.y = 0f;
        expectedPosition.y = 0f;

        float positionError = Vector3.Distance(actualPosition, expectedPosition);
        float yawError = Mathf.Abs(Mathf.DeltaAngle(
            _actorTransform.rotation.eulerAngles.y,
            _placementResult.ImpactRotation.eulerAngles.y));

        if (positionError <= RootMotionImpactPositionTolerance &&
            yawError <= RootMotionImpactYawTolerance)
        {
            LogFlow(
                $"root-motion impact matched sampled trajectory positionError={positionError:0.####}m yawError={yawError:0.###}deg");
            return;
        }

        Debug.LogWarning(
            $"[PreCast.Player] Root-motion impact drifted from the sampled trajectory for '{name}' " +
            $"(positionError={positionError:0.####}m, yawError={yawError:0.###}deg).",
            this);
    }

    void OnImpact()
    {
        _state = State.Impact;
        _impactReached = true;

        ReservedBlockResult blockResult = CompleteReservedBlockOnce("HitStart");
        if (blockResult == ReservedBlockResult.Success)
        {
            ApplyImpactKnockback();

            if (_hitLagPending && !_hitLagFired)
                TriggerHitLag();
        }
    }

    void TriggerHitLag()
    {
        _hitLagFired = true;
        _hitLagPending = false;

        SkillGemDefinition skillDef = interruptionSkill != null ? interruptionSkill.skillAsset : null;
        if (skillDef == null || !skillDef.HasHitLag)
            return;

        GlobalTimeScaleManager.Instance.RequestHitLag(skillDef.HitLagDuration, skillDef.HitLagTimeScale, skillDef.HitLagShape);
        LogFlow(
            $"hitlag fired duration={skillDef.HitLagDuration:0.###}s scale={skillDef.HitLagTimeScale:0.###}");
    }

    void ApplyImpactKnockback()
    {
        bool targetAlive = _target.Health != null && _target.Health.IsAlive;
        if (targetAlive && _target.Knockback != null && _actorTransform != null && _target.Transform != null)
        {
            var kb = KnockbackData.FromOrigin(
                _actorTransform.position,
                _target.Transform.position,
                knockbackDistance,
                knockbackDuration,
                knockbackReaction,
                knockbackInterruptActions,
                knockbackProgressCurve);

            if (kb.IsValid)
                _target.Knockback.ApplyKnockback(kb, forceReplace: true);
        }
    }

    void OnPlaybackEvent(CharacterAnimBrain.PlaybackSignal signal)
    {
        if (signal.Kind != CharacterAnimBrain.PlaybackKind.Skill ||
            signal.RequestId != _activeRequestId)
        {
            return;
        }

        if (signal.Phase == CharacterAnimBrain.PlaybackPhase.Interrupted)
        {
            if (_state == State.Casting)
                RunGuaranteedFallback("SkillInterrupted");
            else if (_state == State.Impact)
                CompleteAndRestore();
            return;
        }

        if (signal.Phase != CharacterAnimBrain.PlaybackPhase.Completed)
            return;

        if (_state == State.Casting)
        {
            RunGuaranteedFallback("SkillCompletedBeforeHitStart");
            return;
        }

        if (_state == State.Impact)
            CompleteAndRestore();
    }

    void CompleteAndRestore()
    {
        _state = State.Completed;
        LogFlow(
            $"completed playerRequestId={_activeRequestId} targetRequestId={_blockReservation.RequestId} reservationId={_blockReservation.ReservationId}");
        Cleanup();
    }

    ReservedBlockResult CompleteReservedBlockOnce(string source)
    {
        if (_blockCompletionAttempted)
            return _blockCompletionResult;

        _blockCompletionAttempted = true;
        ReservedBlockResult result = _blockReservation.IsValid &&
                                     _blockReservation.Controller != null
            ? _blockReservation.Controller.CompleteReservedBlock(_blockReservation)
            : ReservedBlockResult.InvalidReservation;
        _blockCompletionResult = result;
        _blockCompletedSuccessfully = result == ReservedBlockResult.Success;
        LogFlow(
            $"block completion source={source} result={result} playerRequestId={_activeRequestId} " +
            $"targetRequestId={_blockReservation.RequestId} reservationId={_blockReservation.ReservationId}",
            warning: result == ReservedBlockResult.InvalidReservation);
        return result;
    }

    void RollbackBeforeGuarantee(string reason)
    {
        if (_guaranteeCommitted)
        {
            RunGuaranteedFallback(reason);
            return;
        }

        LogFlow(
            $"rollback reason={reason} state={_state} targetRequestId={_blockReservation.RequestId} " +
            $"reservationId={_blockReservation.ReservationId}",
            warning: true);
        UnsubscribeSkillEvents();
        _blockReservation.Controller?.CancelReservedBlock(_blockReservation);
        RestoreOriginalActorPose();
        _state = State.Cancelled;
        Cleanup();
    }

    void RunGuaranteedFallback(string reason)
    {
        if (!_guaranteeCommitted)
        {
            RollbackBeforeGuarantee(reason);
            return;
        }

        LogFlow(
            $"guaranteed fallback reason={reason} state={_state} playerRequestId={_activeRequestId} " +
            $"targetRequestId={_blockReservation.RequestId} reservationId={_blockReservation.ReservationId}",
            warning: true);

        _state = State.Cancelled;
        UnsubscribeSkillEvents();
        StopActiveSkillPlayback();
        CompleteReservedBlockOnce(reason);
        Cleanup();
    }

    void Cleanup()
    {
        if (_cleanupRunning || _state == State.Idle)
            return;

        _cleanupRunning = true;

        State finalState = _state;
        int playerRequestId = _activeRequestId;
        int targetRequestId = _blockReservation.RequestId;
        int reservationId = _blockReservation.ReservationId;

        UnsubscribeSkillEvents();
        RestoreVisibleState();
        RestorePlayerMovement();
        CharacterPlacementReservationRegistry.Shared.ReleaseOwner(_actorTransform);

        _target = default;
        _blockReservation = default;
        _activeRequestId = 0;
        _timeoutTimer = 0f;
        _blockCompletedSuccessfully = false;
        _guaranteeCommitted = false;
        _blockCompletionAttempted = false;
        _blockCompletionResult = default;
        _impactReached = false;
        _hitLagFired = false;
        _hitLagPending = false;
        _placementResult = default;
        _hasOriginalActorPose = false;
        _state = State.Idle;
        _cleanupRunning = false;
        LogFlow(
            $"cleanup finalState={finalState} playerRequestId={playerRequestId} targetRequestId={targetRequestId} reservationId={reservationId}");
    }

    void CacheOriginalActorPose()
    {
        _hasOriginalActorPose = _actorTransform != null;
        if (!_hasOriginalActorPose)
            return;

        _originalActorPosition = _actorTransform.position;
        _originalActorRotation = _actorTransform.rotation;
    }

    void RestoreOriginalActorPose()
    {
        if (!_hasOriginalActorPose)
            return;

        ApplyActorPose(_originalActorPosition, _originalActorRotation);
        _hasOriginalActorPose = false;
        LogFlow("original actor pose restored");
    }

    void StopActiveSkillPlayback()
    {
        if (_animDriver != null && _activeRequestId > 0)
            _animDriver.CancelSkillCastRequest(_activeRequestId);
    }

    void SuspendPlayerMovement()
    {
        if (_movement == null || _movementSuspended)
            return;

        _movementWasEnabled = _movement.enabled;
        if (_movementWasEnabled)
            _movement.enabled = false;

        _movementSuspended = true;
    }

    void RestorePlayerMovement()
    {
        if (!_movementSuspended)
            return;

        if (_movement != null)
            _movement.enabled = _movementWasEnabled;

        _movementWasEnabled = false;
        _movementSuspended = false;
    }

    void ApplyActorPose(Vector3 pos, Quaternion rot)
    {
        CharacterController cc = playerContext != null ? playerContext.cc : null;
        Rigidbody rb = playerContext != null ? playerContext.rb : null;
        ActorPoseSnapper.Snap(_actorTransform, cc, rb, pos, rot);
    }

    void FaceTarget()
    {
        if (_actorTransform == null || _target.Transform == null) return;

        Vector3 dir = _target.Transform.position - _actorTransform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            ApplyActorPose(_actorTransform.position, Quaternion.LookRotation(dir, Vector3.up));
    }

    void HideVisualForSnap()
    {
        if (_actorFader == null || !_actorFader.gameObject.activeInHierarchy)
            return;

        _actorFader.SetHiddenImmediate();
        _visualHiddenForSnap = true;
        LogFlow("visual hidden for snap");
    }

    void BeginVisualFadeIn()
    {
        if (!_visualHiddenForSnap || _actorFader == null)
            return;

        _visualHiddenForSnap = false;
        _actorFader.BeginAnimationLifecycle(hideOnAnimationComplete: false);
        LogFlow("fade-in started");
    }

    void RestoreVisibleState()
    {
        bool wasHiddenForSnap = _visualHiddenForSnap;
        _visualHiddenForSnap = false;

        if (_actorFader == null || !_actorFader.gameObject.activeInHierarchy)
            return;

        _actorFader.BeginAnimationLifecycle(hideOnAnimationComplete: false);

        if (wasHiddenForSnap)
            LogFlow("visibility restored after hidden transition");
    }

    void UnsubscribeSkillEvents()
    {
        if (_animBrain != null)
        {
            _animBrain.SkillTimelineEventRaised -= OnTimelineEvent;
            _animBrain.PlaybackEvent -= OnPlaybackEvent;
        }
    }

    void ResolveRefs()
    {
        if (playerContext == null)
            TryGetComponent(out playerContext);
        if (playerContext == null)
            playerContext = GetComponentInParent<PlayerContext>();

        if (playerContext != null)
        {
            playerContext.ResolveReferences();
            _animBrain = playerContext.AnimBrain;
            _animDriver = playerContext.AnimDriver;
            _skillManager = playerContext.SkillManager;
            _movement = playerContext.movement;
            _actorFader = playerContext.GetComponentInChildren<ASPHelperDitherFader>(true);
        }

        _actorTransform = playerContext != null ? playerContext.transform : transform;
    }

    void LogFlow(string message, bool warning = false)
    {
        if (!logInterruptionFlow)
            return;

        string formatted = $"[PreCast.Player] player='{name}' {message}";
        if (warning)
            Debug.LogWarning(formatted, this);
        else
            Debug.Log(formatted, this);
    }

    static string ResolveName(Transform target)
    {
        return target != null ? target.name : "<none>";
    }
}
