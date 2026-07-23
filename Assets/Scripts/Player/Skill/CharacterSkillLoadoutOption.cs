using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class CharacterSkillLoadoutOption
{
    public string optionId;
    public string displayName;

    [Header("Active Gem")]
    public SkillGemDefinition skillAsset;
    [Min(1)] public int skillLevel = 1;

    [Header("Support Gems")]
    public SupportGemDefinition[] supportAssets;

    public int maxSupportSlots = 3;

    [Header("Active Skill Tree")]
    [Tooltip("Optional. Leave empty to use the Upgrade Tree assigned to the Skill Asset.")]
    [FormerlySerializedAs("upgradeTree")]
    public SkillUpgradeTreeDefinition upgradeTreeOverride;

    public bool IsConfigured => skillAsset != null;
    public SkillUpgradeTreeDefinition ResolvedUpgradeTree => upgradeTreeOverride != null
        ? upgradeTreeOverride
        : skillAsset != null ? skillAsset.upgradeTree : null;

    public string ResolvedOptionId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(optionId))
                return optionId.Trim();

            if (skillAsset != null && !string.IsNullOrWhiteSpace(skillAsset.skillId))
                return skillAsset.skillId.Trim();

            return skillAsset != null ? skillAsset.name : string.Empty;
        }
    }

    public string ResolvedDisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName.Trim();

            if (skillAsset != null && !string.IsNullOrWhiteSpace(skillAsset.displayName))
                return skillAsset.displayName.Trim();

            return skillAsset != null ? skillAsset.name : ResolvedOptionId;
        }
    }
}
