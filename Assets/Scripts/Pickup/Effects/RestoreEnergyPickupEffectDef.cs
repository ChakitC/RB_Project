using UnityEngine;

[CreateAssetMenu(fileName = "Restore Energy Pickup Effect", menuName = "Game/Pickup Effect/Restore Energy")]
public class RestoreEnergyPickupEffectDef : PickupEffectDef
{
    [SerializeField, Min(0f)] private float amount = 25f;

    public override bool CanApply(GameObject target, in PickupContext context)
    {
        var skillUser = FindTargetComponent<SkillUserSystem>(target);
        return skillUser != null && skillUser.CanRestoreEnergy(amount);
    }

    public override bool Apply(GameObject target, in PickupContext context)
    {
        var skillUser = FindTargetComponent<SkillUserSystem>(target);
        return skillUser != null && skillUser.RestoreEnergy(amount);
    }
}
