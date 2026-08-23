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
    public float effectDuration;
    public float healPower;

    // 0..100 (%)
    public float critChance;

    // 2.0 = 200%
    public float critMultiplier;

    // How many casts can be banked at once. 1 = classic single-cooldown behavior.
    public int maxCharges;
}

[System.Serializable]
public class SkillInstance
{
    public SkillGemDefinition def;

    [System.NonSerialized]
    public SkillUpgradeStatSnapshot upgradeSnapshot;

    [System.NonSerialized]
    private SkillChargeState _charges;

    /// <summary>
    /// Charge pool for this skill. Every slot pointing at the same
    /// <see cref="SkillGemDefinition"/> is bound to one shared pool by
    /// <see cref="CharacterSkillManager"/>, so spending a charge in one slot is immediately
    /// visible in the other. The lazy fallback only exists for instances created outside the
    /// manager (tests, tooling).
    /// </summary>
    private SkillChargeState Charges => _charges ??= new SkillChargeState();

    /// <summary>Binds this instance to the shared pool for its definition.</summary>
    public void BindCharges(SkillChargeState shared)
    {
        if (shared != null)
            _charges = shared;
    }

    public bool HasBoundCharges => _charges != null;

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
                effectDuration = 0f,
                healPower = 0f,
                critChance = 0f,
                critMultiplier = 2f,
                maxCharges = 1
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
            effectDuration = def.baseEffectDuration,
            healPower = def.baseHealPower,
            critChance = def.baseCritChance,
            critMultiplier = 2f,
            maxCharges = Mathf.Max(1, def.baseMaxCharges)
        };

        if (user?.StatsHub != null && def.damageCoefficient > 0f)
            stats.damage += Mathf.Max(0f, user.StatsHub.GetSkillBaseDamage()) * def.damageCoefficient;

        upgradeSnapshot?.Apply(stats);

        ApplyCasterStats(user, stats);

        stats.critChance = Mathf.Clamp(stats.critChance, 0f, 100f);
        stats.projectileCount = Mathf.Max(1, stats.projectileCount);
        stats.areaRadius = Mathf.Max(0f, stats.areaRadius);
        stats.manaCost = Mathf.Max(0f, stats.manaCost);
        stats.castTime = Mathf.Max(0f, stats.castTime);
        stats.cooldown = Mathf.Max(0f, stats.cooldown);
        stats.staggerPower = Mathf.Max(0f, stats.staggerPower);
        stats.effectDuration = Mathf.Max(0f, stats.effectDuration);
        stats.healPower = Mathf.Max(0f, stats.healPower);
        stats.critMultiplier = Mathf.Max(1f, stats.critMultiplier);
        stats.maxCharges = Mathf.Max(1, stats.maxCharges);

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

        Charges.Refresh(stats.maxCharges, Time.time);
        if (!Charges.HasCharge)
            return false;

        if (user.currentEnagy < stats.manaCost)
            return false;

        return true;
    }

    public bool CanCast(ISkillUser user) => CanCast(user, out _);

    /// <summary>
    /// Affordability check under a cost policy. <see cref="SkillCastCostPolicy.IgnoreEnergyRespectCharge"/>
    /// is the reason this exists: plain <see cref="CanCast(ISkillUser, out FinalSkillStats)"/> would
    /// refuse a free assist whose caster simply has no energy, and the legacy "ignore everything"
    /// flag would let it fire straight through its own cooldown.
    /// </summary>
    public bool CanCast(ISkillUser user, SkillCastCostPolicy costPolicy, out FinalSkillStats stats)
    {
        stats = null;
        if (def == null || def.payload == null || user == null)
            return false;

        if (costPolicy.IgnoresCharge())
            return true;

        stats = GetFinalStats(user);

        Charges.Refresh(stats.maxCharges, Time.time);
        if (!Charges.HasCharge)
            return false;

        if (!costPolicy.IgnoresEnergy() && user.currentEnagy < stats.manaCost)
            return false;

        return true;
    }

    public bool Cast(ISkillUser user, CharacterAnimBrain animBrain = null, int requestId = 0)
    {
        return Cast(user, animBrain, requestId, out _);
    }

    public bool Cast(
        ISkillUser user,
        CharacterAnimBrain animBrain,
        int requestId,
        out SkillExecutionResult result)
    {
        if (!TryReserveCast(user, ignoreResourceCosts: false, stampCooldown: true, out SkillCastReservation reservation))
        {
            result = SkillExecutionResult.Failed(
                SkillExecutionFailureReason.Rejected,
                "Skill is on cooldown, out of energy, or has no payload.");
            return false;
        }

        return ExecuteAndSettle(reservation, animBrain, requestId, out result);
    }

    public bool TryCastIgnoringResourceCosts(ISkillUser user, CharacterAnimBrain animBrain = null, int requestId = 0, bool stampCooldown = true)
    {
        return TryCastIgnoringResourceCosts(user, animBrain, requestId, stampCooldown, out _);
    }

    public bool TryCastIgnoringResourceCosts(
        ISkillUser user,
        CharacterAnimBrain animBrain,
        int requestId,
        bool stampCooldown,
        out SkillExecutionResult result)
    {
        if (!TryReserveCast(user, ignoreResourceCosts: true, stampCooldown, out SkillCastReservation reservation))
        {
            result = SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingRuntimeContext,
                "Skill instance has no definition, payload, or caster.");
            return false;
        }

        return ExecuteAndSettle(reservation, animBrain, requestId, out result);
    }

    /// <summary>
    /// Takes this cast's charge and energy out of the pools up front and freezes the stats it was
    /// priced with. Everything after this point — animation, wind-up, cast point, payload — runs
    /// against that one snapshot, so a buff landing mid-wind-up cannot change what this cast costs
    /// or what it does, and a second press cannot spend the charge this one is holding.
    ///
    /// The caller owns the returned reservation and must settle it exactly once with
    /// <see cref="SkillCastReservation.Commit"/>, <see cref="SkillCastReservation.CommitChargeOnly"/>,
    /// or <see cref="SkillCastReservation.Release"/>.
    /// </summary>
    public bool TryReserveCast(
        ISkillUser user,
        bool ignoreResourceCosts,
        bool stampCooldown,
        out SkillCastReservation reservation)
    {
        return TryReserveCast(
            user,
            SkillCastCostPolicies.FromLegacyFlag(ignoreResourceCosts),
            stampCooldown,
            out reservation);
    }

    public bool TryReserveCast(
        ISkillUser user,
        SkillCastCostPolicy costPolicy,
        bool stampCooldown,
        out SkillCastReservation reservation)
    {
        reservation = null;

        if (def == null || def.payload == null || user == null)
            return false;

        FinalSkillStats stats = GetFinalStats(user);
        float now = Time.time;
        Charges.Refresh(stats.maxCharges, now);

        int token = SkillCastReservation.NextToken();
        SkillUserSystem energyOwner = user as SkillUserSystem;

        // A free cast still tries for a charge so it can stamp a cooldown, but an empty pool never
        // stops it — that is what "ignores resource costs" means.
        bool chargeReserved = Charges.TryReserve(token, now);
        if (!costPolicy.IgnoresCharge() && !chargeReserved)
            return false;

        float energyAmount = costPolicy.IgnoresEnergy() ? 0f : stats.manaCost;
        bool energyReserved = false;

        if (energyAmount > 0f)
        {
            if (energyOwner != null)
            {
                energyReserved = energyOwner.TryReserveEnergy(token, energyAmount);
                if (!energyReserved)
                {
                    if (chargeReserved)
                        Charges.ReleaseReservation(token);

                    return false;
                }
            }
            else if (user.currentEnagy < energyAmount)
            {
                // ISkillUser implementations that are not a SkillUserSystem cannot hold energy, so
                // they keep the old check-now / spend-at-commit behaviour.
                if (chargeReserved)
                    Charges.ReleaseReservation(token);

                return false;
            }
        }

        reservation = new SkillCastReservation(
            token,
            stats,
            Charges,
            chargeReserved,
            user,
            energyOwner,
            energyAmount,
            energyReserved,
            stampCooldown);
        return true;
    }

    /// <summary>
    /// Runs the payload against a reservation's frozen stats and reports what it produced. Does not
    /// settle the reservation: the caller decides whether this cast commits or rolls back.
    /// </summary>
    public bool ExecuteReserved(
        SkillCastReservation reservation,
        CharacterAnimBrain animBrain,
        int requestId,
        out SkillExecutionResult result,
        SkillTargetHandle primaryTarget = null)
    {
        if (reservation == null || def == null || def.payload == null || reservation.User == null)
        {
            result = SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingAuthoringData,
                "Skill has no execution payload or no reserved caster.");
            return false;
        }

        var castContext = new SkillCastContext(
            reservation.User, def, reservation.Stats, animBrain, requestId, upgradeSnapshot, primaryTarget);

        result = def.payload.ExecuteWithResult(castContext);
        return result.Success;
    }

    bool ExecuteAndSettle(
        SkillCastReservation reservation,
        CharacterAnimBrain animBrain,
        int requestId,
        out SkillExecutionResult result,
        SkillTargetHandle primaryTarget = null)
    {
        if (!ExecuteReserved(reservation, animBrain, requestId, out result, primaryTarget))
        {
            reservation.Release();
            return false;
        }

        reservation.Commit();
        return true;
    }

    /// <summary>
    /// Consumes ONLY the per-instance charge (no energy spend, no payload).
    /// Used when a pre-cast cast is blocked/interrupted before cast point but should still
    /// consume its cooldown. Returns the computed stats so the caller can also consume the
    /// shared (per-definition) charge.
    /// </summary>
    public bool TryStampCooldownOnly(ISkillUser user, out FinalSkillStats stats)
    {
        stats = null;
        if (def == null || user == null)
            return false;

        stats = GetFinalStats(user);
        Charges.Refresh(stats.maxCharges, Time.time);
        Charges.TryConsume(Time.time, stats.cooldown);
        return true;
    }

    /// <summary>
    /// Current charge readout for HUD and tooltips. Keeps the pool in sync with the caster's
    /// live upgrade selection before reporting.
    /// </summary>
    public bool TryGetChargeStatus(ISkillUser user, out SkillChargeStatus status)
    {
        status = default;
        if (def == null || user == null)
            return false;

        FinalSkillStats stats = GetFinalStats(user);
        float now = Time.time;
        Charges.Refresh(stats.maxCharges, now);

        status = new SkillChargeStatus(
            Charges.AvailableCharges,
            Charges.MaxCharges,
            Charges.GetNextChargeRemaining(now),
            Charges.GetNextChargeDuration(now, stats.cooldown));
        return true;
    }
}

/// <summary>Read-only charge snapshot for UI.</summary>
public readonly struct SkillChargeStatus
{
    public readonly int Available;
    public readonly int Max;

    /// <summary>Seconds until the next charge returns. 0 when the pool is full.</summary>
    public readonly float NextChargeRemaining;

    /// <summary>Length of the recharge segment in flight, for fill-amount math. 0 when full.</summary>
    public readonly float NextChargeDuration;

    public bool HasCharge => Available > 0;
    public bool IsRecharging => NextChargeDuration > 0f;

    /// <summary>0 at the start of the current recharge, 1 the moment the charge returns.</summary>
    public float NextChargeProgress01 => NextChargeDuration > 0f
        ? Mathf.Clamp01(1f - NextChargeRemaining / NextChargeDuration)
        : 1f;

    public SkillChargeStatus(int available, int max, float nextChargeRemaining, float nextChargeDuration)
    {
        Available = available;
        Max = max;
        NextChargeRemaining = nextChargeRemaining;
        NextChargeDuration = nextChargeDuration;
    }
}
