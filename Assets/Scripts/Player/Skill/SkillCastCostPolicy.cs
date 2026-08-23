/// <summary>
/// What a cast is allowed to skip paying for.
///
/// The older <c>ignoreResourceCosts</c> boolean only had two settings, and "free" meant free of
/// everything: no energy AND an empty charge pool could not block the cast. That is correct for
/// guaranteed interruptions, which must never be refused, but wrong for a skill that costs no
/// energy yet still has to respect its own cooldown.
/// </summary>
public enum SkillCastCostPolicy
{
    /// <summary>Pay energy, and require an available charge.</summary>
    Normal = 0,

    /// <summary>
    /// Pay nothing and cast even with an empty pool. Legacy <c>ignoreResourceCosts: true</c>.
    /// A charge is still reserved when one happens to be free, so the cast can stamp a cooldown
    /// if <c>StampCooldown</c> asks it to.
    /// </summary>
    IgnoreEnergyAndCharge = 1,

    /// <summary>
    /// Skip the energy cost, but still require an available charge. Used by assists that are
    /// free to the party's resources yet must stay on their own cooldown.
    /// </summary>
    IgnoreEnergyRespectCharge = 2,
}

public static class SkillCastCostPolicies
{
    /// <summary>Legacy boolean mapping. Must stay exact: existing callers depend on it.</summary>
    public static SkillCastCostPolicy FromLegacyFlag(bool ignoreResourceCosts)
    {
        return ignoreResourceCosts
            ? SkillCastCostPolicy.IgnoreEnergyAndCharge
            : SkillCastCostPolicy.Normal;
    }

    /// <summary>True when an empty charge pool must not refuse the cast.</summary>
    public static bool IgnoresCharge(this SkillCastCostPolicy policy)
    {
        return policy == SkillCastCostPolicy.IgnoreEnergyAndCharge;
    }

    /// <summary>True when the caster pays no energy.</summary>
    public static bool IgnoresEnergy(this SkillCastCostPolicy policy)
    {
        return policy != SkillCastCostPolicy.Normal;
    }
}
