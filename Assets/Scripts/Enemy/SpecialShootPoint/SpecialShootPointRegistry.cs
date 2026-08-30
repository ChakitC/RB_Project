using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Collider to runtime-point lookup for the direct-hit path.
///
/// Pooled point colliders deliberately do <em>not</em> join
/// <see cref="CharacterColliderRefs"/>'s authored hit-zone list: that list is exact-collider matched
/// and is what keeps ordinary hit-zone validation honest. A separate registry lets a point carry its
/// authored hit zone without weakening that check, and gives the point a safe
/// register/unregister path across pool reuse.
///
/// Registration is scoped to the window where the collider is actually live, so a point that is
/// pooled, disabled, or destroyed can never resolve a hit.
/// </summary>
public static class SpecialShootPointRegistry
{
    static readonly Dictionary<int, SpecialShootPointInstance> ByColliderId = new();

    public static void Register(Collider collider, SpecialShootPointInstance instance)
    {
        if (collider == null || instance == null)
            return;

        ByColliderId[collider.GetInstanceID()] = instance;
    }

    public static void Unregister(Collider collider)
    {
        if (collider == null)
            return;

        ByColliderId.Remove(collider.GetInstanceID());
    }

    /// <summary>
    /// Resolves the point a collider belongs to. A registered but no-longer-live point is pruned
    /// rather than returned, so a stale entry cannot silently absorb a shot.
    /// </summary>
    public static bool TryResolve(Collider collider, out SpecialShootPointInstance instance)
    {
        instance = null;

        if (collider == null)
            return false;

        int id = collider.GetInstanceID();
        if (!ByColliderId.TryGetValue(id, out instance))
            return false;

        if (instance == null)
        {
            ByColliderId.Remove(id);
            return false;
        }

        return true;
    }
}
