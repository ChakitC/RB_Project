using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PassiveActionDefinition
{
    public string actionId = "action";
    public PassiveActionType actionType = PassiveActionType.GrantModifier;
    public PassiveTargetSelector targetSelector = PassiveTargetSelector.Self;

    [Header("Identity Overrides")]
    public string modifierKeyOverride;
    public string appliedByIdOverride;
    public string emittedEventSourceIdOverride;

    [Header("Runtime Modifier")]
    public List<PassiveStatModifier> modifiers = new();
    [Min(1)] public int grantedStacks = 1;
    [Min(1)] public int maxStacks = 1;
    [Min(0f)] public float durationSeconds = 1f;
    public PassiveModifierStackPolicy stackPolicy = PassiveModifierStackPolicy.Replace;

    [Header("Status Effect")]
    public StatusEffectDef statusEffect;
    [Min(1)] public int statusInitialStacks = 1;

    [Header("Child Event")]
    public PassiveEventType emittedEventType = PassiveEventType.None;
    public float emittedValue;
}
