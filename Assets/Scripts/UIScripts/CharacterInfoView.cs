using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(FollowWorldToScreenUI))]
public sealed class CharacterInfoView : MonoBehaviour
{
    [Serializable]
    struct RoleIconBinding
    {
        public CharacterCombatRole role;
        public Sprite icon;
    }

    [Header("Content")]
    [SerializeField] private GameObject[] contentRoots;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text weaponTypeText;
    [SerializeField] private TMP_Text combatRoleText;
    [SerializeField] private Image combatRoleIcon;

    [Header("Role Icons")]
    [SerializeField] private RoleIconBinding[] roleIcons;

    FollowWorldToScreenUI followTarget;
    PartySlot partySlot;

    void Awake()
    {
        followTarget = GetComponent<FollowWorldToScreenUI>();
    }

    void OnEnable()
    {
        BindSlot(ResolvePartySlot());
    }

    void OnDisable()
    {
        if (partySlot != null)
            partySlot.SelectedChanged -= OnSelectedChanged;

        partySlot = null;
    }

    public void BindSlot(PartySlot slot)
    {
        if (partySlot == slot)
        {
            Refresh();
            return;
        }

        if (partySlot != null)
            partySlot.SelectedChanged -= OnSelectedChanged;

        partySlot = slot;

        if (partySlot != null)
            partySlot.SelectedChanged += OnSelectedChanged;

        Refresh();
    }

    public void Refresh()
    {
        ApplyCharacter(partySlot != null ? partySlot.Selected : null);
    }

    PartySlot ResolvePartySlot()
    {
        if (followTarget == null)
            followTarget = GetComponent<FollowWorldToScreenUI>();

        Transform target = followTarget != null ? followTarget.target : null;
        return target != null ? target.GetComponentInParent<PartySlot>() : null;
    }

    void OnSelectedChanged(CharacterStats character)
    {
        ApplyCharacter(character);
    }

    void ApplyCharacter(CharacterStats character)
    {
        bool hasCharacter = character != null;
        SetContentVisible(hasCharacter);

        if (!hasCharacter)
            return;

        if (characterNameText != null)
        {
            characterNameText.text = string.IsNullOrWhiteSpace(character.characterName)
                ? character.name
                : character.characterName;
        }

        if (weaponTypeText != null)
            weaponTypeText.text = FormatWeaponType(character.CharacterWeaponType);

        if (combatRoleText != null)
            combatRoleText.text = character.combatRole.ToString();

        if (combatRoleIcon != null)
        {
            Sprite icon = FindRoleIcon(character.combatRole);
            combatRoleIcon.sprite = icon;
            combatRoleIcon.enabled = icon != null;
        }
    }

    void SetContentVisible(bool visible)
    {
        if (contentRoots == null)
            return;

        for (int i = 0; i < contentRoots.Length; i++)
        {
            GameObject contentRoot = contentRoots[i];
            if (contentRoot != null && contentRoot.activeSelf != visible)
                contentRoot.SetActive(visible);
        }
    }

    Sprite FindRoleIcon(CharacterCombatRole role)
    {
        if (roleIcons == null)
            return null;

        for (int i = 0; i < roleIcons.Length; i++)
        {
            if (roleIcons[i].role == role)
                return roleIcons[i].icon;
        }

        return null;
    }

    static string FormatWeaponType(WeaponType weaponType)
    {
        switch (weaponType)
        {
            case WeaponType.Smg:
                return "SMG";
            case WeaponType.Hmg:
                return "HMG";
            default:
                return weaponType.ToString();
        }
    }
}
