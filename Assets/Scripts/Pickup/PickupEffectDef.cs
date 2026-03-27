using UnityEngine;

public abstract class PickupEffectDef : ScriptableObject
{
    public virtual bool CanApply(GameObject target, in PickupContext context)
    {
        return target != null;
    }

    public abstract bool Apply(GameObject target, in PickupContext context);

    protected static T FindTargetComponent<T>(GameObject target) where T : Component
    {
        if (target == null)
            return null;

        var component = target.GetComponent<T>();
        if (component != null)
            return component;

        component = target.GetComponentInParent<T>();
        if (component != null)
            return component;

        return target.GetComponentInChildren<T>();
    }
}
