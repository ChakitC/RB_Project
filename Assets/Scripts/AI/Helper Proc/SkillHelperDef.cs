using UnityEngine;

[CreateAssetMenu(fileName = "SkillHelperDef", menuName = "Game/Helper Proc/Skill Helper")]
public class SkillHelperDef : ScriptableObject
{
    [Header("Identity")]
    public string helperId;
    public string displayName;
    [TextArea]
    public string description;

    [Header("Trigger")]
    public PassiveEventType triggerEvent = PassiveEventType.Hit;
    public PassiveOriginFilter originFilter = PassiveOriginFilter.ExternalOnly;
    [Range(0f, 1f)] public float procChance = 0.005f;
    [Min(0f)] public float internalCooldownSeconds = 0f;
    public bool requireTarget = true;
    public bool requireAttackId;
    public bool oncePerAttackId = true;
    public bool requireOwnerAlive = true;

    [Header("Execution")]
    public SkillGemDefinition executionSkill;
    [Min(1)] public int executionSkillLevel = 1;
    public bool hideHelperOnSkillComplete = true;
    public bool blockWhileHelperBusy = true;

    [Header("Debug")]
    public bool debugLogging;

    public string RuntimeId => string.IsNullOrWhiteSpace(helperId) ? name : helperId;
    public int ClampedSkillLevel => Mathf.Max(1, executionSkillLevel);
}
