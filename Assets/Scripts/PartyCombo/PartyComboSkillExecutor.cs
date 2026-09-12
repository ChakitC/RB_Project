using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PartyComboStartKind
{
    Accepted = 0,
    Busy = 1,
    Paused = 2,
    Invalid = 3,
    PlacementFailed = 4,
    CastRejected = 5,
}

[DisallowMultipleComponent]
public sealed class PartyComboSkillExecutor : MonoBehaviour
{
    sealed class ActiveExecution
    {
        public ChainActorRole Role;
        public PartyRuntimeActor Actor;
        public object OwnerToken;
        public int OfferId;
        public ulong SessionId;
        public ulong ExecutionId;
        public Vector3 OriginPosition;
        public Quaternion OriginRotation;
        public CharacterPlacementReservationService.Handle PlacementReservation;
        public bool HasPlacementReservation;
        public bool Committed;
        public bool Cleaned;
        public PartyComboExecutionProfile Profile;
        public CharacterSkillManager SkillManager;
        public int RequestId;
        public Action<ActiveSkillCastInfo> OnCommitted;
        public Action<PartyComboRejectReason> OnFailed;
        public Coroutine StartRoutine;
        public Coroutine RecoveryRoutine;
    }

    readonly Dictionary<ChainActorRole, ActiveExecution> activeByRole =
        new Dictionary<ChainActorRole, ActiveExecution>();

    PartyRuntime party;
    ulong sessionId;

    public void BindParty(PartyRuntime runtime, ulong bindingSessionId)
    {
        CancelAll();
        party = runtime;
        sessionId = bindingSessionId;
    }

    public void UnbindParty()
    {
        sessionId = 0;
        party = null;
        CancelAll();
    }

    public bool IsExecuting(ChainActorRole role)
    {
        return activeByRole.ContainsKey(role);
    }

    public PartyComboStartKind TryStart(
        PartyComboOpportunity offer,
        in ComboExecutionProvenance provenance,
        Action<ActiveSkillCastInfo> onCommitted,
        Action<PartyComboRejectReason> onFailed)
    {
        if (offer == null || party == null || sessionId == 0 || offer.SessionId != sessionId)
            return PartyComboStartKind.Invalid;
        if (GlobalTimeScaleManager.Instance.IsPaused)
            return PartyComboStartKind.Paused;
        if (activeByRole.ContainsKey(offer.OwnerRole))
            return PartyComboStartKind.Busy;

        PartyRuntimeActor actor = party.GetActor(offer.OwnerRole);
        CharacteContext ownerContext = actor?.Context;
        FieldAllyMember member = actor?.FieldMember;
        ownerContext?.ResolveReferences();
        if (actor == null || ownerContext == null || member == null || ownerContext.SkillManager == null)
            return PartyComboStartKind.Invalid;
        if (ownerContext.HealthSystem == null || !ownerContext.HealthSystem.IsAlive)
            return PartyComboStartKind.Invalid;
        if (offer.Target == null ||
            !offer.Target.TryResolveAliveTarget(
                out Transform targetTransform,
                out AITargetIdentity targetIdentity))
        {
            return PartyComboStartKind.Invalid;
        }

        var execution = new ActiveExecution
        {
            Role = offer.OwnerRole,
            Actor = actor,
            OwnerToken = new object(),
            OfferId = offer.OfferId,
            SessionId = sessionId,
            ExecutionId = provenance.ExecutionId,
            OriginPosition = ownerContext.transform.position,
            OriginRotation = ownerContext.transform.rotation,
            Profile = offer.Definition.executionProfile,
            OnCommitted = onCommitted,
            OnFailed = onFailed,
        };

        bool protectActor = execution.Profile == null || execution.Profile.protectActorDuringExecution;
        if (!member.TryBeginTransientExecution(execution.OwnerToken, protectActor))
            return PartyComboStartKind.Busy;

        activeByRole.Add(execution.Role, execution);
        if (!TryApplyPlacement(execution, targetTransform, targetIdentity))
        {
            Cleanup(execution, restoreOrigin: true);
            return PartyComboStartKind.PlacementFailed;
        }

        CharacterSkillManager manager = ownerContext.SkillManager;
        execution.SkillManager = manager;
        SkillCastStartResult startResult = manager.TryStartPartyComboSkill(
            offer.Definition,
            $"party-combo:{offer.Definition.RuntimeId}",
            offer.Target,
            info => HandleCommitted(execution, info),
            (info, result) => HandleFailed(execution, PartyComboRejectReason.PayloadFailed),
            (info, reason) => HandleFailed(execution, PartyComboRejectReason.CastRejected),
            offer.ChainId,
            offer.Depth + 1,
            provenance);

        if (!startResult.Started)
        {
            if (!execution.Cleaned)
                Cleanup(execution, restoreOrigin: true);
            return PartyComboStartKind.CastRejected;
        }

        execution.RequestId = startResult.RequestId;
        if (IsCurrent(execution) && !execution.Committed)
            execution.StartRoutine = StartCoroutine(WaitForCommit(execution));

        return PartyComboStartKind.Accepted;
    }

