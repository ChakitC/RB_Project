using UnityEngine;

/// <summary>
/// A volume that can absorb hostile projectiles. Implemented by <see cref="BarrierRuntime"/>;
/// projectiles only ever see this interface.
/// </summary>
public interface IProjectileBarrier
{
    bool IsBarrierActive { get; }
    Vector3 BarrierCenter { get; }
    float BarrierRadius { get; }

    /// <summary>
    /// True when this barrier should stop a projectile fired by <paramref name="sourceActor"/>.
    /// Friendly fire and unknown-faction shots always pass through.
    /// </summary>
    bool BlocksProjectileFrom(GameObject sourceActor);

    /// <summary>
    /// Spends barrier HP on an incoming shot. The shot is always fully consumed — a hit that
    /// breaks the barrier does not carry leftover damage through to whatever is behind it.
    /// </summary>
    void AbsorbProjectile(float damage, Vector3 hitPoint, Vector3 hitNormal);
}
