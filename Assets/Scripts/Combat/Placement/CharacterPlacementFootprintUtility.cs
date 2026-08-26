using UnityEngine;

public static class CharacterPlacementFootprintUtility
{
    public static bool TryGetColliderFootprint(
        Collider collider,
        Transform footprintRoot,
        out CharacterPlacementFootprint footprint,
        out string error)
    {
        footprint = default;
        error = string.Empty;
        if (collider == null)
        {
            error = "Placement footprint collider is missing.";
            return false;
        }

        Transform root = footprintRoot != null ? footprintRoot : collider.transform;
        Vector3 scale = Abs(collider.transform.lossyScale);
        Vector3 center = root.InverseTransformPoint(collider.transform.position);
        Quaternion rotation = RelativeRotation(root, collider.transform);

        if (collider is BoxCollider box)
        {
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, scale);
            center = root.InverseTransformPoint(collider.transform.TransformPoint(box.center));
            footprint = new CharacterPlacementFootprint(
                CharacterPlacementShape.Box,
                center,
                halfExtents,
                Mathf.Max(halfExtents.x, halfExtents.z),
                halfExtents.y * 2f,
                rotation);
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
            center = root.InverseTransformPoint(collider.transform.TransformPoint(capsule.center));
            footprint = new CharacterPlacementFootprint(
                CharacterPlacementShape.Circle,
                center,
                new Vector3(radius, height * 0.5f, radius),
                radius,
                height,
                rotation,
                CapsuleAxis(capsule.direction));
            return true;
        }

        if (collider is SphereCollider sphere)
        {
            float radius = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            center = root.InverseTransformPoint(collider.transform.TransformPoint(sphere.center));
            footprint = new CharacterPlacementFootprint(
                CharacterPlacementShape.Circle,
                center,
                new Vector3(radius, radius, radius),
                radius,
                radius * 2f,
                rotation);
            return true;
        }

        error = $"Unsupported placement footprint collider '{collider.GetType().Name}'.";
        return false;
    }

    public static CharacterPlacementFootprint CreateFallbackBox(
        Vector3 center,
        Vector3 halfExtents)
    {
        halfExtents = new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(halfExtents.x)),
            Mathf.Max(0.01f, Mathf.Abs(halfExtents.y)),
            Mathf.Max(0.01f, Mathf.Abs(halfExtents.z)));
        return new CharacterPlacementFootprint(
            CharacterPlacementShape.Box,
            center,
            halfExtents,
            Mathf.Max(halfExtents.x, halfExtents.z),
            halfExtents.y * 2f);
    }

    public static CharacterPlacementFootprint CreateVerticalCapsuleFootprint(
        Transform footprintRoot,
        Transform capsuleTransform,
        Vector3 localCenter,
        float radius,
        float height)
    {
        Transform root = footprintRoot != null ? footprintRoot : capsuleTransform;
        Vector3 scale = Abs(capsuleTransform.lossyScale);
        float scaledRadius = radius * Mathf.Max(scale.x, scale.z);
        float scaledHeight = height * scale.y;
        Vector3 center = root.InverseTransformPoint(
            capsuleTransform.TransformPoint(localCenter));
        return new CharacterPlacementFootprint(
            CharacterPlacementShape.Circle,
            center,
            new Vector3(scaledRadius, scaledHeight * 0.5f, scaledRadius),
            scaledRadius,
            scaledHeight,
            RelativeRotation(root, capsuleTransform));
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
