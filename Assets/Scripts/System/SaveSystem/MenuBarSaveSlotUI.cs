using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MenuBarSaveSlotUI : MonoBehaviour
{
    enum PendingAction
    {
        None,
        SwitchSlot,
        ResetSlot
    }

    static readonly Color PanelColor = new(0.035f, 0.045f, 0.065f, 0.94f);
    static readonly Color DialogColor = new(0.055f, 0.065f, 0.085f, 1f);
    static readonly Color ButtonColor = new(0.18f, 0.2f, 0.24f, 1f);
    static readonly Color CurrentSlotColor = new(0.12f, 0.5f, 0.72f, 1f);
    static readonly Color ResetColor = new(0.65f, 0.14f, 0.14f, 1f);

    readonly Button[] _slotButtons = new Button[SaveManager.SaveSlotCount];
    readonly Text[] _slotLabels = new Text[SaveManager.SaveSlotCount];
    readonly Image[] _slotImages = new Image[SaveManager.SaveSlotCount];

    Button _resetButton;
    GameObject _confirmationPanel;
    Text _confirmationText;
    Button _confirmButton;
    Button _cancelButton;
    PendingAction _pendingAction;
    int _pendingSlot = -1;
    Font _font;

    void Awake()
    {
        BuildUi();
        BindButtons();
        HideConfirmation();
    }

    void OnEnable()
    {
        Refresh();
    }

    void OnDisable()
    {
        HideConfirmation();
    }

    void OnDestroy()
    {
        for (int i = 0; i < _slotButtons.Length; i++)
            _slotButtons[i]?.onClick.RemoveAllListeners();

        _resetButton?.onClick.RemoveAllListeners();
        _confirmButton?.onClick.RemoveAllListeners();
        _cancelButton?.onClick.RemoveAllListeners();
    }

    void BuildUi()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        RectTransform root = (RectTransform)transform;

        RectTransform panel = CreateRect(
            "Save Slot Panel",
            root,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(24f, -24f),
            new Vector2(360f, 330f));
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = PanelColor;

        CreateText(
            "Title",
            panel,
            "SAVE DATA",
            new Vector2(18f, -16f),
            new Vector2(324f, 30f),
            20,
            TextAnchor.MiddleLeft);

        for (int i = 0; i < SaveManager.SaveSlotCount; i++)
        {
            Button button = CreateButton(
                $"Save {i + 1}",
                panel,
                new Vector2(18f, -58f - (i * 54f)),
                new Vector2(324f, 44f),
                ButtonColor,
                out Text label,
                out Image image);

            _slotButtons[i] = button;
            _slotLabels[i] = label;
            _slotImages[i] = image;
        }

        _resetButton = CreateButton(
            "Reset Current Save",
            panel,
            new Vector2(18f, -230f),
            new Vector2(324f, 44f),
            ResetColor,
            out _,
            out _);

        CreateText(
            "Hint",
            panel,
            "Switching or resetting reloads Basement.",
            new Vector2(18f, -286f),
            new Vector2(324f, 24f),
            12,
            TextAnchor.MiddleCenter);

        BuildConfirmation(root);
    }

    void BuildConfirmation(RectTransform root)
    {
        RectTransform overlay = CreateRect(
            "Save Slot Confirmation",
            root,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;

        Image overlayImage = overlay.gameObject.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.72f);
        _confirmationPanel = overlay.gameObject;

        RectTransform dialog = CreateRect(
            "Dialog",
            overlay,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(480f, 220f));
        Image dialogImage = dialog.gameObject.AddComponent<Image>();
        dialogImage.color = DialogColor;

        _confirmationText = CreateText(
            "Message",
            dialog,
            string.Empty,
            new Vector2(24f, -24f),
            new Vector2(432f, 96f),
            18,
            TextAnchor.MiddleCenter);

        _confirmButton = CreateButton(
            "Confirm",
            dialog,
            new Vector2(24f, -148f),
            new Vector2(200f, 48f),
            ResetColor,
            out _,
            out _);
        _cancelButton = CreateButton(
            "Cancel",
            dialog,
            new Vector2(256f, -148f),
            new Vector2(200f, 48f),
            ButtonColor,
            out _,
            out _);
    }

    void BindButtons()
    {
        for (int i = 0; i < _slotButtons.Length; i++)
        {
            int slot = i;
            _slotButtons[i].onClick.AddListener(() => RequestSlotSwitch(slot));
        }

        _resetButton.onClick.AddListener(RequestReset);
        _confirmButton.onClick.AddListener(ConfirmPendingAction);
        _cancelButton.onClick.AddListener(HideConfirmation);
    }

    void Refresh()
    {
        SaveManager manager = SaveManager.Instance;
        bool hasManager = manager != null;
        int currentSlot = hasManager ? manager.currentSlot : 0;

        for (int i = 0; i < _slotButtons.Length; i++)
        {
            bool isCurrent = hasManager && i == currentSlot;
            bool isEmpty = !hasManager || !manager.HasSaveData(i);

            _slotButtons[i].interactable = hasManager;
            _slotImages[i].color = isCurrent ? CurrentSlotColor : ButtonColor;
            _slotLabels[i].text = isCurrent
                ? $"Save {i + 1} - Current"
                : isEmpty ? $"Save {i + 1} - Empty" : $"Save {i + 1}";
        }

        _resetButton.interactable = hasManager && manager.HasSaveData(currentSlot);
    }

    void RequestSlotSwitch(int slot)
    {
        SaveManager manager = SaveManager.Instance;
        if (manager == null || slot == manager.currentSlot)
            return;

        _pendingAction = PendingAction.SwitchSlot;
        _pendingSlot = slot;
        ShowConfirmation($"Switch to Save {slot + 1}?\nUnsaved progress will be lost.");
    }

    void RequestReset()
    {
        SaveManager manager = SaveManager.Instance;
        if (manager == null || !manager.HasSaveData(manager.currentSlot))
            return;

        _pendingAction = PendingAction.ResetSlot;
        _pendingSlot = manager.currentSlot;
        ShowConfirmation($"Reset Save {_pendingSlot + 1}?\nThis cannot be undone.");
    }

    void ConfirmPendingAction()
    {
        SaveManager manager = SaveManager.Instance;
        PendingAction action = _pendingAction;
        int slot = _pendingSlot;
        HideConfirmation();

        if (manager == null)
            return;

        bool completed = action switch
        {
            PendingAction.SwitchSlot => manager.SwitchSlotAndLoadBasement(slot),
            PendingAction.ResetSlot => manager.ResetCurrentSlotAndLoadBasement(),
            _ => false
        };

        if (!completed)
            Refresh();
    }

    void ShowConfirmation(string message)
    {
        _confirmationText.text = message;
        _confirmationPanel.SetActive(true);
        _confirmationPanel.transform.SetAsLastSibling();
    }

    void HideConfirmation()
    {
        _pendingAction = PendingAction.None;
        _pendingSlot = -1;

        if (_confirmationPanel != null)
            _confirmationPanel.SetActive(false);
    }

    Button CreateButton(
        string labelValue,
        RectTransform parent,
        Vector2 position,
        Vector2 size,
        Color backgroundColor,
        out Text label,
        out Image image)
    {
        RectTransform rect = CreateRect(labelValue, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
        image = rect.gameObject.AddComponent<Image>();
        image.color = backgroundColor;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        button.colors = colors;

        label = CreateText(
            "Label",
            rect,
            labelValue,
            Vector2.zero,
            Vector2.zero,
            16,
            TextAnchor.MiddleCenter);
        RectTransform labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        return button;
    }

    Text CreateText(
        string objectName,
        RectTransform parent,
        string value,
        Vector2 position,
        Vector2 size,
        int fontSize,
        TextAnchor alignment)
    {
        RectTransform rect = CreateRect(objectName, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = _font;
        text.text = value;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    static RectTransform CreateRect(
        string objectName,
        RectTransform parent,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 position,
        Vector2 size)
    {
        GameObject child = new(objectName, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
