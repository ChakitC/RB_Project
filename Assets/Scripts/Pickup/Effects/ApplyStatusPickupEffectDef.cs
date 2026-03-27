using UnityEngine;

[CreateAssetMenu(fileName = "Apply Status Pickup Effect", menuName = "Game/Pickup Effect/Apply Status")]
public class ApplyStatusPickupEffectDef : PickupEffectDef
{
    [SerializeField] private StatusEffectDef effect;
    [SerializeField, Min(1)] private int stacks = 1;

    public override bool CanApply(GameObject target, in PickupContext context)
    {
        return effect != null && FindTargetComponent<StatusEffectController>(target) != null;
    }

    public override bool Apply(GameObject target, in PickupContext context)
    {
        if (effect == null)
            return false;

        var controller = FindTargetComponent<StatusEffectController>(target);
        if (controller == null)
            return false;

        GameObject source = context.SourceObject != null ? context.SourceObject : context.PickupObject;
        return controller.ApplyEffect(effect, source, stacks) != null;
    }
}
