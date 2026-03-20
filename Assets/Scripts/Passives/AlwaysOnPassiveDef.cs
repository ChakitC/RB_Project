using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Passives/Always On")]
public sealed class AlwaysOnPassiveDef : PassiveDefinition
{
    public List<PassiveStatModifier> modifiers = new();

    public override PassiveKind Kind => PassiveKind.AlwaysOn;
}
