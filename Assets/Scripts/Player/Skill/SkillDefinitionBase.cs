using System.Collections.Generic;
using UnityEngine;

public abstract class SkillDefinitionBase : ScriptableObject
{
    public abstract SkillUpgradeTreeDefinition UpgradeTree { get; }
    public abstract string SkillDefinitionId { get; }
    public abstract string SkillDefinitionDisplayName { get; }
    public abstract Sprite SkillDefinitionIcon { get; }
    public abstract void CollectUpgradeIds(List<string> ids);
}
