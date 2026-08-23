using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ActiveSkillVariantCardView : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] Image icon;
    [SerializeField] Image frame;
    [SerializeField] TMP_Text title;
    [SerializeField] TMP_Text subtitle;
    [SerializeField] GameObject selectedMarker;

    int _optionIndex;
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

    public void Bind(
        SkillLoadoutOptionDescriptor option,
        int optionIndex,
        bool selected,
        SkillScreenTheme theme,
        Action<int> clicked)
    {
        _optionIndex = optionIndex;
        _clicked = clicked;

        Sprite sprite = option != null ? option.Icon : null;
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (title != null)
            title.text = option != null ? option.DisplayName : "Missing Variant";

        // Only a proc has a trigger to describe; a skill the player casts leaves this blank rather
        // than showing an empty line where the card expects text.
        if (subtitle != null)
        {
            string caption = BuildCaption(option);
            bool hasCaption = !string.IsNullOrWhiteSpace(caption);
            subtitle.text = hasCaption ? caption : string.Empty;
            subtitle.gameObject.SetActive(hasCaption);
        }

        if (selectedMarker != null)
            selectedMarker.SetActive(selected);

        if (frame != null && theme != null)
        {
            if (theme.cardFrame != null)
                frame.sprite = theme.cardFrame;
            frame.color = selected ? theme.selectedColor : theme.activeCardColor;
        }

        if (button != null)
            button.interactable = !selected && option != null;
    }

    /// <summary>
    /// Trigger line plus the proc's own description. Only a proc fills either in, so a Stryker
    /// variant returns empty and the caption is hidden rather than reserving blank space.
    /// </summary>
    static string BuildCaption(SkillLoadoutOptionDescriptor option)
    {
        if (option == null || option.HelperProc == null)
            return string.Empty;

        bool hasTrigger = !string.IsNullOrWhiteSpace(option.TriggerSummary);
        bool hasDescription = !string.IsNullOrWhiteSpace(option.Description);

        if (hasTrigger && hasDescription)
            return $"{option.TriggerSummary}\n{option.Description.Trim()}";

        if (hasTrigger)
            return option.TriggerSummary;

        return hasDescription ? option.Description.Trim() : string.Empty;
    }

    void HandleClick()
    {
        _clicked?.Invoke(_optionIndex);
    }
}
