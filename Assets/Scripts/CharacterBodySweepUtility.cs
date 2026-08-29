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
    static readonly RaycastHit[] HitBuffer = new RaycastHit[HitBufferSize];

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
        if (desiredDistance <= 0f)
            return 0f;

        float castDistance = desiredDistance + collisionPadding;
        int count;

        switch (shape.Kind)
        {
            case CharacterBodySweepKind.Box:
                count = Physics.BoxCastNonAlloc(
                    shape.Point0,
                    shape.HalfExtents,
                    direction,
                    HitBuffer,
                    shape.Rotation,
                    castDistance,
                    collisionMask,
                    queryTriggers);
                break;

            case CharacterBodySweepKind.Sphere:
                count = Physics.SphereCastNonAlloc(
                    shape.Point0,
                    shape.Radius,
                    direction,
                    HitBuffer,
                    castDistance,
                    collisionMask,
                    queryTriggers);
                break;

            default:
                count = Physics.CapsuleCastNonAlloc(
                    shape.Point0,
                    shape.Point1,
                    shape.Radius,
                    direction,
                    HitBuffer,
                    castDistance,
                    collisionMask,
                    queryTriggers);
                break;
        }

        float allowedDistance = desiredDistance;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = HitBuffer[i];
            if (hit.collider == null)
                continue;

            if (IsOwnedByActor(hit.transform, actorRoot))
                continue;

            allowedDistance = Mathf.Min(
                allowedDistance,
                Mathf.Max(0f, hit.distance - collisionPadding));
        }

        return allowedDistance;
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
