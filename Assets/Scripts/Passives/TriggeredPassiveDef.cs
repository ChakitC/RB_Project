using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Passives/Triggered")]
public sealed class TriggeredPassiveDef : PassiveDefinition
{
    public List<TriggeredPassiveRule> rules = new();

    public override PassiveKind Kind => PassiveKind.Triggered;

    public override void CollectUpgradeIds(List<string> ids)
    {
        if (ids == null || rules == null)
            return;

        for (int i = 0; i < rules.Count; i++)
        {
            string id = rules[i]?.requiredUpgradeId;
            if (!string.IsNullOrWhiteSpace(id))
                ids.Add(id.Trim());
        }
    }
}

[Serializable]
public sealed class TriggeredPassiveRule
{
    public string ruleId = "rule";

    [Tooltip("Optional. Rule stays inert unless the owning slot's snapshot grants this id.")]
    [UpgradeIdPicker] public string requiredUpgradeId;

    public PassiveEventType trigger = PassiveEventType.None;
    public PassiveOriginFilter originFilter = PassiveOriginFilter.ExternalOnly;
    [Min(1)] public int requiredCount = 1;
    [Min(0f)] public float countWindowSeconds;
    [Min(0f)] public float cooldownSeconds;
    public PassiveCounterConsumeMode counterConsumeMode = PassiveCounterConsumeMode.ResetAll;
    public bool requireTarget;
    public bool requireAttackId;
    public bool oncePerTargetPerChain = true;

    [Header("Optional Event Source")]
    public PassiveEventSourceKind eventSourceKind = PassiveEventSourceKind.None;
    public string eventSourceId;
    [Min(0f)] public float eventSourceFloatValue = 2f;
    public int eventSourceIntValue;

    public List<PassiveActionDefinition> actions = new();

    public string RuntimeRuleId => string.IsNullOrWhiteSpace(ruleId) ? "rule" : ruleId;
    public string RuntimeEventSourceId
    {
        get
        {
            if (eventSourceKind == PassiveEventSourceKind.None)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(eventSourceId))
                return eventSourceId;

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1}:{2:0.###}:{3}",
                eventSourceKind,
                trigger,
                eventSourceFloatValue,
                eventSourceIntValue);
        }
    }
}
