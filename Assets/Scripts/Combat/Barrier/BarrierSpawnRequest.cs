using UnityEngine;

/// <summary>Everything a <see cref="BarrierRuntime"/> needs to come alive.</summary>
public sealed class BarrierSpawnRequest
{
    /// <summary>Faction owner. The barrier blocks whoever is hostile to this context.</summary>
    public CharacteContext Owner { get; set; }

    /// <summary>
    /// What this barrier is attached to. The runtime uses it to pick the right liveness rule —
    /// a cast-position barrier has no anchor to lose, a caster barrier ends when the caster dies.
    /// </summary>
    public BarrierAnchorMode AnchorMode { get; set; }

    /// <summary>Transform the barrier follows. Null pins the barrier to <see cref="FallbackPosition"/>.</summary>
    public Transform Anchor { get; set; }

    /// <summary>Used when <see cref="Anchor"/> is null, or as the initial placement.</summary>
    public Vector3 FallbackPosition { get; set; }

    /// <summary>Optional summon anchor. The barrier ends as soon as this summon stops being active.</summary>
    public SummonedEntityRuntime AnchorSummon { get; set; }

    public float Radius { get; set; }
    public float Lifetime { get; set; }
    public float MaxHealth { get; set; }
}
