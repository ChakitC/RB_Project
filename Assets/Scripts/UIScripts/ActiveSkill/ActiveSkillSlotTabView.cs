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
    [SerializeField] GameObject passiveBadge;
    [SerializeField] Image skillIcon;
    [SerializeField] TMP_Text skillName;
    [SerializeField] TMP_Text description;

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

    public void Bind(int slotIndex, string text, bool selected, bool isPassive, SkillScreenTheme theme, Action<int> clicked)
    {
        _slotIndex = slotIndex;
        _clicked = clicked;
        if (label != null)
            label.text = text;
        if (selectedMarker != null)
            selectedMarker.SetActive(selected);
        if (passiveBadge != null)
            passiveBadge.SetActive(isPassive);
        if (background != null && theme != null)
            background.color = selected ? theme.selectedColor : theme.inactiveCardColor;
        if (button != null)
            button.interactable = !selected;
    }

    void HandleClick()
    {
        _clicked?.Invoke(_slotIndex);
    }

    public void SetEquippedSkill(SkillLoadoutOptionDescriptor option)
    {
        if (skillIcon != null)
        {
            skillIcon.sprite = option?.Icon;
            skillIcon.enabled = option?.Icon != null;
        }
        if (skillName != null)
            skillName.text = option != null ? option.DisplayName : "Not available yet";
        if (description != null)
            description.text = option != null ? option.Description : "Skill mapping is not ready.";
    }
}
