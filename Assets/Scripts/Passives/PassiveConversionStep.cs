using System;

[Serializable]
public sealed class PassiveConversionStep : PassiveStep
{
    public StatType source = StatType.StaminaRegenRate;
    public StatType destination = StatType.MaxStamina;
    public PassiveNumber fraction = new(1);
    public PassiveNumber ratio = new(4);
    public override bool RunOnRefresh => true;
    public override PassiveResult Execute(PassiveExecution execution)
    {
        execution.Runtime.SetConversion(execution.Key, fraction.Resolve(execution), ratio.Resolve(execution));
        return PassiveResult.Success();
    }
}
