using System;
using UnityEngine;

public enum PassiveKind
{
    AlwaysOn,
    Triggered,
    Custom
}

public enum PassiveEventType
{
    // Serialized: append only. Never reorder, renumber, or reuse a retired value.
    None = 0,
    ShotFired = 1,
    Hit = 2,
    Kill = 3,
    TakeDamage = 4,
    DamagePrevented = 5,
    PerfectDodge = 6,
    Reload = 7,
    DashStarted = 8,
    DashEnded = 9,
    MovementDistanceReached = 10,
    StatusApplied = 11,
    StatusStackChanged = 12,
    ComboSkillCommitted = 13,
}

public enum PassiveEventOrigin
{
    External,
    Passive,
    StatusEffect,
    System
}

public enum PassiveOriginFilter
{
    ExternalOnly,
    NonPassive,
    PassiveOnly,
    Any
}

public enum PassiveModifierStackPolicy
{
    Replace,
    RefreshDuration,
    AddStacks,
    Independent,
    IgnoreWhileActive
}

public enum PassiveCounterConsumeMode
{
    ResetAll,
    CarryOver
}

public enum PassiveTargetSelector
{
    Self,
    EventTarget
}

public enum PassiveActionType
{
    GrantModifier,
    ApplyStatusEffect,
    EmitEvent
}

[Serializable]
public sealed class PassiveStatModifier
{
    [Tooltip("Stat affected by this modifier. Stability is authored as a final percentage from 0 to 100.")]
    public StatType statType;

    [Tooltip("For Stability, Flat adds percentage points. Example: 30 Stability + 10 Flat = 40%.")]
    public ModifierOp operation = ModifierOp.Flat;

    [Tooltip("For Stability with Flat, enter percentage points rather than a 0-1 ratio.")]
    public float value;
}
