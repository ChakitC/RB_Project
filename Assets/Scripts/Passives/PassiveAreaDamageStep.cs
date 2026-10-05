using System;
using UnityEngine;

[Serializable]
public sealed class PassiveAreaDamageStep : PassiveStep
{
    public PassiveNumber damage = new();
    public PassiveNumber maximumDamage = new(1000000);
    public PassiveNumber radius = new(3);
    public override PassiveResult Execute(PassiveExecution execution)
    {
        if (execution.EventTarget == null) return new(PassiveOutcome.Skipped);
        var evt = execution.Event;
        return WeaponAffixAreaDamage.ApplyResolved(execution.Owner, execution.Owner.CombatEventBus, execution.EventTarget.transform.position,
            radius.Resolve(execution), Mathf.Min(damage.Resolve(execution), maximumDamage.Resolve(execution)),
            evt.Metadata.WeaponInstanceId, execution.Runtime.Grant.Definition.passiveId, evt.AttackId, evt.ChainId, evt.Depth);
    }
}
