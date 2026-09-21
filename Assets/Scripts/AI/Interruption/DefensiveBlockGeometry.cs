using UnityEngine;

public static class DefensiveBlockGeometry
{
    // A close Contact guard must not extend through the incoming actor. Freeze this
    // offset on arrival so the guard does not follow an actor that later passes it.
    public static float ContactGuardOffset(Vector3 guardRoot, Vector3 forward, Vector3 casterRoot,
        float authoredOffset, float halfDepth) => Mathf.Clamp(
            Vector3.Dot(casterRoot - guardRoot, forward) - halfDepth, 0f, authoredOffset);

    // Clip forward root motion at a ready guard, without pulling an already-passed
    // actor backwards. Lateral misses, retreat and vertical motion remain unchanged.
    public static Vector3 ConstrainMotionAtGuard(Vector3 position, Vector3 delta, Vector3 guard,
        Vector3 forward, float halfWidth, float halfDepth)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < .0001f) return delta;
        forward.Normalize();
        float before = Vector3.Dot(position - guard, forward) - halfDepth;
        float advance = Vector3.Dot(delta, forward);
        if (before < -.0001f || advance >= 0f || before + advance >= 0f) return delta;
        float fraction = Mathf.Clamp01(Mathf.Max(0f, before) / -advance);
        Vector3 contact = position + delta * fraction - guard;
        if (Mathf.Abs(Vector3.Dot(contact, Vector3.Cross(Vector3.up, forward))) > halfWidth) return delta;
        return new Vector3(delta.x * fraction, delta.y, delta.z * fraction);
    }

    // A planar lane prediction ranks commands only; it never confirms an impact.
    public static bool TryPredictThreat(Vector3 origin, Vector3 forward, Vector3 player,
        float halfWidth, float forwardReach, float speed, out float contactSeconds)
    {
        contactSeconds = float.PositiveInfinity;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f || speed <= 0f || halfWidth < 0f) return false;
        forward.Normalize();
        Vector3 delta = player - origin;
        delta.y = 0f;
        float along = Vector3.Dot(delta, forward);
        // An attack already moving away from/past the player is not a new threat.
        if (along <= 0f) return false;
        float across = Mathf.Abs(Vector3.Dot(delta, Vector3.Cross(Vector3.up, forward)));
        if (across > halfWidth) return false;
        contactSeconds = Mathf.Max(0f, along - Mathf.Max(0f, forwardReach)) / speed;
        return true;
    }

    public static bool PreferThreat(float time, float distanceSquared, int id,
        float bestTime, float bestDistanceSquared, int bestId)
    {
        if (!Mathf.Approximately(time, bestTime)) return time < bestTime;
        if (!Mathf.Approximately(distanceSquared, bestDistanceSquared)) return distanceSquared < bestDistanceSquared;
        return id < bestId;
    }

    // The leading surface of the charging hit volume must approach the front of the guard.
    public static bool SweepsGuard(Vector3 previous, Vector3 current, float radius,
        Vector3 guard, Vector3 forward, float halfWidth, float halfHeight, float halfDepth = 0f)
        => TrySweepGuard(previous, current, radius, guard, forward, halfWidth, halfHeight, halfDepth, out _);

    public static bool TrySweepGuard(Vector3 previous, Vector3 current, float radius,
        Vector3 guard, Vector3 forward, float halfWidth, float halfHeight, float halfDepth, out float fraction)
    {
        fraction = float.PositiveInfinity;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return false;
        forward.Normalize();
        halfDepth = Mathf.Max(0f, halfDepth);
        float before = Vector3.Dot(previous - guard, forward) - radius - halfDepth;
        float after = Vector3.Dot(current - guard, forward) - radius - halfDepth;
        if (before + (radius + halfDepth) * 2f < 0f || after > 0f || after > before + 0.0001f) return false;
        float t = before <= 0f ? 0f : Mathf.Clamp01(before / Mathf.Max(0.0001f, before - after));
        Vector3 contact = Vector3.Lerp(previous, current, t) - guard;
        if (Mathf.Abs(Vector3.Dot(contact, Vector3.Cross(Vector3.up, forward))) > halfWidth + radius ||
            Mathf.Abs(contact.y) > halfHeight + radius) return false;
        fraction = t;
        return true;
    }

    // Relative swept AABBs use the same frame interval for the hitbox and the moving victim.
    // This is an ordering veto only; normal hitbox contacts remain responsible for damage.
    public static bool TrySweepBounds(Vector3 previous, Vector3 current, Vector3 extents,
        Vector3 targetPrevious, Bounds target, out float fraction)
    {
        fraction = float.PositiveInfinity;
        Vector3 start = previous - targetPrevious;
        Vector3 end = current - target.center;
        Vector3 expanded = extents + target.extents;
        float enter = 0f, exit = 1f;
        for (int axis = 0; axis < 3; axis++)
        {
            float delta = end[axis] - start[axis];
            if (Mathf.Abs(delta) < 0.000001f)
            {
                if (Mathf.Abs(start[axis]) > expanded[axis]) return false;
                continue;
            }
            float a = (-expanded[axis] - start[axis]) / delta;
            float b = (expanded[axis] - start[axis]) / delta;
            enter = Mathf.Max(enter, Mathf.Min(a, b));
            exit = Mathf.Min(exit, Mathf.Max(a, b));
            if (enter > exit) return false;
        }
        fraction = enter;
        return true;
    }

    // A newly activated wide hitbox can cover both volumes at t=0. A ready guard whose
    // front is ahead of the victim can intercept that activation. Applied damage still
    // vetoes this separately, so this never repairs a hit that was already delivered.
    public static bool GuardContactWins(float guardFraction, float playerFraction, float guardLead = 0f) =>
        guardFraction < playerFraction || (guardFraction == 0f && playerFraction == 0f && guardLead > 0f);
}
