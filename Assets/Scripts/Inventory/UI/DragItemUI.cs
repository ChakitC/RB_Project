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
    bool initialized;
    bool isShowing;

    void Awake()
    {
        Initialize();

        if (!isShowing)
            Hide();
    }

    void Initialize()
    {
        if (initialized)
            return;

        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        initialized = true;
    }

    public void SetCanvas(Canvas canvas)
    {
        rootCanvas = canvas;
    }

    public void Show(InventorySlotData slotData, Vector2 screenPosition, Camera eventCamera = null)
    {
        Initialize();
        isShowing = true;
        gameObject.SetActive(true);
        ApplySlot(slotData);

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        Move(screenPosition, eventCamera);
    }

    public void Move(Vector2 screenPosition, Camera eventCamera = null)
    {
        Initialize();

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
        Initialize();
        isShowing = false;

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
            bool showAmount = hasItem && !slotData.HasUniqueInstance && slotData.quantity > 1;
            amountText.text = showAmount ? slotData.quantity.ToString() : string.Empty;
            amountText.gameObject.SetActive(showAmount);
        }
    }
}
