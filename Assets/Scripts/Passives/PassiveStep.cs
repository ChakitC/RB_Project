using System;
using System.Collections.Generic;

public enum PassiveOutcome { Skipped, Succeeded, Failed }

public readonly struct PassiveResult
{
    public readonly PassiveOutcome Status;
    public readonly float Amount;
    public readonly CharacteContext Target;
    public readonly IReadOnlyList<CharacteContext> SuccessfulTargets;
    public PassiveResult(PassiveOutcome status, float amount = 0, CharacteContext target = null)
    { Status = status; Amount = amount; Target = target; SuccessfulTargets = target != null ? new[] { target } : Array.Empty<CharacteContext>(); }
    public PassiveResult(float amount, List<CharacteContext> targets)
    { Status = targets != null && targets.Count > 0 ? PassiveOutcome.Succeeded : PassiveOutcome.Skipped; Amount = amount; SuccessfulTargets = targets; Target = targets?.Count > 0 ? targets[targets.Count - 1] : null; }
    public static PassiveResult Success(float amount = 0, CharacteContext target = null) => new(PassiveOutcome.Succeeded, amount, target);
}

[Serializable]
public abstract class PassiveStep
{
    public string id = Guid.NewGuid().ToString("N");
    public string successSignal;
    public string successValueVariable;
    public virtual bool RunOnRefresh => false;
    public abstract PassiveResult Execute(PassiveExecution execution);
}
