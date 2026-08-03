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
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image equippedCharacterIconImage;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private GameObject hoverHighlight;
    [SerializeField] private Outline borderOutline;
    [SerializeField, Min(0f)] private float hoverDetailDelay = 3f;
    [SerializeField, Min(0f)] private float equippedCharacterIconSize = 28f;
    [SerializeField] private Vector2 equippedCharacterIconOffset = new(-4f, 4f);
    [SerializeField] private Color emptyBackgroundColor = new(0.055f, 0.065f, 0.075f, 0.72f);
    [SerializeField] private Color filledBackgroundColor = new(0.09f, 0.105f, 0.12f, 1f);
    [SerializeField] private Color emptyBorderColor = new(0.2f, 0.22f, 0.24f, 0.7f);
    [SerializeField] private Color commonBorderColor = new(0.45f, 0.48f, 0.52f, 1f);
    [SerializeField] private Color rareBorderColor = new(0.2f, 0.55f, 0.95f, 1f);
    [SerializeField] private Color epicBorderColor = new(0.7f, 0.35f, 0.95f, 1f);
    [SerializeField] private Color hoverBorderColor = new(1f, 0.68f, 0.2f, 1f);

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

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (borderOutline == null)
            borderOutline = GetComponent<Outline>();

        if (borderOutline == null)
        {
            borderOutline = gameObject.AddComponent<Outline>();
            borderOutline.effectDistance = new Vector2(2f, -2f);
            borderOutline.useGraphicAlpha = true;
        }

        if (iconImage != null)
        {
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
        }

        if (amountText != null)
            amountText.raycastTarget = false;
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
            hoverHighlight.SetActive(slotData != null && !slotData.IsEmpty);

        isPointerInside = true;
        CachePointerPosition(eventData);
        BeginHoverDetailCountdown();
        ApplyVisualState();
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
        ApplyVisualState();
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
        bool hasItem = slotData != null && !slotData.IsEmpty;

        if (canvasGroup != null)
            canvasGroup.alpha = isDraggingVisual ? 0.35f : 1f;

        if (backgroundImage != null)
            backgroundImage.color = hasItem ? filledBackgroundColor : emptyBackgroundColor;

        if (borderOutline != null)
            borderOutline.effectColor = ResolveBorderColor(hasItem);
    }

    Color ResolveBorderColor(bool hasItem)
    {
        if (!hasItem)
            return emptyBorderColor;

        if (isPointerInside && !isDraggingVisual)
            return hoverBorderColor;

        if (slotData.HasWeaponInstance && slotData.weaponInstance != null)
        {
            return slotData.weaponInstance.rarity switch
            {
                WeaponRarity.Rare => rareBorderColor,
                WeaponRarity.Epic => epicBorderColor,
                _ => commonBorderColor
            };
        }

        return commonBorderColor;
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
