using UnityEngine;

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
