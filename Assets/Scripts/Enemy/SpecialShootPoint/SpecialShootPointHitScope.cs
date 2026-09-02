using System;
using UnityEngine;

/// <summary>
/// The direct-hit contract for Special Shoot Points.
///
/// One direct player ranged hit is one gameplay result. The enemy takes damage exactly once through
/// its ordinary pipeline — armor, crit, hit-zone multiplier, weapon and skill modifiers all still
/// apply — and the very same <see cref="DamageResult.AppliedDamage"/> is then fed to the point. The
/// point never calls <c>TakeDamage</c> itself and never publishes a second damage event, so the
/// player sees one damage number.
///
/// The scope also opens the meter's deferral <em>before</em> <c>TakeDamage</c> runs, which is what
/// makes the final-point shot atomic: HP damage, the shot's own stagger, the point damage, and the
/// Special Point reward all land before anything is allowed to react to a full meter.
///
/// Usage — the <c>using</c> is not optional, it is the <c>try/finally</c> that guarantees an
/// exception or an early return cannot leave the meter permanently deferred:
/// <code>
/// using (var scope = SpecialShootPointHitScope.Begin(hitCollider, target, creditedActor))
/// {
///     DamageResult result = ApplyDamageToTarget(...);
///     scope.ApplyPointDamage(result);
/// }
/// </code>
///
/// Deliberately reachable only from direct-collision paths. AoE, explosions, splash, weapon-affix
/// area damage, melee, ally/helper/AI attacks, chain steps, and status ticks must never route
/// through it.
/// </summary>
public readonly struct SpecialShootPointHitScope : IDisposable
{
    readonly SpecialShootPointController _controller;
    readonly SpecialShootPointInstance _point;
    readonly StaggerMeter _meter;
    readonly GameObject _creditedActor;

    SpecialShootPointHitScope(
        SpecialShootPointController controller,
        SpecialShootPointInstance point,
        StaggerMeter meter,
        GameObject creditedActor)
    {
        _controller = controller;
        _point = point;
        _meter = meter;
        _creditedActor = creditedActor;
    }

    /// <summary>An inert scope. Every ordinary hit produces one of these and pays almost nothing.</summary>
    public static SpecialShootPointHitScope None => default;

    /// <summary>True when the collider that was hit is a live, eligible Special Shoot Point.</summary>
    public bool HasPoint => _point != null;

    /// <summary>
    /// The hit zone the selected anchor authored, or <see cref="CharacterHitZone.None"/> for an
    /// inert scope. A head anchor therefore takes the ordinary Headshot path.
    /// </summary>
    public CharacterHitZone HitZone => _point != null ? _point.HitZone : CharacterHitZone.None;

    /// <summary>
    /// Opens the transaction for one direct hit. Cheap and side-effect-free when
    /// <paramref name="hitCollider"/> is not a live point, which is every ordinary shot.
    /// </summary>
    /// <param name="target">The enemy resolved for this hit. Must be the point's own owner.</param>
    /// <param name="creditedActor">
    /// The actor credited with the shot. Only the player may damage a point, so ally, helper, and
    /// AI hits produce an inert scope.
    /// </param>
    public static SpecialShootPointHitScope Begin(
        Collider hitCollider,
        IDamageable target,
        GameObject creditedActor)
    {
        return Begin(hitCollider, target, creditedActor, default, 0f, false);
    }

    /// <summary>
    /// Opens the transaction for one direct hit, and — when the collider struck was an outer body
    /// hit zone rather than a point — credits the shot to a point its trajectory would have reached.
    ///
    /// An enemy's authored hit zones are coarse boxes, so a weak point sitting on the body surface
    /// is still inside them and the projectile trigger is consumed before it can reach the point.
    /// This resolves that ordering. It is not aim assist: the projectile is never steered and the
    /// point is never widened — <paramref name="shot"/> must genuinely intersect the point's own
    /// collider, within the profile's redirect distance.
    /// </summary>
    /// <param name="shot">The projectile's current position and travel direction.</param>
    /// <param name="shotBacktrack">
    /// How far <paramref name="shot"/>'s origin was rewound along the trajectory to reach the real
    /// impact point. Added to the profile's redirect budget so the rewind does not consume it.
    /// </param>
    public static SpecialShootPointHitScope Begin(
        Collider hitCollider,
        IDamageable target,
        GameObject creditedActor,
        Ray shot,
        float shotBacktrack = 0f,
        bool allowTrajectoryRedirect = true)
    {
        bool direct = SpecialShootPointRegistry.TryResolve(hitCollider, out SpecialShootPointInstance point);

        if (!direct)
        {
            if (!allowTrajectoryRedirect)
                return None;

            if (!TryRedirectToPointBehindHitZone(hitCollider, target, creditedActor, shot, shotBacktrack, out point))
            {
                TraceMiss(hitCollider, target, creditedActor);
                return None;
            }
        }

        SpecialShootPointController controller = point.Owner;
        if (controller == null)
            return None;

        if (!controller.AcceptsPointDamageFrom(point, creditedActor))
        {
            Log(controller,
                $"rejected by AcceptsPointDamageFrom: phase={controller.Phase} " +
                $"hittable={point.IsHittable} credit={(creditedActor != null ? creditedActor.name : "null")}");
            return None;
        }

        // The projectile must have resolved this point's own enemy. A shot that resolved a different
        // actor has no business feeding this point, whatever collider it happened to overlap.
        if (target == null || !ReferenceEquals(target, controller.OwnerDamageable))
        {
            Log(controller, "target is not this point's own enemy");
            return None;
        }

        Log(controller,
            $"{(direct ? "DIRECT" : "REDIRECT")} hit on '{point.transform.parent?.name}' " +
            $"via collider '{hitCollider?.name}' zone={point.HitZone}");

        StaggerMeter meter = controller.Meter;
        meter?.BeginDirectHitStaggerDeferral();

        return new SpecialShootPointHitScope(controller, point, meter, creditedActor);
    }

    /// <summary>
    /// Resolves the point a shot was heading for when an outer hit zone stopped it first.
    /// Deliberately scoped to the enemy that was actually struck: a shot cannot be credited to a
    /// point on some other actor that happens to lie along the same line.
    /// </summary>
    static bool TryRedirectToPointBehindHitZone(
        Collider hitCollider,
        IDamageable target,
        GameObject creditedActor,
        Ray shot,
        float shotBacktrack,
        out SpecialShootPointInstance point)
    {
        point = null;

        if (hitCollider == null || target == null)
            return false;

        // A zero direction means the caller had no trajectory to offer.
        if (shot.direction.sqrMagnitude < 0.0001f)
            return false;

        EnemyContext enemy = hitCollider.GetComponentInParent<EnemyContext>();
        if (enemy == null)
            return false;

        SpecialShootPointController controller = enemy.SpecialShootPoints;
        if (controller == null)
            return false;

        // The struck collider and the point must belong to the same enemy.
        if (!ReferenceEquals(target, controller.OwnerDamageable))
            return false;

        return controller.TryResolvePointAlongShot(shot, shotBacktrack, creditedActor, out point);
    }

    /// <summary>
    /// Explains why a shot on an enemy that has a live round was not credited to any point.
    /// </summary>
    static void TraceMiss(Collider hitCollider, IDamageable target, GameObject creditedActor)
    {
        if (hitCollider == null)
            return;

        EnemyContext enemy = hitCollider.GetComponentInParent<EnemyContext>();
        SpecialShootPointController controller = enemy != null ? enemy.SpecialShootPoints : null;
        if (controller == null || !controller.IsRoundActive)
            return;

        Log(controller,
            $"NO POINT for hit on '{hitCollider.name}' — phase={controller.Phase} " +
            $"livePoints={controller.PointsRemaining} " +
            $"credit={(creditedActor != null ? creditedActor.name : "null")} " +
            $"sameEnemy={ReferenceEquals(target, controller.OwnerDamageable)}");
    }

    /// <summary>
    /// Traces the direct-hit path, gated on the owning profile's <c>debugLogging</c> — the same
    /// place and shape as <c>ChainAttackTeleportProfileDef.debugLogging</c>. Reading the flag off
    /// the controller that is already in hand means there is no static state to reset on every
    /// domain reload, and no toggle that only code can reach.
    /// </summary>
    static void Log(SpecialShootPointController controller, string message)
    {
        if (controller == null || controller.Profile == null || !controller.Profile.debugLogging)
            return;

        Debug.Log($"[SSP-HIT] {message}", controller);
    }

    /// <summary>
    /// Feeds the already-applied enemy damage to the point. Call once, inside the scope, with the
    /// result of the single <c>TakeDamage</c> this hit performed.
    /// </summary>
    public void ApplyPointDamage(in DamageResult result)
    {
        if (_controller == null || _point == null)
            return;

        if (!result.Applied)
            return;

        // Death wins outright. A shot that kills the enemy must not also complete the round and
        // resurrect it into a Mini Stun and ChainReady.
        if (!result.IsAliveAfter)
            return;

        _controller.ApplyPointDamage(_point, result.AppliedDamage, _creditedActor);
    }

    /// <summary>
    /// Commits the transaction. If the meter filled from the shot's ordinary stagger and the round
    /// did not complete, this is where ChainReady finally happens.
    /// </summary>
    public void Dispose()
    {
        _meter?.EndDirectHitStaggerDeferral();
    }
}
