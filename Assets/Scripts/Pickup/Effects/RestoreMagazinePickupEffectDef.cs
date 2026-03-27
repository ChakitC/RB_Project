using UnityEngine;

[CreateAssetMenu(fileName = "Restore Magazine Pickup Effect", menuName = "Game/Pickup Effect/Restore Magazine")]
public class RestoreMagazinePickupEffectDef : PickupEffectDef
{
    [SerializeField, Min(1)] private int amount = 1;
    [SerializeField] private bool fillToMax = false;

    public override bool CanApply(GameObject target, in PickupContext context)
    {
        var weapon = FindTargetComponent<WeaponSystem>(target);
        return weapon != null && weapon.CanRestoreMagazine(amount, fillToMax);
    }

    public override bool Apply(GameObject target, in PickupContext context)
    {
        var weapon = FindTargetComponent<WeaponSystem>(target);
        return weapon != null && weapon.RestoreMagazine(amount, fillToMax);
    }
}
