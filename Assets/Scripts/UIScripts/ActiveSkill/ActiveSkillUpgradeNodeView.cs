using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ActiveSkillNodeVisualState
{
    Locked,
    Available,
    Unlocked,
}

public sealed class ActiveSkillUpgradeNodeView : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] Image icon;
    [SerializeField] Image frame;
    [SerializeField] TMP_Text costText;
    [SerializeField] GameObject availableGlow;
    [SerializeField] GameObject unlockedMarker;
    [SerializeField] GameObject selectedMarker;

    string _nodeId;
    Action<string> _clicked;

    public string NodeId => _nodeId;
    public Vector2 AnchoredPosition => transform is RectTransform rect ? rect.anchoredPosition : Vector2.zero;

    void Awake()
    {
        if (button != null)
            button.onClick.AddListener(HandleClick);
    }

    void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    public void Bind(
        SkillUpgradeNodeData node,
        ActiveSkillNodeVisualState state,
        bool selected,
        SkillScreenTheme theme,
        Action<string> clicked)
    {
        _nodeId = node != null ? node.RuntimeNodeId : string.Empty;
        _clicked = clicked;

        if (transform is RectTransform rect)
        {
            float size = node != null
                ? node.ResolvedRuntimeSize
                : SkillUpgradeNodeData.BaseRuntimeSize;
            rect.sizeDelta = Vector2.one * size;
        }

        if (icon != null)
        {
            icon.sprite = node != null ? node.icon : null;
            icon.enabled = icon.sprite != null;
            if (theme != null)
                icon.color = state == ActiveSkillNodeVisualState.Locked ? theme.lockedColor : Color.white;
        }

        if (costText != null)
            costText.text = node != null ? Mathf.Max(1, node.cost).ToString() : string.Empty;

        if (availableGlow != null)
            availableGlow.SetActive(state == ActiveSkillNodeVisualState.Available);
        if (unlockedMarker != null)
            unlockedMarker.SetActive(state == ActiveSkillNodeVisualState.Unlocked);
        if (selectedMarker != null)
            selectedMarker.SetActive(selected);

        if (frame != null && theme != null)
        {
            bool important = node != null &&
                node.ResolvedVisualScale > SkillUpgradeNodeData.MinVisualScale;
            frame.sprite = important && theme.importantNodeFrame != null
                ? theme.importantNodeFrame
                : theme.nodeFrame;
            frame.color = selected
                ? theme.selectedColor
                : state switch
                {
                    ActiveSkillNodeVisualState.Available => theme.availableColor,
                    ActiveSkillNodeVisualState.Unlocked => theme.unlockedColor,
                    _ => theme.lockedColor,
                };
        }

        if (button != null)
            button.interactable = node != null;
    }

    void HandleClick()
    {
        if (!string.IsNullOrWhiteSpace(_nodeId))
            _clicked?.Invoke(_nodeId);
    }
}
