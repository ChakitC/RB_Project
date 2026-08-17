using System.Collections.Generic;

/// <summary>
/// Active barriers in the scene. The physics layer does the per-frame work; this registry only
/// exists so a barrier can be resolved from any collider in its hierarchy and so tooling can
/// enumerate what is live.
/// </summary>
public static class BarrierRegistry
{
    static readonly List<BarrierRuntime> active = new();

    public static IReadOnlyList<BarrierRuntime> Active => active;

    /// <summary>
    /// O(1) "is any barrier live right now?". Projectiles check this before doing any barrier
    /// work, so a scene with no barrier pays nothing per projectile per physics step.
    /// </summary>
    public static bool HasActiveBarrier => active.Count > 0;

    public static void Register(BarrierRuntime barrier)
    {
        if (barrier != null && !active.Contains(barrier))
            active.Add(barrier);
    }

    public static void Unregister(BarrierRuntime barrier)
    {
        if (barrier != null)
            active.Remove(barrier);
    }

    public static int CountActive()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (active[i] == null || !active[i].IsBarrierActive)
                active.RemoveAt(i);
        }

        return active.Count;
    }
}
