using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ค่าบาลานซ์ของการลง status หนึ่งครั้ง — authored ที่ apply site (สกิล/กระสุน/passive/pickup).
/// ฟิลด์ที่เว้นว่างจะ fallback ไปใช้ค่าใน StatusEffectDef. นี่คือ shape เดียวที่ทุก apply-site ถืออยู่จริง
/// (ผ่าน field ชื่อ spec/statusSpec) — ไม่มี field ซ้ำที่ apply-site ไหนอีกแล้ว.
///
/// อ่านต่อถ้าจะเพิ่ม apply-site ใหม่ที่ต้องรองรับ asset เดิมที่มีข้อมูลอยู่แล้ว (ดูหัวข้อ Legacy Migration ด้านล่าง).
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

    [Tooltip("0 = ใช้ duration ของ StatusEffectDef")]
    [Min(0f)]
    public float durationOverride;

    [Tooltip("0 = ใช้ tickDamage ของ StatusEffectDef. ติดลบ = heal")]
    public float tickDamageOverride;

    public bool HasModifierOverride =>
        modifiersOverrideEnabled || (modifiers != null && modifiers.Count > 0);

    /// <summary>
    /// ใช้ตอน apply site มี "skill-level duration fallback" ของตัวเอง (เช่น FinalSkillStats.effectDuration)
    /// ที่ต้องมาเป็นอันดับรองจาก durationOverride ของ spec เอง — คืน spec เดิมถ้าไม่ต้อง merge (ไม่ mutate ของเดิม,
    /// สร้าง instance ใหม่เฉพาะตอนต้อง merge จริงๆ เพื่อไม่ให้ค่า runtime-only เขียนทับ spec ที่ serialize อยู่ใน asset).
    /// </summary>
    public StatusApplicationSpec ResolveWithDurationFallback(float fallbackDuration)
    {
        if (durationOverride > 0f || fallbackDuration <= 0f)
            return this;

        return new StatusApplicationSpec
        {
            effect = effect,
            stacks = stacks,
            modifiers = modifiers,
            modifiersOverrideEnabled = modifiersOverrideEnabled,
            durationOverride = fallbackDuration,
            tickDamageOverride = tickDamageOverride,
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

        if (modifiers == null)
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

// ---------------------------------------------------------------------------------------------
// Legacy Migration — lazy resolve at use time, NOT ISerializationCallbackReceiver
// ---------------------------------------------------------------------------------------------
// เมื่อ apply-site เดิมมี flat field (effect/stacks) ที่ asset จริงอ้างอิงอยู่แล้ว ห้าม nest
// StatusApplicationSpec ทับตำแหน่งเดิมตรงๆ — Unity serialize ตามชื่อ field ตรงๆ ไม่ได้ auto-migrate ข้าม
// shape ให้ ถ้าเปลี่ยนจาก `effect`/`stacks` แบบแบนเป็น `spec.effect`/`spec.stacks` ข้อมูลเดิมจะหาย
// เงียบๆ ตอนเปิด asset ครั้งแรก (ไม่มี error).
//
// ⚠️ ลองใช้ ISerializationCallbackReceiver.OnAfterDeserialize() มาก่อนแล้วพบว่าใช้ไม่ได้จริง: Unity ไม่
// การันตีว่า UnityEngine.Object reference (เช่น field `effect` ที่ชี้ไป StatusEffectDef asset คนละไฟล์)
// resolve เสร็จแล้วตอน OnAfterDeserialize รัน โดยเฉพาะ cross-asset reference ที่ยังไม่เคยถูกโหลดเข้าหน่วยความจำ
// มาก่อน — ทดสอบจริงพบว่า asset ที่ reference ของมันบังเอิญโหลดอยู่แล้ว migrate ผ่าน แต่ asset ที่ reference ยัง
// ไม่เคยโหลด จะได้ `effect` เป็น null ตอนอ่านใน OnAfterDeserialize (ทั้งที่ field มีค่าจริงถ้าอ่านทีหลัง) —
// เป็นบั๊กที่ไม่คงที่ ขึ้นกับ asset load order ของ session นั้นๆ ห้ามใช้แพทเทิร์นนี้กับ field ที่เป็น
// UnityEngine.Object reference.
//
// แพทเทิร์นที่ใช้จริง — เก็บ legacy field ไว้เฉยๆ ไม่ migrate/มิวเทตอะไรตอน deserialize เลย แล้ว resolve แบบ
// lazy ตอนถูกใช้งานจริง (Execute()/Apply()) ซึ่งรันหลัง Awake/OnEnable เสมอ — จุดที่ Unity การันตีว่า object
// reference ทุกตัว resolve ครบแล้ว (ตัวอย่างจาก ApplyStatusSkillPayloadDef.StatusApplication):
//
//   [Serializable]
//   public sealed class StatusApplication
//   {
//       // เก็บค่าเก่าไว้เฉยๆ ห้ามอ่าน/เขียนที่อื่นนอกจาก ResolvedSpec()
//       [SerializeField, HideInInspector] StatusEffectDef effect;
//       [SerializeField, HideInInspector] int stacks;
//
//       public StatusApplicationSpec spec = new();
//
//       // เรียกจาก Execute()/Apply() เท่านั้น ห้ามเรียกจาก OnAfterDeserialize/constructor
//       public StatusApplicationSpec ResolvedSpec()
//       {
//           if (spec != null && spec.effect != null)
//               return spec;
//
//           if (effect == null)
//               return spec;
//
//           return new StatusApplicationSpec
//           {
//               effect = effect,
//               stacks = stacks > 0 ? stacks : 1,
//               modifiers = spec?.modifiers,
//               durationOverride = spec?.durationOverride ?? 0f,
//               tickDamageOverride = spec?.tickDamageOverride ?? 0f,
//           };
//       }
//   }
//
// ไม่ mutate `spec`/asset เลย (สร้าง instance ใหม่เฉพาะตอนต้อง fallback จริง) — designer ที่ authored
// spec.effect ใหม่แล้วจะ override legacy field ให้เองทันทีเพราะเช็ค `spec.effect != null` ก่อนเสมอ
// legacy field ที่ไม่เคยใช้อีกเลยหลัง author ใหม่ก็แค่เป็น dead data นอนอยู่เฉยๆ ปลอดภัย.
//
// ใช้แพทเทิร์นนี้ที่:
// - ApplyStatusSkillPayloadDef.StatusApplication / .ConditionalStatus
// - TauntSkillPayloadDef.ConditionalStatus
// - HealAreaStep.ConditionalStatus
// - PassiveActionDefinition (statusEffect/statusInitialStacks → statusSpec, method ชื่อ ResolvedStatusSpec())
// - ApplyStatusPickupEffectDef (effect/stacks → spec, บน ScriptableObject ตรงๆ ไม่ใช่ nested class)
//
// ApplyStatusOnHitModule.StatusApplication ไม่ต้องทำ — ไม่มี asset ไหนอ้างอิง GUID ของไฟล์นี้เลย (เช็คแล้ว)
// เลย nest StatusApplicationSpec ตรงๆ ได้โดยไม่มีความเสี่ยงข้อมูลหาย.
