using UnityEngine;

[CreateAssetMenu(fileName = "Restore Magazine Pickup Effect", menuName = "Game/Pickup Effect/Restore Magazine")]
public class RestoreMagazinePickupEffectDef : PickupEffectDef
{
    enum AmmoRestoreTarget
    {
        ReserveAmmo,
        Magazine
    }

    [SerializeField, Min(1)] private int amount = 1;
    [SerializeField] private bool fillToMax = false;
    [SerializeField] private AmmoRestoreTarget restoreTarget = AmmoRestoreTarget.ReserveAmmo;

    public override bool CanApply(GameObject target, in PickupContext context)
    {
        var weapon = FindTargetComponent<WeaponSystem>(target);
        return weapon != null && CanRestore(weapon);
    }

    public override bool Apply(GameObject target, in PickupContext context)
    {
        var weapon = FindTargetComponent<WeaponSystem>(target);
        return weapon != null && Restore(weapon);
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
}
