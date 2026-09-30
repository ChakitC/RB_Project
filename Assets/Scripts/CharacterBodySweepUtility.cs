using UnityEngine;

/// <summary>
/// Shared displacement probing for characters that move without a <see cref="CharacterController"/>.
///
/// Enemies navigate through <see cref="UnityEngine.AI.NavMeshAgent"/> and carry no controller, so
/// knockback and vertical motion have to resolve their own collision. The only supported body shape
/// is the authored <see cref="CharacterColliderRefs.CharacterPositionCollider"/>; a hierarchy search
/// for "some CapsuleCollider" is not a valid substitute because several characters would return a
/// hit-zone capsule instead of their body.
///
/// This utility only answers "how far may this body travel". It owns no actor state, no NavMesh
/// suspension, and no life-state decisions.
/// </summary>
public static class CharacterBodySweepUtility
{
    const int HitBufferSize = 16;

    // Both motors probe from LateUpdate on the main thread, so one shared buffer keeps the sweep
    // allocation-free on a per-frame path.
    static RaycastHit[] HitBuffer = new RaycastHit[HitBufferSize];

    /// <summary>
    /// True when the collider can act as an actor body: present, enabled, solid, and a shape the
    /// sweep can cast with.
    /// </summary>
    public static bool IsUsableBody(Collider collider)
    {
        if (collider == null || !collider.enabled || collider.isTrigger)
            return false;

        return collider is CapsuleCollider || collider is BoxCollider || collider is SphereCollider;
    }

    /// <summary>
    /// Reads world-space cast geometry from <paramref name="collider"/>, honouring its transform
    /// scale, centre, and rotation.
    /// </summary>
    public static bool TryResolveShape(Collider collider, out CharacterBodySweepShape shape)
    {
        shape = default;

        if (!IsUsableBody(collider))
            return false;

        Transform colliderTransform = collider.transform;
        Vector3 scale = Abs(colliderTransform.lossyScale);

        if (collider is CapsuleCollider capsule)
        {
            float heightScale = capsule.direction == 0
                ? scale.x
                : capsule.direction == 1 ? scale.y : scale.z;
            float radiusScale = capsule.direction == 0
                ? Mathf.Max(scale.y, scale.z)
                : capsule.direction == 1 ? Mathf.Max(scale.x, scale.z) : Mathf.Max(scale.x, scale.y);

            float radius = Mathf.Max(0.001f, capsule.radius * radiusScale);
            float height = Mathf.Max(capsule.height * heightScale, radius * 2f + 0.001f);
            float half = Mathf.Max(0f, height * 0.5f - radius);

            Vector3 center = colliderTransform.TransformPoint(capsule.center);
            Vector3 axis = colliderTransform.rotation * CapsuleAxis(capsule.direction);

            shape = new CharacterBodySweepShape(
                CharacterBodySweepKind.Capsule,
                center + axis * half,
                center - axis * half,
                radius,
                Vector3.zero,
                colliderTransform.rotation);
            return true;
        }

        if (collider is BoxCollider box)
        {
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, scale);
            halfExtents = new Vector3(
                Mathf.Max(0.001f, Mathf.Abs(halfExtents.x)),
                Mathf.Max(0.001f, Mathf.Abs(halfExtents.y)),
                Mathf.Max(0.001f, Mathf.Abs(halfExtents.z)));

            Vector3 center = colliderTransform.TransformPoint(box.center);

            shape = new CharacterBodySweepShape(
                CharacterBodySweepKind.Box,
                center,
                center,
                Mathf.Max(halfExtents.x, halfExtents.z),
                halfExtents,
                colliderTransform.rotation);
            return true;
        }

