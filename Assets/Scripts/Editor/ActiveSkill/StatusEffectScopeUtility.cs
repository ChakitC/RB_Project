#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scope ของ <see cref="StatusEffectDef"/> ที่ใช้ตัดสินว่าสกิลไหนหยิบ Def ตัวนั้นมาใช้ได้บ้าง.
/// </summary>
public enum StatusEffectScope
{
    /// <summary>ยังไม่ได้ติด label — ใช้ได้ทุกสกิลแต่ validator จะเตือน.</summary>
    Legacy,

    /// <summary>ใช้ได้ทุกสกิล.</summary>
    Global,

    /// <summary>ใช้ได้เฉพาะสกิลเจ้าของ (อ่านจาก StatusOwner.&lt;GUID&gt;).</summary>
    Unique,
}

/// <summary>
/// Scope ถูกเก็บเป็น Asset Label ไม่ใช่ field บน <see cref="StatusEffectDef"/> — เพราะเป็นข้อมูล authoring
/// ล้วนๆ (ไม่มีใครอ่านตอนรัน) และการเพิ่ม serialized field ลงบน Def จะบังคับให้ status asset เดิมทุกตัว
/// re-serialize. Label ยังค้นได้ตรงๆ ผ่าน AssetDatabase.FindAssets("l:...") ด้วย.
///
/// Label ที่ใช้:
/// <list type="bullet">
/// <item><c>StatusScope.Global</c> — ใช้ได้ทุกสกิล</item>
/// <item><c>StatusScope.Unique</c> — ใช้ได้เฉพาะเจ้าของ</item>
/// <item><c>StatusOwner.&lt;Skill GUID&gt;</c> — ระบุเจ้าของของ Unique</item>
/// </list>
/// </summary>
public static class StatusEffectScopeUtility
{
    public const string GlobalLabel = "StatusScope.Global";
    public const string UniqueLabel = "StatusScope.Unique";
    public const string OwnerLabelPrefix = "StatusOwner.";

    public const string GlobalBuffFolder = "Assets/Data/StatusEffects/Buffs";
    public const string GlobalDebuffFolder = "Assets/Data/StatusEffects/Debuffs";
    public const string GlobalControlFolder = "Assets/Data/StatusEffects/Control";
    public const string UniqueRootFolder = "Assets/Data/StatusEffects/Unique";

    /// <summary>Scope ที่ label ประกาศไว้. Def ที่ติดทั้งสอง label ถือเป็น Unique (แคบกว่าเสมอชนะ).</summary>
    public static StatusEffectScope GetScope(StatusEffectDef definition)
    {
        string[] labels = GetLabels(definition);
        if (labels == null)
            return StatusEffectScope.Legacy;

        for (int i = 0; i < labels.Length; i++)
        {
            if (string.Equals(labels[i], UniqueLabel, StringComparison.Ordinal))
                return StatusEffectScope.Unique;
        }

        for (int i = 0; i < labels.Length; i++)
        {
            if (string.Equals(labels[i], GlobalLabel, StringComparison.Ordinal))
                return StatusEffectScope.Global;
        }

        return StatusEffectScope.Legacy;
    }

    /// <summary>GUID ของสกิลเจ้าของ (Unique เท่านั้น), หรือ null.</summary>
    public static string GetOwnerGuid(StatusEffectDef definition)
    {
        string[] labels = GetLabels(definition);
        if (labels == null)
            return null;

        for (int i = 0; i < labels.Length; i++)
        {
            string label = labels[i];
            if (label != null && label.StartsWith(OwnerLabelPrefix, StringComparison.Ordinal))
            {
                string guid = label.Substring(OwnerLabelPrefix.Length).Trim();
                return string.IsNullOrEmpty(guid) ? null : guid;
            }
        }

        return null;
    }

    public static SkillDefinitionBase GetOwner(StatusEffectDef definition)
    {
        string guid = GetOwnerGuid(definition);
        if (string.IsNullOrEmpty(guid))
            return null;

        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<SkillDefinitionBase>(path);
    }

