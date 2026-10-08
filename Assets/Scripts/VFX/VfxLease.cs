using UnityEngine;

// A reference to one rental, never to a future use of the same pooled object.
public readonly struct VfxLease
{
    readonly PooledVfxHandle handle;
    readonly ulong generation;

    public VfxLease(GameObject instance)
    {
        handle = instance != null ? instance.GetComponent<PooledVfxHandle>() : null;
        generation = handle != null ? handle.Generation : 0;
    }

    public bool IsValid => handle != null && handle.IsCurrent(generation);
    public GameObject Instance => IsValid ? handle.gameObject : null;

    public void Stop(bool allowParticlesToFinish = false, float extraLife = 0f)
    {
        if (!IsValid) return;
        var spawner = handle.Pool != null ? handle.Pool.GetComponent<VfxSpawner>() : null;
        if (spawner != null)
            spawner.StopLoopingVfx(handle.gameObject, allowParticlesToFinish, extraLife);
        else
            handle.ReturnToPool();
    }
}
