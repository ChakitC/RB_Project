using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Status Effect")]
public sealed class StatusEffectDef : ScriptableObject
{
    [Header("Identity")]
    public string effectId;
    public Sprite icon;
    public StatusEffectCategory category = StatusEffectCategory.Neutral;

    [Header("Visual")]
    public StatusEffectVfxProfile vfxProfile;

    [Header("Lifetime")]
    [Min(0f)] public float duration = 5f;
    [Min(0)] public int maxStacks = 1;
    public StackMode stackMode = StackMode.RefreshDuration;

    [Tooltip("true = แยก instance ต่อผู้ลง (แต่ละแหล่งมีอายุ+ค่าของตัวเองและรวมกันแบบ multiplicative). " +
        "false = พฤติกรรมเดิม (instance เดียวต่อ effect ไม่ว่าใครลง). " +
        "ห้ามเปิดกับ effect ที่ removeEffect-by-def ใช้ถอด (เช่น Morph) หรือที่ source ไม่ใช่ actor จริง (เช่น pickup). " +
        "Taunt Def ต้องเปิด — แต่ละผู้ยั่วต้องมี instance ของตัวเองเพื่อให้ fallback กลับผู้ยั่วคนก่อนหน้าได้.")]
    public bool separatePerSource;

    [Header("Tick")]
    [Min(0f)] public float tickInterval;
    public float tickDamage;

    [Header("Gameplay")]
    public StatusLocomotionPose locomotionPose = StatusLocomotionPose.Auto;
    public ControlBlockFlags controlBlocks = ControlBlockFlags.None;
    public bool pushStunnedState;
    public List<string> tags = new();
    public List<StatusEffectModifier> modifiers = new();

    [Header("Triggered Stacking")]
    public List<StatusEffectTriggerRule> triggerRules = new();

    public bool IsPermanent => duration <= 0f;
    public bool HasTick => tickInterval > 0f && !Mathf.Approximately(tickDamage, 0f);
    public int ClampedMaxStacks => maxStacks <= 0 ? int.MaxValue : maxStacks;
}
