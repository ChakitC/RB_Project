using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewSkillGem",
    menuName = "Game/Skill Gem"
)]
public class SkillGemDefinition : ScriptableObject
{
    [Header("Identity")]
    public string skillId;           // ใช้เป็น unique key ในระบบ (ไม่โชว์ผู้เล่นก็ได้)
    public string displayName;      // ชื่อโชว์ใน UI
    [TextArea]
    public string description;      // คำบรรยาย

    public Sprite icon;

    [Header("Tag / Type")]
    public SkillTag tags;           // ใช้บอกว่าเป็น Melee/Spell/Projectile ฯลฯ

    [Header("Base Stats (Level 1)")]
    public int maxLevel = 20;

    public float baseDamage = 10f;
    public float baseManaCost = 10f;
    public float baseCastTime = 0.5f;
    public float baseCooldown = 0f;

    [Header("Extra Parameters")]
    public bool AreaofEffec = false;
    public float projectileSpeed = 5;
    public float baseRadius = 0f;
    public int baseProjectilesCount = 1;
    public float baseCritChance = 5f; // เปอร์เซ็นต์ก็ได้ เช่น 5 = 5%

    [Header("Execution")]
    public SkillPayloadDef payload;

    [Header("Visual / Prefab")]
    public GameObject skillPrefab;      // projectile / AoE / effect อะไรก็ว่าไป
    public GameObject BallVfxPrefab;
    public GameObject SkillVfxhit;
    public AudioCue castCue;

    [Header("Animation / Animancer")]
    [Tooltip("Per-skill Animancer transition. Set the clip plus Fade, Speed, Start Time, and optional transition events here.")]
    public ClipTransition skillClip;

    public float projectileHitVfxScale = 1f;
  
    [Range(0f, 1f)]
    public float castPointNormalized = 0.35f; // ตำแหน่งปล่อยสกิลใน shared skill clip แบบ normalized time

    // --------- Optional: ถ้าอยากทำเลเวลสกิลแบบละเอียด ---------

    [Serializable]
    public class LevelData
    {
        public int requiredLevel;     // เลเวลตัวละครขั้นต่ำ
        public float damage;
        public float manaCost;
        public float castTime;
        public float cooldown;
        public float radius;
        public int projectiles;
        public float critChance;

        public bool HasAnyOverride()
        {
            return !Mathf.Approximately(damage, 0f) ||
                   !Mathf.Approximately(manaCost, 0f) ||
                   !Mathf.Approximately(castTime, 0f) ||
                   !Mathf.Approximately(cooldown, 0f) ||
                   !Mathf.Approximately(radius, 0f) ||
                   projectiles != 0 ||
                   !Mathf.Approximately(critChance, 0f);
        }
    }

    [Tooltip("ถ้าใส่รายการนี้ จะใช้ค่านี้แทน base เมื่อคำนวณตามเลเวล")]
    public List<LevelData> perLevelData = new();

    public int ClampLevel(int level)
    {
        return Mathf.Clamp(level, 1, Mathf.Max(1, maxLevel));
    }

    public LevelData GetLevelData(int level)
    {
        if (perLevelData == null || perLevelData.Count == 0)
            return null;

        int clampedLevel = ClampLevel(level);
        LevelData bestMatch = null;
        int bestRequiredLevel = int.MinValue;

        for (int i = 0; i < perLevelData.Count; i++)
        {
            var entry = perLevelData[i];
            if (entry == null)
                continue;

            if (!entry.HasAnyOverride())
                continue;

            int requiredLevel = Mathf.Max(1, entry.requiredLevel);
            if (requiredLevel > clampedLevel)
                continue;

            if (bestMatch != null && requiredLevel <= bestRequiredLevel)
                continue;

            bestMatch = entry;
            bestRequiredLevel = requiredLevel;
        }

        if (bestMatch != null)
            return bestMatch;

        int index = Mathf.Clamp(clampedLevel - 1, 0, perLevelData.Count - 1);
        return perLevelData[index];
    }

    public void ApplyLevelData(FinalSkillStats stats, int level)
    {
        if (stats == null)
            return;

        var levelData = GetLevelData(level);
        if (levelData == null)
            return;

        stats.damage = levelData.damage;
        stats.manaCost = levelData.manaCost;
        stats.castTime = levelData.castTime;
        stats.cooldown = levelData.cooldown;
        stats.areaRadius = levelData.radius;
        stats.projectileCount = levelData.projectiles;
        stats.critChance = levelData.critChance;
    }

    public float GetCastPointNormalized()
    {
        if (!float.IsFinite(castPointNormalized))
            return 0.35f;

        return Mathf.Clamp(castPointNormalized, 0f, 0.999f);
    }
}
