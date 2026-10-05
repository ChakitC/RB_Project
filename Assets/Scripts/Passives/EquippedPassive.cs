public readonly struct EquippedPassive
{
    public readonly PassiveDefinition Definition;
    public readonly SkillUpgradeStatSnapshot Upgrades;
    public readonly string GrantId;

    public EquippedPassive(PassiveDefinition definition, SkillUpgradeStatSnapshot upgrades = null, string grantId = null)
    {
        Definition = definition;
        Upgrades = upgrades;
        GrantId = grantId;
    }
}
