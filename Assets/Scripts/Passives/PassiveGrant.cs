public readonly struct PassiveGrant
{
    public readonly PassiveDefinition Definition;
    public readonly string SourceId;
    public readonly string GrantId;
    public readonly SkillUpgradeStatSnapshot Upgrades;
    public readonly PassiveGrantOptions Options;

    public PassiveGrant(PassiveDefinition definition, string sourceId, string grantId, SkillUpgradeStatSnapshot upgrades = null, PassiveGrantOptions options = null)
    {
        Definition = definition;
        SourceId = sourceId;
        GrantId = grantId;
        Upgrades = upgrades;
        Options = options;
    }

    public string Key => SourceId + "/" + GrantId + "/" + Definition.DefinitionId;
}