        if (collider is SphereCollider sphere)
        {
            float radius = Mathf.Max(
                0.001f,
                sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)));
            Vector3 center = colliderTransform.TransformPoint(sphere.center);

            shape = new CharacterBodySweepShape(
                CharacterBodySweepKind.Sphere,
                center,
                center,
                radius,
                Vector3.zero,
                colliderTransform.rotation);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the distance the body may travel along <paramref name="direction"/> before the first
    /// blocking hit. Colliders under <paramref name="actorRoot"/> are ignored so the actor never
    /// blocks itself on its own hit zones.
    /// </summary>
    public static float ResolveAllowedDistance(
        in CharacterBodySweepShape shape,
        Vector3 direction,
        float desiredDistance,
        float collisionPadding,
        LayerMask collisionMask,
        QueryTriggerInteraction queryTriggers,
        Transform actorRoot)
    {
        return ResolveAllowedDistance(shape, direction, desiredDistance, collisionPadding,
            collisionMask, queryTriggers, actorRoot, out _);
    }

    static float ResolveAllowedDistance(
        in CharacterBodySweepShape shape,
        Vector3 direction,
        float desiredDistance,
        float collisionPadding,
        LayerMask collisionMask,
        QueryTriggerInteraction queryTriggers,
        Transform actorRoot,
        out RaycastHit blockingHit,
        Collider overlapBody = null,
        Vector3 bodyOffset = default)
    {
        blockingHit = default;
        if (desiredDistance <= 0f)
            return 0f;

        float castDistance = desiredDistance + collisionPadding;
        int count;

        // NonAlloc results are unordered. A full buffer may omit the nearest wall, especially
        // when the actor's own hit zones fill it, so grow and repeat before choosing a hit.
        while (true)
        {
            count = Cast(shape, direction, castDistance, collisionMask, queryTriggers);
            if (count < HitBuffer.Length)
                break;
            System.Array.Resize(ref HitBuffer, HitBuffer.Length * 2);
        }

        float allowedDistance = desiredDistance;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = HitBuffer[i];
            if (hit.collider == null || IsOwnedByActor(hit.transform, actorRoot))
                continue;

            // A cast starting in contact can return -direction as a synthetic normal. Resolve
            // the actual contact so tangential motion along a floor/wall remains possible.
            if (hit.distance <= 0.00001f && overlapBody != null &&
                Physics.ComputePenetration(
                    overlapBody, overlapBody.transform.position + bodyOffset, overlapBody.transform.rotation,
                    hit.collider, hit.collider.transform.position, hit.collider.transform.rotation,
                    out Vector3 separationNormal, out _))
            {
                if (Vector3.Dot(direction, separationNormal) >= -0.00001f)
                    continue;
                hit.normal = separationNormal;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                blockingHit = hit;
                allowedDistance = Mathf.Min(desiredDistance, Mathf.Max(0f, hit.distance - collisionPadding));
            }
        }

        return allowedDistance;
    }

    static int Cast(in CharacterBodySweepShape shape, Vector3 direction, float castDistance,
        LayerMask collisionMask, QueryTriggerInteraction queryTriggers)
    {
        switch (shape.Kind)
        {
            case CharacterBodySweepKind.Box:
                return Physics.BoxCastNonAlloc(
                    shape.Point0,
                    shape.HalfExtents,
                    direction,
                    HitBuffer,
                    shape.Rotation,
                    castDistance,
                    collisionMask,
                    queryTriggers);

            case CharacterBodySweepKind.Sphere:
                return Physics.SphereCastNonAlloc(
                    shape.Point0,
                    shape.Radius,
                    direction,
                    HitBuffer,
                    castDistance,
                    collisionMask,
                    queryTriggers);

            default:
                return Physics.CapsuleCastNonAlloc(
                    shape.Point0,
                    shape.Point1,
                    shape.Radius,
                    direction,
                    HitBuffer,
                    castDistance,
                    collisionMask,
                    queryTriggers);
        }
    }

    /// <summary>
    /// Sweeps translation, then projects the unused distance along the blocking surface. This
    /// keeps forward root motion moving when the clip also presses down against the floor.
    /// Each projected segment is swept again so sliding cannot bypass a second wall at a corner.
    /// </summary>
    public static Vector3 ResolveSlidingDelta(
        in CharacterBodySweepShape shape, Vector3 desiredDelta, float collisionPadding,
        LayerMask collisionMask, QueryTriggerInteraction queryTriggers, Transform actorRoot,
        Collider body)
    {
        Vector3 moved = Vector3.zero;
        Vector3 remaining = desiredDelta;
        for (int i = 0; i < 3 && remaining.sqrMagnitude > 0.00000001f; i++)
        {
            var currentShape = new CharacterBodySweepShape(shape.Kind,
                shape.Point0 + moved, shape.Point1 + moved, shape.Radius,
                shape.HalfExtents, shape.Rotation);
            float distance = remaining.magnitude;
            Vector3 direction = remaining / distance;
            float allowed = ResolveAllowedDistance(currentShape, direction, distance,
                collisionPadding, collisionMask, queryTriggers, actorRoot, out RaycastHit hit, body, moved);
            Vector3 step = direction * allowed;
            moved += step;
            if (allowed >= distance || hit.collider == null)
                break;

            remaining -= step;
            float intoSurface = Vector3.Dot(remaining, hit.normal);
            if (intoSurface >= -0.00001f)
                break;
            remaining -= hit.normal * intoSurface;
        }
        return moved;
    }

    /// <summary>
    /// Convenience wrapper returning the safe displacement vector for a desired delta.
    /// </summary>
    public static Vector3 ResolveSafeDelta(
        in CharacterBodySweepShape shape,
        Vector3 desiredDelta,
        float collisionPadding,
        LayerMask collisionMask,
        QueryTriggerInteraction queryTriggers,
        Transform actorRoot)
    {
        float desiredDistance = desiredDelta.magnitude;
        if (desiredDistance <= 0.0001f)
            return Vector3.zero;

        Vector3 direction = desiredDelta / desiredDistance;
        float allowedDistance = ResolveAllowedDistance(
            shape,
            direction,
            desiredDistance,
            collisionPadding,
            collisionMask,
            queryTriggers,
            actorRoot);

        return direction * allowedDistance;
    }

    /// <summary>
    /// True when <paramref name="candidate"/> belongs to this actor rather than merely sharing a
    /// scene or encounter root with it.
    /// </summary>
    public static bool IsOwnedByActor(Transform candidate, Transform actorRoot)
    {
        return candidate != null &&
               actorRoot != null &&
               (candidate == actorRoot || candidate.IsChildOf(actorRoot));
    }

    static Vector3 CapsuleAxis(int direction)
    {
        return direction == 0 ? Vector3.right : direction == 2 ? Vector3.forward : Vector3.up;
    }

    static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }
}
