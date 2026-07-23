using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ActiveSkillSlotTabView : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] Image background;
    [SerializeField] TMP_Text label;
    [SerializeField] GameObject selectedMarker;

    int _slotIndex;
    Action<int> _clicked;

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

    public void Bind(int slotIndex, string text, bool selected, SkillScreenTheme theme, Action<int> clicked)
    {
        _slotIndex = slotIndex;
        _clicked = clicked;
        if (label != null)
            label.text = text;
        if (selectedMarker != null)
            selectedMarker.SetActive(selected);
        if (background != null && theme != null)
            background.color = selected ? theme.selectedColor : theme.inactiveCardColor;
        if (button != null)
            button.interactable = !selected;
    }

    void HandleClick()
    {
        _clicked?.Invoke(_slotIndex);
    }
}
