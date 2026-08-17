using UnityEngine;

/// <summary>A barrier absorbed a shot but survived.</summary>
public readonly struct BarrierDamagedEventData
{
    public readonly BarrierRuntime Barrier;
    public readonly float Damage;
    public readonly float RemainingHealth;
    public readonly float MaxHealth;
    public readonly Vector3 HitPoint;
    public readonly Vector3 HitNormal;

    public float RemainingHealth01 => MaxHealth > 0f ? Mathf.Clamp01(RemainingHealth / MaxHealth) : 0f;

    public BarrierDamagedEventData(
        BarrierRuntime barrier,
        float damage,
        float remainingHealth,
        float maxHealth,
        Vector3 hitPoint,
        Vector3 hitNormal)
    {
        Barrier = barrier;
        Damage = damage;
        RemainingHealth = remainingHealth;
        MaxHealth = maxHealth;
        HitPoint = hitPoint;
        HitNormal = hitNormal;
    }
}

public enum BarrierEndReason
{
    /// <summary>HP reached zero. A broken barrier never regenerates; it needs a fresh cast.</summary>
    Broken = 0,

    /// <summary>The authored lifetime ran out.</summary>
    Expired = 1,

    /// <summary>The anchor died, despawned, or was destroyed.</summary>
    AnchorLost = 2,
}

/// <summary>A barrier stopped existing, for any reason.</summary>
public readonly struct BarrierBrokenEventData
{
    public readonly BarrierRuntime Barrier;
    public readonly BarrierEndReason Reason;
    public readonly Vector3 Position;
    public readonly Vector3 HitPoint;
    public readonly Vector3 HitNormal;

    public BarrierBrokenEventData(
        BarrierRuntime barrier,
        BarrierEndReason reason,
        Vector3 position,
        Vector3 hitPoint,
        Vector3 hitNormal)
    {
        Barrier = barrier;
        Reason = reason;
        Position = position;
        HitPoint = hitPoint;
        HitNormal = hitNormal;
    }
}
