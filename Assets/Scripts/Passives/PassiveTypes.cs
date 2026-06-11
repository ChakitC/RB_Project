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
    None,
    ShotFired,
    Hit,
    Kill,
    TakeDamage,
    DamagePrevented,
    PerfectDodge,
    Reload,
    DashStarted,
    DashEnded,
    MovementDistanceReached
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
