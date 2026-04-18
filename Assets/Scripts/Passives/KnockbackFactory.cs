using UnityEngine;

public readonly struct KnockbackBuildContext
{
    public KnockbackBuildContext(
        Vector3 origin,
        Vector3 hitPoint,
        Vector3 fallbackDirection,
        Vector3 explicitDirection = default,
        bool flattenToGround = true)
    {
        this.origin = origin;
        this.hitPoint = hitPoint;
        this.fallbackDirection = fallbackDirection;
        this.explicitDirection = explicitDirection;
        this.flattenToGround = flattenToGround;
    }

    public Vector3 origin { get; }
    public Vector3 hitPoint { get; }
    public Vector3 fallbackDirection { get; }
    public Vector3 explicitDirection { get; }
    public bool flattenToGround { get; }
}

public static class KnockbackFactory
{
    const float MinDistance = 0.001f;
    const float MinDuration = 0.001f;
    const float MinDirectionSqrMagnitude = 0.0001f;

    public static bool TryBuild(
        in KnockbackSettings settings,
        in KnockbackBuildContext context,
        out KnockbackData knockback)
    {
        knockback = default;

        if (!IsValidScalar(settings.distance, MinDistance) ||
            !IsValidScalar(settings.duration, MinDuration))
        {
            return false;
        }

        if (!TryResolveDirection(in context, out Vector3 direction))
            return false;

        knockback = new KnockbackData(
            direction,
            settings.distance,
            settings.duration,
            context.hitPoint,
            settings.reaction,
            settings.interruptActions,
            settings.progressCurve,
            context.flattenToGround);

        return knockback.IsValid;
    }

    static bool TryResolveDirection(in KnockbackBuildContext context, out Vector3 direction)
    {
        if (TryGetUsableDirection(context.explicitDirection, context.flattenToGround, out direction))
            return true;

        if (TryGetUsableDirection(context.hitPoint - context.origin, context.flattenToGround, out direction))
            return true;

        if (TryGetUsableDirection(context.fallbackDirection, context.flattenToGround, out direction))
            return true;

        direction = Vector3.zero;
        return false;
    }

    static bool TryGetUsableDirection(Vector3 candidate, bool flattenToGround, out Vector3 direction)
    {
        direction = SanitizeDirection(candidate, flattenToGround);
        return direction.sqrMagnitude > MinDirectionSqrMagnitude;
    }

    static Vector3 SanitizeDirection(Vector3 direction, bool flattenToGround)
    {
        if (!IsFinite(direction))
            return Vector3.zero;

        if (flattenToGround)
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);

        return direction;
    }

    static bool IsValidScalar(float value, float minValue)
    {
        return IsFinite(value) && value > minValue;
    }

    static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
