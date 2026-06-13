using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class CharacterSkillEntry
{
    [Header("Active Gems")]
    public SkillGemDefinition skillAsset;
    [Min(1)] public int skillLevel = 1;

    [Header("Support Gems")]
    public SupportGemDefinition[] supportAssets;

    [HideInInspector]
    public SkillInstance runtimeSkill;

    public int maxSupportSlots = 3;

    public bool IsConfigured => skillAsset != null;
}

[System.Serializable]
public class SkillSlot
{
    public KeyCode hotkey;

    [Header("Active Gems")]
    public SkillGemDefinition skillAsset;
    [Min(1)] public int skillLevel = 1;

    [Header("Support Gems")]
    public SupportGemDefinition[] supportAssets;

    [HideInInspector]
    public SkillInstance runtimeSkill;

    public int maxSupportSlots = 3;
}

[System.Serializable]
public sealed class HelperProcSlot
{
    [FormerlySerializedAs("helperChainSkill")]
    public SkillHelperDef helperProc;

    public bool IsConfigured => helperProc != null;

    public SkillHelperDef ResolveHelperProc()
    {
        return helperProc;
    }
}

[System.Serializable]
public sealed class PassiveSkillSlot
{
    public PassiveDefinition passiveAsset;

    public bool IsConfigured => passiveAsset != null;
}
