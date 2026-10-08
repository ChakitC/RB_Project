using System;
using System.Collections.Generic;

/// <summary>Refreshes a grant-local deadline and resets only the declared variables on expiry.</summary>
[Serializable]
public sealed class PassiveExpiryStep : PassiveStep
{
    public PassiveNumber duration = new(4);
    public List<string> variables = new();
    public string expirySignal;
    public bool cancelOnUpgradeChange;
    public override PassiveResult Execute(PassiveExecution execution)
    {
        execution.Runtime.SetExpiry(execution, duration.Resolve(execution), variables, expirySignal, cancelOnUpgradeChange);
        return PassiveResult.Success();
    }
}
