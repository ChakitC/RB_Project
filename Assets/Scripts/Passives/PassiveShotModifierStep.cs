using System;
using UnityEngine;

public enum PassiveShotField { DamagePercent, CritRate, CritMultiplier, StaggerPercent, ImpactDamagePercent }

/// <summary>Patches the prepared shot or damage. Reservations are committed by the weapon owner.</summary>
[Serializable]
public sealed class PassiveShotModifierStep : PassiveStep
{
    public PassiveShotField field;
    public PassiveNumber value = new();
    public float radius = 2.5f;
    public override PassiveResult Execute(PassiveExecution execution)
    {
        var phase = execution.WeaponPhase;
        if (phase == null) return new(PassiveOutcome.Failed);
        float amount = value.Resolve(execution);
        if (phase.IsDamage)
        {
            if (field == PassiveShotField.DamagePercent) phase.Damage.Damage *= Mathf.Max(0, 1 + amount * .01f);
            else if (field == PassiveShotField.StaggerPercent) phase.Damage.StaggerPower *= Mathf.Max(0, 1 + amount * .01f);
            else return new(PassiveOutcome.Failed);
        }
        else switch (field)
        {
            case PassiveShotField.DamagePercent: phase.Shot.Damage *= Mathf.Max(0, 1 + amount * .01f); break;
            case PassiveShotField.CritRate: phase.Shot.CritRate = amount; break;
            case PassiveShotField.CritMultiplier: phase.Shot.CritMultiplier += amount; break;
            case PassiveShotField.StaggerPercent: phase.Shot.StaggerPower *= Mathf.Max(0, 1 + amount * .01f); break;
            case PassiveShotField.ImpactDamagePercent:
                phase.Shot.ImpactPayload = new WeaponAffixImpactPayload(execution.Runtime.Grant.Definition.passiveId,
                    radius, phase.Shot.Damage * amount * .01f); break;
        }
        return PassiveResult.Success();
    }
}
