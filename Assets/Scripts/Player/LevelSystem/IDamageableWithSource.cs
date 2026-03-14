using UnityEngine;

public interface IDamageableWithSource : IDamageable
{
    void TakeDamage(float finalDamage, GameObject attacker);
}