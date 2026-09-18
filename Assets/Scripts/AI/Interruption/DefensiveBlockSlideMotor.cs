using UnityEngine;

public static class DefensiveBlockSlideMotor
{
    public static CharacterBodySweepShape ResolveShape(in CharacterPlacementFootprint footprint, Transform root)
    {
        Vector3 center = root.TransformPoint(footprint.CenterOffset);
        Quaternion rotation = root.rotation * footprint.Rotation;
        if (footprint.Shape == CharacterPlacementShape.Box)
            return new CharacterBodySweepShape(CharacterBodySweepKind.Box, center, center,
                footprint.Radius, footprint.HalfExtents, rotation);
        Vector3 axis = rotation * footprint.Axis;
        float half = Mathf.Max(0f, footprint.Height * 0.5f - footprint.Radius);
        return new CharacterBodySweepShape(CharacterBodySweepKind.Capsule, center + axis * half,
            center - axis * half, footprint.Radius, Vector3.zero, rotation);
    }
}
