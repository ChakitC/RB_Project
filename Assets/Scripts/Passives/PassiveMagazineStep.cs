using System;
using UnityEngine;

[Serializable]
public sealed class PassiveMagazineStep : PassiveStep
{
    public PassiveNumber ratio = new(0.2f);
    public int minimum = 1;
    public override PassiveResult Execute(PassiveExecution execution)
    {
        var weapon = execution.Owner.WeaponSystem;
        if (weapon == null || weapon.MagazineSize <= 0) return default;
        int requested = Mathf.Max(minimum, Mathf.CeilToInt(weapon.MagazineSize * ratio.Resolve(execution)));
        int restored = weapon.RestoreMagazineActual(requested);
        return restored > 0 ? PassiveResult.Success(restored, execution.Owner) : default;
    }
}
