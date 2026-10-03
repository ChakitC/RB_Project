using UnityEngine;

/// <summary>Selection only: no scene queries, clocks, random generator or events.</summary>
public sealed class AITargetSelector
{
    public float Score(AITargetCandidate candidate, AITargetMemory memory, AITargetScanPolicy policy, float now)
    {
        float score = candidate.BasePriority;
        if (candidate.Identity == AITargetIdentity.Player) score += policy.PlayerBonus;
        if (candidate.Identity == AITargetIdentity.Companion) score += policy.CompanionBonus;
        switch (candidate.CombatRole)
        {
            case CharacterCombatRole.Tank: score += policy.TankBonus; break;
            case CharacterCombatRole.Healer: score += policy.HealerBonus; break;
            case CharacterCombatRole.Support: score += policy.SupportBonus; break;
            case CharacterCombatRole.Sniper: score += policy.SniperBonus; break;
            case CharacterCombatRole.Assault: score += policy.AssaultBonus; break;
        }
        if (policy.Radius > .01f) score += (1f - Mathf.Clamp01(candidate.Distance / policy.Radius)) * policy.DistanceWeight;
        if (policy.UseThreat) score += memory.ThreatFor(candidate.Handle, now, policy.ThreatDecay) * policy.ThreatWeight;
        if (candidate.Handle == memory.Current) score += policy.Stickiness;
        if (!candidate.HasLineOfSight) score -= policy.LosPenalty;
        return score;
    }

    public AITargetCandidate Select(AITargetMemory memory, AITargetCandidate best,
        AITargetCandidate current, AITargetCandidate taunt, AITargetSelectionPolicy policy,
        float now, bool force, out AITargetSelectionReason reason)
    {
        AITargetCandidate selected = default;
        reason = AITargetSelectionReason.None;
        bool endingTaunt = memory.Reason == AITargetSelectionReason.Taunt && taunt.Handle.InstanceId == 0;
        if (taunt.Handle.InstanceId != 0)
        {
            selected = taunt;
            reason = AITargetSelectionReason.Taunt;
        }
        else if (current.IsVisible && !endingTaunt)
        {
            selected = current;
            float threshold = Mathf.Max(policy.SwitchThreshold, Mathf.Abs(current.Score) * policy.SwitchRatio);
            if (best.Handle != current.Handle && best.Handle.InstanceId != 0 && best.Score > current.Score + threshold &&
                (force || (now - memory.AcquiredTime >= policy.MinLockDuration && now >= memory.NextEvaluation)))
                selected = best;
            if (force || now >= memory.NextEvaluation) memory.NextEvaluation = now + policy.EvaluationDelay;
            reason = AITargetSelectionReason.Score;
        }
        else if (best.Handle.InstanceId != 0 && (endingTaunt || force || !memory.Current.IsLive ||
            now - memory.LastSeenTime >= policy.LostHoldDuration))
        {
            selected = best;
            reason = AITargetSelectionReason.Score;
        }
        else if (memory.Current.IsLive && memory.LastSeen == memory.Current &&
            now - memory.LastSeenTime <= policy.GracePeriod)
        {
            selected = current;
            selected.Handle = memory.Current;
            selected.Point = memory.LastSeenPosition;
            reason = AITargetSelectionReason.Memory;
        }

        if (selected.Handle != memory.Current)
        {
            memory.AcquiredTime = selected.Handle.InstanceId != 0 ? now : float.NegativeInfinity;
            memory.NextEvaluation = now + policy.EvaluationDelay;
        }
        memory.Current = selected.Handle;
        memory.Reason = reason;
        if (selected.IsVisible) memory.Remember(selected, now);
        if (!policy.KeepLastSeen && now - memory.LastSeenTime > policy.GracePeriod) memory.ClearLastSeen();
        return selected;
    }
}
