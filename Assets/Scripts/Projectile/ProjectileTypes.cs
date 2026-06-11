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
    public int depth;
    public PassiveEventOrigin origin;
    public string originPassiveId;
    public string originRuleId;
    public Projectile projectilePrefab;
}
