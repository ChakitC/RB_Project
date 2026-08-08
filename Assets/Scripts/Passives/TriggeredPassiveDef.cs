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
            TriggeredPassiveRule rule = rules[i];
            if (rule == null)
                continue;

            if (!string.IsNullOrWhiteSpace(rule.requiredUpgradeId))
                ids.Add(rule.requiredUpgradeId.Trim());

            if (rule.upgradeOverrides == null)
                continue;

            for (int j = 0; j < rule.upgradeOverrides.Count; j++)
            {
                string overrideId = rule.upgradeOverrides[j]?.upgradeId;
                if (!string.IsNullOrWhiteSpace(overrideId))
                    ids.Add(overrideId.Trim());
            }
        }
    }
}

public enum PassiveRuleField
{
    RequiredCount,
    CountWindowSeconds,
    CooldownSeconds,
}

[Serializable]
public sealed class PassiveRuleFieldOverride
{
    [Tooltip("Required. Override is inert without it.")]
    [UpgradeIdPicker] public string upgradeId;
    public PassiveRuleField field;
    public ModifierOp operation;
    public float value;
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

    [Header("Numeric Overrides")]
    public List<PassiveRuleFieldOverride> upgradeOverrides = new();

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

    public int ResolveRequiredCount(SkillUpgradeStatSnapshot upgrades) =>
        Mathf.Max(1, Mathf.RoundToInt(ResolveField(PassiveRuleField.RequiredCount, requiredCount, upgrades)));

    public float ResolveCountWindowSeconds(SkillUpgradeStatSnapshot upgrades) =>
        Mathf.Max(0f, ResolveField(PassiveRuleField.CountWindowSeconds, countWindowSeconds, upgrades));

    public float ResolveCooldownSeconds(SkillUpgradeStatSnapshot upgrades) =>
        Mathf.Max(0f, ResolveField(PassiveRuleField.CooldownSeconds, cooldownSeconds, upgrades));

    // Matches CharacterStatFormula.ApplyModifiers: AddPercent is whole percentages (10 => +10%),
    // Multiply is a direct factor.
    float ResolveField(PassiveRuleField field, float baseValue, SkillUpgradeStatSnapshot upgrades)
    {
        if (upgradeOverrides == null || upgrades == null)
            return baseValue;

        float flat = 0f;
        float addPercent01 = 0f;
        float multiply = 1f;

        for (int i = 0; i < upgradeOverrides.Count; i++)
        {
            PassiveRuleFieldOverride fieldOverride = upgradeOverrides[i];
            if (fieldOverride == null || fieldOverride.field != field || !upgrades.HasUpgrade(fieldOverride.upgradeId))
                continue;

            switch (fieldOverride.operation)
            {
                case ModifierOp.Flat:
                    flat += fieldOverride.value;
                    break;
                case ModifierOp.AddPercent:
                    addPercent01 += fieldOverride.value * 0.01f;
                    break;
                case ModifierOp.Multiply:
                    multiply *= Mathf.Max(0f, fieldOverride.value);
                    break;
            }
        }

        return (baseValue + flat) * (1f + addPercent01) * multiply;
    }
}
