using System.Collections;
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
    [SerializeField] private Image equippedCharacterIconImage;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private GameObject hoverHighlight;
    [SerializeField, Min(0f)] private float hoverDetailDelay = 3f;
    [SerializeField, Min(0f)] private float equippedCharacterIconSize = 28f;
    [SerializeField] private Vector2 equippedCharacterIconOffset = new(-4f, 4f);

    CanvasGroup canvasGroup;
    IInventorySlotUIOwner owner;
    InventorySlotData slotData;
    Sprite equippedCharacterIcon;
    int slotIndex = -1;
    bool isDraggingVisual;
    bool isPointerInside;
    bool hasPointerPosition;
    Vector2 lastPointerScreenPosition;
    Coroutine hoverDetailRoutine;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    void OnDisable()
    {
        isPointerInside = false;
        hasPointerPosition = false;
        StopHoverDetailCountdown();
        HideHoverDetails();
    }

    public void Bind(IInventorySlotUIOwner inventoryUI, int index, InventorySlotData data)
    {
        owner = inventoryUI;
        slotIndex = index;
        slotData = data;
        equippedCharacterIcon = null;
        RefreshVisuals();
    }

    public void SetEquippedCharacterIcon(Sprite icon)
    {
        equippedCharacterIcon = icon;

        if (equippedCharacterIcon != null)
            EnsureEquippedCharacterIconImage();

        ApplyEquippedCharacterIcon();
    }

    public void SetDraggingVisual(bool isDragging)
    {
        isDraggingVisual = isDragging;

        if (isDragging)
        {
            StopHoverDetailCountdown();
            HideHoverDetails();
        }
        else if (isPointerInside)
        {
            BeginHoverDetailCountdown();
        }

        ApplyVisualState();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        StopHoverDetailCountdown();
        HideHoverDetails();
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

        isPointerInside = true;
        CachePointerPosition(eventData);
        BeginHoverDetailCountdown();
        owner?.HandlePointerEnter(slotIndex);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverHighlight != null)
            hoverHighlight.SetActive(false);

        isPointerInside = false;
        hasPointerPosition = false;
        StopHoverDetailCountdown();
        HideHoverDetails();
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
            bool showAmount = hasItem && !slotData.HasUniqueInstance && slotData.quantity > 1;
            amountText.text = showAmount ? slotData.quantity.ToString() : string.Empty;
            amountText.gameObject.SetActive(showAmount);
        }

        if (hoverHighlight != null)
            hoverHighlight.SetActive(isPointerInside && hasItem);

        if (!hasItem)
        {
            equippedCharacterIcon = null;
            StopHoverDetailCountdown();
            HideHoverDetails();
        }
        else if (InventorySlotTooltipUI.IsShowingForSlot(this))
        {
            InventorySlotTooltipUI.RefreshForSlot(
                this,
                slotData,
                ResolveTooltipScreenPosition(),
                ResolveRootCanvas(),
                ResolveEventCamera());
        }
        else if (isPointerInside && !isDraggingVisual)
        {
            BeginHoverDetailCountdown();
        }

        ApplyVisualState();
        ApplyEquippedCharacterIcon();
    }

    void ApplyVisualState()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = isDraggingVisual ? 0.35f : 1f;
    }

    void EnsureEquippedCharacterIconImage()
    {
        if (equippedCharacterIconImage != null)
            return;

        var iconObject = new GameObject("EquippedCharacterIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.transform.SetParent(transform, false);

        var rectTransform = iconObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(1f, 0f);
        rectTransform.anchorMax = new Vector2(1f, 0f);
        rectTransform.pivot = new Vector2(1f, 0f);
        rectTransform.sizeDelta = new Vector2(equippedCharacterIconSize, equippedCharacterIconSize);
        rectTransform.anchoredPosition = equippedCharacterIconOffset;

        equippedCharacterIconImage = iconObject.GetComponent<Image>();
        equippedCharacterIconImage.preserveAspect = true;
        equippedCharacterIconImage.raycastTarget = false;
        equippedCharacterIconImage.enabled = false;
    }

    void ApplyEquippedCharacterIcon()
    {
        if (equippedCharacterIconImage == null)
            return;

        equippedCharacterIconImage.sprite = equippedCharacterIcon;
        equippedCharacterIconImage.enabled = equippedCharacterIcon != null;
        equippedCharacterIconImage.gameObject.SetActive(equippedCharacterIcon != null);
        equippedCharacterIconImage.transform.SetAsLastSibling();
    }

    void BeginHoverDetailCountdown()
    {
        StopHoverDetailCountdown();

        if (!CanShowHoverDetails())
            return;

        hoverDetailRoutine = StartCoroutine(ShowHoverDetailsAfterDelay());
    }

    void StopHoverDetailCountdown()
    {
        if (hoverDetailRoutine == null)
            return;

        StopCoroutine(hoverDetailRoutine);
        hoverDetailRoutine = null;
    }

    IEnumerator ShowHoverDetailsAfterDelay()
    {
        if (hoverDetailDelay > 0f)
            yield return new WaitForSeconds(hoverDetailDelay);

        hoverDetailRoutine = null;

        if (!CanShowHoverDetails())
            yield break;

        ShowHoverDetails();
    }

    void ShowHoverDetails()
    {
        var rootCanvas = ResolveRootCanvas();
        var tooltip = InventorySlotTooltipUI.GetOrCreate(rootCanvas);
        if (tooltip == null)
            return;

        tooltip.ShowFor(
            this,
            slotData,
            ResolveTooltipScreenPosition(),
            rootCanvas,
            ResolveEventCamera());
    }

    void HideHoverDetails()
    {
        InventorySlotTooltipUI.HideForSlot(this);
    }

    bool CanShowHoverDetails()
    {
        return isPointerInside &&
               !isDraggingVisual &&
               slotData != null &&
               !slotData.IsEmpty &&
               slotData.item != null;
    }

    void CachePointerPosition(PointerEventData eventData)
    {
        if (eventData == null)
        {
            hasPointerPosition = false;
            return;
        }

        lastPointerScreenPosition = eventData.position;
        hasPointerPosition = true;
    }

    Vector2 ResolveTooltipScreenPosition()
    {
        if (hasPointerPosition)
            return lastPointerScreenPosition;

        if (transform is RectTransform rectTransform)
            return RectTransformUtility.WorldToScreenPoint(ResolveEventCamera(), rectTransform.position);

        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    Canvas ResolveRootCanvas()
    {
        return GetComponentInParent<Canvas>();
    }

    Camera ResolveEventCamera()
    {
        var canvas = ResolveRootCanvas();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }
}
