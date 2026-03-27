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
            damage = def.baseDamage,
            areaRadius = def.baseRadius,
            projectileCount = def.baseProjectilesCount,
            manaCost = def.baseManaCost,
            castTime = def.baseCastTime,
            cooldown = def.baseCooldown,
            critChance = def.baseCritChance,
            critMultiplier = 2f
        };

        def.ApplyLevelData(stats, def.ClampLevel(level));

        foreach (var support in supports)
        {
            if (support == null || !support.CanSupport(def))
                continue;

            support.Apply(stats);
        }

        ApplyCasterStats(user, stats);

        stats.critChance = Mathf.Clamp(stats.critChance, 0f, 100f);
        stats.projectileCount = Mathf.Max(1, stats.projectileCount);
        stats.areaRadius = Mathf.Max(0f, stats.areaRadius);
        stats.manaCost = Mathf.Max(0f, stats.manaCost);
        stats.castTime = Mathf.Max(0f, stats.castTime);
        stats.cooldown = Mathf.Max(0f, stats.cooldown);
        stats.critMultiplier = Mathf.Max(1f, stats.critMultiplier);

        return stats;
    }

    void ApplyCasterStats(ISkillUser user, FinalSkillStats stats)
    {
        if (user == null || stats == null)
            return;

        var hub = user.StatsHub;
        if (hub == null)
            return;

        stats.critChance += hub.CritRatePercent;
        stats.critMultiplier += hub.CritMultiplier - 1f;
    }

    public bool CanCast(ISkillUser user, out FinalSkillStats stats)
    {
        stats = null;
        if (def == null || user == null)
            return false;

        stats = GetFinalStats(user);

        if (Time.time < _lastCastTime + stats.cooldown)
            return false;

        if (user.currentEnagy < stats.manaCost)
            return false;

        return true;
    }

    public bool CanCast(ISkillUser user) => CanCast(user, out _);

    public void Cast(ISkillUser user)
    {
        if (!CanCast(user, out var stats))
            return;

        user.SpendEnagy(stats.manaCost);

        foreach (var support in supports)
        {
            if (support == null)
                continue;

            support.OnCast(this, stats);
        }

        var castContext = new SkillCastContext(user, def, stats);
        if (TryExecutePayload(castContext) || TryCastLegacyProjectile(castContext))
        {
            _lastCastTime = Time.time;
            return;
        }

        _lastCastTime = Time.time;
    }

    bool TryExecutePayload(SkillCastContext castContext)
    {
        if (def == null || def.payload == null)
            return false;

        def.payload.Execute(castContext);
        return true;
    }

    bool TryCastLegacyProjectile(SkillCastContext castContext)
    {
        if (def == null || def.skillPrefab == null || castContext == null || castContext.CastOrigin == null)
            return false;

        var prefabProj = def.skillPrefab.GetComponent<Projectile>();
        if (prefabProj == null)
        {
            Debug.LogError($"Skill '{def.name}' is missing a payload and its prefab has no Projectile component.");
            return false;
        }

        int projectileCount = castContext.SkillStats != null
            ? Mathf.Max(1, castContext.SkillStats.projectileCount)
            : 1;

        for (int i = 0; i < projectileCount; i++)
        {
            Vector3 dir = ComputeLegacyProjectileDirection(castContext, i, projectileCount);
            var projGO = Object.Instantiate(
                def.skillPrefab,
                castContext.CastOrigin.position,
                Quaternion.LookRotation(dir, Vector3.up));

            var proj = projGO.GetComponent<Projectile>();
            if (proj == null)
                continue;

            proj.InitFromSkillDef(
                proj.config,
                castContext.User,
                def,
                castContext.SkillStats,
                dir,
                prefabProj);
        }

        return true;
    }

    static Vector3 ComputeLegacyProjectileDirection(SkillCastContext castContext, int projectileIndex, int projectileCount)
    {
        Vector3 dir = castContext != null ? castContext.AimDirection : Vector3.forward;
        if (projectileCount > 1)
        {
            float spread = 10f;
            float t = (float)projectileIndex / (projectileCount - 1) - 0.5f;
            float angle = t * spread;
            dir = Quaternion.AngleAxis(angle, Vector3.up) * dir;
        }

        return dir.normalized;
    }
}
