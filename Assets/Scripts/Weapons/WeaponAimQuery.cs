using UnityEngine;

public sealed class WeaponAimQuery
{
    readonly RaycastHit[] raycastHits = new RaycastHit[64];
    const string HurtboxLayerName = "Hit";
    const int UnresolvedLayer = -2;
    static int hurtboxLayer = UnresolvedLayer;
    static bool warnedMissingHurtboxLayer;

    public bool TryGetFirstValidHit(
        CharacteContext owner, LayerMask aimCollisionMask,
        Ray ray,
        float distance,
        float radius,
        bool includeHurtboxTriggers,
        out RaycastHit result)
    {
        QueryTriggerInteraction triggerInteraction = includeHurtboxTriggers
            ? QueryTriggerInteraction.Collide
            : QueryTriggerInteraction.Ignore;

        int hitCount = radius > 0f
            ? Physics.SphereCastNonAlloc(
                ray,
                radius,
                raycastHits,
                distance,
                aimCollisionMask,
                triggerInteraction)
            : Physics.RaycastNonAlloc(
                ray,
                raycastHits,
                distance,
                aimCollisionMask,
                triggerInteraction);

        float nearestDistance = float.PositiveInfinity;
        result = default;
        int acceptedTriggerLayer = includeHurtboxTriggers ? ResolveHurtboxLayer() : -1;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];
            if (hit.distance >= nearestDistance)
                continue;

            Collider collider = hit.collider;
            if (collider == null)
                continue;

            if (collider.isTrigger && collider.gameObject.layer != acceptedTriggerLayer)
                continue;

            if (ShouldIgnore(owner, collider))
                continue;

            nearestDistance = hit.distance;
            result = hit;
        }

        return nearestDistance < float.PositiveInfinity;
    }

    static int ResolveHurtboxLayer()
    {
        if (hurtboxLayer != UnresolvedLayer)
            return hurtboxLayer;

        hurtboxLayer = LayerMask.NameToLayer(HurtboxLayerName);
        if (hurtboxLayer < 0 && !warnedMissingHurtboxLayer)
        {
            warnedMissingHurtboxLayer = true;
            Debug.LogWarning(
                $"Layer '{HurtboxLayerName}' is missing. Aim points cannot land on hurtboxes.");
        }

        return hurtboxLayer;
    }

    static bool ShouldIgnore(CharacteContext owner, Collider candidate)
    {
        if (candidate == null)
            return true;

        if (owner != null && candidate.transform.IsChildOf(owner.transform))
            return true;

        CharacteContext other = CharacterContextModuleLookup.ResolveContext(candidate);
        if (other == null)
            return false;

        if (other == owner ||
            AreFriendly(owner, other))
        {
            return true;
        }

        // Match weapon-projectile hit eligibility. The movement capsule can sit
        // in front of a limb, but projectiles ignore it when this actor has hit zones.
        // Aiming at that capsule makes the muzzle ray diverge before reaching the limb.
        if (other.ColliderRefs == null)
            other.ResolveReferences();

        CharacterColliderRefs colliderRefs = other.ColliderRefs;
        return colliderRefs != null && colliderRefs.HasHitZones &&
               !colliderRefs.TryResolveHitZone(candidate, out _) &&
               !SpecialShootPointRegistry.TryResolve(candidate, out _);
    }

    static bool AreFriendly(CharacteContext owner, CharacteContext other)
    {
        if (owner == null) return false;
        bool ownerFriendly = owner.TargetIdentity == AITargetIdentity.Player ||
                             owner.TargetIdentity == AITargetIdentity.Companion;
        bool otherFriendly = other.TargetIdentity == AITargetIdentity.Player ||
                             other.TargetIdentity == AITargetIdentity.Companion;
        return (ownerFriendly && otherFriendly) ||
               (owner.TargetIdentity == AITargetIdentity.Enemy && other.TargetIdentity == AITargetIdentity.Enemy);
    }
}
