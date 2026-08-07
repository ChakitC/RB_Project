using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Passives/Custom")]
public sealed class CustomPassiveDef : PassiveDefinition
{
    public List<PassiveCustomBehavior> behaviors = new();

    public override PassiveKind Kind => PassiveKind.Custom;

    public override void CollectUpgradeIds(List<string> ids)
    {
        if (ids == null || behaviors == null)
            return;

        for (int i = 0; i < behaviors.Count; i++)
            behaviors[i]?.CollectUpgradeIds(ids);
    }
}
