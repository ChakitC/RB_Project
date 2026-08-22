using UnityEngine;

public readonly struct ProjectileHitInfo
{
    public readonly Vector3 point;
    public readonly Vector3 normal;
    public readonly Collider collider;
    public readonly bool hasPoint;

    public ProjectileHitInfo(Vector3 point, Vector3 normal, Collider collider, bool hasPoint = true)
    {
        this.point = point;
        this.normal = normal;
        this.collider = collider;
        this.hasPoint = hasPoint;
    }

    public bool TryGetPoint(out Vector3 resolvedPoint)
    {
        resolvedPoint = point;
        return hasPoint;
    }

    public Vector3 ResolvePoint(Vector3 fallbackPoint)
    {
        return hasPoint ? point : fallbackPoint;
    }

    public static ProjectileHitInfo WithoutPoint(Vector3 normal, Collider collider)
    {
        return new ProjectileHitInfo(default, normal, collider, hasPoint: false);
    }
}

[System.Serializable]
public struct ProjectileStats
{
    public float damage;
    public float speed;
    public float staggerPower;
}

public struct ProjectileContext
{
    public Transform sourceActor;
    public Transform collisionIgnoreRoot;
    public Transform aimTarget;
    public CombatEventBus combatEventBus;
    public StatusEffectController statusEffectController;
    public Vector3 dir;
    public ProjectileStats stats;
    public AudioCue hitCue;
    public string damageSourceId;
    public string attackId;
    public ulong chainId;

    /// <summary>Combat / passive event chain depth. Carries gameplay meaning; not a spawn budget.</summary>
    public int depth;

    /// <summary>
    /// How many split hops this projectile is from the shot that was fired. Purely a spawn budget,
    /// kept separate from <see cref="depth"/> so capping splits cannot distort event chains.
    /// </summary>
    public int splitGeneration;

    /// <summary>
    /// Effective split budget inherited down the chain, as <c>min</c> of every budget authored so
    /// far. Zero means nothing has been inherited yet. Carried in the context rather than read
    /// per-module so a permissive child config cannot widen a limit its ancestor already set.
    /// </summary>
    public int splitBudget;
    public PassiveEventOrigin origin;
    public string originPassiveId;
    public string originRuleId;
    public Projectile projectilePrefab;
    public bool useHitZones;
    public CombatEventMetadata combatMetadata;
    public CombatAttributionSnapshot attribution;
    public IWeaponAffixPreDamageRuntime preDamageRuntime;
    public WeaponAffixImpactPayload affixImpactPayload;
}
