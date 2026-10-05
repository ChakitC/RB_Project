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

    [Header("Composition")]
    public PassiveRecipe recipe;
    [Header("Presentation")]
    [Tooltip("Optional HUD prefab. The HUD creates one view per installed grant.")]
    public PassiveHudView hudPrefab;
    [SerializeField, HideInInspector] private string definitionId;
    public string DefinitionId => string.IsNullOrEmpty(definitionId) ? "instance:" + GetInstanceID() : definitionId;
#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        string path = UnityEditor.AssetDatabase.GetAssetPath(this);
        if (!string.IsNullOrEmpty(path)) definitionId = UnityEditor.AssetDatabase.AssetPathToGUID(path);
    }
    public void SetDefinitionIdForMigration(string id) => definitionId = id;
#endif
    public abstract PassiveKind Kind { get; }

    public string RuntimeId => string.IsNullOrWhiteSpace(passiveId) ? name : passiveId;

    public override SkillUpgradeTreeDefinition UpgradeTree => upgradeTree;
    public override string SkillDefinitionId => RuntimeId;
    public override string SkillDefinitionDisplayName => RuntimeId;
    public override Sprite SkillDefinitionIcon => icon;

    public override void CollectUpgradeIds(List<string> ids) => PassiveRecipeValidator.CollectUpgradeIds(recipe, ids);
}
