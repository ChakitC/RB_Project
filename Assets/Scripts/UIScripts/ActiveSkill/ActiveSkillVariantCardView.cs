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
        CharacterSkillLoadoutOption option,
        int optionIndex,
        bool selected,
        SkillScreenTheme theme,
        Action<int> clicked)
    {
        _optionIndex = optionIndex;
        _clicked = clicked;

        Sprite sprite = option != null && option.skillAsset != null ? option.skillAsset.icon : null;
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (title != null)
            title.text = option != null ? option.ResolvedDisplayName : "Missing Variant";

        if (selectedMarker != null)
            selectedMarker.SetActive(selected);

        if (frame != null && theme != null)
        {
            if (theme.cardFrame != null)
                frame.sprite = theme.cardFrame;
            frame.color = selected ? theme.selectedColor : theme.activeCardColor;
        }

        if (button != null)
            button.interactable = !selected && option != null && option.IsConfigured;
    }

    void HandleClick()
    {
        _clicked?.Invoke(_optionIndex);
    }
}
