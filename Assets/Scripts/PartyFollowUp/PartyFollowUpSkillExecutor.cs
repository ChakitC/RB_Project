using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "PartyComboStartKind")]
public enum PartyFollowUpStartKind
{
    Accepted = 0,
    Busy = 1,
    Paused = 2,
    Invalid = 3,
    PlacementFailed = 4,
    CastRejected = 5,
}

[DisallowMultipleComponent]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "PartyComboSkillExecutor")]
public sealed class PartyFollowUpSkillExecutor : MonoBehaviour
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
        public PartyFollowUpExecutionProfile Profile;
        public CharacterSkillManager SkillManager;
        public int RequestId;
        public Action<ActiveSkillCastInfo> OnCommitted;
        public Action<PartyFollowUpRejectReason> OnFailed;
        public Coroutine StartRoutine;
        public Coroutine RecoveryRoutine;
        public PartyFollowUpPresentationScope Presentation;
        public bool UsesRootMotion;
        public bool PreserveVerticalMotion;
        public float PlacementTime;
        public CharacterPlacementResult Placement;
        public TargetedSkillPlacementPoseCheck PlacementPoseCheck;
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

    public PartyFollowUpStartKind TryStart(
        PartyFollowUpOpportunity offer,
        in FollowUpExecutionProvenance provenance,
        Action<ActiveSkillCastInfo> onCommitted,
        Action<PartyFollowUpRejectReason> onFailed)
    {
        if (offer == null || party == null || sessionId == 0 || offer.SessionId != sessionId)
            return PartyFollowUpStartKind.Invalid;
        if (GlobalTimeScaleManager.Instance.IsPaused)
            return PartyFollowUpStartKind.Paused;
        if (activeByRole.ContainsKey(offer.OwnerRole))
            return PartyFollowUpStartKind.Busy;

        PartyRuntimeActor actor = party.GetActor(offer.OwnerRole);
        CharacteContext ownerContext = actor?.Context;
        FieldAllyMember member = actor?.FieldMember;
        ownerContext?.ResolveReferences();
        if (actor == null || ownerContext == null || member == null || ownerContext.SkillManager == null)
            return PartyFollowUpStartKind.Invalid;
        if (ownerContext.HealthSystem == null || !ownerContext.HealthSystem.IsAlive)
            return PartyFollowUpStartKind.Invalid;
        if (offer.Target == null ||
            !offer.Target.TryResolveAliveTarget(
                out Transform targetTransform,
                out AITargetIdentity targetIdentity))
        {
            return PartyFollowUpStartKind.Invalid;
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
            return PartyFollowUpStartKind.Busy;

        activeByRole.Add(execution.Role, execution);
        if (!TryApplyPlacement(execution, targetTransform, targetIdentity, offer.Definition.executionSkill))
        {
            Cleanup(execution, restoreOrigin: true);
            return PartyFollowUpStartKind.PlacementFailed;
        }

        CharacterSkillManager manager = ownerContext.SkillManager;
        execution.SkillManager = manager;
        // Skill playback already enables root motion; usePlanarRootMotion only suppresses Y.
        SkillCastStartResult startResult = manager.TryStartPartyFollowUpSkill(
            offer.Definition,
            $"party-follow-up:{offer.Definition.RuntimeId}",
            offer.Target,
            info => HandleCommitted(execution, info),
            (info, result) => HandleFailed(execution, PartyFollowUpRejectReason.PayloadFailed),
            (info, reason) => HandleFailed(execution, PartyFollowUpRejectReason.CastRejected),
            offer.ChainId,
            offer.Depth + 1,
            provenance,
            usePlanarRootMotion: execution.UsesRootMotion && !execution.PreserveVerticalMotion);

        if (!startResult.Started)
        {
            if (!execution.Cleaned)
                Cleanup(execution, restoreOrigin: true);
            return PartyFollowUpStartKind.CastRejected;
        }

        execution.RequestId = startResult.RequestId;
        if (IsCurrent(execution) && execution.UsesRootMotion)
        {
            var placement = execution.Placement;
            execution.PlacementPoseCheck = TargetedSkillPlacementPoseCheck.Create(
                ownerContext.AnimBrain, ownerContext.transform, execution.RequestId,
                offer.Definition.executionSkill, execution.PlacementTime,
                TargetedSkillPlacementResult.RootMotion(placement.StartPosition, placement.StartRotation,
                    placement.ImpactPosition, placement.ImpactRotation, 0f),
                preserveVerticalMotion: execution.PreserveVerticalMotion);
        }
        if (IsCurrent(execution) && !execution.Committed)
        {
            PartyFollowUpPresentationProfile presentation = execution.Profile?.presentation;
            if (presentation != null && presentation.enabled &&
                !CutsceneDirector.IsCinematicPlaying && !NpcPresentationController.IsActive &&
                manager.TryGetActiveCast(out ActiveSkillCastInfo cast) &&
                cast.RequestId == execution.RequestId && !cast.Released && cast.CastPointNormalized > 0f)
            {
                execution.Presentation = new PartyFollowUpPresentationScope(
                    ownerContext, targetTransform, presentation, cast);
            }
            execution.StartRoutine = StartCoroutine(WaitForCommit(execution));
        }

        return PartyFollowUpStartKind.Accepted;
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

    void OnDisable()
    {
        CancelAll();
    }

    void LateUpdate()
    {
        foreach (var execution in activeByRole.Values)
            execution.PlacementPoseCheck?.Tick();
    }

    void HandleCommitted(ActiveExecution execution, ActiveSkillCastInfo info)
    {
        if (!IsCurrent(execution) || execution.Committed)
            return;

        execution.Committed = true;
        execution.Presentation?.Dispose();
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

    void HandleFailed(ActiveExecution execution, PartyFollowUpRejectReason reason)
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
            execution.Presentation?.Tick();
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
            HandleFailed(execution, PartyFollowUpRejectReason.CastRejected);
        }
    }

    bool TryApplyPlacement(
        ActiveExecution execution,
        Transform targetTransform,
        AITargetIdentity targetIdentity,
        SkillGemDefinition skill)
    {
        PartyFollowUpExecutionProfile profile = execution.Profile;
        if (profile == null || profile.placementPolicy == PartyFollowUpPlacementPolicy.KeepCurrentPosition)
            return true;

        CharacterPlacementAnimationInput animation = null;
        if (profile.placementPolicy == PartyFollowUpPlacementPolicy.AlignPlacementPointToEventTarget)
        {
            CharacterAnimBrain brain = execution.Actor.Context.AnimBrain;
            if (skill == null || brain == null ||
                !brain.TryResolveSkillAnimationClip(skill, out AnimationClip clip) ||
                !brain.TryGetRootMotionSamplingAnimator(out Animator animator) ||
                !TargetedSkillRootMotionTrajectoryCache.TryGetWithVerticalMotion(clip, animator, out var trajectory, out _))
                return false;
            animation = trajectory.PlacementInput;
            execution.PlacementTime = skill.GetPlacementPointNormalized();
        }

        Transform actorTransform = execution.Actor.Context.transform;
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

        Camera camera = GameplayCameraController.Instance != null
            ? GameplayCameraController.Instance.GameplayCamera : Camera.main;
        CharacterPlacementReservationService reservations = CharacterPlacementReservationRegistry.Shared;
        // Match the playback driver's collision policy for the entire trajectory,
        // including movement through the target after the Placement Point.
        RootMotionCCDriver rootMotionDriver = execution.Actor.Context.RootMotionDriver;
        LayerMask ignoredLayers = 0;
        if (skill != null && skill.IgnoreCharacterCollisionDuringRootMotion)
        {
            // NavMesh actors (including Aires) apply authored motion directly and
            // have no CC adapter. Use the project's character layers for those rigs.
            ignoredLayers = rootMotionDriver != null ? rootMotionDriver.CharacterCollisionLayers
                : LayerMask.GetMask("Player", "Enemy", "Ally");
        }
        if (!PartyFollowUpPlacementResolver.TryResolve(execution.Actor.Context, actorCollider, footprint,
                targetTransform, targetIdentity, profile, camera, reservations,
                out CharacterPlacementRequest request, out CharacterPlacementResult result,
                animation, execution.PlacementTime, ignoredLayers) ||
            !reservations.TryReserve(request, result, out execution.PlacementReservation))
        {
            return false;
        }
        execution.HasPlacementReservation = true;
        execution.UsesRootMotion = request.EffectivePlanarRootMotion;
        execution.PreserveVerticalMotion = animation != null && animation.PreserveVerticalMotion;
        execution.Placement = result;
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
        execution.Presentation?.Dispose();
        if (!execution.Committed && execution.SkillManager != null &&
            execution.SkillManager.TryGetActiveCast(out ActiveSkillCastInfo activeCast) &&
            activeCast.RequestId == execution.RequestId)
        {
            execution.SkillManager.TryCancelActiveCast(SkillCastCancelReason.InvalidState);
        }
        if (execution.StartRoutine != null)
            StopCoroutine(execution.StartRoutine);
        if (execution.RecoveryRoutine != null)
            StopCoroutine(execution.RecoveryRoutine);

        if (activeByRole.TryGetValue(execution.Role, out ActiveExecution current) &&
            ReferenceEquals(current, execution))
        {
            activeByRole.Remove(execution.Role);
        }

        CharacteContext context = execution.Actor?.Context;
        // A committed cast can still be moving during cancellation/timeout. Stop that
        // request before returning the actor or releasing its reserved trajectory.
        if (execution.UsesRootMotion && context != null)
            context.AnimBrain?.CancelSkillCastRequest(execution.RequestId);
        if (execution.HasPlacementReservation)
            CharacterPlacementReservationRegistry.Shared.Release(execution.PlacementReservation);
        if (restoreOrigin &&
            context != null &&
            execution.Profile != null &&
            execution.Profile.exitPolicy == PartyFollowUpExitPolicy.ReturnToRecordedOrigin)
        {
            context.transform.SetPositionAndRotation(execution.OriginPosition, execution.OriginRotation);
        }

        execution.Actor?.FieldMember?.EndTransientExecution(execution.OwnerToken);
    }
}
