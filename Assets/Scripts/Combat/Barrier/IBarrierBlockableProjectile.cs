using UnityEngine;

/// <summary>
/// Implemented by projectile types that a barrier is allowed to swallow. The barrier gate reads
/// only what it needs to decide "hostile, and travelling inward?" and how much HP the shot costs.
/// </summary>
public interface IBarrierBlockableProjectile
{
    /// <summary>Actor that fired this projectile. Null means unknown faction, which passes through.</summary>
    GameObject BarrierSourceActor { get; }

    /// <summary>Where the projectile started. A shot born inside a barrier is never blocked by it.</summary>
    Vector3 BarrierSpawnPosition { get; }

    /// <summary>Current travel direction, used to let outbound shots leave.</summary>
    Vector3 BarrierTravelDirection { get; }

    /// <summary>
    /// Damage this shot spends on the barrier: after range falloff, crit, and damage multipliers,
    /// but before the target's armor and hit-zone scaling.
    /// </summary>
    float GetBarrierImpactDamage();

    /// <summary>
    /// The barrier consumed this projectile. Implementations must despawn without running any
    /// OnHit, status, explosion, split/chain, or gameplay hit VFX.
    /// </summary>
    void OnBlockedByBarrier(in BarrierBlockContext context);
}

/// <summary>Where and by what a projectile was stopped.</summary>
public readonly struct BarrierBlockContext
{
    public readonly IProjectileBarrier Barrier;
    public readonly Vector3 HitPoint;
    public readonly Vector3 HitNormal;

    public BarrierBlockContext(IProjectileBarrier barrier, Vector3 hitPoint, Vector3 hitNormal)
    {
        Barrier = barrier;
        HitPoint = hitPoint;
        HitNormal = hitNormal;
    }
}
