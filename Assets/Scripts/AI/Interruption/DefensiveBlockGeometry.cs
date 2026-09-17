using UnityEngine;

public static class DefensiveBlockGeometry
{
    // The leading surface of the charging hit volume must approach the front of the guard.
    public static bool SweepsGuard(Vector3 previous, Vector3 current, float radius,
        Vector3 guard, Vector3 forward, float halfWidth, float halfHeight)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return false;
        forward.Normalize();
        float before = Vector3.Dot(previous - guard, forward) - radius;
        float after = Vector3.Dot(current - guard, forward) - radius;
        if (before + radius * 2f < 0f || after > 0f || after > before + 0.0001f) return false;
        float t = before <= 0f ? 0f : Mathf.Clamp01(before / Mathf.Max(0.0001f, before - after));
        Vector3 contact = Vector3.Lerp(previous, current, t) - guard;
        return Mathf.Abs(Vector3.Dot(contact, Vector3.Cross(Vector3.up, forward))) <= halfWidth + radius &&
               Mathf.Abs(contact.y) <= halfHeight + radius;
    }
}
