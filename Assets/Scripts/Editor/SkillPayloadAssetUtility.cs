#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

internal static class SkillPayloadAssetUtility
{
    private static List<Type> cachedPayloadTypes;

    public static IReadOnlyList<Type> GetPayloadTypes()
    {
        if (cachedPayloadTypes != null)
            return cachedPayloadTypes;

        cachedPayloadTypes = TypeCache.GetTypesDerivedFrom<SkillPayloadDef>()
            .Where(type => type != null && !type.IsAbstract && !type.IsGenericTypeDefinition)
            .OrderBy(GetPayloadTypePriority)
            .ThenBy(GetPayloadDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return cachedPayloadTypes;
    }

    public static string GetPayloadDisplayName(Type payloadType)
    {
        if (payloadType == null)
            return "None";

        string typeName = payloadType.Name;
        string[] suffixes = { "SkillPayloadDef", "PayloadDef", "Definition", "Def" };
        for (int i = 0; i < suffixes.Length; i++)
        {
            string suffix = suffixes[i];
            if (!typeName.EndsWith(suffix, StringComparison.Ordinal))
                continue;

            typeName = typeName.Substring(0, typeName.Length - suffix.Length);
            break;
        }

        var builder = new StringBuilder(typeName.Length + 8);
        for (int i = 0; i < typeName.Length; i++)
        {
            char current = typeName[i];
            if (i > 0 && char.IsUpper(current) && !char.IsWhiteSpace(typeName[i - 1]))
                builder.Append(' ');

            builder.Append(current);
        }

        return builder.Length > 0 ? builder.ToString() : payloadType.Name;
    }

    public static bool IsEmbedded(SkillGemDefinition skill, SkillPayloadDef payload)
    {
        if (skill == null || payload == null || !AssetDatabase.IsSubAsset(payload))
            return false;

        string skillPath = AssetDatabase.GetAssetPath(skill);
        return !string.IsNullOrEmpty(skillPath) &&
               string.Equals(skillPath, AssetDatabase.GetAssetPath(payload), StringComparison.OrdinalIgnoreCase);
    }

    public static List<SkillPayloadDef> GetEmbeddedPayloads(SkillGemDefinition skill)
    {
        var result = new List<SkillPayloadDef>();
        if (skill == null)
            return result;

        string skillPath = AssetDatabase.GetAssetPath(skill);
        if (string.IsNullOrEmpty(skillPath))
            return result;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(skillPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is SkillPayloadDef embeddedPayload)
                result.Add(embeddedPayload);
        }

        return result;
    }

    public static SkillPayloadDef ReplaceWithEmbedded(
        SkillGemDefinition skill,
        Type payloadType,
        bool recordUndo = true)
    {
        if (skill == null)
            throw new ArgumentNullException(nameof(skill));
        if (payloadType == null || payloadType.IsAbstract || !typeof(SkillPayloadDef).IsAssignableFrom(payloadType))
            throw new ArgumentException("Payload type must be a concrete SkillPayloadDef.", nameof(payloadType));

        string skillPath = AssetDatabase.GetAssetPath(skill);
        if (string.IsNullOrEmpty(skillPath))
            throw new InvalidOperationException("Save the SkillGemDefinition as an asset before creating its execution payload.");

        SkillPayloadDef previousPayload = skill.payload;
        if (recordUndo)
            Undo.RecordObject(skill, "Replace Skill Execution");

        var newPayload = ScriptableObject.CreateInstance(payloadType) as SkillPayloadDef;
        if (newPayload == null)
            throw new InvalidOperationException($"Could not create payload type '{payloadType.FullName}'.");

        newPayload.name = $"{GetPayloadDisplayName(payloadType)} Execution";
        newPayload.hideFlags = HideFlags.None;

        if (recordUndo)
            Undo.RegisterCreatedObjectUndo(newPayload, "Create Skill Execution");

        AssetDatabase.AddObjectToAsset(newPayload, skill);
        skill.payload = newPayload;
        EditorUtility.SetDirty(newPayload);
        EditorUtility.SetDirty(skill);

        if (previousPayload != null &&
            previousPayload != newPayload &&
            IsEmbedded(skill, previousPayload))
        {
            if (recordUndo)
                Undo.DestroyObjectImmediate(previousPayload);
            else
                UnityEngine.Object.DestroyImmediate(previousPayload, true);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(skillPath, ImportAssetOptions.ForceUpdate);
        return newPayload;
    }

    public static void RemoveExecution(SkillGemDefinition skill, bool recordUndo = true)
    {
        if (skill == null || skill.payload == null)
            return;

        SkillPayloadDef previousPayload = skill.payload;
        bool wasEmbedded = IsEmbedded(skill, previousPayload);

        if (recordUndo)
            Undo.RecordObject(skill, "Remove Skill Execution");

        skill.payload = null;
        EditorUtility.SetDirty(skill);

        if (wasEmbedded)
        {
            if (recordUndo)
                Undo.DestroyObjectImmediate(previousPayload);
            else
                UnityEngine.Object.DestroyImmediate(previousPayload, true);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(skill), ImportAssetOptions.ForceUpdate);
    }

    private static int GetPayloadTypePriority(Type payloadType)
    {
        if (payloadType == typeof(ProjectileSkillPayloadDef)) return 0;
        if (payloadType == typeof(PrefabHitboxSkillPayloadDef)) return 1;
        if (payloadType == typeof(ApplyStatusSkillPayloadDef)) return 2;
        if (payloadType == typeof(SpawnPickupSkillPayloadDef)) return 3;
        return 100;
    }
}
#endif
