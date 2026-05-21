using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ShopItemRowUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text stockText;
    [SerializeField] private TMP_Text reasonText;
    [SerializeField] private Button buyButton;

    ShopPanelUI owner;
    ShopItemIconHoverTarget iconHoverTarget;
    InventorySlotData previewSlotData;
    int entryIndex = -1;

    void Awake()
    {
        if (buyButton != null)
            buyButton.onClick.AddListener(HandleBuyClicked);

        ConfigureIconHoverTarget();
    }

    void OnDestroy()
    {
        if (buyButton != null)
            buyButton.onClick.RemoveListener(HandleBuyClicked);

        HideTooltip();
    }

    public void ConfigureReferences(
        Image icon,
        TMP_Text itemName,
        TMP_Text quantity,
        TMP_Text price,
        TMP_Text stock,
        TMP_Text reason,
        Button buy)
    {
        if (buyButton != null)
            buyButton.onClick.RemoveListener(HandleBuyClicked);

        iconImage = icon;
        nameText = itemName;
        quantityText = quantity;
        priceText = price;
        stockText = stock;
        reasonText = reason;
        buyButton = buy;

        if (buyButton != null)
            buyButton.onClick.AddListener(HandleBuyClicked);

        ConfigureIconHoverTarget();
    }

    public void Bind(
        ShopPanelUI shopPanel,
        int index,
        ShopCatalogEntry entry,
        int remainingStock,
        bool canBuy,
        string reason)
    {
        owner = shopPanel;
        entryIndex = index;
        previewSlotData = entry != null ? entry.CreatePreviewSlotData() : null;

        if (entry == null)
        {
            ApplyEmptyState();
            return;
        }

        if (iconImage != null)
        {
            Sprite icon = entry.item != null ? entry.item.icon : null;
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (nameText != null)
            nameText.text = entry.ResolveDisplayName();

        if (quantityText != null)
            quantityText.text = entry.QuantityPerPurchase > 1 ? $"x{entry.QuantityPerPurchase}" : string.Empty;

        if (priceText != null)
            priceText.text = $"Price: {entry.BuyPrice:N0}";

        if (stockText != null)
            stockText.text = remainingStock < 0 ? "Stock: Unlimited" : $"Stock: {remainingStock:N0}";

        if (reasonText != null)
        {
            reasonText.overflowMode = TextOverflowModes.Ellipsis;
            reasonText.text = canBuy ? entry.ResolveWeaponInlineSummary() : reason ?? string.Empty;
        }

        if (buyButton != null)
            buyButton.interactable = canBuy;
    }

    void ApplyEmptyState()
    {
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (nameText != null)
            nameText.text = string.Empty;

        if (quantityText != null)
            quantityText.text = string.Empty;

        if (priceText != null)
            priceText.text = string.Empty;

        if (stockText != null)
            stockText.text = string.Empty;

        if (reasonText != null)
            reasonText.text = string.Empty;

        previewSlotData = null;
        HideTooltip();

        if (buyButton != null)
            buyButton.interactable = false;
    }

    void ConfigureIconHoverTarget()
    {
        if (iconImage == null)
            return;

        iconImage.raycastTarget = true;

        iconHoverTarget = iconImage.GetComponent<ShopItemIconHoverTarget>();
        if (iconHoverTarget == null)
            iconHoverTarget = iconImage.gameObject.AddComponent<ShopItemIconHoverTarget>();

        iconHoverTarget.Bind(this);
    }

    public void HandleIconPointerEnter(PointerEventData eventData)
    {
        if (previewSlotData == null || previewSlotData.IsEmpty || previewSlotData.item == null)
            return;

        var rootCanvas = ResolveRootCanvas();
        var tooltip = InventorySlotTooltipUI.GetOrCreate(rootCanvas);
        if (tooltip == null)
            return;

        tooltip.ShowFor(
            this,
            previewSlotData,
            ResolveTooltipScreenPosition(eventData),
            rootCanvas,
            ResolveEventCamera(rootCanvas));
    }

    public void HandleIconPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }

    void HideTooltip()
    {
        InventorySlotTooltipUI.HideForOwner(this);
    }

    Vector2 ResolveTooltipScreenPosition(PointerEventData eventData)
    {
        if (eventData != null)
            return eventData.position;

        if (iconImage != null && iconImage.transform is RectTransform rectTransform)
            return RectTransformUtility.WorldToScreenPoint(ResolveEventCamera(ResolveRootCanvas()), rectTransform.position);

        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    Canvas ResolveRootCanvas()
    {
        return GetComponentInParent<Canvas>();
    }

    Camera ResolveEventCamera(Canvas rootCanvas)
    {
        if (rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return rootCanvas.worldCamera;
    }

    void HandleBuyClicked()
    {
        if (owner == null || entryIndex < 0)
            return;

        owner.HandleBuyClicked(entryIndex);
    }
}

public class ShopItemIconHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    ShopItemRowUI owner;

    public void Bind(ShopItemRowUI row)
    {
        owner = row;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.HandleIconPointerEnter(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.HandleIconPointerExit(eventData);
    }
}
