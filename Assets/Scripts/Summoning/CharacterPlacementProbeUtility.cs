using UnityEngine;
using UnityEngine.AI;

public enum CharacterPlacementShape
{
    Circle,
    Box,
}

public readonly struct CharacterPlacementFootprint
{
    public CharacterPlacementFootprint(
        CharacterPlacementShape shape,
        Vector3 centerOffset,
        Vector3 halfExtents,
        float radius,
        float height)
        : this(shape, centerOffset, halfExtents, radius, height, Quaternion.identity)
    {
    }

    public CharacterPlacementFootprint(
        CharacterPlacementShape shape,
        Vector3 centerOffset,
        Vector3 halfExtents,
        float radius,
        float height,
        Quaternion rotation)
        : this(shape, centerOffset, halfExtents, radius, height, rotation, Vector3.up)
    {
    }

    public CharacterPlacementFootprint(
        CharacterPlacementShape shape,
        Vector3 centerOffset,
        Vector3 halfExtents,
        float radius,
        float height,
        Quaternion rotation,
        Vector3 axis)
    {
        Shape = shape;
        CenterOffset = centerOffset;
        HalfExtents = halfExtents;
        Radius = Mathf.Max(0.01f, radius);
        Height = Mathf.Max(Mathf.Max(0.1f, height), Radius * 2f);
        Rotation = rotation == default ? Quaternion.identity : rotation;
        Axis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;
    }

    public CharacterPlacementShape Shape { get; }
    public Vector3 CenterOffset { get; }
    public Vector3 HalfExtents { get; }
    public float Radius { get; }
    public float Height { get; }
    public Quaternion Rotation { get; }
    public Vector3 Axis { get; }
}

public static class CharacterPlacementProbeUtility
{
    public static bool TryGetFootprint(
        GameObject prefab,
        SummonMobility mobility,
        out CharacterPlacementFootprint footprint,
        out string error)
    {
        footprint = default;
        error = string.Empty;
        if (prefab == null)
        {
            error = "Summon prefab is missing.";
            return false;
        }

        if (mobility == SummonMobility.Mobile)
            return TryGetMobileFootprint(prefab, out footprint, out error);

        CharacterColliderRefs refs = prefab.GetComponentInChildren<CharacterColliderRefs>(true);
        if (refs == null || refs.CharacterPositionCollider == null)
        {
            error = "Stationary summon requires CharacterColliderRefs.CharacterPositionCollider.";
            return false;
        }

        return TryGetColliderFootprint(
            refs.CharacterPositionCollider,
            prefab.transform,
            out footprint,
            out error);
    }

    public static bool TryGetMobileFootprint(
        GameObject prefab,
        out CharacterPlacementFootprint footprint,
        out string error)
    {
        CharacterController controller = prefab.GetComponentInChildren<CharacterController>(true);
        if (controller != null)
        {
            Vector3 scale = Abs(controller.transform.lossyScale);
            float radius = controller.radius * Mathf.Max(scale.x, scale.z);
            float height = controller.height * scale.y;
            Vector3 center = prefab.transform.InverseTransformPoint(
                controller.transform.TransformPoint(controller.center));
            footprint = new CharacterPlacementFootprint(
                CharacterPlacementShape.Circle,
                center,
                new Vector3(radius, height * 0.5f, radius),
                radius,
                height,
                RelativeRotation(prefab.transform, controller.transform));
            error = string.Empty;
            return true;
        }

        NavMeshAgent agent = prefab.GetComponentInChildren<NavMeshAgent>(true);
        if (agent == null)
        {
            footprint = default;
            error = "Mobile summon requires CharacterController or NavMeshAgent footprint.";
            return false;
        }

        Vector3 agentScale = Abs(agent.transform.lossyScale);
        float agentRadius = agent.radius * Mathf.Max(agentScale.x, agentScale.z);
        float agentHeight = agent.height * agentScale.y;
        Vector3 agentCenter = prefab.transform.InverseTransformPoint(agent.transform.position);
        footprint = new CharacterPlacementFootprint(
            CharacterPlacementShape.Circle,
            agentCenter,
            new Vector3(agentRadius, agentHeight * 0.5f, agentRadius),
            agentRadius,
            agentHeight,
            RelativeRotation(prefab.transform, agent.transform));
        error = string.Empty;
        return true;
    }

    static bool TryGetColliderFootprint(
        Collider collider,
        Transform prefabRoot,
        out CharacterPlacementFootprint footprint,
        out string error)
    {
        Vector3 scale = Abs(collider.transform.lossyScale);
        Vector3 center = prefabRoot.InverseTransformPoint(collider.transform.position);
        Quaternion rotation = RelativeRotation(prefabRoot, collider.transform);

        if (collider is BoxCollider box)
        {
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, scale);
            center = prefabRoot.InverseTransformPoint(collider.transform.TransformPoint(box.center));
            footprint = new CharacterPlacementFootprint(
                CharacterPlacementShape.Box,
                center,
                halfExtents,
                Mathf.Max(halfExtents.x, halfExtents.z),
                halfExtents.y * 2f,
                rotation);
            error = string.Empty;
            return true;
        }

        if (collider is CapsuleCollider capsule)
        {
            float axisScale = capsule.direction == 0
                ? scale.x
                : capsule.direction == 1 ? scale.y : scale.z;
            float height = capsule.height * axisScale;
            float radiusScale = capsule.direction == 0
                ? Mathf.Max(scale.y, scale.z)
                : capsule.direction == 1 ? Mathf.Max(scale.x, scale.z) : Mathf.Max(scale.x, scale.y);
            float radius = capsule.radius * radiusScale;
            center = prefabRoot.InverseTransformPoint(collider.transform.TransformPoint(capsule.center));
            footprint = new CharacterPlacementFootprint(
                CharacterPlacementShape.Circle,
                center,
                new Vector3(radius, height * 0.5f, radius),
                radius,
                height,
                rotation,
                CapsuleAxis(capsule.direction));
            error = string.Empty;
            return true;
        }

        if (collider is SphereCollider sphere)
        {
            float radius = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            center = prefabRoot.InverseTransformPoint(collider.transform.TransformPoint(sphere.center));
            footprint = new CharacterPlacementFootprint(
                CharacterPlacementShape.Circle,
                center,
                new Vector3(radius, radius, radius),
                radius,
                radius * 2f,
                rotation);
            error = string.Empty;
            return true;
        }

        footprint = default;
        error = $"Unsupported stationary footprint collider '{collider.GetType().Name}'. Use BoxCollider, CapsuleCollider, or SphereCollider.";
        return false;
    }

    static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    static Quaternion RelativeRotation(Transform root, Transform child)
    {
        return root != null && child != null
            ? Quaternion.Inverse(root.rotation) * child.rotation
            : Quaternion.identity;
    }

    static Vector3 CapsuleAxis(int direction)
    {
        return direction == 0 ? Vector3.right : direction == 2 ? Vector3.forward : Vector3.up;
    }
}
