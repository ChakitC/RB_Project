public enum SkillValidationSeverity
{
    Warning,
    Error,
}

/// <summary>A definition diagnostic shared by runtime checks and authoring tools.</summary>
public readonly struct SkillValidationIssue
{
    public readonly string Code;
    public readonly SkillValidationSeverity Severity;
    public readonly SkillGemDefinition Skill;
    public readonly string Location;
    public readonly string StepId;
    public readonly SkillPayloadDef Payload;
    public readonly string RequiredUpgradeId;
    public readonly string Message;

    public SkillValidationIssue(string code, SkillValidationSeverity severity,
        SkillGemDefinition skill, string location, string stepId, string message,
        SkillPayloadDef payload = null, string requiredUpgradeId = null)
    {
        Code = code;
        Severity = severity;
        Skill = skill;
        Location = location;
        StepId = stepId;
        Payload = payload;
        RequiredUpgradeId = requiredUpgradeId;
        Message = message;
    }

    public override string ToString() => string.IsNullOrEmpty(Location)
        ? Message : $"{Location}: {Message}";
}
