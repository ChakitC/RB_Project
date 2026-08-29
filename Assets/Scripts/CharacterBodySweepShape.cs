using UnityEngine;

public enum CharacterBodySweepKind
{
    Capsule,
    Box,
    Sphere,
}

/// <summary>
/// World-space cast geometry read from a character's authored body collider
/// (<see cref="CharacterColliderRefs.CharacterPositionCollider"/>). This is the only body shape
/// supported for actors that have no <see cref="CharacterController"/>.
/// </summary>
public readonly struct CharacterBodySweepShape
{
    public readonly CharacterBodySweepKind Kind;

    /// <summary>Capsule end point, or the world centre for box and sphere shapes.</summary>
    public readonly Vector3 Point0;

    /// <summary>Second capsule end point. Equal to <see cref="Point0"/> for box and sphere shapes.</summary>
    public readonly Vector3 Point1;

    /// <summary>Scaled radius for capsule and sphere shapes.</summary>
    public readonly float Radius;

    /// <summary>Scaled half extents for box shapes.</summary>
    public readonly Vector3 HalfExtents;

    /// <summary>World rotation used by box casts.</summary>
    public readonly Quaternion Rotation;

    public CharacterBodySweepShape(
        CharacterBodySweepKind kind,
        Vector3 point0,
        Vector3 point1,
        float radius,
        Vector3 halfExtents,
        Quaternion rotation)
    {
        Kind = kind;
        Point0 = point0;
        Point1 = point1;
        Radius = radius;
        HalfExtents = halfExtents;
        Rotation = rotation;
    }
}
