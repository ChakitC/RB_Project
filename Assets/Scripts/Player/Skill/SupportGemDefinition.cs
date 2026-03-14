using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewSupportGem",
    menuName = "Game/Support Gem"
)]
public class SupportGemDefinition : ScriptableObject
{
    [Header("Identity")] public string supportId; // unique key
    public string displayName;
    [TextArea] public string description;

    public Sprite icon;

    [Header("Can Support Skill Tags")]
    [Tooltip("Support นี้ใช้ได้กับสกิลที่มี Tag ไหนบ้าง เช่น Projectile+Spell เป็นต้น")]
    public SkillTag allowedTags;

    [Header("Stat Modifiers")] [Tooltip("รายการการปรับค่า stat เช่น +30% Damage, +2 Projectile, +20% Mana Cost ฯลฯ")]
    public List<StatModifier> statModifiers = new List<StatModifier>();

    [Header("Leveling (Optional)")] public int maxLevel = 20;

    [Serializable]
    public class LevelScaling
    {
        public int requiredLevel;
        public List<StatModifier> extraModifiers; // mod เพิ่มเมื่อ support นี้เลเวลสูงขึ้น
    }

    public List<LevelScaling> perLevelScaling = new List<LevelScaling>();

    // -----------------------------
    // ใช้ได้กับ Skill นี้ไหม?
    // -----------------------------
    public bool CanSupport(SkillGemDefinition skill)
    {
        if (skill == null) return false;

        // SkillTag เป็น [Flags] enum ใช้ bit mask
        return (skill.tags & allowedTags) != 0;
    }

    // -----------------------------
    // รวม StatModifier ทั้ง base + ที่ได้จากเลเวล
    // -----------------------------
    public List<StatModifier> GetModifiersForLevel(int level)
    {
        var result = new List<StatModifier>();

        if (statModifiers != null)
            result.AddRange(statModifiers);

        if (perLevelScaling != null)
        {
            foreach (var scaling in perLevelScaling)
            {
                if (scaling == null) continue;
                if (level < scaling.requiredLevel) continue;
                if (scaling.extraModifiers == null) continue;

                result.AddRange(scaling.extraModifiers);
            }
        }

        return result;
    }

    // -----------------------------
    // Apply เข้า FinalSkillStats
    // ถูกเรียกจาก SupportInstance.Apply(stats)
    // -----------------------------
    public void Apply(FinalSkillStats stats, int level)
    {
        if (stats == null) return;

        var mods = GetModifiersForLevel(level);

        foreach (var mod in mods)
        {
            if (mod == null) continue;
            ApplySingle(stats, mod);
        }
    }

    // -----------------------------
    // Hook ตอน Cast (optional)
    // เรียกจาก SupportInstance.OnCast(...)
    // -----------------------------
    public void OnCast(SkillInstance skill, FinalSkillStats stats)
    {
        // ตอนนี้ปล่อยว่างไว้ก่อน
        // ถ้าอยากทำ effect แบบ:
        // - เพิ่ม charge
        // - spawn วงแสงรอบตัว
        // - ทำ buff ชั่วคราว ฯลฯ
        // ค่อยมาเขียนเพิ่มตรงนี้
    }

    // ====== Helper ด้านล่างนี้ใช้กับ StatModifier ======

    private void ApplySingle(FinalSkillStats stats, StatModifier mod)
    {
        switch (mod.stat) // <<== ใช้ StatType stat
        {
            case StatType.Damage:
                ApplyToValue(ref stats.damage, mod);
                break;
            case StatType.AreaRadius:
                ApplyToValue(ref stats.areaRadius, mod);
                break;
            case StatType.ProjectileCount:
                ApplyToInt(ref stats.projectileCount, mod);
                break;
            case StatType.ManaCost:
                ApplyToValue(ref stats.manaCost, mod);
                break;
            case StatType.CastTime:
                ApplyToValue(ref stats.castTime, mod);
                break;
            case StatType.Cooldown:
                ApplyToValue(ref stats.cooldown, mod);
                break;
            case StatType.CritChance:
                ApplyToValue(ref stats.critChance, mod);
                break;
        }
    }

    private void ApplyToValue(ref float value, StatModifier mod)
    {
        // สูตรเดียวกับที่ใช้ใน SupportInstance:
        // (ค่าเดิม + add) * mul
        value = (value + mod.add) * mod.mul;
    }

    private void ApplyToInt(ref int value, StatModifier mod)
    {

    }
}