using UnityEngine;

public sealed partial class DefensiveBlockAttack
{
    DefensiveBlockApproachMotor approach;
    Vector3 approachGuardPosition;
    Quaternion approachGuardRotation;
    bool suppressExecution;
    int approachStartFrame;
    public bool IsTimedApproach => approach != null;
    public float ApproachProgress => approach != null ? approach.Progress : 0f;
    public Vector3 ApproachDestination => approach != null ? approach.Destination : transform.position;

    public bool CanApproachGuard(PlayerContext issuer, DefensiveBlockController guard)
    {
        if (guard == null || guard.Settings == null) return false;
        if (guard.Settings.timedApproachSeconds <= 0f) return true;
        return TryPlanApproach(issuer, guard, out _, out _, out _, out _);
    }

    bool TryPlanApproach(PlayerContext issuer, DefensiveBlockController guard, out Vector3 guardPosition,
        out Quaternion guardRotation, out Vector3 destination, out CharacterPlacementFootprint footprint)
    {
        guardPosition = destination = default; guardRotation = Quaternion.identity; footprint = default;
        return profile != null && ctx != null && ctx.AnimBrain != null &&
            ctx.AnimBrain.TryGetActiveSkillNormalizedTime(requestId, out float time) && time + .02f < windowEndNormalized &&
            guard.TryPreviewGuardPose(issuer, this, out guardPosition, out guardRotation) &&
            DefensiveBlockApproachMotor.TryPlan(ctx, guardPosition, guardRotation, profile.approachStandOff,
                worldLayers, out destination, out footprint);
    }

    bool StartApproach(float duration, Vector3 guardPosition, Quaternion guardRotation,
        Vector3 destination, CharacterPlacementFootprint footprint)
    {
        ReleaseWindup();
        if (ctx.AnimDriver == null || !ctx.AnimDriver.TryBeginSkillApproach(requestId, windowEndNormalized - .005f, duration)) return false;
        suppressExecution = true;
        execution?.StopExecution(requestId);
        execution = null;
        // Root motion has relinquished the agent before this scope captures its original state.
        approach = new DefensiveBlockApproachMotor(ctx, footprint, destination, duration, worldLayers);
        approachGuardPosition = guardPosition; approachGuardRotation = guardRotation;
        approachStartFrame = Time.frameCount;
        ctx.AnimBrain.PlaybackEvent += OnApproachPlayback;
        LastResult = "Timed approach: moving to guard";
        return true;
    }

    void TickApproach()
    {
        if (ctx == null || !ctx.isActiveAndEnabled || ctx.LifeGeneration != life || !OwnsCurrentSkill ||
            ctx.HealthSystem == null || !ctx.HealthSystem.IsAlive ||
            (ctx.stateHub != null && ctx.stateHub.LifeSM.CurrentId != LifeStateId.Alive) ||
            (ctx.KnockbackMotor != null && ctx.KnockbackMotor.IsActive) ||
            CutsceneDirector.IsCinematicPlaying || NpcPresentationController.IsActive ||
            defender == null || !defender.IsExecuting)
        { AbortApproach("Timed approach interrupted"); return; }
        if (defender.HasArrived && (Vector3.Distance(defender.ActorContext.transform.position, approachGuardPosition) > .15f ||
            Quaternion.Angle(defender.ActorContext.transform.rotation, approachGuardRotation) > 10f))
        { AbortApproach("Guard moved away from the reserved impact point"); return; }
        if (Time.frameCount == approachStartFrame) return;
        float dt = ctx.UsesWorldSlow ? TimeSlowManager.Instance.WorldDeltaTime : Time.deltaTime;
        if (!approach.Advance(dt)) { AbortApproach("Timed approach path became obstructed"); return; }
        if (approach.Progress < 1f) return;
        if (!defender.IsReadyFor(this, requestId)) { AbortApproach("Guard was not ready at timed impact"); return; }
        var guard = defender;
        resolved = true;
        ReleaseApproach();
        StopOwnedSkill();
        ApplyImpact(guard);
    }

    void OnApproachPlayback(CharacterAnimBrain.PlaybackSignal signal)
    {
        if (approach != null && signal.Kind == CharacterAnimBrain.PlaybackKind.Skill && signal.RequestId == requestId &&
            (signal.Phase == CharacterAnimBrain.PlaybackPhase.Interrupted || signal.Phase == CharacterAnimBrain.PlaybackPhase.Completed))
            AbortApproach("Skill playback interrupted during approach");
    }

    void ReleaseApproach()
    {
        if (ctx != null && ctx.AnimBrain != null) ctx.AnimBrain.PlaybackEvent -= OnApproachPlayback;
        var motor = approach; approach = null;
        motor?.Release();
    }

    void StopOwnedSkill()
    {
        if (ctx == null || ctx.LifeGeneration != life) return;
        // Before release, settle only this pending request through its existing cost policy.
        if (ctx.SkillManager != null && ctx.SkillManager.TryGetActiveCast(out var cast) && cast.RequestId == requestId)
            ctx.SkillManager.TryCancelActiveCast(SkillCastCancelReason.Blocked);
        ctx.AnimDriver?.CancelSkillCastRequest(requestId);
    }

    void AbortApproach(string reason)
    {
        ReleaseApproach();
        StopOwnedSkill();
        resolved = true;
        defender?.CancelFor(this, requestId);
        LastResult = reason;
    }
}
