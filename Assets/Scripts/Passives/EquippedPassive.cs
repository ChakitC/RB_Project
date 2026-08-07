public readonly struct EquippedPassive
{
    public readonly PassiveDefinition Definition;
    public readonly SkillUpgradeStatSnapshot Upgrades;

    public EquippedPassive(PassiveDefinition definition, SkillUpgradeStatSnapshot upgrades = null)
    {
        Definition = definition;
        Upgrades = upgrades;
    }
}
