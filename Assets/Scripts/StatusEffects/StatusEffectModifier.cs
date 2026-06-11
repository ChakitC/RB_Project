using System;
using UnityEngine;

[Serializable]
public sealed class StatusEffectModifier
{
    [Tooltip("Stat affected by this modifier. Stability is authored as a final percentage from 0 to 100.")]
    public StatType statType;

    [Tooltip("For Stability, Flat adds percentage points. Example: 30 Stability + 10 Flat = 40%.")]
    public ModifierOp operation = ModifierOp.Flat;

    [Tooltip("For Stability with Flat, enter percentage points rather than a 0-1 ratio.")]
    public float value;
}
