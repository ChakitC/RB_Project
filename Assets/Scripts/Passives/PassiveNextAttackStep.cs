using System;

[Serializable]
public sealed class PassiveNextAttackStep : PassiveStep
{
    public string variable;
    public PassiveNumber damagePercentPerUnit = new(2);
    public override bool RunOnRefresh => true;
    public override PassiveResult Execute(PassiveExecution execution)
    {
        execution.Runtime.SetNextAttack(execution, this);
        return PassiveResult.Success();
    }
}
