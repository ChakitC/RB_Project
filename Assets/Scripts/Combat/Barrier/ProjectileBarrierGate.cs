using UnityEngine;

/// <summary>
/// The single place that decides whether a barrier eats a projectile. Every projectile path
/// calls this before wall handling, area damage, damage application, and module callbacks, so a
/// blocked shot never produces an OnHit, a status, an explosion, a split/chain, or hit VFX.
/// </summary>
public static class ProjectileBarrierGate
{
    const string BarrierLayerName = "Barrier";
    const int UnresolvedLayer = -2;

    static int barrierLayer = UnresolvedLayer;
    static bool warnedMissingLayer;

    public static int BarrierLayer
    {
        get
        {
            if (barrierLayer == UnresolvedLayer)
            {
                barrierLayer = LayerMask.NameToLayer(BarrierLayerName);
                if (barrierLayer < 0 && !warnedMissingLayer)
                {
                    warnedMissingLayer = true;
                    Debug.LogWarning(
                        $"[ProjectileBarrierGate] Layer '{BarrierLayerName}' is not defined in Project Settings. " +
                        "Barriers will not block projectiles.");
                }
            }

            return barrierLayer;
        }
    }

    public static int BarrierMask => BarrierLayer >= 0 ? 1 << BarrierLayer : 0;

    public static bool IsBarrierCollider(Collider collider)
    {
        return collider != null && BarrierLayer >= 0 && collider.gameObject.layer == BarrierLayer;
    }

    /// <summary>
    /// Trigger-contact entry point. Returns true when the projectile was absorbed, in which case
    /// the caller must stop processing this contact entirely.
    /// </summary>
    public static bool TryBlock(IBarrierBlockableProjectile projectile, Collider barrierCollider)
    {
        if (!BarrierRegistry.HasActiveBarrier)
            return false;

        if (projectile == null || !IsBarrierCollider(barrierCollider))
            return false;

        IProjectileBarrier barrier = ResolveBarrier(barrierCollider);
        if (barrier == null)
            return false;

        Vector3 projectilePosition = ResolvePosition(projectile);
        Vector3 hitPoint = barrierCollider.ClosestPoint(projectilePosition);
        Vector3 hitNormal = ResolveHitNormal(barrier, hitPoint, projectile);

        return TryBlock(projectile, barrier, hitPoint, hitNormal);
    }

    /// <summary>
    /// Sweep entry point for fast projectiles that would otherwise tunnel past the trigger.
    /// </summary>
    public static bool TrySweepBlock(
        IBarrierBlockableProjectile projectile,
        Vector3 origin,
        float sweepRadius,
        Vector3 sweepDirection,
        float sweepDistance)
    {
        // Hot path: this runs for every projectile every physics step, so bail before touching
        // the physics scene when no barrier exists at all.
        if (!BarrierRegistry.HasActiveBarrier)
            return false;

        int mask = BarrierMask;
        if (projectile == null || mask == 0 || sweepDistance <= 0f)
            return false;

        if (!Physics.SphereCast(
                origin,
                Mathf.Max(0.001f, sweepRadius),
                sweepDirection,
                out RaycastHit hit,
                sweepDistance,
                mask,
                QueryTriggerInteraction.Collide))
        {
            return false;
        }

        IProjectileBarrier barrier = ResolveBarrier(hit.collider);
        if (barrier == null)
            return false;

        Vector3 hitNormal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal : -sweepDirection;
        return TryBlock(projectile, barrier, hit.point, hitNormal);
    }

    static bool TryBlock(
        IBarrierBlockableProjectile projectile,
        IProjectileBarrier barrier,
        Vector3 hitPoint,
        Vector3 hitNormal)
    {
        if (!barrier.IsBarrierActive)
            return false;

        if (!barrier.BlocksProjectileFrom(projectile.BarrierSourceActor))
            return false;

        if (!IsTravellingInward(projectile, barrier))
            return false;

        barrier.AbsorbProjectile(
            Mathf.Max(0f, projectile.GetBarrierImpactDamage()),
            hitPoint,
            hitNormal);

        projectile.OnBlockedByBarrier(new BarrierBlockContext(barrier, hitPoint, hitNormal));
        return true;
    }

    /// <summary>
    /// A barrier only stops shots coming from outside. Anything fired from within the volume —
    /// including the protected turret's own fire — leaves freely.
    /// </summary>
    static bool IsTravellingInward(IBarrierBlockableProjectile projectile, IProjectileBarrier barrier)
    {
        float radius = barrier.BarrierRadius;
        if (radius <= 0f)
            return false;

        Vector3 center = barrier.BarrierCenter;

        if ((projectile.BarrierSpawnPosition - center).sqrMagnitude <= radius * radius)
            return false;

        Vector3 direction = projectile.BarrierTravelDirection;
        if (direction.sqrMagnitude <= 0.0001f)
            return true;

        Vector3 toCenter = center - ResolvePosition(projectile);
        if (toCenter.sqrMagnitude <= 0.0001f)
            return true;

        return Vector3.Dot(direction.normalized, toCenter.normalized) > 0f;
    }

    static Vector3 ResolvePosition(IBarrierBlockableProjectile projectile)
    {
        return projectile is Component component ? component.transform.position : Vector3.zero;
    }

    static Vector3 ResolveHitNormal(
        IProjectileBarrier barrier,
        Vector3 hitPoint,
        IBarrierBlockableProjectile projectile)
    {
        Vector3 outward = hitPoint - barrier.BarrierCenter;
        if (outward.sqrMagnitude > 0.0001f)
            return outward.normalized;

        Vector3 direction = projectile.BarrierTravelDirection;
        return direction.sqrMagnitude > 0.0001f ? -direction.normalized : Vector3.up;
    }

    static IProjectileBarrier ResolveBarrier(Collider collider)
    {
        if (collider == null)
            return null;

        IProjectileBarrier barrier = collider.GetComponent<IProjectileBarrier>();
        return barrier ?? collider.GetComponentInParent<IProjectileBarrier>();
    }
}
