using System.Collections.Generic;
using UnityEngine;

public sealed class AITargetMemory
{
    struct Threat
    {
        public AITargetHandle Handle;
        public float Value;
        public float Time;
    }

    readonly Dictionary<int, Threat> threats = new Dictionary<int, Threat>();
    readonly List<int> removals = new List<int>();
    public AITargetHandle Current;
    public AITargetHandle LastSeen;
    public Vector3 LastSeenPosition;
    public float LastSeenTime = float.NegativeInfinity;
    public float AcquiredTime = float.NegativeInfinity;
    public float NextEvaluation;
    public AITargetSelectionReason Reason;

    public void Remember(AITargetCandidate candidate, float now)
    {
        LastSeen = candidate.Handle;
        LastSeenPosition = candidate.Point;
        LastSeenTime = now;
    }

    public float ThreatFor(AITargetHandle target, float now, float decay)
    {
        if (!threats.TryGetValue(target.InstanceId, out Threat entry) || entry.Handle != target)
            return 0f;
        return Mathf.Max(0f, entry.Value - Mathf.Max(0f, now - entry.Time) * decay);
    }

    public void AddThreat(AITargetHandle target, float amount, float now, float decay, float maximum)
    {
        threats[target.InstanceId] = new Threat
        {
            Handle = target, Value = Mathf.Clamp(ThreatFor(target, now, decay) + amount, 0f, maximum), Time = now
        };
    }

    public void Prune(CharacteContext owner, float now, float decay, bool requireAlive, bool teamFilter, int team)
    {
        removals.Clear();
        foreach (var pair in threats)
            if (!AITargetPerception.IsEligible(owner, pair.Value.Handle, requireAlive, teamFilter, team) ||
                ThreatFor(pair.Value.Handle, now, decay) <= 0.001f)
                removals.Add(pair.Key);
        for (int i = 0; i < removals.Count; i++) threats.Remove(removals[i]);
    }

    public void Forget(CharacteContext ctx)
    {
        if (ctx == null) return;
        threats.Remove(ctx.GetInstanceID());
        if (Current.Context == ctx) Current = default;
        if (LastSeen.Context == ctx) ClearLastSeen();
    }

    public void ClearLastSeen()
    {
        LastSeen = default;
        LastSeenPosition = Vector3.zero;
        LastSeenTime = float.NegativeInfinity;
    }

    public void ClearTargets()
    {
        Current = default;
        ClearLastSeen();
        AcquiredTime = float.NegativeInfinity;
        NextEvaluation = 0f;
        Reason = AITargetSelectionReason.None;
    }

    public void ClearThreat() => threats.Clear();
}
