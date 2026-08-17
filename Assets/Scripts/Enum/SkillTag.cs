using System;
using UnityEngine;
using System.Collections.Generic;

// ใช้ Tag แบบ bitmask เพื่อเช็กว่าสกิลเป็น Melee/Spell/Projectile ฯลฯ
[Flags]
public enum SkillTag
{
    None       = 0,
    Melee      = 1 << 0,
    Ranged     = 1 << 1,
    Spell      = 1 << 2,
    Projectile = 1 << 3,
    Area       = 1 << 4,
    Minion     = 1 << 5,
    Movement   = 1 << 6,
    Trap       = 1 << 7,
    // เพิ่ม tag เพิ่มได้ตามเกมเรา
}

// ชนิด Stat ที่จะเอาไว้ให้ Support ปรับ
public enum StatType
{
    Damage,
    AreaRadius,
    ProjectileCount,
    ManaCost,
    CastTime,
    Cooldown,
    CritChance,
    Armor,
    MoveSpeed,
    CritRate,
    CritMultiplier,
    MaxHP,
    MaxStamina,
    MaxEnergy,
    ReloadTime,
    FireInterval,
    Stability,
    BulletSpeed,
    MaxMagazine,
    StaggerPower,
    StaminaRegenRate,
    MaxReserveAmmo,
    EffectDuration,
    HealPower,

    // Appended only. Serialized skill/passive assets store these by index, so never reorder
    // or insert above this line.
    MaxCharges,
    SummonMaxHP,
    SummonCap,
}

// โมดิฟายค่า Stat แบบ generic
[Serializable]
public class StatModifier
{
    [Tooltip("จะไปปรับค่า stat ตัวไหน")]
    public StatType stat;

    [Tooltip("ค่าที่จะบวกเพิ่ม เช่น +10 damage")]
    public float add = 0f;

    [Tooltip("ตัวคูณ เช่น 1.3 = +30%, 1 = ไม่คูณอะไรเลย")]
    public float mul = 1f;
}
