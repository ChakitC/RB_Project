using System;
using System.Collections.Generic;
using Unity.VisualScripting;
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

    [Header("Visual / Prefab")]
    public GameObject skillPrefab;      // projectile / AoE / effect อะไรก็ว่าไป
    public GameObject BallVfxPrefab;
    public GameObject SkillVfxhit;
        
    public float projectileHitVfxScale = 1f;
    public AnimationClip castAnimation; // ถ้ามีอนิเมชันเฉพาะ

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
    }

    [Tooltip("ถ้าใส่รายการนี้ จะใช้ค่านี้แทน base เมื่อคำนวณตามเลเวล")]
    public List<LevelData> perLevelData = new();

    public LevelData GetLevelData(int level)
    {
        if (perLevelData == null || perLevelData.Count == 0)
            return null;

        // clamp level
        int index = Mathf.Clamp(level - 1, 0, perLevelData.Count - 1);
        return perLevelData[index];
    }
}