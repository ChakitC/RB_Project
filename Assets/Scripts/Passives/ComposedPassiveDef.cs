using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Passives/Composed")]
public sealed class ComposedPassiveDef : PassiveDefinition
{
    public override PassiveKind Kind => PassiveKind.Triggered;
}
