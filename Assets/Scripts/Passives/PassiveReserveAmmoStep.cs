using System;
using UnityEngine;

[Serializable]
public sealed class PassiveReserveAmmoStep : PassiveStep
{
    public PassiveNumber amount = new(1);
    public override PassiveResult Execute(PassiveExecution execution)
    {
        int actual = execution.Owner.WeaponSystem != null
            ? execution.Owner.WeaponSystem.TransferReserveToMagazine(Mathf.Max(0, Mathf.CeilToInt(amount.Resolve(execution)))) : 0;
        return actual > 0 ? PassiveResult.Success(actual, execution.Owner) : new(PassiveOutcome.Skipped);
    }
}
