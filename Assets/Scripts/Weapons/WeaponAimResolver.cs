using UnityEngine;

public static class WeaponAimResolver
{
    public static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

    public static Vector3 ResolveAimPoint(Vector3 origin, in WeaponAimSample aim)
    {
        float depth = Vector3.Dot(aim.Point - origin, aim.ReferenceDirection);
        // Preserve exact close targets in front of the muzzle. Only prevent reverse shots.
        return depth > 0f ? aim.Point : aim.Point + aim.ReferenceDirection * (1f - depth);
    }

    public static bool TryResolveDirection(Vector3 origin, in WeaponAimSample aim,
        float spreadDegrees, out Vector3 direction)
    {
        direction = Vector3.zero;
        if (!IsFinite(origin) || !aim.IsValid || !float.IsFinite(spreadDegrees))
            return false;

        direction = (ResolveAimPoint(origin, aim) - origin).normalized;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = aim.ReferenceDirection;
        if (spreadDegrees <= 0.001f)
            return true;

        Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.999f
            ? Vector3.forward : Vector3.up;
        Quaternion basis = Quaternion.LookRotation(direction, up);
        Vector2 random = Random.insideUnitCircle * spreadDegrees;
        direction = basis * Quaternion.Euler(random.y, random.x, 0f) * Vector3.forward;
        return true;
    }
}
