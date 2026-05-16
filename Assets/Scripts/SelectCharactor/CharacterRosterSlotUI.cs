using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterRosterSlotUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private CharacterDefHolder defHolder;
    [SerializeField] private SpawnOnDragOutButton dragOutButton;

    [Header("State")]
    [SerializeField] private CharacterStats character;

    public CharacterStats Character => character;

    void Awake()
    {
        EnsureRefs();
        ApplyVisuals();
    }

    public void Bind(
        CharacterStats characterDef,
        SpawnOnDragOutButton dragSettingsSource,
        ScrollRect parentScrollRect)
    {
        EnsureRefs();

        character = characterDef;

        if (defHolder != null)
            defHolder.def = characterDef;

        if (dragOutButton != null)
        {
            dragOutButton.SetCharacterDef(characterDef);
            dragOutButton.CopyDragSettingsFrom(dragSettingsSource);
            dragOutButton.SetParentScrollRect(parentScrollRect);
        }

        ApplyVisuals();
    }

    void EnsureRefs()
    {
        if (iconImage == null)
        {
            Transform iconTransform = transform.Find("Icon");
            if (iconTransform != null)
                iconImage = iconTransform.GetComponent<Image>();
        }

        if (iconImage == null)
            iconImage = GetComponentInChildren<Image>(true);

        if (nameText == null)
            nameText = GetComponentInChildren<TMP_Text>(true);

        if (defHolder == null)
        {
            defHolder = GetComponent<CharacterDefHolder>();
            if (defHolder == null)
                defHolder = gameObject.AddComponent<CharacterDefHolder>();
        }

        if (dragOutButton == null)
            dragOutButton = GetComponent<SpawnOnDragOutButton>();
    }

    void ApplyVisuals()
    {
        Sprite icon = character != null ? character.icon : null;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.preserveAspect = true;
        }

        if (nameText != null)
            nameText.text = ResolveDisplayName(character);

        gameObject.name = character != null && !string.IsNullOrWhiteSpace(character.characterId)
            ? $"CharacterRosterSlot_{character.characterId}"
            : "CharacterRosterSlot";
    }

    static string ResolveDisplayName(CharacterStats stats)
    {
        if (stats == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(stats.characterName))
            return stats.characterName;

        if (!string.IsNullOrWhiteSpace(stats.characterId))
            return stats.characterId;

        return stats.name;
    }
}
