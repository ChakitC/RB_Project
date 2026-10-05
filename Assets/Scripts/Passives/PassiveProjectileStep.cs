using System;
using UnityEngine;

[Serializable]
public sealed class PassiveProjectileStep : PassiveStep
{
    public ProjectileConfig config;
    public GameObject prefab;
    public PassiveNumber damageMultiplier = new(1);
    public PassiveNumber speedMultiplier = new(1);
    public override PassiveResult Execute(PassiveExecution execution)
    {
        var weapon = execution.Owner.WeaponSystem;
        return weapon != null && execution.WeaponPhase != null && weapon.SpawnPassiveProjectile(execution.WeaponPhase.Shot,
            config, prefab, damageMultiplier.Resolve(execution), speedMultiplier.Resolve(execution), execution.Runtime.Grant.Definition.passiveId)
            ? PassiveResult.Success() : new(PassiveOutcome.Failed);
    }
}
