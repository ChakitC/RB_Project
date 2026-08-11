#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ตรวจว่า scope ที่ label ประกาศไว้ตรงกับการใช้งานจริงในสกิลทุกตัว.
///
/// ทำเป็น pass แยกจาก <see cref="SkillUpgradeTreeValidator"/> เพราะขอบเขตต่างกัน: อันนั้นตรวจ "ต้นไม้"
/// หนึ่งต้น ส่วนอันนี้ตรวจความสัมพันธ์ status ⇄ skill ทั้งโปรเจกต์ (unique ที่หลุดข้ามสกิล และ effectId ซ้ำ
/// จะมองไม่เห็นเลยถ้ายืนอยู่ในต้นไม้ต้นเดียว).
/// </summary>
public static class StatusEffectScopeValidator
{
    /// <summary>StatusEffectDef → สกิล/passive ทุกตัวที่อ้างถึงมัน (รวม embedded payload sub-assets).</summary>
    public static Dictionary<StatusEffectDef, List<SkillDefinitionBase>> BuildUsageIndex(
        IReadOnlyList<SkillDefinitionBase> owners = null)
    {
        var index = new Dictionary<StatusEffectDef, List<SkillDefinitionBase>>();
        IReadOnlyList<SkillDefinitionBase> assets = owners ?? FindAllSkillDefinitions();

        for (int i = 0; i < assets.Count; i++)
        {
            SkillDefinitionBase owner = assets[i];
            if (owner == null)
                continue;

            foreach (StatusEffectDef definition in EnumerateReferencedEffects(owner))
            {
                if (!index.TryGetValue(definition, out List<SkillDefinitionBase> users))
                {
                    users = new List<SkillDefinitionBase>();
                    index[definition] = users;
                }

                if (!users.Contains(owner))
                    users.Add(owner);
            }
        }

        return index;
    }

