using System;
using System.Collections.Generic;
using UnityEngine;

public enum PassiveResourceOperation { RestoreStamina, Heal }

[Serializable]
public sealed class PassiveResourceStep : PassiveStep
{
    public PassiveResourceOperation operation;
    public PassiveTarget target = new();
    public PassiveNumber amount = new(10);
    public bool percentOfMaximum;

    public override PassiveResult Execute(PassiveExecution execution)
    {
        float applied = 0;
        List<CharacteContext> succeeded = null;
        foreach (var actor in target.Resolve(execution))
        {
            float requested = Mathf.Max(0f, amount.Resolve(execution));
            if (operation == PassiveResourceOperation.RestoreStamina && actor.StaminaSystem != null)
            {
                if (percentOfMaximum) requested *= actor.StaminaSystem.Max * 0.01f;
                float restored = actor.StaminaSystem.Restore(requested);
                if (restored > 0) { applied += restored; (succeeded ??= new()).Add(actor); }
            }
            else if (operation == PassiveResourceOperation.Heal && actor.HealthSystem != null)
            {
                if (percentOfMaximum) requested *= actor.HealthSystem.maximumHealth * 0.01f;
                float healed = actor.HealthSystem.HealActual(requested);
                if (healed > 0) { applied += healed; (succeeded ??= new()).Add(actor); }
            }
        }
        return new PassiveResult(applied, succeeded);
    }
}
