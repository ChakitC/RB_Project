using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SkillCastOrchestrator
{
    private sealed class PendingCastContext
    {
        public SkillCastRequest Request;
        public SkillInstance RuntimeSkill;

        /// <summary>
        /// Charge, energy, and the stats snapshot this cast was priced with, taken out of the
        /// pools the moment the cast started. Settled exactly once: committed at the cast point,
        /// rolled back on cancellation.
        /// </summary>
        public SkillCastReservation Reservation;
        public SkillGemDefinition SkillDef;
        public ISkillUser SkillUser;
        public CharacterAnimDriver AnimationDriver;
        public int RequestId;
        public bool Released;
        public bool Cancelled;
        public float CastPointNormalized;
        public bool RequiresTimelineEvents;
        public readonly List<CombatTimelineEventName> TimelineEventNames = new List<CombatTimelineEventName>();

        public ActiveSkillCastInfo ToInfo()
        {
            return new ActiveSkillCastInfo(
                RequestId,
                RuntimeSkill,
                SkillDef,
                SkillUser,
                AnimationDriver,
                CastPointNormalized,
                Released,
                RequiresTimelineEvents,
                Request.DebugSource, Request.ExecutionKind);
        }
    }

    private readonly Component owner;

    private readonly SkillChargeRegistry chargeRegistry;
    private readonly SkillCastAnimationBinding animationBinding;

    private PendingCastContext pendingCast;
    private int nextCastRequestId = 1;
    private bool startingCast;

    public SkillCastOrchestrator(Component owner) : this(owner, new SkillChargeRegistry())
    {
    }

    internal SkillCastOrchestrator(Component owner, SkillChargeRegistry chargeRegistry)
    {
        this.owner = owner;
        this.chargeRegistry = chargeRegistry;
        animationBinding = new SkillCastAnimationBinding(OnSkillCastMomentReached, OnSkillCastInterrupted);
    }

    public bool HasPendingCast => pendingCast != null;
    public int ActiveRequestId => pendingCast != null ? pendingCast.RequestId : 0;

    public event Action<ActiveSkillCastInfo> CastStarted;
    public event Action<ActiveSkillCastInfo> CastReleased;
    public event Action<ActiveSkillCastInfo> CastCommitted;
    public event Action<ActiveSkillCastInfo, SkillCastCancelReason> CastCancelled;

    /// <summary>
    /// The payload ran but produced no gameplay effect, so nothing was committed: no energy,
    /// no charge, no cooldown. Presenters use this to explain the refusal to the player.
    /// </summary>
    public event Action<ActiveSkillCastInfo, SkillExecutionResult> CastExecutionFailed;

    public void Tick()
    {
        if (pendingCast == null)
            return;

        if (!CanProceed(pendingCast.Request))
            CancelPendingCast(SkillCastCancelReason.InvalidState);
    }

    public void CancelPendingCast()
    {
        CancelPendingCast(SkillCastCancelReason.Disabled);
    }

    public void CancelPendingCast(SkillCastCancelReason reason)
    {
        if (pendingCast == null)
            return;

        PendingCastContext context = pendingCast;
        pendingCast = null;
        animationBinding.Unbind();
        try { CancelPendingCastRequest(context, stopAnimation: reason != SkillCastCancelReason.AnimationInterrupted); }
        finally { SettleCancelledCast(context, reason); }
        context.Request.OnCancelled?.Invoke(context.ToInfo(), reason);
        CastCancelled?.Invoke(context.ToInfo(), reason);
    }

    public bool TryGetActiveCast(out ActiveSkillCastInfo castInfo)
    {
        if (pendingCast == null)
        {
            castInfo = default;
            return false;
        }

        castInfo = pendingCast.ToInfo();
        return true;
    }

    public bool TryCancelActiveCast(SkillCastCancelReason reason)
    {
        if (pendingCast == null || pendingCast.Released)
            return false;

        CancelPendingCast(reason);
        return true;
    }

    public SkillCastStartResult TryStartCast(in SkillCastRequest request)
    {
        if (startingCast) return Rejected();
        startingCast = true;
        try { return StartCast(request); }
        finally { startingCast = false; }
    }

    private SkillCastStartResult StartCast(in SkillCastRequest request)
    {
        if (pendingCast != null)
            return Rejected();

        if (!TryResolveStartState(request, out SkillInstance runtimeSkill, out ISkillUser skillUser, out SkillGemDefinition skillDef))
            return Rejected();

        int requestId = ResolveRequestId(request.RequestedId);
        CharacterAnimDriver executionAnimDriver = request.AnimationDriver;
        bool hasExternalSkillExecutionContext = animationBinding.HasActiveExecutionContext(executionAnimDriver, requestId);
        bool requiresTimelineEvents =
            skillDef.RequiresSkillTimelineEvents ||
            CombatTimelineEventNames.IsValid(request.RequiredTimelineEvent);

        if (!request.UseAnimationDriver &&
            requiresTimelineEvents &&
            !hasExternalSkillExecutionContext)
        {
            WarnMissingTimelineDriver(skillDef, request.DebugSource);
            return Rejected();
        }

        // Resources leave the pools here, before a single animation frame plays. Holding them for
        // the whole wind-up is what stops a second press from spending the same charge, and the
        // stats snapshot taken with them is what this cast keeps being priced by even if a buff
        // lands halfway through.
        if (!runtimeSkill.TryReserveCast(
                skillUser,
                request.CostPolicy,
                request.StampCooldown,
                out SkillCastReservation reservation, request.ExecutionKind))
        {
            return Rejected();
        }

        bool accepted = false;
        Action onAccepted = request.OnAccepted;
        void Accept()
        {
            if (accepted) return;
            accepted = true;
            onAccepted?.Invoke();
        }
        try
        {
            if (request.UseAnimationDriver && executionAnimDriver != null)
            {
                var context = new PendingCastContext
                {
                    Request = request,
                    Reservation = reservation,
                    RuntimeSkill = runtimeSkill,
                    SkillDef = skillDef,
                    SkillUser = skillUser,
                    AnimationDriver = executionAnimDriver,
                    RequestId = requestId,
                    CastPointNormalized = skillDef.GetExecutionCastPointNormalized(request.ExecutionKind),
                    RequiresTimelineEvents = requiresTimelineEvents,
                };

                skillDef.CollectTimelineEventNames(context.TimelineEventNames);
                CombatTimelineEventNames.AddUnique(
                    context.TimelineEventNames,
                    request.RequiredTimelineEvent);

                bool started = animationBinding.TryPlaySkill(executionAnimDriver,
                    context.RequestId,
                    context.SkillDef,
                    context.CastPointNormalized,
                    context.TimelineEventNames,
                    request.UsePlanarRootMotion, request.ExecutionKind, request.MeleeChainWindow, Accept);

                if (started)
                {
                    animationBinding.Bind(context.AnimationDriver, context.RequestId);
                    pendingCast = context;

                    // OnStarted has side effects on the caster (weapon stow, facing snap), so it only
                    // runs once the animation driver has actually accepted the cast.
                    request.OnStarted?.Invoke();
                    CastStarted?.Invoke(context.ToInfo());
                    // Arm the payload synchronously, before a frame-zero HitStart can run.
                    if (request.ExecutionKind == SkillExecutionKind.BasicMelee)
                    {
                        if (!ReleasePendingCast(context.RequestId))
                        {
                            animationBinding.Cancel(executionAnimDriver, context.RequestId);
                            return Rejected();
                        }
                        animationBinding.ReleaseBasicMelee(executionAnimDriver, context.RequestId);
                    }
                    return new SkillCastStartResult(SkillCastStartKind.WaitingForAnimation, context.RequestId);
                }
            }

            // Reaching here means no animation of our own will raise the timeline events - fatal for a
            // payload that needs them, unless someone else is already playing this exact request and
            // will raise them for us. That is the whole point of the external-execution-context path.
            if (requiresTimelineEvents && !hasExternalSkillExecutionContext)
            {
                reservation.Release();
                WarnMissingTimelineDriver(skillDef, request.DebugSource);
                return Rejected();
            }

            if (request.UseAnimationDriver && !request.AllowImmediateFallback)
            {
                reservation.Release();
                return Rejected();
            }

            if (!CanProceed(request))
            {
                reservation.Release();
                return Rejected();
            }

            Accept();
            request.OnStarted?.Invoke();

            ActiveSkillCastInfo immediateInfo = new ActiveSkillCastInfo(
                requestId,
                runtimeSkill,
                skillDef,
                skillUser,
                executionAnimDriver,
                skillDef.GetCastPointNormalized(),
                released: false,
                requiresTimelineEvents: false,
                request.DebugSource, request.ExecutionKind);
            CastStarted?.Invoke(immediateInfo);

            // No wind-up on this path, so the cast point is now. CastReleased always means "reached the
            // cast point"; whether the payload then produced anything is reported separately.
            CastReleased?.Invoke(new ActiveSkillCastInfo(
                requestId,
                runtimeSkill,
                skillDef,
                skillUser,
                executionAnimDriver,
                skillDef.GetCastPointNormalized(),
                released: true,
                requiresTimelineEvents: false,
                request.DebugSource));

            if (!ExecuteReservedCast(
                    request,
                    requestId,
                    runtimeSkill,
                    reservation,
                    skillUser,
                    executionAnimDriver))
            {
                return Rejected();
            }

            return new SkillCastStartResult(SkillCastStartKind.ImmediateSuccess, requestId);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, owner);
            if (pendingCast?.Reservation == reservation)
            {
                animationBinding.Unbind();
                pendingCast = null;
            }
            reservation.Release();
            if (request.UseAnimationDriver) StopFailedAnimation(executionAnimDriver, requestId);
            return Rejected();
        }
        finally
        {
            // Only a pending wind-up may retain a hold after this method returns.
            if (pendingCast?.Reservation != reservation) reservation.Release();
        }
    }

    private bool TryResolveStartState(
        in SkillCastRequest request,
        out SkillInstance runtimeSkill,
        out ISkillUser skillUser,
        out SkillGemDefinition skillDef)
    {
        runtimeSkill = request.RuntimeSkill;
        skillUser = request.SkillUser;
        skillDef = runtimeSkill != null ? runtimeSkill.def : null;

        if (runtimeSkill == null || skillUser == null || skillDef == null ||
            !skillDef.ValidateExecution(request.ExecutionKind, out _))
            return false;

        if (!CanProceed(request))
            return false;

        return request.ExecutionKind == SkillExecutionKind.BasicMelee ||
               runtimeSkill.CanCast(skillUser, request.CostPolicy, out _);
    }

    /// <summary>
    /// Runs the payload for a cast that has reached its cast point, then settles its reservation:
    /// commit when the payload produced a gameplay effect, roll everything back when it did not.
    /// </summary>
    private bool ExecuteReservedCast(
        in SkillCastRequest request,
        int requestId,
        SkillInstance runtimeSkill,
        SkillCastReservation reservation,
        ISkillUser skillUser,
        CharacterAnimDriver executionAnimDriver)
    {
        if (runtimeSkill == null || reservation == null || reservation.IsSettled)
            return false;

        CharacterAnimBrain executionAnimBrain = executionAnimDriver != null
            ? executionAnimDriver.Brain
            : null;

        SkillExecutionResult result;
        bool succeeded;
        try
        {
            succeeded = runtimeSkill.ExecuteReserved(
                    reservation,
                    executionAnimBrain,
                    requestId,
                    out result,
                    request.PrimaryTarget,
                    request.CombatChainId,
                    request.CombatDepth,
                    request.FollowUpProvenance,
                    request.FacingSnapshot, request.ExecutionKind, request.MeleeType);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, owner);
            succeeded = false;
            result = SkillExecutionResult.Failed(SkillExecutionFailureReason.Rejected, exception.Message);
        }
        if (!succeeded)
        {
            reservation.Release();
            RaiseExecutionFailed(request, requestId, runtimeSkill, skillUser, executionAnimDriver, result);
            return false;
        }

        reservation.Commit();
        var committedInfo = new ActiveSkillCastInfo(
            requestId,
            runtimeSkill,
            runtimeSkill.def,
            skillUser,
            executionAnimDriver,
            runtimeSkill.def != null ? runtimeSkill.def.GetCastPointNormalized() : 0f,
            released: true,
            requiresTimelineEvents: false,
            request.DebugSource, request.ExecutionKind);
        NotifyCommitted(request.OnCommitted, committedInfo);
        NotifyCommitted(CastCommitted, committedInfo);
        if (request.ExecutionKind != SkillExecutionKind.BasicMelee)
        {
            try { PlayCastCue(runtimeSkill, skillUser); }
            catch (Exception exception) { Debug.LogException(exception, owner); }
        }
        return true;
    }

    private void NotifyCommitted(Action<ActiveSkillCastInfo> observers, ActiveSkillCastInfo info)
    {
        if (observers == null) return;
        foreach (Action<ActiveSkillCastInfo> observer in observers.GetInvocationList())
        {
            try { observer(info); }
            catch (Exception exception) { Debug.LogException(exception, owner); }
        }
    }

    private void StopFailedAnimation(CharacterAnimDriver driver, int requestId)
    {
        try { animationBinding.Cancel(driver, requestId); }
        catch (Exception exception) { Debug.LogException(exception, owner); }
    }

    private void RaiseExecutionFailed(
        in SkillCastRequest request,
        int requestId,
        SkillInstance runtimeSkill,
        ISkillUser skillUser,
        CharacterAnimDriver animationDriver,
        SkillExecutionResult result)
    {
        if (result.Success)
            return;

        SkillGemDefinition skillDef = runtimeSkill != null ? runtimeSkill.def : null;
        if (!string.IsNullOrEmpty(result.DebugMessage))
        {
            Debug.Log(
                $"[SkillCast] '{(skillDef != null ? skillDef.name : "<unknown>")}' produced no effect ({result.Reason}): {result.DebugMessage}",
                owner);
        }

        var info = new ActiveSkillCastInfo(
                requestId,
                runtimeSkill,
                skillDef,
                skillUser,
                animationDriver,
                skillDef != null ? skillDef.GetCastPointNormalized() : 0f,
                released: true,
                requiresTimelineEvents: false,
                request.DebugSource, request.ExecutionKind);
        NotifyExecutionFailure(CastExecutionFailed, info, result);
        NotifyExecutionFailure(request.OnExecutionFailed, info, result);
    }

    private void NotifyExecutionFailure(Action<ActiveSkillCastInfo, SkillExecutionResult> observers,
        ActiveSkillCastInfo info, SkillExecutionResult result)
    {
        if (observers == null) return;
        foreach (Action<ActiveSkillCastInfo, SkillExecutionResult> observer in observers.GetInvocationList())
        {
            try { observer(info, result); }
            catch (Exception exception) { Debug.LogException(exception, owner); }
        }
    }

    private bool ReleasePendingCast(int requestId)
    {
        if (pendingCast == null ||
            pendingCast.RequestId != requestId ||
            pendingCast.Cancelled ||
            pendingCast.Released)
        {
            return false;
        }

        PendingCastContext context = pendingCast;
        pendingCast = null;
        animationBinding.Unbind();

        try
        {
            if (!CanProceed(context.Request))
            {
                CancelPendingCastRequest(context, stopAnimation: false);
                SettleCancelledCast(context, SkillCastCancelReason.InvalidState);
                context.Request.OnCancelled?.Invoke(context.ToInfo(), SkillCastCancelReason.InvalidState);
                CastCancelled?.Invoke(context.ToInfo(), SkillCastCancelReason.InvalidState);
                return false;
            }

            context.Released = true;
            CastReleased?.Invoke(context.ToInfo());
            bool succeeded = ExecuteReservedCast(
                context.Request,
                context.RequestId,
                context.RuntimeSkill,
                context.Reservation,
                context.SkillUser,
                context.AnimationDriver);
            if (!succeeded) StopFailedAnimation(context.AnimationDriver, requestId);
            return succeeded;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, owner);
            context.Reservation.Release();
            StopFailedAnimation(context.AnimationDriver, requestId);
            RaiseExecutionFailed(context.Request, requestId, context.RuntimeSkill, context.SkillUser,
                context.AnimationDriver, SkillExecutionResult.Failed(SkillExecutionFailureReason.Rejected, exception.Message));
            return false;
        }
        finally { context.Reservation.Release(); }
    }

    private void CancelPendingCastRequest(PendingCastContext context, bool stopAnimation)
    {
        if (context == null)
            return;

        context.Cancelled = true;
        if (stopAnimation)
            animationBinding.Cancel(context.AnimationDriver, context.RequestId);
    }

    private void OnSkillCastMomentReached(int requestId)
    {
        ReleasePendingCast(requestId);
    }

    private void OnSkillCastInterrupted(int requestId)
    {
        if (pendingCast == null || pendingCast.RequestId != requestId)
            return;

        CancelPendingCast(SkillCastCancelReason.AnimationInterrupted);
    }

    private bool CanProceed(in SkillCastRequest request)
    {
        return request.CanProceed == null || request.CanProceed();
    }

    /// <summary>
    /// The one charge pool for this definition on this character. Created on demand and always
    /// non-null for a real definition, so a skill that has never been cast reads as full rather
    /// than as "unknown".
    /// </summary>
    public SkillChargeState GetOrCreateCharges(SkillGemDefinition skillDef)
        => chargeRegistry.GetOrCreateCharges(skillDef);

    /// <summary>Refills every pool. Used on load, where charges are deliberately not persisted.</summary>
    public void ResetAllChargesToFull() => chargeRegistry.ResetAllChargesToFull();

    /// <summary>
    /// Rolls back a cast that never reached its cast point.
    ///
    /// Interrupted, stunned, and invalid-state casts cost nothing at all. A Blocked cast still
    /// burns its cooldown but keeps its energy - except for summon-style skills, which deploy
    /// something into the world: being blocked before the cast point means nothing was deployed,
    /// so that cast must be free.
    /// </summary>
    private void SettleCancelledCast(PendingCastContext context, SkillCastCancelReason reason)
    {
        SkillCastReservation reservation = context != null ? context.Reservation : null;
        if (reservation == null || reservation.IsSettled)
            return;

        // Defensive: past the cast point the execution path owns the settle.
        if (context.Released)
            return;

        bool burnsCooldown =
            reason == SkillCastCancelReason.Blocked &&
            context.Request.StampCooldown &&
            !IsSummonSkill(context.SkillDef);

        if (burnsCooldown)
            reservation.CommitChargeOnly();
        else
            reservation.Release();
    }

    private static bool IsSummonSkill(SkillGemDefinition skillDef)
    {
        return skillDef != null && (skillDef.tags & SkillTag.Minion) != 0;
    }

    private int ResolveRequestId(int requestedId)
    {
        if (requestedId > 0)
            return requestedId;

        if (nextCastRequestId == int.MaxValue)
            nextCastRequestId = 1;

        return nextCastRequestId++;
    }

    private void PlayCastCue(SkillInstance skill, ISkillUser skillUser)
    {
        if (skill == null || skill.def == null || skill.def.castCue == null)
            return;

        Transform castOrigin = skillUser != null && skillUser.CastOrigin != null
            ? skillUser.CastOrigin
            : owner != null
                ? owner.transform
                : null;

        if (castOrigin == null)
            return;

        AudioService.Instance.PlayAttached(skill.def.castCue, castOrigin, Vector3.zero);
    }

    private void WarnMissingTimelineDriver(SkillGemDefinition skillDef, string debugSource)
    {
        string skillLabel = skillDef != null ? skillDef.name : "<unknown>";
        string sourceLabel = string.IsNullOrWhiteSpace(debugSource) ? string.Empty : $" ({debugSource})";
        Debug.LogWarning(
            $"Skill '{skillLabel}' requires Animancer skill timeline events, but no active skill animation driver was available{sourceLabel}.",
            owner);
    }

    private static SkillCastStartResult Rejected()
    {
        return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);
    }
}
