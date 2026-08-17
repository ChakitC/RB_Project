/// <summary>
/// Summon-side numbers shown in the Active Skill node detail panel. Separate from
/// <see cref="FinalSkillStats"/> because the summon payload — not the skill stat block — owns
/// these formulas.
/// </summary>
public readonly struct SkillSummonPreview
{
    /// <summary>False when the skill summons nothing, so the panel skips the whole section.</summary>
    public readonly bool HasSummon;

    /// <summary>
    /// False when max HP cannot be resolved (lobby, where the owner's live max HP is unknown).
    /// The cap is still meaningful in that case.
    /// </summary>
    public readonly bool HasMaxHealth;

    public readonly float MaxHealth;
    public readonly int Cap;

    public SkillSummonPreview(bool hasSummon, bool hasMaxHealth, float maxHealth, int cap)
    {
        HasSummon = hasSummon;
        HasMaxHealth = hasMaxHealth;
        MaxHealth = maxHealth;
        Cap = cap;
    }
}
