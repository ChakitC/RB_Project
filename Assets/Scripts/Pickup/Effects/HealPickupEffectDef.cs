using UnityEngine;

[CreateAssetMenu(fileName = "Heal Pickup Effect", menuName = "Game/Pickup Effect/Heal")]
public class HealPickupEffectDef : PickupEffectDef
{
    [SerializeField, Min(0f)] private float amount = 25f;

    public override bool CanApply(GameObject target, in PickupContext context)
    {
        var health = FindTargetComponent<HealthSystem>(target);
        return health != null && health.CanHeal(amount);
    }

    public override bool Apply(GameObject target, in PickupContext context)
    {
        var health = FindTargetComponent<HealthSystem>(target);
        return health != null && health.Heal(amount);
    }
}
