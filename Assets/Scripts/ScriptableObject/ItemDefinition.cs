using UnityEngine;

public enum ItemType
{
    Consumable, 
    Weapon,
    Armor,
    Material,
    QuestItem,
    SkillGem,
    SupportGem,
    Accessory
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Game/Item")]
public class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    public string itemId;          // key à¹„à¸¡à¹ˆà¸‹à¹‰à¸³ (à¹ƒà¸Šà¹‰à¹€à¸‹à¸Ÿ/à¹‚à¸«à¸¥à¸”)
    public string displayName;     // à¸Šà¸·à¹ˆà¸­à¹€à¸­à¸²à¹„à¸›à¹‚à¸Šà¸§à¹Œ
    [TextArea]
    public string description;

    [Header("Visual")]
    public Sprite icon;
    public GameObject pickupVisualPrefab;
    public Vector3 pickupVisualPositionOffset;
    public Vector3 pickupVisualRotationOffset;
    public Vector3 pickupVisualScale = Vector3.one;

    [Header("Rule")]
    public ItemType itemType;
    public bool stackable = true;
    public int maxStack = 99;

    [Header("Economy")]
    [Min(0)] public int baseBuyPrice;
    [Min(0)] public int baseSellPrice;

    public virtual GameObject ResolvePickupVisualPrefab()
    {
        return pickupVisualPrefab;
    }

    public virtual Vector3 ResolvePickupVisualScale()
    {
        return pickupVisualScale == Vector3.zero ? Vector3.one : pickupVisualScale;
    }
}

