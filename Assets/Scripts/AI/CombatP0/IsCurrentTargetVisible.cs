[Opsive.Shared.Utility.Category("AI/Perception")]
public sealed class IsCurrentTargetVisible : AICombatConditional
{
    protected override bool Evaluate()
    {
        if (sensor == null) return false;
        AITargetSnapshot s = sensor.ReadSnapshot;
        return s.IsCurrentVisible && AICombatTaskUtility.Matches(s.Target, target != null ? target.Value : null) &&
            AICombatTaskUtility.IsEligible(ctx, s.Target);
    }
}
