using System;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
public sealed class StatusEffectModifier
{
    [ValidateInput(nameof(HasValidScope), "WeaponOnly is not supported for this stat.")]
    public StatModifierScope scope;
    bool HasValidScope() => StatModifierRules.IsValidScope(statType, scope);
    [Tooltip("Stat affected by this modifier. Stability is authored as a final percentage from 0 to 100.")]
    public StatType statType;

    [Tooltip("For Stability, Flat adds percentage points. Example: 30 Stability + 10 Flat = 40%.")]
    [OnValueChanged(nameof(HandleOperationChanged))]
    public ModifierOp operation = ModifierOp.Flat;

    [Tooltip(StatModifierRules.ValueTooltip)]
    public float value = 1f;

#if UNITY_EDITOR
    void HandleOperationChanged()
    {
        // สลับมาเป็น Multiply แล้ว value ยังเป็น 0 (เช่น ค่า default เดิมของ Flat/AddPercent) → -100% ทันทีถ้าไม่ตั้งค่า
        if (operation == ModifierOp.Multiply && Mathf.Approximately(value, 0f))
            value = 1f;
    }
#endif
}