    /// <summary>
    /// ทุก StatusEffectDef ที่ asset นี้ (และ sub-asset ของมัน) อ้างถึง.
    ///
    /// เดิน SerializedObject แทนการไล่ field ตามชนิด payload เพราะ apply site เพิ่มใหม่ได้เรื่อยๆ —
    /// จับ object reference ที่เป็น StatusEffectDef ทั้งหมด ครอบคลุมทั้ง applications, conditional status routes
    /// และ tauntStatus โดยไม่ต้องรู้ชื่อ field.
    /// </summary>
    public static IEnumerable<StatusEffectDef> EnumerateReferencedEffects(SkillDefinitionBase owner)
    {
        var seen = new HashSet<StatusEffectDef>();
        foreach (UnityEngine.Object asset in EnumerateAssetObjects(owner))
        {
            if (asset == null)
                continue;

            var serialized = new SerializedObject(asset);
            SerializedProperty iterator = serialized.GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                    continue;

                if (iterator.objectReferenceValue is StatusEffectDef definition && seen.Add(definition))
                    yield return definition;
            }
        }
    }

    public static List<SkillUpgradeValidationIssue> Validate(IReadOnlyList<SkillDefinitionBase> owners = null)
    {
        var issues = new List<SkillUpgradeValidationIssue>();
        Dictionary<StatusEffectDef, List<SkillDefinitionBase>> index = BuildUsageIndex(owners);
        List<StatusEffectDef> definitions = FindAllStatusEffects();

        for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
        {
            StatusEffectDef definition = definitions[definitionIndex];
            StatusEffectScope scope = StatusEffectScopeUtility.GetScope(definition);

            if (scope == StatusEffectScope.Legacy)
            {
                issues.Add(Warning(
                    $"Status '{definition.name}' has no scope label. Label it " +
                    $"{StatusEffectScopeUtility.GlobalLabel} or {StatusEffectScopeUtility.UniqueLabel} " +
                    "so cross-skill reuse stays intentional."));
                continue;
            }

            if (scope != StatusEffectScope.Unique)
                continue;

            string ownerGuid = StatusEffectScopeUtility.GetOwnerGuid(definition);
            if (string.IsNullOrEmpty(ownerGuid))
            {
                issues.Add(Error(
                    $"Status '{definition.name}' is labelled {StatusEffectScopeUtility.UniqueLabel} but has no " +
                    $"{StatusEffectScopeUtility.OwnerLabelPrefix}<guid> label, so no skill can legally use it."));
                continue;
            }

            index.TryGetValue(definition, out List<SkillDefinitionBase> users);
            users ??= new List<SkillDefinitionBase>();
            for (int i = 0; i < users.Count; i++)
            {
                if (StatusEffectScopeUtility.IsUsableBy(definition, users[i], out string reason))
                    continue;

                issues.Add(Error($"{users[i].name}: {reason}"));
            }
        }

        CollectDuplicateEffectIdIssues(definitions, issues);
        return issues;
    }

    /// <summary>
    /// effectId ซ้ำเป็น error เสมอ ไม่ว่าใครใช้อยู่ — instance ที่รันอยู่ถูกจับคู่ด้วย Def reference
    /// แต่ทุกเครื่องมือ authoring/บันทึกที่อ้าง id จะจับคู่ผิดตัวทันทีถ้ามีสองตัวถือ id เดียวกัน.
    /// </summary>
    static void CollectDuplicateEffectIdIssues(
        IReadOnlyList<StatusEffectDef> definitions,
        List<SkillUpgradeValidationIssue> issues)
    {
        var byId = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        for (int i = 0; definitions != null && i < definitions.Count; i++)
        {
            StatusEffectDef definition = definitions[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.effectId))
                continue;

            string id = definition.effectId.Trim();
            if (!byId.TryGetValue(id, out List<string> names))
            {
                names = new List<string>();
                byId[id] = names;
            }

            names.Add(definition.name);
        }

        foreach (KeyValuePair<string, List<string>> entry in byId)
        {
            if (entry.Value.Count < 2)
                continue;

            issues.Add(Error(
                $"Status effect id '{entry.Key}' is used by {entry.Value.Count} assets " +
                $"({string.Join(", ", entry.Value)}). Effect ids must be unique."));
        }
    }

    static List<StatusEffectDef> FindAllStatusEffects()
    {
        var result = new List<StatusEffectDef>();
        string[] guids = AssetDatabase.FindAssets("t:StatusEffectDef");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var definition = AssetDatabase.LoadAssetAtPath<StatusEffectDef>(path);
            if (definition != null)
                result.Add(definition);
        }

        return result;
    }

    /// <summary>สกิลที่ใช้ Def ตัวนี้อยู่ — wizard ใช้กันการกด Global → Unique ทั้งที่มีหลายสกิลใช้.</summary>
    public static List<SkillDefinitionBase> FindUsingSkills(StatusEffectDef definition)
    {
        var result = new List<SkillDefinitionBase>();
        if (definition == null)
            return result;

        IReadOnlyList<SkillDefinitionBase> assets = FindAllSkillDefinitions();
        for (int i = 0; i < assets.Count; i++)
        {
            foreach (StatusEffectDef referenced in EnumerateReferencedEffects(assets[i]))
            {
                if (ReferenceEquals(referenced, definition))
                {
                    result.Add(assets[i]);
                    break;
                }
            }
        }

        return result;
    }

    public static IReadOnlyList<SkillDefinitionBase> FindAllSkillDefinitions()
    {
        var result = new List<SkillDefinitionBase>();
        string[] guids = AssetDatabase.FindAssets("t:SkillDefinitionBase");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var asset = AssetDatabase.LoadAssetAtPath<SkillDefinitionBase>(path);
            if (asset != null)
                result.Add(asset);
        }

        return result;
    }

    [MenuItem("Tools/RB/Skills/Validate Status Effect Scopes")]
    public static void ValidateProject()
    {
        List<SkillUpgradeValidationIssue> issues = Validate();
        int errors = 0;
        int warnings = 0;
        for (int i = 0; i < issues.Count; i++)
        {
            SkillUpgradeValidationIssue issue = issues[i];
            if (issue.Severity == SkillUpgradeValidationSeverity.Error)
            {
                errors++;
                Debug.LogError($"[StatusScope] {issue.Message}");
            }
            else
            {
                warnings++;
                Debug.LogWarning($"[StatusScope] {issue.Message}");
            }
        }

        Debug.Log($"[StatusScope] Validation complete: {errors} errors, {warnings} warnings.");
    }

    static IEnumerable<UnityEngine.Object> EnumerateAssetObjects(SkillDefinitionBase owner)
    {
        string path = AssetDatabase.GetAssetPath(owner);
        if (string.IsNullOrEmpty(path))
        {
            yield return owner;
            yield break;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] != null)
                yield return assets[i];
        }
    }

    static SkillUpgradeValidationIssue Error(string message)
        => new(SkillUpgradeValidationSeverity.Error, message);

    static SkillUpgradeValidationIssue Warning(string message)
        => new(SkillUpgradeValidationSeverity.Warning, message);
}
#endif
