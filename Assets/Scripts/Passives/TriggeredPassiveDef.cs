using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Passives/Triggered")]
public sealed class TriggeredPassiveDef : PassiveDefinition
{
    public List<TriggeredPassiveRule> rules = new();

    public override PassiveKind Kind => PassiveKind.Triggered;
}

[Serializable]
public sealed class TriggeredPassiveRule
{
    public string ruleId = "rule";
    public PassiveEventType trigger = PassiveEventType.None;
    public PassiveOriginFilter originFilter = PassiveOriginFilter.ExternalOnly;
    [Min(1)] public int requiredCount = 1;
    [Min(0f)] public float countWindowSeconds;
    [Min(0f)] public float cooldownSeconds;
    public PassiveCounterConsumeMode counterConsumeMode = PassiveCounterConsumeMode.ResetAll;
    public bool requireTarget;
    public bool requireAttackId;
    public bool oncePerTargetPerChain = true;
    public List<PassiveActionDefinition> actions = new();

    public string RuntimeRuleId => string.IsNullOrWhiteSpace(ruleId) ? "rule" : ruleId;
}
