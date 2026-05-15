using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Restore Magazine Pickup Effect", menuName = "Game/Pickup Effect/Restore Magazine")]
public class RestoreMagazinePickupEffectDef : PickupEffectDef
{
    enum AmmoRestoreTarget
    {
        ReserveAmmo,
        Magazine
    }

    enum AmmoRecipientMode
    {
        CollectorOnly,
        CollectorAndAllies
    }

    [SerializeField, Min(1)] private int amount = 1;
    [SerializeField] private bool fillToMax = false;
    [SerializeField] private AmmoRestoreTarget restoreTarget = AmmoRestoreTarget.ReserveAmmo;
    [SerializeField] private AmmoRecipientMode recipientMode = AmmoRecipientMode.CollectorOnly;

    public override bool CanApply(GameObject target, in PickupContext context)
    {
        List<WeaponSystem> weapons = ResolveTargetWeapons(target);
        for (int i = 0; i < weapons.Count; i++)
        {
            if (CanRestore(weapons[i]))
                return true;
        }

        return false;
    }

    public override bool Apply(GameObject target, in PickupContext context)
    {
        List<WeaponSystem> weapons = ResolveTargetWeapons(target);
        bool restoredAny = false;
        for (int i = 0; i < weapons.Count; i++)
        {
            if (Restore(weapons[i]))
                restoredAny = true;
        }

        return restoredAny;
    }

    bool CanRestore(WeaponSystem weapon)
    {
        return restoreTarget == AmmoRestoreTarget.Magazine
            ? weapon.CanRestoreMagazine(amount, fillToMax)
            : weapon.CanRestoreReserveAmmo(amount, fillToMax);
    }

    bool Restore(WeaponSystem weapon)
    {
        return restoreTarget == AmmoRestoreTarget.Magazine
            ? weapon.RestoreMagazine(amount, fillToMax)
            : weapon.RestoreReserveAmmo(amount, fillToMax);
    }

    List<WeaponSystem> ResolveTargetWeapons(GameObject target)
    {
        List<WeaponSystem> weapons = new();
        HashSet<int> seenContexts = new();
        HashSet<int> seenWeapons = new();

        CharacteContext collectorContext = FindTargetComponent<CharacteContext>(target);
        if (collectorContext != null)
        {
            AddContextWeapon(weapons, seenContexts, seenWeapons, collectorContext);
            if (recipientMode == AmmoRecipientMode.CollectorAndAllies && collectorContext is PlayerContext playerContext)
                AddPlayerAllies(weapons, seenContexts, seenWeapons, playerContext);

            return weapons;
        }

        AddWeapon(weapons, seenWeapons, FindTargetComponent<WeaponSystem>(target));
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
        CharacteContext characterContext)
    {
        if (characterContext == null)
            return;

        int contextId = characterContext.GetInstanceID();
        if (!seenContexts.Add(contextId))
            return;

        characterContext.ResolveReferences();
        WeaponSystem weapon = characterContext.WeaponSystem;
        if (weapon == null)
        {
            weapon = characterContext.GetComponentInChildren<WeaponSystem>(true);
            if (weapon != null)
                characterContext.WeaponSystem = weapon;
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
}
