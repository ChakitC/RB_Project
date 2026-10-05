[Opsive.Shared.Utility.Category("AI/Perception")]
public sealed class HasLastSeenPosition : AICombatConditional
{
    public float maxObservationAge = 10f;
    protected override bool Evaluate()
    {
        if (sensor == null || State == null || State.Owner != new AITargetHandle(ctx)) return false;
        AITargetSnapshot s = sensor.ReadSnapshot;
        float age = sensor.ObservationClock - s.LastSeenTime;
        return !s.HasLiveTarget && AICombatTaskUtility.IsEligible(ctx, s.LastSeen) &&
            age >= 0f && age <= maxObservationAge && !State.IsConsumed(s);
    }
}
