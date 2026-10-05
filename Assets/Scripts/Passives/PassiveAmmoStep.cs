using System;
using UnityEngine;

[Serializable]
public sealed class PassiveAmmoStep : PassiveStep
{
    public PassiveNumber amount = new(1);
    public override PassiveResult Execute(PassiveExecution execution)
    {
        var weapon = execution.Owner.WeaponSystem;
        int actual = weapon != null ? weapon.RestoreMagazineFromPassive(Mathf.Max(0, Mathf.CeilToInt(amount.Resolve(execution)))) : 0;
        return actual > 0 ? PassiveResult.Success(actual, execution.Owner) : new(PassiveOutcome.Skipped);
    }
}
