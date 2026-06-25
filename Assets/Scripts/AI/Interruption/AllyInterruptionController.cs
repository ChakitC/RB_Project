using System;
using Opsive.BehaviorDesigner.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class AllyInterruptionController : MonoBehaviour
{
    enum State { Idle, Reserved, Warping, Casting, Impact, RebaseSettling, Completed, Cancelled }

    const float MotionPoseStablePositionEpsilon = 0.0025f;
    const float MotionPoseStableYawEpsilon = 0.25f;
    const float RequiredMotionPoseStableSeconds = 0.08f;

    [Header("Refs")]
    [SerializeField] private FieldAllyMember fieldAllyMember;

    [Header("Skill")]
    [SerializeField] private CharacterSkillEntry interruptionSkill;
    [SerializeField, AssetsOnly] private ChainAttackTeleportProfileDef teleportProfile;

    [Header("Root Rebase")]
    [SerializeField] private string motionBoneName = "c_traj";
    [FormerlySerializedAs("rebaseHiddenSettleSeconds")]
    [SerializeField, Min(0.1f)] private float rebaseSettleTimeoutSeconds = 0.75f;

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

    AllyContext _ctx;
    CharacterAnimBrain _animBrain;
    CharacterSkillManager _skillManager;
    BehaviorTree _behaviorTree;
    NavMeshAgent _agent;
    AIAimTargetDriver _aimDriver;
    Transform _actorTransform;
    ASPHelperDitherFader _actorFader;
    CharacterVisualController _visualController;
    Transform _visualRoot;
    Vector3 _authoredVisualLocalPosition;
    Quaternion _authoredVisualLocalRotation;

    bool _btWasEnabled;
    bool _btSuspended;
    bool _agentOverrideActive;
    bool _savedAgentStopped;
    bool _savedAgentUpdatePosition;
    bool _savedAgentUpdateRotation;
    bool _rigidbodyOverrideActive;
    bool _savedRigidbodyIsKinematic;
    bool _savedRigidbodyUseGravity;
    bool _visualHiddenForSnap;
    bool _hasAuthoredVisualPose;
    bool _blockCompletedSuccessfully;
    Transform _rebaseMotionBone;
    Animator _rebaseAnimator;
    Vector3 _rebaseAnchorWorldPosition;
    float _rebaseAnchorWorldYaw;
    Vector3 _previousMotionPosePosition;
    float _previousMotionPoseYaw;
    float _rebaseSettleElapsed;
    float _motionPoseStableElapsed;
    bool _hasPreviousMotionPose;

    public bool IsExecuting => _state != State.Idle;

    void Awake()
    {
        ResolveRefs();
    }

    void OnDisable()
    {
        if (_state == State.RebaseSettling)
        {
            LogFlow("disabled during visible root rebase settle; committing current compensation");
            CommitVisibleRootRebase(timedOut: true);
        }
        else if (_state == State.Impact)
        {
            LogFlow("disabled after impact; restoring autonomy");
            CompleteAndRestore();
        }
        else if (_state != State.Idle)
            RunFallback("Disabled");
    }

    void Update()
    {
        if (_state != State.Casting) return;

        _timeoutTimer -= Time.deltaTime;
        if (_timeoutTimer <= 0f)
            RunFallback("ImpactTimeout");
    }

    void LateUpdate()
    {
        if (_state != State.RebaseSettling)
            return;

        TickVisibleRootRebaseSettle();
    }

    public bool IsReadyForInterruption()
    {
        ResolveRefs();
        if (fieldAllyMember == null) return false;
        if (!fieldAllyMember.IsAlive) return false;
        if (fieldAllyMember.IsBusy || fieldAllyMember.IsReserved) return false;
        if (interruptionSkill == null || teleportProfile == null) return false;
        if (_skillManager == null) return false;
        return _skillManager.CanStartExternalSkill(interruptionSkill);
    }

    public bool TryResolveSafePose(Transform targetAnchor, out Vector3 pos, out Quaternion rot)
    {
        ResolveRefs();
        pos = Vector3.zero;
        rot = Quaternion.identity;
        if (targetAnchor == null || teleportProfile == null) return false;

        return ChainAttackTeleportUtility.TryResolveTeleportPose(
            teleportProfile,
            targetAnchor,
            _actorTransform != null ? _actorTransform.rotation : Quaternion.identity,
            out pos,
            out rot,
            probeCollider: _ctx != null && _ctx.ColliderRefs != null
                ? _ctx.ColliderRefs.CharacterPositionCollider
                : null,
            probeRoot: _actorTransform);
    }

    public bool BeginInterruption(InterruptionTargetContext target, PreCastBlockReservation blockReservation, Vector3 safePos, Quaternion safeRot)
    {
        ResolveRefs();
        if (_state != State.Idle)
        {
            LogFlow($"begin rejected reason=StateNotIdle state={_state}", warning: true);
            return false;
        }

        _target = target;
        _blockReservation = blockReservation;
        _blockCompletedSuccessfully = false;
        CacheVisualRootPose();
        _state = State.Reserved;
        LogFlow(
            $"begin target='{ResolveName(target.Transform)}' targetRequestId={blockReservation.RequestId} reservationId={blockReservation.ReservationId} safePosition={safePos}");

        SuspendAutonomy();

        HideVisualForSnap();
        ApplyActorPose(safePos, safeRot);
        FaceTarget();
        LockAim();
        _state = State.Warping;
        LogFlow(
            $"snap applied targetRequestId={blockReservation.RequestId} reservationId={blockReservation.ReservationId} position={safePos}");

        if (_skillManager == null)
        {
            LogFlow("skill start rejected reason=MissingSkillManager", warning: true);
            RunFallback("MissingSkillManager");
            return false;
        }

        var result = _skillManager.TryStartExternalSkill(
            interruptionSkill,
            "interruption",
            CombatTimelineEventName.HitStart);
        if (!result.Started)
        {
            LogFlow($"skill start rejected kind={result.Kind}", warning: true);
            RunFallback($"SkillStart:{result.Kind}");
            return false;
        }

        _activeRequestId = result.RequestId;
        _state = State.Casting;
        _timeoutTimer = impactTimeoutSeconds;
        LogFlow(
            $"skill started kind={result.Kind} allyRequestId={result.RequestId} targetRequestId={blockReservation.RequestId} reservationId={blockReservation.ReservationId}");
        BeginVisualFadeIn();

        if (_animBrain != null)
        {
            _animBrain.SkillTimelineEventRaised += OnTimelineEvent;
            _animBrain.SkillCastInterrupted += OnSkillInterrupted;
        }

        return true;
    }

    void OnTimelineEvent(int requestId, CombatTimelineEventName eventName)
    {
        if (_state != State.Casting) return;
        if (requestId != _activeRequestId) return;
        if (eventName != CombatTimelineEventName.HitStart) return;
        LogFlow(
            $"timeline event={eventName} allyRequestId={requestId} targetRequestId={_blockReservation.RequestId} reservationId={_blockReservation.ReservationId}");
        OnImpact();
    }

    void OnImpact()
    {
        _state = State.Impact;
        UnsubscribeImpactTimeline();

        if (_blockReservation.IsValid && _blockReservation.Controller != null)
        {
            ReservedBlockResult result = _blockReservation.Controller.CompleteReservedBlock(_blockReservation);
            _blockCompletedSuccessfully = result == ReservedBlockResult.Success;
            LogFlow(
                $"impact block result={result} allyRequestId={_activeRequestId} targetRequestId={_blockReservation.RequestId} reservationId={_blockReservation.ReservationId}",
                warning: result != ReservedBlockResult.Success);
        }
        else
        {
            LogFlow(
                $"impact block result={ReservedBlockResult.InvalidReservation} allyRequestId={_activeRequestId}",
                warning: true);
        }

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

        ScheduleRestore();
    }

    void ScheduleRestore()
    {
        if (_animBrain != null)
        {
            _animBrain.SkillCompleted -= OnSkillCompleted;
            _animBrain.SkillCompleted += OnSkillCompleted;
        }
        else
        {
            CompleteAndRestore();
        }
    }

    void OnSkillCompleted()
    {
        if (_state != State.Impact) return;

        if (_blockCompletedSuccessfully && TryBeginVisibleRootRebase())
            return;

        CompleteAndRestore();
    }

    void OnSkillInterrupted(int requestId)
    {
        if (requestId != _activeRequestId) return;

        if (_state == State.Casting)
        {
            RunFallback("SkillInterrupted");
            return;
        }

        if (_state == State.Impact)
            CompleteAndRestore();
    }

    void CompleteAndRestore()
    {
        _state = State.Completed;
        LogFlow(
            $"completed allyRequestId={_activeRequestId} targetRequestId={_blockReservation.RequestId} reservationId={_blockReservation.ReservationId}");
        Cleanup();
    }

    void RunFallback(string reason)
    {
        UnsubscribeSkillEvents();
        LogFlow(
            $"fallback reason={reason} state={_state} allyRequestId={_activeRequestId} targetRequestId={_blockReservation.RequestId} reservationId={_blockReservation.ReservationId}",
            warning: true);

        if (_blockReservation.IsValid && _blockReservation.Controller != null)
        {
            bool targetAlive = _target.Health != null && _target.Health.IsAlive;
            ReservedBlockResult result = _blockReservation.Controller.CompleteReservedBlock(_blockReservation);
            LogFlow(
                $"fallback block result={result} targetRequestId={_blockReservation.RequestId} reservationId={_blockReservation.ReservationId}",
                warning: result != ReservedBlockResult.Success);

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

        _state = State.Cancelled;
        Cleanup();
    }

    void Cleanup()
    {
        if (_state == State.RebaseSettling)
            TryCommitVisibleRootRebase("cleanup", warning: true);

        State finalState = _state;
        int allyRequestId = _activeRequestId;
        int targetRequestId = _blockReservation.RequestId;
        int reservationId = _blockReservation.ReservationId;

        UnsubscribeSkillEvents();
        ClearAim();
        RestoreVisibleState();
        RestoreAutonomy();
        ReleaseAllyReservation();

        _target = default;
        _blockReservation = default;
        _activeRequestId = 0;
        _timeoutTimer = 0f;
        _visualRoot = null;
        _hasAuthoredVisualPose = false;
        _blockCompletedSuccessfully = false;
        ClearVisibleRootRebaseState();
        _state = State.Idle;
        LogFlow(
            $"cleanup finalState={finalState} allyRequestId={allyRequestId} targetRequestId={targetRequestId} reservationId={reservationId} autonomyRestored=true");
    }

    void SuspendAutonomy()
    {
        if (_behaviorTree != null)
        {
            _btWasEnabled = _behaviorTree.enabled;
            if (_btWasEnabled)
            {
                _behaviorTree.enabled = false;
                _btSuspended = true;
            }
        }

        if (_agent != null && _agent.enabled)
        {
            _savedAgentStopped = _agent.isStopped;
            _savedAgentUpdatePosition = _agent.updatePosition;
            _savedAgentUpdateRotation = _agent.updateRotation;
            _agent.isStopped = true;
            _agent.updatePosition = false;
            _agent.updateRotation = false;
            _agentOverrideActive = true;
        }

        Rigidbody actorRigidbody = _ctx != null ? _ctx.rb : null;
        if (actorRigidbody != null)
        {
            _savedRigidbodyIsKinematic = actorRigidbody.isKinematic;
            _savedRigidbodyUseGravity = actorRigidbody.useGravity;
            actorRigidbody.linearVelocity = Vector3.zero;
            actorRigidbody.angularVelocity = Vector3.zero;
            actorRigidbody.useGravity = false;
            actorRigidbody.isKinematic = true;
            _rigidbodyOverrideActive = true;
        }
    }

    void RestoreAutonomy()
    {
        if (_btSuspended && _behaviorTree != null)
        {
            _behaviorTree.enabled = _btWasEnabled;
            _btSuspended = false;
        }

        if (_agentOverrideActive && _agent != null && _agent.enabled)
        {
            _agent.isStopped = _savedAgentStopped;
            _agent.updatePosition = _savedAgentUpdatePosition;
            _agent.updateRotation = _savedAgentUpdateRotation;

            if (_agent.isOnNavMesh)
                _agent.nextPosition = _actorTransform != null ? _actorTransform.position : transform.position;

            _agentOverrideActive = false;
        }

        if (_rigidbodyOverrideActive)
        {
            Rigidbody actorRigidbody = _ctx != null ? _ctx.rb : null;
            if (actorRigidbody != null)
            {
                actorRigidbody.linearVelocity = Vector3.zero;
                actorRigidbody.angularVelocity = Vector3.zero;
                actorRigidbody.useGravity = _savedRigidbodyUseGravity;
                actorRigidbody.isKinematic = _savedRigidbodyIsKinematic;
            }

            _rigidbodyOverrideActive = false;
        }
    }

    void ApplyActorPose(Vector3 pos, Quaternion rot)
    {
        CharacterController characterController = _ctx != null ? _ctx.cc : null;
        bool restoreCharacterController = characterController != null && characterController.enabled;
        if (restoreCharacterController)
            characterController.enabled = false;

        Rigidbody actorRigidbody = _ctx != null ? _ctx.rb : null;
        if (actorRigidbody != null)
        {
            actorRigidbody.linearVelocity = Vector3.zero;
            actorRigidbody.angularVelocity = Vector3.zero;
            actorRigidbody.position = pos;
            actorRigidbody.rotation = rot;
        }

        if (_actorTransform != null)
            _actorTransform.SetPositionAndRotation(pos, rot);

        Physics.SyncTransforms();

        if (restoreCharacterController && characterController != null)
            characterController.enabled = true;

        if (_agent != null && _agent.enabled)
        {
            if (_agent.isOnNavMesh)
            {
                _agent.nextPosition = pos;
            }
            else if (NavMesh.SamplePosition(pos, out NavMeshHit navHit, 1f, NavMesh.AllAreas))
            {
                Vector3 snappedPosition = navHit.position;
                if (_actorTransform != null)
                    _actorTransform.SetPositionAndRotation(snappedPosition, rot);
                if (actorRigidbody != null)
                    actorRigidbody.position = snappedPosition;

                _agent.Warp(snappedPosition);
                _agent.nextPosition = snappedPosition;
                Physics.SyncTransforms();
            }
        }
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

    void CacheVisualRootPose()
    {
        _visualRoot = _visualController != null ? _visualController.ModelRoot : null;
        _hasAuthoredVisualPose = _visualRoot != null;

        if (!_hasAuthoredVisualPose)
            return;

        _authoredVisualLocalPosition = _visualRoot.localPosition;
        _authoredVisualLocalRotation = _visualRoot.localRotation;
    }

    bool TryBeginVisibleRootRebase()
    {
        if (!_hasAuthoredVisualPose || _visualRoot == null)
        {
            LogFlow("root rebase skipped reason=MissingVisualRoot");
            return false;
        }

        _rebaseAnimator = ResolveAnimator();
        _rebaseMotionBone = ResolveMotionBone(_rebaseAnimator);
        if (_rebaseMotionBone == null)
        {
            LogFlow($"root rebase skipped reason=MotionBoneNotFound bone='{motionBoneName}'");
            ClearVisibleRootRebaseState();
            return false;
        }

        _rebaseAnchorWorldPosition = _rebaseMotionBone.position;
        _rebaseAnchorWorldPosition.y = _actorTransform != null
            ? _actorTransform.position.y
            : _rebaseAnchorWorldPosition.y;
        _rebaseAnchorWorldYaw = _rebaseMotionBone.eulerAngles.y;

        if (!ChainAttackActorRootRebaseUtility.TryRebaseToAnimationRoot(
                _ctx,
                _visualRoot,
                _rebaseMotionBone,
                _agent,
                out Vector3 positionDelta,
                out float yawDelta,
                out string failureReason))
        {
            LogFlow($"root rebase skipped reason='{failureReason}'", warning: true);
            ClearVisibleRootRebaseState();
            return false;
        }

        _rebaseSettleElapsed = 0f;
        _motionPoseStableElapsed = 0f;
        _hasPreviousMotionPose = false;
        _state = State.RebaseSettling;

        LogFlow(
            $"visible root rebase started bone='{motionBoneName}' positionDelta={positionDelta} yawDelta={yawDelta:0.###}");
        return true;
    }

    void TickVisibleRootRebaseSettle()
    {
        if (_rebaseMotionBone == null || _rebaseAnimator == null || _visualRoot == null)
        {
            LogFlow("visible root rebase lost required references; committing current compensation", warning: true);
            CommitVisibleRootRebase(timedOut: true);
            return;
        }

        SampleMotionPose(out Vector3 motionPosePosition, out float motionPoseYaw);
        UpdateMotionPoseStability(motionPosePosition, motionPoseYaw);

        if (!ChainAttackActorRootRebaseUtility.TryCompensateVisualRootToAnimationPose(
                _visualRoot,
                _rebaseMotionBone,
                _rebaseAnchorWorldPosition,
                _rebaseAnchorWorldYaw,
                out _,
                out _,
                out string failureReason))
        {
            LogFlow($"visible root rebase compensation failed reason='{failureReason}'", warning: true);
            CommitVisibleRootRebase(timedOut: true);
            return;
        }

        _rebaseSettleElapsed += Time.deltaTime;
        bool rootMotionFinished = _animBrain == null || !_animBrain.RootMotionActive;
        if (rootMotionFinished && _motionPoseStableElapsed >= RequiredMotionPoseStableSeconds)
        {
            CommitVisibleRootRebase(timedOut: false);
            return;
        }

        if (_rebaseSettleElapsed >= Mathf.Max(0.1f, rebaseSettleTimeoutSeconds))
            CommitVisibleRootRebase(timedOut: true);
    }

    void UpdateMotionPoseStability(Vector3 position, float yaw)
    {
        if (!_hasPreviousMotionPose)
        {
            _previousMotionPosePosition = position;
            _previousMotionPoseYaw = yaw;
            _hasPreviousMotionPose = true;
            _motionPoseStableElapsed = 0f;
            return;
        }

        float positionDelta = Vector3.Distance(position, _previousMotionPosePosition);
        float yawDelta = Mathf.Abs(Mathf.DeltaAngle(_previousMotionPoseYaw, yaw));
        _motionPoseStableElapsed =
            positionDelta <= MotionPoseStablePositionEpsilon &&
            yawDelta <= MotionPoseStableYawEpsilon
                ? _motionPoseStableElapsed + Time.deltaTime
                : 0f;

        _previousMotionPosePosition = position;
        _previousMotionPoseYaw = yaw;
    }

    void SampleMotionPose(out Vector3 position, out float yaw)
    {
        position = _rebaseAnimator.transform.InverseTransformPoint(_rebaseMotionBone.position);
        position.y = 0f;
        yaw = Mathf.DeltaAngle(
            _rebaseAnimator.transform.eulerAngles.y,
            _rebaseMotionBone.eulerAngles.y);
    }

    void CommitVisibleRootRebase(bool timedOut)
    {
        TryCommitVisibleRootRebase(
            timedOut ? "settle timeout" : "motion pose stable",
            warning: timedOut);
        CompleteAndRestore();
    }

    bool TryCommitVisibleRootRebase(string reason, bool warning)
    {
        if (_visualRoot == null || !_hasAuthoredVisualPose)
        {
            LogFlow($"visible root rebase commit skipped reason=MissingVisualRoot source='{reason}'", warning: true);
            ClearVisibleRootRebaseState();
            return false;
        }

        bool committed = ChainAttackActorRootRebaseUtility.TryCommitVisualRootCompensation(
            _ctx,
            _visualRoot,
            _authoredVisualLocalPosition,
            _authoredVisualLocalRotation,
            _agent,
            out Vector3 positionDelta,
            out float yawDelta,
            out string failureReason);

        if (committed)
        {
            LogFlow(
                $"visible root rebase committed source='{reason}' positionDelta={positionDelta} yawDelta={yawDelta:0.###}",
                warning);
        }
        else
        {
            LogFlow(
                $"visible root rebase commit failed source='{reason}' reason='{failureReason}'",
                warning: true);
        }

        ClearVisibleRootRebaseState();
        return committed;
    }

    void ClearVisibleRootRebaseState()
    {
        _rebaseMotionBone = null;
        _rebaseAnimator = null;
        _rebaseAnchorWorldPosition = Vector3.zero;
        _rebaseAnchorWorldYaw = 0f;
        _previousMotionPosePosition = Vector3.zero;
        _previousMotionPoseYaw = 0f;
        _rebaseSettleElapsed = 0f;
        _motionPoseStableElapsed = 0f;
        _hasPreviousMotionPose = false;
    }

    Animator ResolveAnimator()
    {
        Animator animator = _visualController != null ? _visualController.animator : null;
        if (animator == null && _ctx != null)
            animator = _ctx.GetComponentInChildren<Animator>(true);

        return animator;
    }

    Transform ResolveMotionBone(Animator animator)
    {
        if (string.IsNullOrWhiteSpace(motionBoneName))
            return null;

        return animator != null
            ? FindChildByName(animator.transform, motionBoneName.Trim())
            : null;
    }

    static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null)
            return null;

        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    void LockAim()
    {
        if (_aimDriver != null && _target.Anchor != null)
            _aimDriver.SetOverrideTarget(_target.Anchor, preferChainAttackPoint: true);
    }

    void ClearAim()
    {
        _aimDriver?.ClearOverride();
    }

    void ReleaseAllyReservation()
    {
        if (fieldAllyMember != null)
            fieldAllyMember.ReleaseReservation(this);
    }

    void UnsubscribeImpactTimeline()
    {
        if (_animBrain != null)
            _animBrain.SkillTimelineEventRaised -= OnTimelineEvent;
    }

    void UnsubscribeSkillEvents()
    {
        if (_animBrain != null)
        {
            _animBrain.SkillTimelineEventRaised -= OnTimelineEvent;
            _animBrain.SkillCompleted -= OnSkillCompleted;
            _animBrain.SkillCastInterrupted -= OnSkillInterrupted;
        }
    }

    void ResolveRefs()
    {
        if (fieldAllyMember == null)
            fieldAllyMember = GetComponent<FieldAllyMember>();

        if (fieldAllyMember != null)
            _actorFader = fieldAllyMember.ActorFaderRef;

        if (_ctx == null)
            TryGetComponent(out _ctx);
        if (_ctx == null)
            _ctx = GetComponentInParent<AllyContext>();
        if (_ctx == null && fieldAllyMember != null)
            _ctx = fieldAllyMember.ActorContextRef as AllyContext;

        if (_ctx != null)
        {
            _ctx.ResolveReferences();
            _animBrain = _ctx.AnimBrain;
            _skillManager = _ctx.SkillManager;
            _behaviorTree = _ctx.BehaviorTree;
            _agent = _ctx.agent;
            _aimDriver = _ctx.AimTargetDriver;
            _visualController = _ctx.Visual;
        }

        if (_visualController == null)
            _visualController = GetComponentInChildren<CharacterVisualController>(true);

        _actorTransform = _ctx != null ? _ctx.transform : transform;
    }

    void LogFlow(string message, bool warning = false)
    {
        if (!logInterruptionFlow)
            return;

        string formatted = $"[PreCast.Ally] ally='{name}' {message}";
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