    public void CancelAll()
    {
        if (activeByRole.Count == 0)
            return;

        ActiveExecution[] snapshot = new ActiveExecution[activeByRole.Count];
        activeByRole.Values.CopyTo(snapshot, 0);
        for (int i = 0; i < snapshot.Length; i++)
            Cleanup(snapshot[i], restoreOrigin: true);
    }

    void HandleCommitted(ActiveExecution execution, ActiveSkillCastInfo info)
    {
        if (!IsCurrent(execution) || execution.Committed)
            return;

        execution.Committed = true;
        if (execution.StartRoutine != null)
        {
            StopCoroutine(execution.StartRoutine);
            execution.StartRoutine = null;
        }
        execution.OnCommitted?.Invoke(info);

        if (!IsCurrent(execution))
            return;

        CharacterAnimBrain brain = info.AnimationDriver != null ? info.AnimationDriver.Brain : null;
        if (brain == null || info.RequestId <= 0)
        {
            Cleanup(execution, restoreOrigin: true);
            return;
        }

        execution.RecoveryRoutine = StartCoroutine(WaitForRecovery(execution, brain, info.RequestId));
    }

    void HandleFailed(ActiveExecution execution, PartyComboRejectReason reason)
    {
        if (!IsCurrent(execution))
            return;

        execution.OnFailed?.Invoke(reason);
        Cleanup(execution, restoreOrigin: true);
    }

