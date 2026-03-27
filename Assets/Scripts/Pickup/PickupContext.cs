using UnityEngine;

public readonly struct PickupContext
{
    public readonly GameObject PickupObject;
    public readonly GameObject SourceObject;
    public readonly Transform SourceRoot;
    public readonly ISkillUser SourceUser;
    public readonly SkillGemDefinition SourceSkillDef;
    public readonly FinalSkillStats SourceSkillStats;

    public PickupContext(
        GameObject pickupObject,
        GameObject sourceObject,
        Transform sourceRoot,
        ISkillUser sourceUser,
        SkillGemDefinition sourceSkillDef,
        FinalSkillStats sourceSkillStats)
    {
        PickupObject = pickupObject;
        SourceObject = sourceObject;
        SourceRoot = sourceRoot;
        SourceUser = sourceUser;
        SourceSkillDef = sourceSkillDef;
        SourceSkillStats = sourceSkillStats;
    }
}
