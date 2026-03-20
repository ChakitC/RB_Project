using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Passives/Custom")]
public sealed class CustomPassiveDef : PassiveDefinition
{
    public List<PassiveCustomBehavior> behaviors = new();

    public override PassiveKind Kind => PassiveKind.Custom;
}
