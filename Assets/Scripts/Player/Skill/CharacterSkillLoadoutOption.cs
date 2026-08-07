using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class CharacterSkillLoadoutOption
{
    public string optionId;
    public string displayName;

    [Header("Skill")]
    [Tooltip("Active gem or passive definition. The asset kind decides the execution mode.")]
    public SkillDefinitionBase skillAsset;

    [Header("Skill Tree")]
    [Tooltip("Optional. Leave empty to use the Upgrade Tree assigned to the Skill Asset.")]
    [FormerlySerializedAs("upgradeTree")]
    public SkillUpgradeTreeDefinition upgradeTreeOverride;

    public bool IsConfigured => skillAsset != null;
    public bool IsPassive => skillAsset is PassiveDefinition;
    public SkillGemDefinition ActiveSkillAsset => skillAsset as SkillGemDefinition;
    public PassiveDefinition PassiveAsset => skillAsset as PassiveDefinition;

    public SkillUpgradeTreeDefinition ResolvedUpgradeTree => upgradeTreeOverride != null
        ? upgradeTreeOverride
        : skillAsset != null ? skillAsset.UpgradeTree : null;

    public string ResolvedOptionId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(optionId))
                return optionId.Trim();

            if (skillAsset != null && !string.IsNullOrWhiteSpace(skillAsset.SkillDefinitionId))
                return skillAsset.SkillDefinitionId.Trim();

            return skillAsset != null ? skillAsset.name : string.Empty;
        }
    }

    public string ResolvedDisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName.Trim();

            if (skillAsset != null && !string.IsNullOrWhiteSpace(skillAsset.SkillDefinitionDisplayName))
                return skillAsset.SkillDefinitionDisplayName.Trim();

            return skillAsset != null ? skillAsset.name : ResolvedOptionId;
        }
    }
}
