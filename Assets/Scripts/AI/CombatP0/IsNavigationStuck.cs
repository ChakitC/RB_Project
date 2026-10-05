[Opsive.Shared.Utility.Category("AI/Navigation")]
public sealed class IsNavigationStuck : AICombatConditional
{
    protected override bool Evaluate()
    {
        if (State == null || State.Owner != new AITargetHandle(ctx) ||
            string.IsNullOrEmpty(State.NavigationFailure) || sensor == null) return false;
        AITargetHandle current = sensor.ReadSnapshot.Target;
        return State.NavigationTarget.InstanceId == 0 ? !current.IsLive :
            AICombatTaskUtility.IsEligible(ctx, State.NavigationTarget) &&
            (State.NavigationTarget == current || (State.SearchActive && !current.IsLive && State.NavigationTarget == State.SearchTarget));
    }
}
