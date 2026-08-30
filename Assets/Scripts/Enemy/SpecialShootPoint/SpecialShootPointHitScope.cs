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
        if (!SpecialShootPointRegistry.TryResolve(hitCollider, out SpecialShootPointInstance point))
            return None;

        SpecialShootPointController controller = point.Owner;
        if (controller == null || !controller.AcceptsPointDamageFrom(point, creditedActor))
            return None;

        // The projectile must have resolved this point's own enemy. A shot that resolved a different
        // actor has no business feeding this point, whatever collider it happened to overlap.
        if (target == null || !ReferenceEquals(target, controller.OwnerDamageable))
            return None;

        StaggerMeter meter = controller.Meter;
        meter?.BeginDirectHitStaggerDeferral();

        return new SpecialShootPointHitScope(controller, point, meter, creditedActor);
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
