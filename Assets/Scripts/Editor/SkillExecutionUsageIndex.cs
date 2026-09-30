using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Execution modes come from authoring bindings, not a skill's Inspector preview.</summary>
[InitializeOnLoad]
public static class SkillExecutionUsageIndex
{
    static Dictionary<SkillGemDefinition, HashSet<SkillExecutionKind>> usages;
    static readonly SkillExecutionKind[] Standard = { SkillExecutionKind.StandardSkill };

    static SkillExecutionUsageIndex()
    {
        EditorApplication.projectChanged += Invalidate;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += _ => Invalidate();
        Undo.undoRedoPerformed += Invalidate;
        Undo.postprocessModifications += modifications => { Invalidate(); return modifications; };
    }

    public static void Invalidate() => usages = null;

    public static bool HasKnownUsage(SkillGemDefinition skill)
    {
        if (usages == null) Rebuild();
        return skill != null && usages.ContainsKey(skill);
    }

    public static IReadOnlyList<SkillExecutionKind> GetKinds(SkillGemDefinition skill)
    {
        if (usages == null) Rebuild();
        if (skill == null || !usages.TryGetValue(skill, out var kinds) || kinds.Count == 0)
            return Standard;
        var result = new List<SkillExecutionKind>(kinds);
        result.Sort();
        return result;
    }

    public static void Rebuild()
    {
        usages = new Dictionary<SkillGemDefinition, HashSet<SkillExecutionKind>>();
        var skillPaths = new Dictionary<string, SkillGemDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (string guid in AssetDatabase.FindAssets("t:SkillGemDefinition", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var skill = AssetDatabase.LoadAssetAtPath<SkillGemDefinition>(path);
            if (skill == null) continue;
            skillPaths[path] = skill;
            if (!skill.TryGetCombo(out var steps)) continue;
            foreach (var step in steps) Add(step.executionSkill, SkillExecutionKind.BasicMelee);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:CharacterAnimProfileSO", new[] { "Assets" }))
        {
            var profile = AssetDatabase.LoadAssetAtPath<CharacterAnimProfileSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (profile == null) continue;
            Add(profile.meleeSkill, SkillExecutionKind.BasicMelee);
            Add(profile.lightMeleeSkill, SkillExecutionKind.BasicMelee);
            Add(profile.heavyMeleeSkill, SkillExecutionKind.BasicMelee);
        }

        // Only execution bindings count. Character voice/cosmetic matching references do not.
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterStats", new[] { "Assets" }))
        {
            var stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(AssetDatabase.GUIDToAssetPath(guid));
            if (stats == null) continue;
            Add(stats.chainAttackSkill, SkillExecutionKind.StandardSkill);
            if (stats.skillSlots == null) continue;
            foreach (var slot in stats.skillSlots)
                if (slot?.options != null)
                    foreach (var option in slot.options) Add(option?.ActiveSkillAsset, SkillExecutionKind.StandardSkill);
        }

        string[] ownerTypes = { "SkillHelperDef", "SkillChainDef", "PartyComboSkillDef",
            "ChainAttackSequenceDef", "HelperChainAttackSequenceDef" };
        foreach (string ownerType in ownerTypes)
            foreach (string guid in AssetDatabase.FindAssets("t:" + ownerType, new[] { "Assets" }))
            {
                var owner = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (owner == null) continue;
                using var serialized = new SerializedObject(owner);
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue is SkillGemDefinition skill)
                        Add(skill, SkillExecutionKind.StandardSkill);
            }

        // Direct scene/prefab skill bindings are standard casts; melee references live in
        // profiles/Combo assets. Nonrecursive dependencies avoid classifying their children twice.
        string[] containers = Array.ConvertAll(
            AssetDatabase.FindAssets("t:Prefab t:Scene", new[] { "Assets" }), AssetDatabase.GUIDToAssetPath);
        foreach (string dependency in AssetDatabase.GetDependencies(containers, false))
            if (skillPaths.TryGetValue(dependency, out var skill)) Add(skill, SkillExecutionKind.StandardSkill);
    }

    static void Add(SkillGemDefinition skill, SkillExecutionKind kind)
    {
        if (skill == null) return;
        if (!usages.TryGetValue(skill, out var kinds)) usages.Add(skill, kinds = new HashSet<SkillExecutionKind>());
        kinds.Add(kind);
    }

    public static void CollectIssues(SkillGemDefinition skill, List<SkillValidationIssue> issues)
    {
        if (skill == null || issues == null) return;
        // A Combo validates each step with BasicMelee itself, regardless of its references.
        if (skill.IsCombo)
        {
            skill.CollectValidationIssues(issues, includeAuthoring: true);
            return;
        }
        foreach (var kind in GetKinds(skill))
        {
            var diagnostics = new List<SkillValidationIssue>();
            skill.CollectValidationIssues(diagnostics, kind, includeAuthoring: true);
            foreach (var issue in diagnostics)
                issues.Add(new SkillValidationIssue(issue.Code, issue.Severity, issue.Skill,
                    string.IsNullOrEmpty(issue.Location) ? kind.ToString() : $"{kind} / {issue.Location}",
                    issue.StepId, issue.Message, issue.Payload, issue.RequiredUpgradeId));
        }
    }
}
