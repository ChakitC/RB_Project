using System;

[Serializable]
public sealed class PassiveDynamicModifierStep : PassiveStep
{
    [Sirenix.OdinInspector.ValidateInput(nameof(HasValidScope), "WeaponOnly is not supported for this stat.")]
    public StatModifierScope scope;
    bool HasValidScope() => StatModifierRules.IsValidScope(stat, scope);
    public PassiveCondition condition = new();
    public StatType stat = StatType.Damage;
    [UnityEngine.Tooltip(StatModifierRules.ValueTooltip)]
    public ModifierOp operation = ModifierOp.AddPercent;
    public PassiveNumber value = new();
    public string minimumVariable;
    public override bool RunOnRefresh => true;
    public override PassiveResult Execute(PassiveExecution execution)
    {
        execution.Runtime.SetDynamic(execution, this);
        return PassiveResult.Success();
    }
}
