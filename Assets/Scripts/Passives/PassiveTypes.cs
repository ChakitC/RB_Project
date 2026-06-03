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
    public StatType statType;
    public ModifierOp operation = ModifierOp.Flat;
    public float value;
}
