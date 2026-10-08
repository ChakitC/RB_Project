using UnityEngine;

public readonly struct WeaponAimSample
{
    public readonly Vector3 Point;
    public readonly Vector3 ReferenceDirection;

    public WeaponAimSample(Vector3 point, Vector3 referenceDirection)
    {
        Point = point;
        ReferenceDirection = referenceDirection.normalized;
    }

    public bool IsValid => WeaponAimResolver.IsFinite(Point) &&
                           WeaponAimResolver.IsFinite(ReferenceDirection) &&
                           ReferenceDirection.sqrMagnitude > 0.0001f;
}
