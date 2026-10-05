/// <summary>Lifetime guard retained by flying projectiles. Target-sensitive rules belong to the equipped grant.</summary>
public sealed class WeaponPassiveDamageAdapter : IWeaponAffixPreDamageRuntime
{
    readonly CharacteContext owner;
    public int Life { get; }
    public WeaponPassiveDamageAdapter(CharacteContext owner) { this.owner = owner; Life = owner.LifeGeneration; }
    public void ModifyDamage(ref WeaponDamageBuildContext damage)
    {
        if (owner == null || !owner.isActiveAndEnabled || owner.LifeGeneration != Life ||
            owner.WeaponSystem?.CurrentWeaponInstance?.instanceId != damage.WeaponInstanceId) return;
        owner.PassiveController?.ModifyWeaponDamage(ref damage);
    }
}
