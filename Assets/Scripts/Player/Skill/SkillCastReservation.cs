using UnityEngine;

/// <summary>
/// One cast's hold on the resources it will need: the charge it took out of the pool, the energy
/// it took out of the caster, and the <see cref="FinalSkillStats"/> snapshot it was priced with.
///
/// A cast is a transaction that spans the whole wind-up, not a single instant. Reserving at the
/// start is what stops a second press from spending the same charge while the first animation is
/// still playing, and freezing the stats snapshot is what stops a buff that lands mid-wind-up from
/// changing the cost, damage, radius, or cooldown of a cast that was already priced.
///
/// Every settle operation is idempotent, so a cast that is cancelled and then torn down again
/// never refunds twice.
/// </summary>
public sealed class SkillCastReservation
{
    static int nextToken = 1;

    /// <summary>Identity of this hold inside the charge pool and the energy pool.</summary>
    public int Token { get; }

    /// <summary>Stats this cast was priced with. Frozen for the whole cast.</summary>
    public FinalSkillStats Stats { get; }

    /// <summary>False for interruption-style casts that must not burn a cooldown.</summary>
    public bool StampCooldown { get; }

    public bool IsSettled { get; private set; }

    /// <summary>Caster this hold belongs to. The payload runs against this same user.</summary>
    public ISkillUser User => user;

    readonly SkillChargeState charges;
    readonly bool chargeReserved;

    readonly ISkillUser user;
    readonly SkillUserSystem energyOwner;
    readonly float energyAmount;
    readonly bool energyReserved;

    /// <summary>Pre-allocated token, so the caller can reserve before the object exists.</summary>
    internal static int NextToken()
    {
        if (nextToken == int.MaxValue)
            nextToken = 1;

        return nextToken++;
    }

    /// <summary>
    /// Built by <see cref="SkillInstance"/>, which must reserve against a token before it can
    /// decide whether the reservation is viable at all.
    /// </summary>
    internal SkillCastReservation(
        int token,
        FinalSkillStats stats,
        SkillChargeState charges,
        bool chargeReserved,
        ISkillUser user,
        SkillUserSystem energyOwner,
        float energyAmount,
        bool energyReserved,
        bool stampCooldown)
    {
        Token = token;
        Stats = stats;
        StampCooldown = stampCooldown;

        this.charges = charges;
        this.chargeReserved = chargeReserved;
        this.user = user;
        this.energyOwner = energyOwner;
        this.energyAmount = energyAmount;
        this.energyReserved = energyReserved;
    }

    /// <summary>Spends everything this cast held: energy for real, charge onto its cooldown.</summary>
    public void Commit()
    {
        if (IsSettled)
            return;

        IsSettled = true;
        CommitCharge();
        CommitEnergy();
    }

    /// <summary>
    /// Burns the cooldown but refunds the energy. This is the "blocked pre-cast" rule: the skill
    /// goes on cooldown even though nothing came out of it, but the caster keeps their energy.
    /// </summary>
    public void CommitChargeOnly()
    {
        if (IsSettled)
            return;

        IsSettled = true;
        CommitCharge();
        ReleaseEnergy();
    }

    /// <summary>Refunds everything. The cast cost nothing.</summary>
    public void Release()
    {
        if (IsSettled)
            return;

        IsSettled = true;
        ReleaseCharge();
        ReleaseEnergy();
    }

    void CommitCharge()
    {
        if (!chargeReserved || charges == null)
            return;

        if (!StampCooldown)
        {
            charges.ReleaseReservation(Token);
            return;
        }

        float cooldown = Stats != null ? Stats.cooldown : 0f;
        charges.CommitReservation(Token, Time.time, cooldown);
    }

    void ReleaseCharge()
    {
        if (chargeReserved && charges != null)
            charges.ReleaseReservation(Token);
    }

    void CommitEnergy()
    {
        if (energyReserved && energyOwner != null)
        {
            energyOwner.CommitEnergyReservation(Token);
            return;
        }

        // Callers that are not a SkillUserSystem cannot hold energy, so the spend happens here at
        // commit time instead. They keep the old behaviour exactly.
        if (energyAmount > 0f)
            user?.SpendEnagy(energyAmount);
    }

    void ReleaseEnergy()
    {
        if (energyReserved && energyOwner != null)
            energyOwner.ReleaseEnergyReservation(Token);
    }
}
