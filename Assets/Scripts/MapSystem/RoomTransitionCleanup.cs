using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a room transition is allowed to destroy. Anything living under the party or inside a cached
/// room belongs to somebody who outlives the transition: the party keeps its own effects, and a
/// cached room keeps the uncollected drops the player left there for when they walk back in.
/// </summary>
public readonly struct RoomTransitionCleanupScope
{
    private readonly Transform partyRoot;
    private readonly IReadOnlyList<Transform> roomRoots;

    public RoomTransitionCleanupScope(Transform partyRoot, IReadOnlyList<Transform> roomRoots)
    {
        this.partyRoot = partyRoot;
        this.roomRoots = roomRoots;
    }

    public bool IsOwnedByRunContent(Transform instance)
    {
        if (instance == null)
            return true;

        if (partyRoot != null && instance.IsChildOf(partyRoot))
            return true;

        if (roomRoots == null)
            return false;

        for (int i = 0; i < roomRoots.Count; i++)
        {
            Transform root = roomRoots[i];
            if (root != null && instance.IsChildOf(root))
                return true;
        }

        return false;
    }
}

/// <summary>
/// Clears the loose transient objects a room transition leaves behind: in-flight projectiles,
/// dropped pickups, and world VFX. Room-owned content is not its business — the room cache clears
/// the outgoing room's encounter and temporary roots explicitly.
/// </summary>
public static class RoomTransitionCleanup
{
    public static void ClearTransientWorldObjects(in RoomTransitionCleanupScope scope)
    {
        DestroyActiveObjects<SkillPickup>(scope);
        DestroyActiveObjects<Bullet>(scope);
        DestroyActiveObjects<SkillProjectile>(scope);
        DespawnActiveProjectiles(scope);
        ReturnWorldVfxToPool(scope);
    }

    static void DestroyActiveObjects<T>(in RoomTransitionCleanupScope scope) where T : Component
    {
        T[] objects = Object.FindObjectsByType<T>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < objects.Length; i++)
        {
            T instance = objects[i];
            if (instance == null || scope.IsOwnedByRunContent(instance.transform))
                continue;

            instance.gameObject.SetActive(false);
            Object.Destroy(instance.gameObject);
        }
    }

    static void DespawnActiveProjectiles(in RoomTransitionCleanupScope scope)
    {
        Projectile[] projectiles = Object.FindObjectsByType<Projectile>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < projectiles.Length; i++)
        {
            Projectile projectile = projectiles[i];
            if (projectile == null || scope.IsOwnedByRunContent(projectile.transform))
                continue;

            projectile.DespawnForRoomTransition();
        }
    }

    static void ReturnWorldVfxToPool(in RoomTransitionCleanupScope scope)
    {
        PooledVfxHandle[] handles = Object.FindObjectsByType<PooledVfxHandle>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < handles.Length; i++)
        {
            PooledVfxHandle handle = handles[i];
            if (handle == null ||
                scope.IsOwnedByRunContent(handle.transform) ||
                IsOwnedByActiveParty(handle.transform))
            {
                continue;
            }

            handle.ReturnToPool();
        }
    }

    /// <summary>
    /// Status presentation lives on the character, not in the room, so it has to survive a warp even
    /// when the character is not parented under the party root.
    /// </summary>
    static bool IsOwnedByActiveParty(Transform instance)
    {
        if (instance == null)
            return false;

        CharacteContext context = instance.GetComponentInParent<CharacteContext>();
        if (context == null)
            return false;

        return context.PreservesOwnedVfxDuringRoomTransition;
    }
}
