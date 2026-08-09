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

    public static HashSet<SkillPayloadDef> GetReachablePayloads(
        SkillPayloadDef root,
        List<SkillPayloadDef> duplicateReferences = null)
    {
        var reachable = new HashSet<SkillPayloadDef>();
        CollectReachablePayloads(root, reachable, duplicateReferences);
        return reachable;
    }

    private static void CollectReachablePayloads(
        SkillPayloadDef candidate,
        HashSet<SkillPayloadDef> reachable,
        List<SkillPayloadDef> duplicateReferences)
    {
        if (candidate == null)
            return;

        if (!reachable.Add(candidate))
        {
            if (duplicateReferences != null && !duplicateReferences.Contains(candidate))
                duplicateReferences.Add(candidate);
            return;
        }

        if (candidate is not CompositeSkillPayloadDef composite)
            return;

        IReadOnlyList<SkillEffectStep> steps = composite.Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] is PayloadStep payloadStep)
                CollectReachablePayloads(payloadStep.Payload, reachable, duplicateReferences);
        }
    }

    private static List<SkillPayloadDef> GetEmbeddedPayloadGraphPostOrder(
        SkillGemDefinition skill,
        SkillPayloadDef root)
    {
        var ordered = new List<SkillPayloadDef>();
        if (!IsEmbedded(skill, root))
            return ordered;

        CollectPayloadGraphPostOrder(skill, root, new HashSet<SkillPayloadDef>(), ordered);
        return ordered;
    }

    private static void CollectPayloadGraphPostOrder(
        SkillGemDefinition skill,
        SkillPayloadDef candidate,
        HashSet<SkillPayloadDef> visited,
        List<SkillPayloadDef> ordered)
    {
        if (candidate == null || !visited.Add(candidate))
            return;

        if (candidate is CompositeSkillPayloadDef composite)
        {
            IReadOnlyList<SkillEffectStep> steps = composite.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] is PayloadStep payloadStep)
                    CollectPayloadGraphPostOrder(skill, payloadStep.Payload, visited, ordered);
            }
        }

        if (IsEmbedded(skill, candidate))
            ordered.Add(candidate);
    }

    public static bool IsReferencedByComposite(
        CompositeSkillPayloadDef composite,
        SkillPayloadDef payload)
    {
        if (composite == null || payload == null)
            return false;

        IReadOnlyList<SkillEffectStep> steps = composite.Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] is PayloadStep payloadStep && payloadStep.Payload == payload)
                return true;
        }

        return false;
    }

    private static void DestroyEmbeddedPayloads(List<SkillPayloadDef> payloads, bool recordUndo)
    {
        for (int i = 0; i < payloads.Count; i++)
        {
            SkillPayloadDef payload = payloads[i];
            if (payload == null)
                continue;

            if (recordUndo)
                Undo.DestroyObjectImmediate(payload);
            else
                UnityEngine.Object.DestroyImmediate(payload, true);
        }
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
        List<SkillPayloadDef> previousPayloadGraph =
            GetEmbeddedPayloadGraphPostOrder(skill, previousPayload);
        if (recordUndo)
            Undo.RegisterCompleteObjectUndo(skill, "Replace Skill Execution");

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

        if (previousPayload != newPayload)
            DestroyEmbeddedPayloads(previousPayloadGraph, recordUndo);

        return newPayload;
    }

    public static void RemoveExecution(SkillGemDefinition skill, bool recordUndo = true)
    {
        if (skill == null || skill.payload == null)
            return;

        SkillPayloadDef previousPayload = skill.payload;
        List<SkillPayloadDef> previousPayloadGraph =
            GetEmbeddedPayloadGraphPostOrder(skill, previousPayload);

        if (recordUndo)
            Undo.RegisterCompleteObjectUndo(skill, "Remove Skill Execution");

        skill.payload = null;
        EditorUtility.SetDirty(skill);

        DestroyEmbeddedPayloads(previousPayloadGraph, recordUndo);
    }

    // Moved from SkillGemDefinitionEditor so the skill Inspector and Active Skill Tree Editor can
    // share the same ownership mutation. Callers own Save/Discard; this utility only records Undo
    // and marks the affected skill asset dirty.
    public static SkillPayloadDef CreateEmbeddedStepPayload(
        SkillGemDefinition skill,
        CompositeSkillPayloadDef composite,
        int stepIndex,
        Type payloadType,
        SkillPayloadDef previousWrapped)
    {
        if (skill == null || composite == null || payloadType == null)
            return null;

        string skillPath = AssetDatabase.GetAssetPath(skill);
        if (string.IsNullOrEmpty(skillPath))
            return null;

        Undo.RecordObject(composite, "Replace Composite Step Payload");

        var newPayload = ScriptableObject.CreateInstance(payloadType) as SkillPayloadDef;
        if (newPayload == null)
            return null;

        newPayload.name = $"{GetPayloadDisplayName(payloadType)} Step Execution";
        newPayload.hideFlags = HideFlags.None;

        Undo.RegisterCreatedObjectUndo(newPayload, "Create Composite Step Payload");
        AssetDatabase.AddObjectToAsset(newPayload, skill);
        composite.SetStepPayload(stepIndex, newPayload);
        EditorUtility.SetDirty(newPayload);
        EditorUtility.SetDirty(composite);
        EditorUtility.SetDirty(skill);

        if (previousWrapped != null &&
            previousWrapped != newPayload &&
            IsEmbedded(skill, previousWrapped) &&
            !IsReferencedByComposite(composite, previousWrapped))
        {
            Undo.DestroyObjectImmediate(previousWrapped);
        }

        return newPayload;
    }

    public static void RemoveEmbeddedStepPayload(
        SkillGemDefinition skill,
        CompositeSkillPayloadDef composite,
        int stepIndex,
        SkillPayloadDef wrapped)
    {
        if (skill == null || composite == null)
            return;

        Undo.RecordObject(composite, "Remove Composite Step Payload");
        composite.SetStepPayload(stepIndex, null);
        EditorUtility.SetDirty(composite);
        EditorUtility.SetDirty(skill);

        if (wrapped != null &&
            IsEmbedded(skill, wrapped) &&
            !IsReferencedByComposite(composite, wrapped))
        {
            Undo.DestroyObjectImmediate(wrapped);
        }
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
