using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class CharacterSkillEntry
{
    [Header("Active Gems")]
    public SkillGemDefinition skillAsset;

    [HideInInspector]
    public SkillInstance runtimeSkill;

    public bool IsConfigured => skillAsset != null;
}

[System.Serializable]
public class SkillSlot
{
    public KeyCode hotkey;

    [Header("Active Gems")]
    public SkillGemDefinition skillAsset;

    [HideInInspector]
    public SkillInstance runtimeSkill;
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
