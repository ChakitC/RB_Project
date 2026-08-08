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

    // Legacy-field fallback (lazy resolve, NOT ISerializationCallbackReceiver — see the "Legacy Migration"
    // block at the bottom of StatusEffects/StatusApplicationSpec.cs for why) — several Passive.*.asset files
    // have real `statusEffect`/`statusInitialStacks` data serialized flat here.
    [Header("Status Effect")]
    [SerializeField, HideInInspector] StatusEffectDef statusEffect;
    [SerializeField, HideInInspector] int statusInitialStacks;

    public StatusApplicationSpec statusSpec = new();

    /// <summary>Call from PassiveController at apply time only — never during deserialization.</summary>
    public StatusApplicationSpec ResolvedStatusSpec()
    {
        if (statusSpec != null && statusSpec.effect != null)
            return statusSpec;

        if (statusEffect == null)
            return statusSpec;

        return new StatusApplicationSpec
        {
            effect = statusEffect,
            stacks = statusInitialStacks > 0 ? statusInitialStacks : 1,
            modifiers = statusSpec?.modifiers,
            durationOverride = statusSpec?.durationOverride ?? 0f,
            tickDamageOverride = statusSpec?.tickDamageOverride ?? 0f,
        };
    }

    [Header("Child Event")]
    public PassiveEventType emittedEventType = PassiveEventType.None;
    public float emittedValue;
}
