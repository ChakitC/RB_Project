using UnityEngine;

public enum AITargetIdentity
{
    Auto = 0,
    Generic = 1,
    Player = 2,
    Companion = 3,
    Enemy = 4,
    Neutral = 5
}

// Legacy serialized values from the first scoring pass.
public enum AITargetRole
{
    Auto = 0,
    Generic = 1,
    Player = 2,
    Companion = 3,
    Tank = 4,
    Healer = 5,
    Support = 6,
    Sniper = 7
}

public interface IAITargetable
{
    Transform AimPoint { get; }
    bool IsAlive { get; }
    bool IsTargetable { get; }
    int TeamId { get; }
    AITargetIdentity TargetIdentity { get; }
    CharacterCombatRole CombatRole { get; }
    float BaseTargetPriority { get; }
    float ThreatMultiplier { get; }
}
