using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class FinalSkillStats
{
    public float damage;
    public float areaRadius;
    public int projectileCount;
    public float manaCost;
    public float castTime;
    public float cooldown;

    // 0..100 (%)
    public float critChance;

    // 2.0 = 200%
    public float critMultiplier;
}

[System.Serializable]
public class SkillInstance
{
    public SkillGemDefinition def;
    public int level = 1;

    public List<SupportInstance> supports = new List<SupportInstance>();

    private float _lastCastTime = -999f;

    public FinalSkillStats GetFinalStats(ISkillUser user)
    {
        // กัน NullRef เผื่อมีใครเรียกตรงๆ
        if (def == null)
        {
            return new FinalSkillStats
            {
                damage = 0f,
                areaRadius = 0f,
                projectileCount = 1,
                manaCost = 0f,
                castTime = 0f,
                cooldown = 0f,
                critChance = 0f,
                critMultiplier = 2f
            };
        }

        var stats = new FinalSkillStats
        {
            damage          = def.baseDamage,
            areaRadius      = def.baseRadius,
            projectileCount = def.baseProjectilesCount,
            manaCost        = def.baseManaCost,
            castTime        = def.baseCastTime,
            cooldown        = def.baseCooldown,

            critChance      = def.baseCritChance, // 0..100
            critMultiplier  = 2f                  // base ของสกิล
        };

        def.ApplyLevelData(stats, def.ClampLevel(level));

        // 1) Apply supports
        foreach (var s in supports)
        {
            if (s == null) continue;
            if (!s.CanSupport(def)) continue;
            s.Apply(stats);
        }

        // 2) Apply caster stats (StatsHub)
        ApplyCasterStats(user, stats);

        // 3) clamp
        stats.critChance      = Mathf.Clamp(stats.critChance, 0f, 100f);
        stats.projectileCount = Mathf.Max(1, stats.projectileCount);
        stats.areaRadius      = Mathf.Max(0f, stats.areaRadius);
        stats.manaCost        = Mathf.Max(0f, stats.manaCost);
        stats.castTime        = Mathf.Max(0f, stats.castTime);
        stats.cooldown        = Mathf.Max(0f, stats.cooldown);
        stats.critMultiplier  = Mathf.Max(1f, stats.critMultiplier);

        return stats;
    }

    void ApplyCasterStats(ISkillUser user, FinalSkillStats stats)
    {
        if (user == null || stats == null) return;

        var hub = user.StatsHub;
        if (hub == null) return;

        // ใช้ crit จากผู้ร่าย (0..100)
        stats.critChance += hub.CritRatePercent;

        // อย่า override ทับ support — ให้เพิ่ม "โบนัสจากผู้ร่าย" เข้าไป
        // ผู้ร่ายมี CritMultiplier เป็น "ตัวคูณเต็ม" เช่น 2.3 -> โบนัส = +0.3
        stats.critMultiplier += (hub.CritMultiplier - 1f);

        // ถ้าจะให้ดาเมจสกิลได้โบนัสจากตัวละคร (แล้วแต่ดีไซน์):
        // stats.damage += hub.GetSkillBaseDamage();
    }

    public bool CanCast(ISkillUser user, out FinalSkillStats stats)
    {
        stats = null;
        if (def == null || user == null) return false;

        stats = GetFinalStats(user);

        if (Time.time < _lastCastTime + stats.cooldown) return false;
        if (user.currentEnagy < stats.manaCost) return false;

        return true;
    }

    public bool CanCast(ISkillUser user) => CanCast(user, out _);

    public void Cast(ISkillUser user)
    {
        if (!CanCast(user, out var stats))
            return;

        user.SpendEnagy(stats.manaCost);

        foreach (var s in supports)
        {
            if (s == null) continue;
            s.OnCast(this, stats);
        }

        if (def.skillPrefab == null || user.CastOrigin == null)
        {
            _lastCastTime = Time.time;
            return;
        }

        // prefab ต้องมี Projectile
        var prefabProj = def.skillPrefab.GetComponent<Projectile>();
        if (prefabProj == null)
        {
            Debug.LogError($"Skill '{def.name}' prefab missing Projectile component");
            _lastCastTime = Time.time;
            return;
        }

        for (int i = 0; i < stats.projectileCount; i++)
        {
            // Skill identity is fixed when the cast starts, but origin/aim are sampled here at
            // release time so the projectile follows the live cast socket and facing.
            Vector3 dir = user.AimDirection;

            if (dir.sqrMagnitude < 0.0001f)
                dir = user.AimTransform ? user.AimTransform.forward : Vector3.forward;

            dir.Normalize();

            if (stats.projectileCount > 1)
            {
                float spread = 10f;
                float t = (float)i / (stats.projectileCount - 1) - 0.5f; // -0.5..0.5
                float angle = t * spread;
                dir = Quaternion.AngleAxis(angle, Vector3.up) * dir;
            }

            var projGO = Object.Instantiate(
                def.skillPrefab,
                user.CastOrigin.position,
                Quaternion.LookRotation(dir, Vector3.up)
            );

            var proj = projGO.GetComponent<Projectile>();
            if (proj == null) continue;

            // ✅ “ดึงจาก def อย่างเดียว” + เซ็ต ctx/stats ให้ครบในเมธอดเดียว
            proj.InitFromSkillDef(
                proj.config,     // ใช้ config ที่ติดบน prefab (หรือจะส่ง cfg จากที่อื่นก็ได้)
                user,
                def,
                stats,
                dir,
                prefabProj
            );
        }

        _lastCastTime = Time.time;
    }
    

}
