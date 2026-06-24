using System;
using Opsive.BehaviorDesigner.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class AllyInterruptionController : MonoBehaviour
{
    enum State { Idle, Reserved, Warping, Casting, Impact, Completed, Cancelled }

    [Header("Refs")]
    [SerializeField] private FieldAllyMember fieldAllyMember;

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

    State _state;
    InterruptionTargetContext _target;
    PreCastBlockReservation _blockReservation;
    int _activeRequestId;
    float _timeoutTimer;

    CharacteContext _ctx;
    CharacterAnimBrain _animBrain;
    CharacterSkillManager _skillManager;
    BehaviorTree _behaviorTree;
    NavMeshAgent _agent;
    AIAimTargetDriver _aimDriver;
    Transform _actorTransform;

    bool _btWasEnabled;
    bool _btSuspended;
    bool _agentOverrideActive;
    bool _savedAgentStopped;
    bool _savedAgentUpdatePosition;
    bool _savedAgentUpdateRotation;

    public bool IsExecuting => _state != State.Idle;

    void Awake()
    {
        ResolveRefs();
    }

    void OnDisable()
    {
        if (_state == State.Impact)
            CompleteAndRestore();
        else if (_state != State.Idle)
            RunFallback();
    }

    void Update()
    {
        if (_state != State.Casting) return;

        _timeoutTimer -= Time.deltaTime;
        if (_timeoutTimer <= 0f)
            RunFallback();
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
            probeCollider: fieldAllyMember != null ? fieldAllyMember.ChainTeleportProbeColliderRef : null,
            probeRoot: _actorTransform);
    }

    public bool BeginInterruption(InterruptionTargetContext target, PreCastBlockReservation blockReservation, Vector3 safePos, Quaternion safeRot)
    {
        ResolveRefs();
        if (_state != State.Idle) return false;

        _target = target;
        _blockReservation = blockReservation;
        _state = State.Reserved;

        SuspendAutonomy();

        WarpToPosition(safePos, safeRot);
        FaceTarget();
        LockAim();
        _state = State.Warping;

        var result = _skillManager.TryStartExternalSkill(interruptionSkill, "interruption");
        if (!result.Started)
        {
            RunFallback();
            return false;
        }

        _activeRequestId = result.RequestId;
        _state = State.Casting;
        _timeoutTimer = impactTimeoutSeconds;

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
        OnImpact();
    }

    void OnImpact()
    {
        _state = State.Impact;
        UnsubscribeImpactTimeline();

        if (_blockReservation.IsValid && _blockReservation.Controller != null)
            _blockReservation.Controller.CompleteReservedBlock(_blockReservation);

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

        CompleteAndRestore();
    }

    void OnSkillInterrupted(int requestId)
    {
        if (requestId != _activeRequestId) return;

        if (_state == State.Casting)
        {
            RunFallback();
            return;
        }

        if (_state == State.Impact)
            CompleteAndRestore();
    }

    void CompleteAndRestore()
    {
        _state = State.Completed;
        Cleanup();
    }

    void RunFallback()
    {
        UnsubscribeSkillEvents();

        if (_blockReservation.IsValid && _blockReservation.Controller != null)
        {
            bool targetAlive = _target.Health != null && _target.Health.IsAlive;
            _blockReservation.Controller.CompleteReservedBlock(_blockReservation);

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
        UnsubscribeSkillEvents();
        ClearAim();
        RestoreAutonomy();
        ReleaseAllyReservation();

        _target = default;
        _blockReservation = default;
        _activeRequestId = 0;
        _timeoutTimer = 0f;
        _state = State.Idle;
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
    }

    void WarpToPosition(Vector3 pos, Quaternion rot)
    {
        if (_actorTransform != null)
        {
            _actorTransform.position = pos;
            _actorTransform.rotation = rot;
        }

        if (_agent != null && _agent.enabled)
            _agent.Warp(pos);
    }

    void FaceTarget()
    {
        if (_actorTransform == null || _target.Transform == null) return;

        Vector3 dir = _target.Transform.position - _actorTransform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            _actorTransform.rotation = Quaternion.LookRotation(dir, Vector3.up);
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
        {
            _ctx = fieldAllyMember.ActorContextRef;
            _animBrain = fieldAllyMember.AnimBrainRef;
            _skillManager = fieldAllyMember.SkillManager;
            _behaviorTree = fieldAllyMember.BehaviorTreeRef;
            _agent = fieldAllyMember.AgentRef;
            _aimDriver = fieldAllyMember.AimTargetDriverRef;
            _actorTransform = fieldAllyMember.TransformRef;
        }
        else
        {
            if (_ctx == null) TryGetComponent(out _ctx);
            if (_ctx == null) _ctx = GetComponentInParent<CharacteContext>();
            if (_ctx != null)
            {
                _ctx.ResolveReferences();
                _animBrain = _ctx.AnimBrain;
                _skillManager = _ctx.SkillManager;
            }
            _actorTransform = _ctx != null ? _ctx.transform : transform;
        }
    }
}
