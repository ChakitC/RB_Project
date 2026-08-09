using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ค่าบาลานซ์ของการลง status หนึ่งครั้ง — authored ที่ apply site (สกิล/กระสุน/passive/pickup).
///
/// เจ้าของค่า:
/// - <see cref="StatusEffectDef"/> เป็นเจ้าของ identity/stackMode/maxStacks/control/tags/trigger rules/presentation
///   และค่าบาลานซ์ตั้งต้น
/// - spec นี้เป็นเจ้าของค่าราย application: stacks, modifiers, duration, tick damage, tick interval
///
/// ทุก channel ที่ override ได้มี enable flag ของตัวเอง — ไม่ใช้ `0` เป็น sentinel. เมื่อ override เปิด ค่า `0`
/// คือค่าจริง (duration 0 = permanent, tick damage 0 = ปิด damage/heal, tick interval 0 = ปิด tick).
/// channel ที่ไม่ได้เปิด override จะติดตามค่าใน Def เสมอ.
///
/// ห้าม express ความต่างของ stackMode, maxStacks, control behavior, tags, trigger rules หรือ presentation
/// จาก apply site — ถ้าต้องต่างให้สร้าง <see cref="StatusEffectDef"/> คนละตัว. สอง application ที่ใช้ Def
/// เดียวกันคือ status identity เดียวกัน; ความแรงต่างกันอย่างเดียวไม่ใช่เหตุผลให้ clone asset.
/// </summary>
[Serializable]
public sealed class StatusApplicationSpec
{
    public StatusEffectDef effect;

    [Min(1)]
    public int stacks = 1;

    [Tooltip("Uses StatusEffectDef modifiers until edited. An explicit empty override applies no stat modifiers. Debuffs should use ModifierOp.Multiply for diminishing stacking.")]
    public List<StatusEffectModifier> modifiers = new();

    [SerializeField, HideInInspector]
    bool modifiersOverrideEnabled;

    [Tooltip("Application duration. 0 with the override enabled = permanent.")]
    [Min(0f)]
    public float durationOverride;

    [SerializeField, HideInInspector]
    bool durationOverrideEnabled;

    [Tooltip("Per-tick damage for this application. Negative = heal. 0 with the override enabled = no tick damage/heal.")]
    public float tickDamageOverride;

    [SerializeField, HideInInspector]
    bool tickDamageOverrideEnabled;

    [Tooltip("Seconds between ticks for this application. 0 with the override enabled = ticking disabled.")]
    [Min(0f)]
    public float tickIntervalOverride;

    [SerializeField, HideInInspector]
    bool tickIntervalOverrideEnabled;

    public bool HasModifierOverride => modifiersOverrideEnabled;
    public bool HasDurationOverride => durationOverrideEnabled;
    public bool HasTickDamageOverride => tickDamageOverrideEnabled;
    public bool HasTickIntervalOverride => tickIntervalOverrideEnabled;

    /// <summary>คัดลอกทุก channel (รวม enable flag) — ใช้ตอนต้องสร้าง spec ชั่วคราวโดยไม่แตะ spec ที่ serialize อยู่ใน asset</summary>
    public StatusApplicationSpec Clone()
    {
        return new StatusApplicationSpec
        {
            effect = effect,
            stacks = stacks,
            modifiers = modifiers,
            modifiersOverrideEnabled = modifiersOverrideEnabled,
            durationOverride = durationOverride,
            durationOverrideEnabled = durationOverrideEnabled,
            tickDamageOverride = tickDamageOverride,
            tickDamageOverrideEnabled = tickDamageOverrideEnabled,
            tickIntervalOverride = tickIntervalOverride,
            tickIntervalOverrideEnabled = tickIntervalOverrideEnabled,
        };
    }

    /// <summary>
    /// เติม issue string ลง buffer ถ้า spec นี้ author ผิด (เรียกจาก CollectValidationIssues ของ apply site)
    /// </summary>
    public void CollectValidationIssues(List<string> issues, string contextLabel)
    {
        if (issues == null)
            return;

        if (effect == null)
        {
            issues.Add($"{contextLabel}: missing StatusEffectDef.");
            return;
        }

        if (modifiers == null || !HasModifierOverride)
            return;

        for (int i = 0; i < modifiers.Count; i++)
        {
            var modifier = modifiers[i];
            if (modifier == null)
                continue;

            if (modifier.operation == ModifierOp.Multiply && modifier.value <= 0f)
            {
                issues.Add(
                    $"{contextLabel}: modifier #{i} on '{effect.name}' uses Multiply with value <= 0 " +
                    "(this zeroes or inverts the stat — did you forget to set it?).");
            }
        }
    }
}
