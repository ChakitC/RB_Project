using System;

[Serializable]
public sealed class PassiveReserveVariableStep : PassiveStep
{
    public string variable;
    public PassiveNumber amount = new(1);
    public string commitSignal;
    public override PassiveResult Execute(PassiveExecution execution)
    {
        if (execution.WeaponPhase == null || execution.WeaponPhase.IsDamage) return new(PassiveOutcome.Failed);
        return execution.WeaponPhase.Reserve(execution, variable, amount.Resolve(execution), commitSignal)
            ? PassiveResult.Success() : new(PassiveOutcome.Skipped);
    }
}
