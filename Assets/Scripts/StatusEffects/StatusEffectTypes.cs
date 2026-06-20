using System;

public enum StatusEffectCategory
{
    Neutral,
    Buff,
    Debuff
}

public enum ModifierOp
{
    Flat,
    AddPercent,
    Multiply
}

public enum StackMode
{
    RefreshDuration,
    AddStackAndRefresh,
    IndependentInstances,
    StrongestOnly
}

public enum EffectTriggerType
{
    None,
    OnShotFired,
    OnHit,
    OnKill,
    OnTakeDamage,
    OnReload
}

[Flags]
public enum ControlBlockFlags
{
    None = 0,
    Move = 1 << 0,
    Shoot = 1 << 1,
    Skill = 1 << 2,
    Rotate = 1 << 3
}
