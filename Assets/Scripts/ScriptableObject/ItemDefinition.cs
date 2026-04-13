using UnityEngine;

public enum ItemType
{
    Consumable, 
    Weapon,
    Armor,
    Material,
    QuestItem,
    SkillGem,
    SupportGem
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Game/Item")]
public class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    public string itemId;          // key ไม่ซ้ำ (ใช้เซฟ/โหลด)
    public string displayName;     // ชื่อเอาไปโชว์
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
    public GameObject pickupPrefab;

    public virtual GameObject ResolvePickupVisualPrefab()
    {
        return pickupVisualPrefab;
    }

    public virtual Vector3 ResolvePickupVisualScale()
    {
        return pickupVisualScale == Vector3.zero ? Vector3.one : pickupVisualScale;
    }
}

