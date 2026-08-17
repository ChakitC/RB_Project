/// <summary>
/// Team check for barriers.
///
/// The rule is stated positively — "are these two known to be on opposing sides?" — rather than
/// as "not friendly". A negated friendliness test treats every unknown or neutral actor as
/// hostile, which would make a barrier swallow projectiles it has no business touching.
/// </summary>
public static class BarrierFactionUtility
{
    /// <summary>
    /// True only when both sides resolve to a known team and those teams oppose each other.
    /// <see cref="AITargetIdentity.Auto"/>, <see cref="AITargetIdentity.Generic"/>,
    /// <see cref="AITargetIdentity.Neutral"/>, and any missing context all return false.
    /// </summary>
    public static bool AreHostile(CharacteContext a, CharacteContext b)
    {
        if (a == null || b == null)
            return false;

        if (!TryGetSide(a.TargetIdentity, out bool aIsPlayerSide) ||
            !TryGetSide(b.TargetIdentity, out bool bIsPlayerSide))
        {
            return false;
        }

        return aIsPlayerSide != bIsPlayerSide;
    }

    /// <summary>
    /// True only when both sides resolve to a known team and those teams match. Unknown or
    /// neutral identities return false, so callers must not treat this as the inverse of
    /// <see cref="AreHostile"/>.
    /// </summary>
    public static bool AreFriendly(CharacteContext a, CharacteContext b)
    {
        if (a == null || b == null)
            return false;

        if (!TryGetSide(a.TargetIdentity, out bool aIsPlayerSide) ||
            !TryGetSide(b.TargetIdentity, out bool bIsPlayerSide))
        {
            return false;
        }

        return aIsPlayerSide == bIsPlayerSide;
    }

    static bool TryGetSide(AITargetIdentity identity, out bool isPlayerSide)
    {
        switch (identity)
        {
            case AITargetIdentity.Player:
            case AITargetIdentity.Companion:
                isPlayerSide = true;
                return true;

            case AITargetIdentity.Enemy:
                isPlayerSide = false;
                return true;

            default:
                // Auto, Generic, Neutral: no side, so no barrier interaction.
                isPlayerSide = false;
                return false;
        }
    }
}
