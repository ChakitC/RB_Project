using System;
using System.Collections.Generic;
using UnityEngine;

public enum PassiveValueSource { Constant, Parameter, EventValue, StaminaRatio, MissingStaminaRatio, Variable, AppliedDamage, MaxHealth, Overkill, ResolvedDamage, AmmoAfter, MaxMagazine, CurrentMagazineSize }

[Serializable]
public sealed class PassiveNumber
{
    public PassiveValueSource source;
    public float value;
    public string reference;
    public string multiplyParameter;
    public string multiplyVariable;
    public bool roundToInteger;
    public bool ceilToInteger;
    public float multiply = 1f;
    public float add;
    public float minimum = -1000000f;
    public float maximum = 1000000f;
    public List<PassiveNumericUpgrade> upgrades = new();

    public PassiveNumber() { }
    public PassiveNumber(float value) { this.value = value; }
    public bool UsesResource => source == PassiveValueSource.StaminaRatio || source == PassiveValueSource.MissingStaminaRatio;
    public bool IsStatic => (source == PassiveValueSource.Constant || source == PassiveValueSource.Parameter) && string.IsNullOrEmpty(multiplyVariable);

    public float Resolve(PassiveExecution execution, bool readOnly = false)
    {
        float raw = source switch
        {
            PassiveValueSource.AppliedDamage => execution.Event.Metadata.AppliedDamage,
            PassiveValueSource.MaxHealth => execution.Event.Metadata.MaxHealth,
            PassiveValueSource.Overkill => execution.Event.Metadata.OverkillAmount,
            PassiveValueSource.ResolvedDamage => execution.Event.Metadata.ResolvedDamage,
            PassiveValueSource.AmmoAfter => execution.Event.Metadata.AmmoAfter,
            PassiveValueSource.MaxMagazine => execution.Event.Metadata.MaxMagazine,
            PassiveValueSource.CurrentMagazineSize => execution.Owner?.WeaponSystem != null ? execution.Owner.WeaponSystem.MagazineSize : 0,
            PassiveValueSource.Parameter => execution.NumberParameter(reference),
            PassiveValueSource.EventValue => execution.Event.Value,
            PassiveValueSource.StaminaRatio => execution.Runtime.StaminaRatio,
            PassiveValueSource.MissingStaminaRatio => 1f - execution.Runtime.StaminaRatio,
            PassiveValueSource.Variable => readOnly ? execution.PeekVariable(reference) : execution.ReadVariable(reference),
            _ => value
        };
        if (!string.IsNullOrEmpty(multiplyParameter)) raw *= execution.NumberParameter(multiplyParameter);
        if (!string.IsNullOrEmpty(multiplyVariable)) raw *= execution.PeekVariable(multiplyVariable);
        float flat = 0f, percent = 0f, factor = 1f;
        foreach (var entry in upgrades)
        {
            if (entry == null || execution.Grant.Upgrades?.HasUpgrade(entry.upgradeId) != true) continue;
            switch (entry.operation)
            {
                case ModifierOp.Flat: flat += entry.value; break;
                case ModifierOp.AddPercent: percent += entry.value * 0.01f; break;
                case ModifierOp.Multiply: factor *= Mathf.Max(0f, entry.value); break;
            }
        }
        float result = ((raw * multiply + add + flat) * (1f + percent)) * factor;
        if (roundToInteger) result = Mathf.Round(result);
        if (ceilToInteger) result = Mathf.Ceil(result);
        return float.IsFinite(result) ? Mathf.Clamp(result, minimum, maximum) : 0f;
    }
}
