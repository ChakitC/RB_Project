using System;
using UnityEngine;

[Serializable]
public sealed class StatusEffectTriggerRule
{
    public EffectTriggerType triggerType = EffectTriggerType.None;
    [Min(1)] public int requiredCount = 1;
    [Min(1)] public int grantedStacks = 1;
    [Min(0)] public int maxStacks;
    public bool resetCounterAfterGrant = true;
}
