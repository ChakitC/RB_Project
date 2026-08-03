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
    public float staggerPower;

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

    [System.NonSerialized]
    public SkillUpgradeStatSnapshot upgradeSnapshot;

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
                staggerPower = 0f,
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
            staggerPower = def.baseStaggerPower,
            critChance = def.baseCritChance,
            critMultiplier = 2f
        };

        def.ApplyLevelData(stats, def.ClampLevel(level));
        if (user?.StatsHub != null && def.damageCoefficient > 0f)
            stats.damage += Mathf.Max(0f, user.StatsHub.GetSkillBaseDamage()) * def.damageCoefficient;

        upgradeSnapshot?.Apply(stats);

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
        stats.staggerPower = Mathf.Max(0f, stats.staggerPower);
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
        if (def == null || def.payload == null || user == null)
            return false;

        stats = GetFinalStats(user);

        if (Time.time < _lastCastTime + stats.cooldown)
            return false;

        if (user.currentEnagy < stats.manaCost)
            return false;

        return true;
    }

    public bool CanCast(ISkillUser user) => CanCast(user, out _);

    public bool Cast(ISkillUser user, CharacterAnimBrain animBrain = null, int requestId = 0)
    {
        if (!CanCast(user, out var stats))
            return false;

        return ExecuteCast(user, stats, spendEnergy: true, animBrain, requestId);
    }

    public bool TryCastIgnoringResourceCosts(ISkillUser user, CharacterAnimBrain animBrain = null, int requestId = 0, bool stampCooldown = true)
    {
        if (def == null || user == null)
            return false;

        var stats = GetFinalStats(user);
        return ExecuteCast(user, stats, spendEnergy: false, animBrain, requestId, stampCooldown);
    }

    /// <summary>
    /// Stamps ONLY the per-instance cooldown (no energy spend, no support OnCast, no payload).
    /// Used when a pre-cast cast is blocked/interrupted before cast point but should still
    /// consume its cooldown. Returns the computed stats so the caller can also stamp any
    /// shared (per-definition) cooldown.
    /// </summary>
    public bool TryStampCooldownOnly(ISkillUser user, out FinalSkillStats stats)
    {
        stats = null;
        if (def == null || user == null)
            return false;

        stats = GetFinalStats(user);
        _lastCastTime = Time.time;
        return true;
    }

    bool ExecuteCast(
        ISkillUser user,
        FinalSkillStats stats,
        bool spendEnergy,
        CharacterAnimBrain animBrain = null,
        int requestId = 0,
        bool stampCooldown = true)
    {
        if (def == null || def.payload == null || user == null || stats == null)
            return false;

        if (spendEnergy)
            user.SpendEnagy(stats.manaCost);

        foreach (var support in supports)
        {
            if (support == null)
                continue;

            support.OnCast(this, stats);
        }

        var castContext = new SkillCastContext(user, def, stats, animBrain, requestId);
        bool executed = TryExecutePayload(castContext);
        if (executed && stampCooldown)
            _lastCastTime = Time.time;
        return executed;
    }

    bool TryExecutePayload(SkillCastContext castContext)
    {
        if (castContext == null || castContext.Execution == null)
            return false;

        castContext.Execution.Execute(castContext);
        return true;
    }
}