    IEnumerator WaitForRecovery(ActiveExecution execution, CharacterAnimBrain brain, int requestId)
    {
        float timeout = execution.Profile != null
            ? Mathf.Max(0.1f, execution.Profile.recoveryTimeoutSeconds)
            : 4f;
        float elapsed = 0f;

        while (IsCurrent(execution) && elapsed < timeout)
        {
            if (brain == null || !brain.TryGetActiveSkillNormalizedTime(requestId, out _))
                break;

            if (!GlobalTimeScaleManager.Instance.IsPaused)
                elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Cleanup(execution, restoreOrigin: true);
    }

    IEnumerator WaitForCommit(ActiveExecution execution)
    {
        float timeout = execution.Profile != null
            ? Mathf.Max(0.1f, execution.Profile.startTimeoutSeconds)
            : 2f;
        float elapsed = 0f;

        while (IsCurrent(execution) && !execution.Committed && elapsed < timeout)
        {
            if (!GlobalTimeScaleManager.Instance.IsPaused)
                elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        execution.StartRoutine = null;
        if (!IsCurrent(execution) || execution.Committed)
            yield break;

        if (execution.SkillManager == null ||
            !execution.SkillManager.TryCancelActiveCast(SkillCastCancelReason.InvalidState))
        {
            HandleFailed(execution, PartyComboRejectReason.CastRejected);
        }
    }

    bool TryApplyPlacement(
        ActiveExecution execution,
        Transform targetTransform,
        AITargetIdentity targetIdentity)
    {
        PartyComboExecutionProfile profile = execution.Profile;
        if (profile == null || profile.placementPolicy == PartyComboPlacementPolicy.KeepCurrentPosition)
            return true;

        Transform actorTransform = execution.Actor.Context.transform;
        Vector3 away = actorTransform.position - targetTransform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
            away = -targetTransform.forward;
        away.Normalize();

        Vector3 position = targetTransform.position + away * profile.desiredRange;
        position += targetTransform.TransformVector(profile.localOffset);
        Vector3 facing = targetTransform.position - position;
        facing.y = 0f;
        Quaternion rotation = facing.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(facing.normalized, Vector3.up)
            : actorTransform.rotation;

        Collider actorCollider = actorTransform.GetComponentInChildren<Collider>(true);
        CharacterPlacementFootprint footprint;
        if (!CharacterPlacementFootprintUtility.TryGetColliderFootprint(
                actorCollider,
                actorTransform,
                out footprint,
                out _))
        {
            footprint = CharacterPlacementFootprintUtility.CreateFallbackBox(
                Vector3.up,
                new Vector3(0.35f, 1f, 0.35f));
        }

        var candidate = new CharacterPlacementRequest.Candidate(position, rotation, 0f, 0);
        var request = new CharacterPlacementRequest(
            actorTransform,
            actorCollider,
            footprint,
            targetIdentity,
            targetTransform,
            CharacterPlacementRequest.AnchorSnapshot.Capture(targetTransform, targetIdentity),
            new[] { candidate },
            null,
            0f,
            null,
            profile.requireUnobstructedPosition ? Physics.DefaultRaycastLayers : 0,
            0,
            actorTransform,
            execution.Actor.Context,
            effectivePlanarRootMotion: false,
            animationRequired: false,
            mobileActor: true,
            runtimePolicy: CharacterPlacementRuntimePolicy.CreateDefault(
                profile.requireNavMesh,
                profile.navMeshSampleDistance,
                QueryTriggerInteraction.Ignore));

        CharacterPlacementReservationService reservations = CharacterPlacementReservationRegistry.Shared;
        if (!CharacterPlacementResolver.TryResolve(request, reservations, out CharacterPlacementResult result) ||
            !reservations.TryReserve(request, result, out execution.PlacementReservation))
        {
            return false;
        }

        execution.HasPlacementReservation = true;
        actorTransform.SetPositionAndRotation(result.StartPosition, result.StartRotation);
        return true;
    }

    bool IsCurrent(ActiveExecution execution)
    {
        return execution != null &&
               !execution.Cleaned &&
               execution.SessionId == sessionId &&
               activeByRole.TryGetValue(execution.Role, out ActiveExecution current) &&
               ReferenceEquals(current, execution);
    }

    void Cleanup(ActiveExecution execution, bool restoreOrigin)
    {
        if (execution == null || execution.Cleaned)
            return;

        execution.Cleaned = true;
        if (execution.StartRoutine != null)
            StopCoroutine(execution.StartRoutine);
        if (execution.RecoveryRoutine != null)
            StopCoroutine(execution.RecoveryRoutine);

        if (activeByRole.TryGetValue(execution.Role, out ActiveExecution current) &&
            ReferenceEquals(current, execution))
        {
            activeByRole.Remove(execution.Role);
        }

        if (execution.HasPlacementReservation)
            CharacterPlacementReservationRegistry.Shared.Release(execution.PlacementReservation);

        CharacteContext context = execution.Actor?.Context;
        if (restoreOrigin &&
            context != null &&
            execution.Profile != null &&
            execution.Profile.exitPolicy == PartyComboExitPolicy.ReturnToRecordedOrigin)
        {
            context.transform.SetPositionAndRotation(execution.OriginPosition, execution.OriginRotation);
        }

        execution.Actor?.FieldMember?.EndTransientExecution(execution.OwnerToken);
    }
}
