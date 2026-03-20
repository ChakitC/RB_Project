using UnityEngine;

public abstract class PassiveCustomBehavior : ScriptableObject
{
    public virtual void OnEquipped(PassiveController controller, CustomPassiveDef definition) { }
    public virtual void OnUnequipped(PassiveController controller, CustomPassiveDef definition) { }
    public virtual void OnPassiveEvent(PassiveController controller, CustomPassiveDef definition, in PassiveEventContext context) { }
}
