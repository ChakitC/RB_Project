using UnityEngine;

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
