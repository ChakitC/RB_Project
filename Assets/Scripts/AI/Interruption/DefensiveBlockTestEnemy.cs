using System;
using UnityEngine;

[Serializable]
public sealed class DefensiveBlockTestEnemy
{
    public string label;
    public GameObject prefab;
    public SkillGemDefinition[] skills = Array.Empty<SkillGemDefinition>();
    public string DisplayName => string.IsNullOrWhiteSpace(label) ? prefab != null ? prefab.name : "Missing enemy" : label;
}
