using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Charge pool for one active skill. A single-charge pool behaves exactly like the plain
/// cooldown it replaces: consume one charge, wait the cooldown, get it back.
///
/// Recharge is sequential, not parallel. Spending two charges at t=0 with a 12s cooldown
/// returns them at t=12 and t=24, never both at t=12.
///
/// Time base is <see cref="Time.time"/>, supplied by the caller. That means charges keep
/// ticking through the custom world-slow used by hitlag and skill cutscenes, and only stop
/// when the game itself is paused.
/// </summary>
public sealed class SkillChargeState
{
    /// <summary>Absolute times at which a spent charge returns, kept sorted ascending.</summary>
    readonly List<float> pendingReadyAt = new();

    /// <summary>
    /// Tokens of charges held for casts that have started but not reached their cast point.
    /// A reserved charge is already gone from <see cref="AvailableCharges"/>, so a second cast
    /// cannot spend it while the first one is still winding up.
    /// </summary>
    readonly List<int> reservedTokens = new();

    int maxCharges = 1;
    int availableCharges = 1;

    public int MaxCharges => maxCharges;
    public int AvailableCharges => availableCharges;
    public bool HasCharge => availableCharges > 0;
    public int RechargingCount => pendingReadyAt.Count;
    public int ReservedCount => reservedTokens.Count;

    public bool IsReserved(int token) => reservedTokens.Contains(token);

    /// <summary>
    /// Applies the current max from <see cref="FinalSkillStats"/> and refills any charge whose
    /// recharge has matured. Safe to call every frame.
    /// </summary>
    public void Refresh(int newMaxCharges, float now)
    {
        SyncMax(newMaxCharges);
        Tick(now);
    }

    /// <summary>
    /// Spends one charge and queues its recharge behind whatever is already recharging.
    /// A non-positive duration means "no cooldown", so the charge is never actually spent.
    /// </summary>
    public bool TryConsume(float now, float rechargeDuration)
    {
        Tick(now);

        if (availableCharges <= 0)
            return false;

        if (!(rechargeDuration > 0f))
            return true;

        availableCharges--;

        float segmentStart = pendingReadyAt.Count > 0
            ? Mathf.Max(now, pendingReadyAt[pendingReadyAt.Count - 1])
            : now;

        pendingReadyAt.Add(segmentStart + rechargeDuration);
        return true;
    }

    /// <summary>
    /// Holds one charge for a cast that has started but not yet reached its cast point. The charge
    /// leaves <see cref="AvailableCharges"/> immediately, so a second press cannot spend the same
    /// charge during the wind-up. Idempotent: reserving with a token that already holds a charge
    /// succeeds without taking a second one.
    /// </summary>
    public bool TryReserve(int token, float now)
    {
        Tick(now);

        if (reservedTokens.Contains(token))
            return true;

        if (availableCharges <= 0)
            return false;

        availableCharges--;
        reservedTokens.Add(token);
        return true;
    }

    /// <summary>
    /// Turns a held charge into a real recharge segment. A non-positive duration means "no
    /// cooldown", so the charge comes straight back exactly like <see cref="TryConsume"/> does.
    /// Idempotent: committing an unknown or already-settled token does nothing.
    /// </summary>
    public bool CommitReservation(int token, float now, float rechargeDuration)
    {
        Tick(now);

        if (!reservedTokens.Remove(token))
            return false;

        if (!(rechargeDuration > 0f))
        {
            availableCharges = Mathf.Min(maxCharges, availableCharges + 1);
            return true;
        }

        float segmentStart = pendingReadyAt.Count > 0
            ? Mathf.Max(now, pendingReadyAt[pendingReadyAt.Count - 1])
            : now;

        pendingReadyAt.Add(segmentStart + rechargeDuration);
        return true;
    }

    /// <summary>
    /// Gives a held charge back without starting any cooldown. Idempotent: releasing an unknown or
    /// already-settled token does nothing.
    /// </summary>
    public bool ReleaseReservation(int token)
    {
        if (!reservedTokens.Remove(token))
            return false;

        availableCharges = Mathf.Min(maxCharges, availableCharges + 1);
        return true;
    }

    /// <summary>Seconds until the next charge returns, or 0 when the pool is already full.</summary>
    public float GetNextChargeRemaining(float now)
    {
        if (pendingReadyAt.Count == 0)
            return 0f;

        return Mathf.Max(0f, pendingReadyAt[0] - now);
    }

    /// <summary>
    /// Total length of the recharge segment currently in flight, so a UI fill can show
    /// progress rather than a bare countdown. 0 when nothing is recharging.
    /// </summary>
    public float GetNextChargeDuration(float now, float rechargeDuration)
    {
        if (pendingReadyAt.Count == 0)
            return 0f;

        return Mathf.Max(GetNextChargeRemaining(now), Mathf.Max(0f, rechargeDuration));
    }

    public void ResetToFull()
    {
        pendingReadyAt.Clear();

        // Outstanding reservations are dropped rather than kept: their commit/release becomes a
        // no-op, so an in-flight cast can never push the refilled pool above its maximum.
        reservedTokens.Clear();
        availableCharges = Mathf.Max(1, maxCharges);
    }

    void Tick(float now)
    {
        int matured = 0;
        while (matured < pendingReadyAt.Count && now >= pendingReadyAt[matured])
            matured++;

        if (matured <= 0)
            return;

        pendingReadyAt.RemoveRange(0, matured);
        availableCharges = Mathf.Min(maxCharges, availableCharges + matured);
    }

    /// <summary>
    /// Unlocking a charge makes it available immediately. Losing one clamps the pool but keeps
    /// the oldest recharge segments running rather than restarting any timer.
    /// </summary>
    void SyncMax(int newMaxCharges)
    {
        int clampedMax = Mathf.Max(1, newMaxCharges);
        if (clampedMax == maxCharges)
        {
            TrimOverflow();
            return;
        }

        if (clampedMax > maxCharges)
        {
            availableCharges += clampedMax - maxCharges;
            maxCharges = clampedMax;
        }
        else
        {
            maxCharges = clampedMax;
        }

        TrimOverflow();
    }

    void TrimOverflow()
    {
        availableCharges = Mathf.Clamp(availableCharges, 0, maxCharges);

        // Drop the newest (latest-maturing) segments first so an already-running recharge is
        // never restarted by a max-charge change.
        int allowedPending = Mathf.Max(0, maxCharges - availableCharges);
        if (pendingReadyAt.Count > allowedPending)
            pendingReadyAt.RemoveRange(allowedPending, pendingReadyAt.Count - allowedPending);
    }
}
