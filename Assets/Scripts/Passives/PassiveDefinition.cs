using System.Collections.Generic;
using UnityEngine;

public abstract class PassiveDefinition : SkillDefinitionBase
{
    [Header("Identity")]
    public string passiveId;
    public Sprite icon;

    [TextArea(2, 4)]
    public string description;

    [Header("Progression")]
    [Tooltip("Default upgrade tree used by loadout options with no override.")]
    public SkillUpgradeTreeDefinition upgradeTree;

    public abstract PassiveKind Kind { get; }

    public string RuntimeId => string.IsNullOrWhiteSpace(passiveId) ? name : passiveId;

    public override SkillUpgradeTreeDefinition UpgradeTree => upgradeTree;
    public override string SkillDefinitionId => RuntimeId;
    public override string SkillDefinitionDisplayName => RuntimeId;
    public override Sprite SkillDefinitionIcon => icon;

    public override void CollectUpgradeIds(List<string> ids) { }
}
