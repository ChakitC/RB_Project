using TMPro;
using UnityEngine;
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
    int entryIndex = -1;

    void Awake()
    {
        if (buyButton != null)
            buyButton.onClick.AddListener(HandleBuyClicked);
    }

    void OnDestroy()
    {
        if (buyButton != null)
            buyButton.onClick.RemoveListener(HandleBuyClicked);
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
            priceText.text = entry.BuyPrice.ToString("N0");

        if (stockText != null)
            stockText.text = remainingStock < 0 ? "Stock: Unlimited" : $"Stock: {remainingStock:N0}";

        if (reasonText != null)
            reasonText.text = canBuy ? string.Empty : reason ?? string.Empty;

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

        if (buyButton != null)
            buyButton.interactable = false;
    }

    void HandleBuyClicked()
    {
        if (owner == null || entryIndex < 0)
            return;

        owner.HandleBuyClicked(entryIndex);
    }
}
