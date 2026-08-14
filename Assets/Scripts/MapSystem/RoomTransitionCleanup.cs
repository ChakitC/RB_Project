using UnityEngine;

public static class RoomTransitionCleanup
{
    public static void ClearTransientWorldObjects()
    {
        DestroyActiveObjects<SkillPickup>();
        DestroyActiveObjects<Bullet>();
        DestroyActiveObjects<SkillProjectile>();
        DespawnActiveProjectiles();
        ReturnWorldVfxToPool();
    }

    static void DestroyActiveObjects<T>() where T : Component
    {
        T[] objects = Object.FindObjectsByType<T>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < objects.Length; i++)
        {
            T instance = objects[i];
            if (instance == null)
                continue;

            instance.gameObject.SetActive(false);
            Object.Destroy(instance.gameObject);
        }
    }

    static void DespawnActiveProjectiles()
    {
        Projectile[] projectiles = Object.FindObjectsByType<Projectile>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < projectiles.Length; i++)
        {
            if (projectiles[i] != null)
                projectiles[i].DespawnForRoomTransition();
        }
    }

    static void ReturnWorldVfxToPool()
    {
        PooledVfxHandle[] handles = Object.FindObjectsByType<PooledVfxHandle>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < handles.Length; i++)
        {
            PooledVfxHandle handle = handles[i];
            if (handle == null || IsOwnedByActiveParty(handle.transform))
                continue;

            handle.ReturnToPool();
        }
    }

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
