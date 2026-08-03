using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(InteractableLink))]
public class AmmoRefillInteractable : MonoBehaviour, IInteractable
{
    enum AmmoRestoreTarget
    {
        ReserveAmmo,
        Magazine,
        MagazineAndReserve
    }

    enum AmmoRecipientMode
    {
        InteractorOnly,
        InteractorAndAllies
    }

    [Header("Interaction")]
    [SerializeField] private int priority;
    [SerializeField] private string prompt = "Refill Ammo";

    [Header("Ammo")]
    [SerializeField, Min(1)] private int amount = 30;
    [SerializeField] private bool fillToMax = true;
    [SerializeField] private AmmoRestoreTarget restoreTarget = AmmoRestoreTarget.ReserveAmmo;
    [SerializeField] private AmmoRecipientMode recipientMode = AmmoRecipientMode.InteractorOnly;

    [Header("After Use")]
    [SerializeField] private bool deactivateAfterUse = true;
    [SerializeField] private GameObject objectToDeactivate;

    public int Priority => priority;

    public void ConfigurePartyReserveRefill(bool oneUse = true)
    {
        fillToMax = true;
        restoreTarget = AmmoRestoreTarget.ReserveAmmo;
        recipientMode = AmmoRecipientMode.InteractorAndAllies;
        deactivateAfterUse = oneUse;
    }

    public string GetPrompt(Interactor interactor)
    {
        return prompt;
    }

    public bool CanInteract(Interactor interactor)
    {
        List<WeaponSystem> weapons = ResolveTargetWeapons(interactor);
        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponSystem weapon = weapons[i];
            if (weapon != null && CanRestore(weapon))
                return true;
        }

        return false;
    }

    public void Interact(Interactor interactor)
    {
        List<WeaponSystem> weapons = ResolveTargetWeapons(interactor);
        bool restoredAny = false;
        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponSystem weapon = weapons[i];
            if (weapon != null && Restore(weapon))
                restoredAny = true;
        }

        if (restoredAny)
            DeactivateIfNeeded();
    }

    bool CanRestore(WeaponSystem weapon)
    {
        if (weapon == null)
            return false;

        switch (restoreTarget)
        {
            case AmmoRestoreTarget.Magazine:
                return weapon.CanRestoreMagazine(amount, fillToMax);

            case AmmoRestoreTarget.MagazineAndReserve:
                return weapon.CanRestoreMagazine(amount, fillToMax) ||
                       weapon.CanRestoreReserveAmmo(amount, fillToMax);

            case AmmoRestoreTarget.ReserveAmmo:
            default:
                return weapon.CanRestoreReserveAmmo(amount, fillToMax);
        }
    }

    bool Restore(WeaponSystem weapon)
    {
        if (weapon == null)
            return false;

        bool restoredAny = false;
        switch (restoreTarget)
        {
            case AmmoRestoreTarget.Magazine:
                restoredAny = weapon.RestoreMagazine(amount, fillToMax);
                break;

            case AmmoRestoreTarget.MagazineAndReserve:
                restoredAny |= weapon.RestoreReserveAmmo(amount, fillToMax);
                restoredAny |= weapon.RestoreMagazine(amount, fillToMax);
                break;

            case AmmoRestoreTarget.ReserveAmmo:
            default:
                restoredAny = weapon.RestoreReserveAmmo(amount, fillToMax);
                break;
        }

        return restoredAny;
    }

    List<WeaponSystem> ResolveTargetWeapons(Interactor interactor)
    {
        List<WeaponSystem> weapons = new();
        HashSet<int> seenContexts = new();
        HashSet<int> seenWeapons = new();

        if (interactor == null)
            return weapons;

        CharacteContext context = interactor.OwnerContext;
        if (context != null)
        {
            AddContextWeapon(weapons, seenContexts, seenWeapons, context);
            if (recipientMode == AmmoRecipientMode.InteractorAndAllies && context is PlayerContext playerContext)
                AddPlayerAllies(weapons, seenContexts, seenWeapons, playerContext);

            return weapons;
        }

        AddWeapon(weapons, seenWeapons, interactor.GetComponentInParent<WeaponSystem>());
        AddWeapon(weapons, seenWeapons, interactor.GetComponentInChildren<WeaponSystem>(true));
        return weapons;
    }

    void AddPlayerAllies(
        List<WeaponSystem> weapons,
        HashSet<int> seenContexts,
        HashSet<int> seenWeapons,
        PlayerContext playerContext)
    {
        if (playerContext == null)
            return;

        AddHelperAlly(weapons, seenContexts, seenWeapons, playerContext.allyHelper);
        AddFieldAllies(weapons, seenContexts, seenWeapons, playerContext.fieldAllyManager);
    }

    void AddHelperAlly(
        List<WeaponSystem> weapons,
        HashSet<int> seenContexts,
        HashSet<int> seenWeapons,
        AllyHelperManager helperManager)
    {
        GameObject helperObject = helperManager != null ? helperManager.HelperObject : null;
        if (helperObject == null)
            return;

        AddContextWeapon(
            weapons,
            seenContexts,
            seenWeapons,
            helperObject.GetComponentInChildren<AllyContext>(true));
    }

    void AddFieldAllies(
        List<WeaponSystem> weapons,
        HashSet<int> seenContexts,
        HashSet<int> seenWeapons,
        FieldAllyManager fieldAllyManager)
    {
        if (fieldAllyManager == null)
            return;

        foreach (FieldAllyMember member in fieldAllyManager.RegisteredMembers)
        {
            if (member == null)
                continue;

            AddContextWeapon(weapons, seenContexts, seenWeapons, member.ActorContext);
        }
    }

    void AddContextWeapon(
        List<WeaponSystem> weapons,
        HashSet<int> seenContexts,
        HashSet<int> seenWeapons,
        CharacteContext context)
    {
        if (context == null)
            return;

        int contextId = context.GetInstanceID();
        if (!seenContexts.Add(contextId))
            return;

        context.ResolveReferences();
        WeaponSystem weapon = context.WeaponSystem;
        if (weapon == null)
        {
            weapon = context.GetComponentInChildren<WeaponSystem>(true);
            if (weapon != null)
                context.WeaponSystem = weapon;
        }

        AddWeapon(weapons, seenWeapons, weapon);
    }

    void AddWeapon(List<WeaponSystem> weapons, HashSet<int> seenWeapons, WeaponSystem weapon)
    {
        if (weapon == null)
            return;

        int weaponId = weapon.GetInstanceID();
        if (seenWeapons.Add(weaponId))
            weapons.Add(weapon);
    }

    void DeactivateIfNeeded()
    {
        if (!deactivateAfterUse)
            return;

        GameObject target = objectToDeactivate != null ? objectToDeactivate : gameObject;
        target.SetActive(false);
    }
}
