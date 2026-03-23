using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class UIPassiveNodeItem : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Image frameImage;
    [SerializeField] private GameObject selectedMarker;
    [SerializeField] private GameObject unlockedMarker;
    [SerializeField] private GameObject lockedMarker;
    [SerializeField] private Button button;

    UIPassiveTreePanel _owner;
    PassiveTreeNodeData _nodeData;
    string _nodeId;

    public string NodeId => _nodeId;

    public Vector2 AnchoredPosition
    {
        get
        {
            if (transform is RectTransform rectTransform)
                return rectTransform.anchoredPosition;

            return Vector2.zero;
        }
    }

    void Awake()
    {
        if (button != null)
            button.onClick.AddListener(HandleButtonClick);
    }

    void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleButtonClick);
    }

    public void Bind(
        UIPassiveTreePanel owner,
        PassiveTreeNodeData nodeData,
        bool unlocked,
        bool canUnlock,
        bool selected,
        Color lockedColor,
        Color availableColor,
        Color unlockedColor,
        Color selectedColor)
    {
        _owner = owner;
        _nodeData = nodeData;
        _nodeId = nodeData != null ? nodeData.RuntimeNodeId : string.Empty;

        if (transform is RectTransform rectTransform && nodeData != null)
            rectTransform.anchoredPosition = nodeData.uiPosition;

        RefreshVisuals(unlocked, canUnlock, selected, lockedColor, availableColor, unlockedColor, selectedColor);
    }

    public void RefreshVisuals(
        bool unlocked,
        bool canUnlock,
        bool selected,
        Color lockedColor,
        Color availableColor,
        Color unlockedColor,
        Color selectedColor)
    {
        if (iconImage != null)
        {
            iconImage.sprite = _nodeData != null ? _nodeData.icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }

        if (titleText != null)
            titleText.text = _nodeData != null ? _nodeData.ResolvedDisplayName : "-";

        if (costText != null)
            costText.text = _nodeData != null ? _nodeData.cost.ToString() : string.Empty;

        if (selectedMarker != null)
            selectedMarker.SetActive(selected);

        if (unlockedMarker != null)
            unlockedMarker.SetActive(unlocked);

        if (lockedMarker != null)
            lockedMarker.SetActive(!unlocked);

        if (frameImage != null)
        {
            if (selected)
                frameImage.color = selectedColor;
            else if (unlocked)
                frameImage.color = unlockedColor;
            else if (canUnlock)
                frameImage.color = availableColor;
            else
                frameImage.color = lockedColor;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        _owner?.SelectNode(_nodeId);
    }

    void HandleButtonClick()
    {
        _owner?.SelectNode(_nodeId);
    }
}
