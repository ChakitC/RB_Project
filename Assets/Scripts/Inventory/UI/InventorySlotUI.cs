using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class InventorySlotUI : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IDropHandler,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private GameObject hoverHighlight;

    CanvasGroup canvasGroup;
    IInventorySlotUIOwner owner;
    InventorySlotData slotData;
    int slotIndex = -1;
    bool isDraggingVisual;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Bind(IInventorySlotUIOwner inventoryUI, int index, InventorySlotData data)
    {
        owner = inventoryUI;
        slotIndex = index;
        slotData = data;
        RefreshVisuals();
    }

    public void SetDraggingVisual(bool isDragging)
    {
        isDraggingVisual = isDragging;
        ApplyVisualState();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        owner?.BeginDrag(slotIndex, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        owner?.UpdateDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        owner?.EndDrag(eventData);
    }

    public void OnDrop(PointerEventData eventData)
    {
        owner?.HandleDrop(slotIndex);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        owner?.HandleSlotClick(slotIndex, eventData);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverHighlight != null)
            hoverHighlight.SetActive(true);

        owner?.HandlePointerEnter(slotIndex);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverHighlight != null)
            hoverHighlight.SetActive(false);

        owner?.HandlePointerExit(slotIndex);
    }

    void RefreshVisuals()
    {
        bool hasItem = slotData != null && !slotData.IsEmpty;
        var icon = hasItem && slotData.item != null ? slotData.item.icon : null;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (amountText != null)
        {
            bool showAmount = hasItem && !slotData.HasWeaponInstance && slotData.quantity > 1;
            amountText.text = showAmount ? slotData.quantity.ToString() : string.Empty;
            amountText.gameObject.SetActive(showAmount);
        }

        if (hoverHighlight != null)
            hoverHighlight.SetActive(false);

        ApplyVisualState();
    }

    void ApplyVisualState()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = isDraggingVisual ? 0.35f : 1f;
    }
}
