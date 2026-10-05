using System;

[Serializable]
public sealed class PassiveDynamicModifierStep : PassiveStep
{
    public PassiveCondition condition = new();
    public StatType stat = StatType.Damage;
    public ModifierOp operation = ModifierOp.AddPercent;
    public PassiveNumber value = new();
    public override bool RunOnRefresh => true;
    public override PassiveResult Execute(PassiveExecution execution)
    {
        execution.Runtime.SetDynamic(execution, this);
        return PassiveResult.Success();
    }
}
