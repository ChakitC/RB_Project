using UnityEngine;

public sealed partial class DefensiveBlockAttack
{
    public bool IsCooperative => profile != null && profile.mode == DefensiveBlockMode.Cooperative;
    public CooperativeBlockSession CooperativeSession { get; private set; }
    bool HasCooperativeSession => CooperativeSession != null && CooperativeSession.IsActive;

    bool TryCaptureCooperativeDamage(out CooperativeBlockDamageSnapshot snapshot, bool previewOnly = false)
    {
        snapshot = null;
        var steps = profile?.Steps(WindowIndex);
        if (execution == null || steps == null || steps.Length == 0) return false;
        // Step index is payload order, independent of how the author ordered the window's bindings.
        int first = int.MaxValue;
        foreach (int step in steps) first = Mathf.Min(first, step);
        return previewOnly ? execution.CanCaptureCooperativeDamage(requestId, life, first) :
            execution.TryCaptureCooperativeDamage(requestId, life, first, out snapshot);
    }

    internal bool TryPlanCooperative(PlayerContext issuer, DefensiveBlockController helper,
        out CharacterPlacementRequest playerPose, out CharacterPlacementRequest allyPose,
        out Vector3 destination, out CharacterPlacementFootprint footprint)
        => TryPlanCooperative(issuer, helper, out playerPose, out allyPose, out destination, out footprint, out _);

    internal bool TryPlanCooperative(PlayerContext issuer, DefensiveBlockController helper,
        out CharacterPlacementRequest playerPose, out CharacterPlacementRequest allyPose,
        out Vector3 destination, out CharacterPlacementFootprint footprint, out string reason)
    {
        playerPose = allyPose = null; destination = default; footprint = default;
        reason = "Cooperative window or pair unavailable";
        var guard = issuer != null ? issuer.DefensiveBlock : null;
        if (!IsCooperative || !WindowOpen || guard == null || helper == null || guard == helper) return false;
        reason = "Player not ready for Cooperative Block";
        if (!guard.CanBeginCooperative(issuer, this)) return false;
        reason = "Ally not ready for Cooperative Block";
        if (!helper.CanBeginCooperative(issuer, this)) return false;
        reason = "Cooperative approach playback unavailable";
        if (!CanPlanApproachPlayback()) return false;
        reason = "Player and attacker positions overlap";
        Vector3 forward = ctx.transform.position - issuer.transform.position; forward.y = 0f;
        if (forward.sqrMagnitude < .01f) return false;
        Quaternion rotation = Quaternion.LookRotation(forward);
        reason = "Unsafe Cooperative Player placement";
        if (!guard.TryCooperativePose(issuer, this, issuer.transform.position, rotation, out playerPose, out var p)) return false;
        Vector3 right = rotation * Vector3.right;
        float separation = guard.CooperativeRadius + helper.CooperativeRadius + .15f;
        reason = "No safe Cooperative Ally placement";
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 desired = p.StartPosition + right * (side * separation);
            if (!helper.TryCooperativePose(issuer, this, desired, rotation, out allyPose, out var a)) continue;
            reason = "No safe Cooperative attacker approach path";
            if (DefensiveBlockApproachMotor.TryPlan(ctx, (p.StartPosition + a.StartPosition) * .5f, rotation,
                    profile.approachStandOff, worldLayers, out destination, out footprint))
            { reason = "Ready"; return true; }
        }
        return false;
    }

    InterruptionCommandResult RequestCooperativeBlock(PlayerContext issuer)
    {
        if (issuer.interruptionCommand == null)
            return Result(InterruptionCommandResult.SkillRejected, "Player Block command controller missing");
        if (!issuer.interruptionCommand.TrySelectCooperativeAlly(this, out var helper, out string reason))
            return Result(InterruptionCommandResult.NoAvailableAlly, reason);
        if (!TryPlanCooperative(issuer, helper, out var p, out var a, out var destination, out var footprint, out reason))
            return Result(InterruptionCommandResult.TeleportFailed, reason);
        if (!TryCaptureCooperativeDamage(out var snapshot))
            return Result(InterruptionCommandResult.SkillRejected, "Cooperative damage snapshot unavailable");
        acceptedWindow = WindowIndex; acceptedWindowEnd = CurrentWindowEnd; protectedPlayer = issuer;
        var session = new CooperativeBlockSession(this, issuer, helper, snapshot, profile.cooperative);
        CooperativeSession = session;
        if (!session.Begin(p, a, destination, footprint, profile.ApproachDuration))
        { session.Cancel(); return Result(InterruptionCommandResult.SkillRejected, "Cooperative preparation cancelled"); }
        issuer.interruptionCommand.SetCooperativeSession(session);
        return Result(InterruptionCommandResult.Success, "Cooperative block preparing");
    }

    internal bool BeginCooperativeApproach(int id, float duration)
    {
        if (id != requestId || !OwnsCurrentSkill) return false;
        if (!BeginApproachPlayback(duration)) return false;
        suppressExecution = true; execution?.StopExecution(id); execution = null;
        return true;
    }

    internal void ResolveCooperative(int id, CooperativeBlockState result)
    {
        if (id != requestId || ctx == null || ctx.LifeGeneration != life) return;
        resolved = true; suppressExecution = true;
        execution?.StopExecution(id); execution = null;
        StopOwnedSkill();
        if (result == CooperativeBlockState.Success) SuccessCount++;
        LastResult = "Cooperative block: " + result;
    }
}
