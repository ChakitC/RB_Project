using UnityEngine;

public interface IDamageable
{
    void TakeDamage(in DamageContext damageContext);
    
    bool IsAlive { get; }
    
}

public interface IHasArmor
{
    float Armor { get; }
}

public static class DamageableExtensions
{
    public static void TakeDamage(
        this IDamageable damageable,
        float finalDamage,
        GameObject attacker = null,
        string sourceId = null,
        string attackId = null,
        ulong chainId = 0,
        int depth = 0,
        PassiveEventOrigin origin = PassiveEventOrigin.External,
        string originPassiveId = null,
        string originRuleId = null,
        KnockbackData knockback = default)
    {
        if (damageable == null)
            return;

        string resolvedSourceId = !string.IsNullOrWhiteSpace(sourceId)
            ? sourceId
            : attacker != null ? $"attacker:{attacker.name}" : "damage";

        ulong resolvedChainId = chainId == 0 ? CombatEventBus.NextChainId() : chainId;

        var damageContext = new DamageContext(
            finalDamage,
            attacker,
            resolvedSourceId,
            attackId,
            resolvedChainId,
            depth,
            origin,
            originPassiveId,
            originRuleId,
            knockback);

        damageable.TakeDamage(in damageContext);
    }
}


