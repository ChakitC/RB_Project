using System;
using System.Collections.Generic;

[Serializable]
public sealed class PassiveModifierStep : PassiveStep
{
    public PassiveTarget target = new();
    public PassiveActionDefinition modifier = new();
    public bool whileEquipped;
    public override bool RunOnRefresh => whileEquipped;
    public override PassiveResult Execute(PassiveExecution execution)
    {
        if (whileEquipped)
        {
            execution.Runtime.SetEquippedModifier(execution.Key, modifier.modifiers);
            return PassiveResult.Success();
        }
        List<CharacteContext> succeeded = null;
        foreach (var actor in target.Resolve(execution))
        {
            var controller = actor.PassiveController;
            if (controller != null && controller.TryApplyRuntimeModifier(modifier,
                string.IsNullOrWhiteSpace(modifier.modifierKeyOverride) ? execution.Key : modifier.modifierKeyOverride,
                controller.Clock, execution.Runtime.Grant.Upgrades, execution.Grant.Options?.WeaponInstanceId)) (succeeded ??= new()).Add(actor);
        }
        return new PassiveResult(0, succeeded);
    }
}
