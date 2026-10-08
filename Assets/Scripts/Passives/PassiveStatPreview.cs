using System.Collections.Generic;

/// <summary>Projects only unconditional Equipped stats. No runtime, subscription, counter or save write.</summary>
public static class PassiveStatPreview
{
    public static void Append(in PassiveGrant grant, List<RuntimeStatModifier> output)
    {
        if (grant.Definition?.recipe == null) return;
        if (PassiveRecipeValidator.Validate(grant.Definition.recipe).Count != 0) return;
        var execution = new PassiveExecution(grant);
        foreach (var rule in grant.Definition.recipe.rules)
            if (rule.trigger == PassiveRuleTrigger.Equipped && rule.condition.IsStatic && rule.condition.Evaluate(execution))
                Steps(rule.steps, execution, output, new HashSet<PassiveFragment>());
    }
    static void Steps(List<PassiveStep> steps, PassiveExecution execution, List<RuntimeStatModifier> output, HashSet<PassiveFragment> stack)
    {
        foreach (var step in steps)
        {
            if (step is PassiveDynamicModifierStep dynamic && dynamic.condition.IsStatic && dynamic.condition.Evaluate(execution) && dynamic.value.IsStatic)
                output.Add(new RuntimeStatModifier(dynamic.stat, dynamic.operation, dynamic.value.Resolve(execution, true), execution.Key + "/" + step.id, dynamic.scope, execution.Grant.Options?.WeaponInstanceId));
            else if (step is PassiveModifierStep modifier && modifier.whileEquipped)
                foreach (var stat in modifier.modifier.modifiers)
                {
                    float value = stat.value;
                    if (execution.Grant.Upgrades?.TryGetAggregate(stat.statType, out float add, out float multiply) == true) value = (value + add) * multiply;
                    output.Add(new RuntimeStatModifier(stat.statType, stat.operation, value, execution.Key + "/" + step.id, stat.scope, execution.Grant.Options?.WeaponInstanceId));
                }
            else if (step is PassiveBranchStep branch && branch.condition.IsStatic)
                Steps(branch.condition.Evaluate(execution) ? branch.whenTrue : branch.whenFalse, execution, output, stack);
            else if (step is PassiveFragmentStep call && call.fragment != null && stack.Add(call.fragment))
            { Steps(call.fragment.steps, execution.Enter(call), output, stack); stack.Remove(call.fragment); }
        }
    }
}