    public static string GetAssetGuid(UnityEngine.Object asset)
    {
        if (asset == null)
            return null;

        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path))
            return null;

        string guid = AssetDatabase.AssetPathToGUID(path);
        return string.IsNullOrEmpty(guid) ? null : guid;
    }

    /// <summary>เขียน label ใหม่ทับ scope เดิมทั้งชุด (ทั้ง scope และ owner) โดยไม่แตะ label อื่นของทีม.</summary>
    public static void SetScope(StatusEffectDef definition, StatusEffectScope scope, SkillDefinitionBase owner)
    {
        if (definition == null)
            return;

        var labels = new List<string>();
        string[] existing = GetLabels(definition);
        if (existing != null)
        {
            for (int i = 0; i < existing.Length; i++)
            {
                string label = existing[i];
                if (string.IsNullOrEmpty(label))
                    continue;
                if (string.Equals(label, GlobalLabel, StringComparison.Ordinal))
                    continue;
                if (string.Equals(label, UniqueLabel, StringComparison.Ordinal))
                    continue;
                if (label.StartsWith(OwnerLabelPrefix, StringComparison.Ordinal))
                    continue;

                labels.Add(label);
            }
        }

        switch (scope)
        {
            case StatusEffectScope.Global:
                labels.Add(GlobalLabel);
                break;
            case StatusEffectScope.Unique:
                labels.Add(UniqueLabel);
                string ownerGuid = GetAssetGuid(owner);
                if (!string.IsNullOrEmpty(ownerGuid))
                    labels.Add(OwnerLabelPrefix + ownerGuid);
                break;
        }

        AssetDatabase.SetLabels(definition, labels.ToArray());
        EditorUtility.SetDirty(definition);
    }

    /// <summary>
    /// สกิลนี้ใช้ Def ตัวนี้ได้ไหม. <paramref name="reason"/> ว่าง = ใช้ได้สนิท, มีข้อความแต่ return true = ใช้ได้แต่ควรเตือน.
    /// </summary>
    public static bool IsUsableBy(StatusEffectDef definition, SkillDefinitionBase skill, out string reason)
    {
        reason = null;
        if (definition == null)
        {
            reason = "No Status Effect Def selected.";
            return false;
        }

        switch (GetScope(definition))
        {
            case StatusEffectScope.Global:
                return true;

            case StatusEffectScope.Unique:
                string ownerGuid = GetOwnerGuid(definition);
                string skillGuid = GetAssetGuid(skill);
                if (string.IsNullOrEmpty(ownerGuid))
                {
                    reason =
                        $"'{definition.name}' is labelled {UniqueLabel} but carries no {OwnerLabelPrefix}<guid> " +
                        "label, so no skill can claim it.";
                    return false;
                }

                if (!string.Equals(ownerGuid, skillGuid, StringComparison.Ordinal))
                {
                    SkillDefinitionBase owner = GetOwner(definition);
                    string ownerName = owner != null ? owner.name : ownerGuid;
                    reason =
                        $"'{definition.name}' is unique to '{ownerName}' and cannot be used by " +
                        $"'{(skill != null ? skill.name : "<no skill>")}'. Duplicate it as unique, or promote it to Global.";
                    return false;
                }

                return true;

            default:
                reason =
                    $"'{definition.name}' has no scope label yet (legacy asset). Label it {GlobalLabel} or " +
                    $"{UniqueLabel} so cross-skill reuse stays intentional.";
                return true;
        }
    }

    /// <summary>Def ทุกตัวในโปรเจกต์ที่สกิลนี้เลือกได้ (Global + Legacy + Unique ของตัวเอง).</summary>
    public static List<StatusEffectDef> FindSelectableEffects(SkillDefinitionBase skill)
    {
        var result = new List<StatusEffectDef>();
        string[] guids = AssetDatabase.FindAssets("t:StatusEffectDef");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var definition = AssetDatabase.LoadAssetAtPath<StatusEffectDef>(path);
            if (definition == null)
                continue;

            if (IsUsableBy(definition, skill, out _))
                result.Add(definition);
        }

        result.Sort((first, second) => string.Compare(first.name, second.name, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    /// <summary>Def ตัวอื่นที่ใช้ effectId เดียวกัน — effectId ต้องไม่ซ้ำทั้งโปรเจกต์.</summary>
    public static List<StatusEffectDef> FindEffectsWithId(string effectId, StatusEffectDef ignore)
    {
        var result = new List<StatusEffectDef>();
        if (string.IsNullOrWhiteSpace(effectId))
            return result;

        string trimmed = effectId.Trim();
        string[] guids = AssetDatabase.FindAssets("t:StatusEffectDef");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var definition = AssetDatabase.LoadAssetAtPath<StatusEffectDef>(path);
            if (definition == null || ReferenceEquals(definition, ignore))
                continue;

            if (string.Equals(definition.effectId?.Trim(), trimmed, StringComparison.Ordinal))
                result.Add(definition);
        }

        return result;
    }

    public static string GetDefaultFolder(
        StatusEffectScope scope,
        StatusEffectCategory category,
        SkillDefinitionBase owner)
    {
        if (scope == StatusEffectScope.Unique)
        {
            string skillName = owner != null ? SanitizeFolderName(owner.name) : "Unassigned";
            return $"{UniqueRootFolder}/{skillName}";
        }

        switch (category)
        {
            case StatusEffectCategory.Buff:
                return GlobalBuffFolder;
            case StatusEffectCategory.Debuff:
                return GlobalDebuffFolder;
            default:
                return GlobalControlFolder;
        }
    }

    /// <summary>สร้างโฟลเดอร์ทีละชั้นจนครบ path (AssetDatabase.CreateFolder สร้างได้ทีละชั้นเท่านั้น).</summary>
    public static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts = folderPath.Replace('\\', '/').Split('/');
        if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.Ordinal))
            throw new InvalidOperationException($"Status effect folder '{folderPath}' must live under Assets/.");

        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    public static string SanitizeFolderName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Unnamed";

        char[] invalid = System.IO.Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            builder.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }

        return builder.ToString().Trim();
    }

    public static string DescribeScope(StatusEffectDef definition)
    {
        switch (GetScope(definition))
        {
            case StatusEffectScope.Global:
                return "Global";
            case StatusEffectScope.Unique:
                SkillDefinitionBase owner = GetOwner(definition);
                return owner != null ? $"Unique to {owner.name}" : "Unique (owner missing)";
            default:
                return "Legacy (unlabelled)";
        }
    }

    // In-memory assets (smoke tests) have no path, so GetLabels would throw/return nothing useful.
    static string[] GetLabels(StatusEffectDef definition)
    {
        if (definition == null)
            return null;

        return string.IsNullOrEmpty(AssetDatabase.GetAssetPath(definition))
            ? Array.Empty<string>()
            : AssetDatabase.GetLabels(definition);
    }
}
#endif
