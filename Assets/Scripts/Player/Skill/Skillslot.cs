using UnityEngine;

[System.Serializable]
public class SkillSlot
{
    public KeyCode hotkey;   
    // public SkillInstance skill; 
    
    [Header("Active Gems")]
    public SkillGemDefinition skillAsset;
    
    [Header("Support Gems")]
    public SupportGemDefinition[] supportAssets; 
    
    
    [HideInInspector]    
    public SkillInstance runtimeSkill;
    
    
    public int maxSupportSlots = 3;    // สกิลที่ถูกใส่ในช่องนี้ (อาจเป็น null)
}