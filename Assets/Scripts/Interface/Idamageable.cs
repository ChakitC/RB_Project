using UnityEngine;

public interface IDamageable
{
    void TakeDamage(float finalDamage);
    
    bool IsAlive { get; }
    
}

public interface IHasArmor
{
    float Armor { get; }
}


