using System;
using System.Collections.Generic;

public interface IStatModifierProvider
{
    event Action StatModifiersChanged;

    void AppendStatModifiers(List<RuntimeStatModifier> buffer);
}

public readonly struct RuntimeStatModifier
{
    public RuntimeStatModifier(StatType statType, ModifierOp operation, float value, string modifierKey = null)
    {
        StatType = statType;
        Operation = operation;
        Value = value;
        ModifierKey = modifierKey;
    }

    public StatType StatType { get; }
    public ModifierOp Operation { get; }
    public float Value { get; }
    public string ModifierKey { get; }
}
