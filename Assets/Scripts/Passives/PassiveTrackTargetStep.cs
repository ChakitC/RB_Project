using System;

[Serializable]
public sealed class PassiveTrackTargetStep : PassiveStep
{
    public string targetKey = "target";
    public string resetVariable = "stacks";
    public override PassiveResult Execute(PassiveExecution execution)
    {
        if (execution.EventTarget == null) return new(PassiveOutcome.Skipped);
        string key = execution.ScopedKey(targetKey);
        if (!execution.Runtime.MatchesTarget(key, execution.EventTarget))
            execution.WriteVariable(resetVariable, 0);
        execution.Runtime.TrackTarget(key, execution.EventTarget);
        return PassiveResult.Success();
    }
}
