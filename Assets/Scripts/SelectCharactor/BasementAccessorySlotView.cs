using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BasementAccessorySlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private int slotIndex;
    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private Image iconBackground;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text effectText;
    [SerializeField] private Image[] categoryColorImages;
    [SerializeField] private Color offensiveColor = new Color32(235, 126, 17, 255);
    [SerializeField] private Color defensiveColor = new Color32(135, 166, 62, 255);
    [SerializeField] private Color utilityColor = new Color32(79, 181, 216, 255);
    [SerializeField, Range(0f, 1f)] private float iconFrameLighten = 0.55f;
    InventorySlotData data;
    Canvas canvas;
    public int SlotIndex => slotIndex;

    public void Bind(BasementAccessoryScreenController screen, InventorySlotData value)
    {
        data = value;
        canvas = screen.GetComponent<Canvas>();
        button.onClick.RemoveListener(OpenPicker);
        owner = screen;
        button.onClick.AddListener(OpenPicker);
        bool filled = value != null && !value.IsEmpty;
        icon.sprite = filled ? value.item.icon : null;
        icon.enabled = icon.sprite != null;
        Color categoryColor = filled && value.item is AccessoryDefinition accessory
            ? GetCategoryColor(accessory.category) : Color.white;
        if (categoryColorImages != null)
            foreach (var image in categoryColorImages)
                if (image != null) image.color = categoryColor;
        if (iconBackground != null) iconBackground.color = Color.Lerp(categoryColor, Color.white, iconFrameLighten);
        string displayName = filled
            ? AccessoryDisplayNameResolver.ResolveName(value.item as AccessoryDefinition, value.accessoryInstance)
            : "Empty";
        nameText.text = FitName(nameText, displayName);
        effectText.text = filled ? BasementAccessoryScreenController.BuildEffectSummary(value) : "Click to equip";
    }

    BasementAccessoryScreenController owner;
    Color GetCategoryColor(AccessoryCategory category) => category switch
    {
        AccessoryCategory.Offensive => offensiveColor,
        AccessoryCategory.Defensive => defensiveColor,
        AccessoryCategory.Utility => utilityColor,
        _ => Color.white
    };

    static string FitName(TMP_Text label, string value)
    {
        float width = Mathf.Max(1, label.rectTransform.rect.width - 12);
        if (label.GetPreferredValues(value, float.PositiveInfinity, float.PositiveInfinity).x <= width) return value;
        while (value.Length > 0)
        {
            value = value.Substring(0, value.Length - 1).TrimEnd();
            if (label.GetPreferredValues(value + "…", float.PositiveInfinity, float.PositiveInfinity).x <= width)
                return value + "…";
        }
        return "…";
    }
    void OpenPicker() => owner?.SelectSlot(slotIndex);
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner == null || !owner.CanInteract || data == null || data.IsEmpty) return;
        InventorySlotTooltipUI.GetOrCreate(canvas)?.ShowFor(this, data, eventData.position, canvas, null, effectText.font);
    }
    public void OnPointerExit(PointerEventData eventData) => InventorySlotTooltipUI.HideForOwner(this);
    void OnDisable() => InventorySlotTooltipUI.HideForOwner(this);
}
