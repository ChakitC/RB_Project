using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class DragItemUI : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private Vector2 pointerOffset = new Vector2(20f, -20f);

    CanvasGroup canvasGroup;
    Canvas rootCanvas;

    void Awake()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        Hide();
    }

    public void SetCanvas(Canvas canvas)
    {
        rootCanvas = canvas;
    }

    public void Show(InventorySlotData slotData, Vector2 screenPosition, Camera eventCamera = null)
    {
        ApplySlot(slotData);
        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        Move(screenPosition, eventCamera);
    }

    public void Move(Vector2 screenPosition, Camera eventCamera = null)
    {
        if (rectTransform == null)
            return;

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        if (rootCanvas == null)
        {
            rectTransform.position = screenPosition;
            return;
        }

        var canvasRect = rootCanvas.transform as RectTransform;
        var cameraToUse = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : eventCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, cameraToUse, out var localPosition))
        {
            rectTransform.anchoredPosition = localPosition + pointerOffset;
            return;
        }

        rectTransform.position = screenPosition;
    }

    public void Hide()
    {
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (amountText != null)
        {
            amountText.text = string.Empty;
            amountText.gameObject.SetActive(false);
        }

        gameObject.SetActive(false);
    }

    void ApplySlot(InventorySlotData slotData)
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
    }
}
