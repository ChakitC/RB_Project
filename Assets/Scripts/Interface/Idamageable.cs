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
        KnockbackData knockback = default,
        StaggerPayload stagger = default)
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
            knockback,
            stagger);

        damageable.TakeDamage(in damageContext);
    }
}

public static class DamageableResolver
{
    public static IDamageable ResolveFrom(Collider collider)
    {
        return collider != null ? ResolveFrom(collider.transform) : null;
    }

    public static IDamageable ResolveFrom(Component component)
    {
        return component != null ? ResolveFrom(component.transform) : null;
    }

    public static IDamageable ResolveFrom(Transform transform)
    {
        if (transform == null)
            return null;

        CharacteContext context = transform.GetComponentInParent<CharacteContext>();
        if (context != null)
        {
            HealthSystem health = ResolveContextHealth(context);
            if (health != null)
                return health;
        }

        return transform.GetComponentInParent<IDamageable>();
    }

    static HealthSystem ResolveContextHealth(CharacteContext context)
    {
        if (context == null)
            return null;

        context.ResolveReferences();

        if (context.HealthSystem != null)
            return context.HealthSystem;

        HealthSystem health = context.GetComponent<HealthSystem>();
        if (health == null)
            health = context.GetComponentInChildren<HealthSystem>(true);

        if (health != null)
            context.HealthSystem = health;

        return health;
    }
}
