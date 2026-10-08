using System;
using System.Collections.Generic;

public interface IStatModifierProvider
{
    event Action StatModifiersChanged;

    void AppendStatModifiers(List<RuntimeStatModifier> buffer);
}

public readonly struct RuntimeStatModifier
{
    public RuntimeStatModifier(StatType statType, ModifierOp operation, float value, string modifierKey = null,
        StatModifierScope scope = StatModifierScope.Global, string weaponInstanceId = null)
    {
        StatType = statType;
        Operation = operation;
        Value = value;
        ModifierKey = modifierKey;
        Scope = scope;
        WeaponInstanceId = scope == StatModifierScope.WeaponOnly ? weaponInstanceId : null;
    }

    public StatType StatType { get; }
    public ModifierOp Operation { get; }
    public float Value { get; }
    public string ModifierKey { get; }
    public StatModifierScope Scope { get; }
    public string WeaponInstanceId { get; }
}
