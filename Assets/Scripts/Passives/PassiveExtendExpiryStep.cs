using System;

[Serializable]
public sealed class PassiveExtendExpiryStep : PassiveStep
{
    public string timerVariable;
    public PassiveNumber seconds = new(1);
    public PassiveNumber maximumExtension = new(3);
    public override PassiveResult Execute(PassiveExecution execution)
    {
        float actual = execution.Runtime.ExtendExpiry(execution.ResolveVariableKey(timerVariable),
            seconds.Resolve(execution), maximumExtension.Resolve(execution));
        return actual > 0 ? PassiveResult.Success(actual, execution.Owner) : new(PassiveOutcome.Skipped);
    }
}
