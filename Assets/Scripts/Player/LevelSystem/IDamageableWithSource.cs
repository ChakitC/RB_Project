using UnityEngine;

public interface IDamageableWithSource : IDamageable
{
    void TakeDamage(float finalDamage, GameObject attacker);
}

public interface IDamageableWithContext : IDamageable
{
    void TakeDamage(in DamageContext damageContext);
}
