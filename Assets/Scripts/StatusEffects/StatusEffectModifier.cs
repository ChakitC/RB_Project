using System;
using UnityEngine;

[Serializable]
public sealed class StatusEffectModifier
{
    public StatType statType;
    public ModifierOp operation = ModifierOp.Flat;
    public float value;
}
