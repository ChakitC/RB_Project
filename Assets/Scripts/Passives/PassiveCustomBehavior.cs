using System.Collections.Generic;
using UnityEngine;

public abstract class PassiveCustomBehavior : ScriptableObject
{
    public virtual void OnEquipped(PassiveController controller, CustomPassiveDef definition, SkillUpgradeStatSnapshot upgrades) { }
    public virtual void OnUnequipped(PassiveController controller, CustomPassiveDef definition) { }
    public virtual void OnPassiveEvent(PassiveController controller, CustomPassiveDef definition, in PassiveEventContext context, SkillUpgradeStatSnapshot upgrades) { }
    public virtual void CollectUpgradeIds(List<string> ids) { }
}
