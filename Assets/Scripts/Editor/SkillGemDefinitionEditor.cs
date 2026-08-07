#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillGemDefinition))]
public sealed class SkillGemDefinitionEditor : OdinEditor
{
    private UnityEditor.Editor payloadEditor;
    private SkillPayloadDef cachedPayload;
    private readonly Dictionary<SkillPayloadDef, UnityEditor.Editor> stepPayloadEditors = new();

    protected override void OnDisable()
    {
        DestroyPayloadEditor();
        DestroyStepPayloadEditors();
        base.OnDisable();
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (targets.Length != 1 || target is not SkillGemDefinition skill)
        {
            EditorGUILayout.HelpBox("Execution authoring is available when one skill asset is selected.", MessageType.Info);
            return;
        }

        DrawExecutionAuthoring(skill);
    }

    private void DrawExecutionAuthoring(SkillGemDefinition skill)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Execution Authoring", EditorStyles.boldLabel);

        IReadOnlyList<Type> payloadTypes = SkillPayloadAssetUtility.GetPayloadTypes();
        if (payloadTypes.Count == 0)
        {
            EditorGUILayout.HelpBox("No concrete SkillPayloadDef types were found.", MessageType.Error);
            return;
        }

        string ownershipLabel;
        MessageType ownershipMessageType;
        if (skill.payload == null)
        {
            ownershipLabel = "This skill has no execution payload.";
            ownershipMessageType = MessageType.Error;
        }
        else if (!SkillPayloadAssetUtility.IsEmbedded(skill, skill.payload))
        {
            ownershipLabel = "External execution payloads are unsupported. Remove it and create an embedded execution.";
            ownershipMessageType = MessageType.Error;
        }
        else
        {
            ownershipLabel = "The execution payload is embedded and owned by this skill asset.";
            ownershipMessageType = MessageType.Info;
        }

        EditorGUILayout.HelpBox(ownershipLabel, ownershipMessageType);

        string[] displayNames = new string[payloadTypes.Count];
        int selectedIndex = 0;
        for (int i = 0; i < payloadTypes.Count; i++)
        {
            displayNames[i] = SkillPayloadAssetUtility.GetPayloadDisplayName(payloadTypes[i]);
            if (skill.payload != null && skill.payload.GetType() == payloadTypes[i])
                selectedIndex = i;
        }

        EditorGUI.BeginChangeCheck();
        int nextIndex = EditorGUILayout.Popup("Execution Type", selectedIndex, displayNames);
        if (EditorGUI.EndChangeCheck())
        {
            Type nextType = payloadTypes[Mathf.Clamp(nextIndex, 0, payloadTypes.Count - 1)];
            if (skill.payload == null || ConfirmPayloadReplacement(skill.payload, nextType))
            {
                SkillPayloadAssetUtility.ReplaceWithEmbedded(skill, nextType);
                RefreshPayloadEditor(skill.payload);
                GUIUtility.ExitGUI();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (skill.payload == null)
            {
                if (GUILayout.Button("Create Execution"))
                {
                    SkillPayloadAssetUtility.ReplaceWithEmbedded(skill, payloadTypes[selectedIndex]);
                    RefreshPayloadEditor(skill.payload);
                    GUIUtility.ExitGUI();
                }
            }
            using (new EditorGUI.DisabledScope(skill.payload == null))
            {
                if (GUILayout.Button("Remove Execution"))
                {
                    if (EditorUtility.DisplayDialog(
                            "Remove Skill Execution",
                            $"Remove the execution payload from '{skill.name}'? Embedded execution data will be deleted.",
                            "Remove",
                            "Cancel"))
                    {
                        SkillPayloadAssetUtility.RemoveExecution(skill);
                        RefreshPayloadEditor(null);
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        DrawPayloadValidation(skill.payload);
        DrawEmbeddedPayloadInspector(skill);
        DrawCompositeStepAuthoring(skill);
    }

    private void DrawPayloadValidation(SkillPayloadDef payload)
    {
        if (payload == null)
            return;

        var issues = new List<string>();
        payload.CollectValidationIssues(issues);
        if (issues.Count == 0)
            return;

        EditorGUILayout.HelpBox(string.Join("\n", issues), MessageType.Error);
    }

    private void DrawEmbeddedPayloadInspector(SkillGemDefinition skill)
    {
        if (skill.payload == null || !SkillPayloadAssetUtility.IsEmbedded(skill, skill.payload))
            return;

        RefreshPayloadEditor(skill.payload);
        if (payloadEditor == null)
            return;

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        payloadEditor.OnInspectorGUI();
        EditorGUILayout.EndVertical();
    }

    private void RefreshPayloadEditor(SkillPayloadDef payload)
    {
        if (cachedPayload == payload && payloadEditor != null)
            return;

        DestroyPayloadEditor();
        cachedPayload = payload;
        if (cachedPayload != null)
            payloadEditor = CreateEditor(cachedPayload);
    }

    private void DestroyPayloadEditor()
    {
        if (payloadEditor != null)
            DestroyImmediate(payloadEditor);

        payloadEditor = null;
        cachedPayload = null;
    }

    private void DrawCompositeStepAuthoring(SkillGemDefinition skill)
    {
        if (skill.payload is not CompositeSkillPayloadDef composite)
        {
            DestroyStepPayloadEditors();
            return;
        }

        IReadOnlyList<SkillEffectStep> steps = composite.Steps;
        List<Type> stepPayloadTypes = SkillPayloadAssetUtility.GetPayloadTypes()
            .Where(t => t != typeof(CompositeSkillPayloadDef))
            .ToList();

        var liveWrappedPayloads = new HashSet<SkillPayloadDef>();
        bool hasPayloadStep = false;
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] is PayloadStep)
            {
                hasPayloadStep = true;
                break;
            }
        }

        if (hasPayloadStep)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Composite Step Payloads", EditorStyles.boldLabel);
        }

        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] is not PayloadStep payloadStep)
                continue;

            SkillPayloadDef wrapped = payloadStep.Payload;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Step {i}: Payload", EditorStyles.miniBoldLabel);

            if (stepPayloadTypes.Count == 0)
            {
                EditorGUILayout.HelpBox("No concrete SkillPayloadDef types were found.", MessageType.Error);
                EditorGUILayout.EndVertical();
                continue;
            }

            string[] displayNames = new string[stepPayloadTypes.Count];
            int selectedIndex = -1;
            for (int t = 0; t < stepPayloadTypes.Count; t++)
            {
                displayNames[t] = SkillPayloadAssetUtility.GetPayloadDisplayName(stepPayloadTypes[t]);
                if (wrapped != null && wrapped.GetType() == stepPayloadTypes[t])
                    selectedIndex = t;
            }

            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUILayout.Popup("Payload Type", Mathf.Max(selectedIndex, 0), displayNames);
            if (EditorGUI.EndChangeCheck())
            {
                Type nextType = stepPayloadTypes[nextIndex];
                if (wrapped == null || ConfirmPayloadReplacement(wrapped, nextType))
                {
                    SkillPayloadDef newWrapped = CreateEmbeddedStepPayload(skill, composite, i, nextType, wrapped);
                    RefreshStepPayloadEditor(newWrapped);
                    GUIUtility.ExitGUI();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (wrapped == null)
                {
                    if (GUILayout.Button("Create Step Payload"))
                    {
                        Type createType = stepPayloadTypes[Mathf.Max(selectedIndex, 0)];
                        SkillPayloadDef newWrapped = CreateEmbeddedStepPayload(skill, composite, i, createType, null);
                        RefreshStepPayloadEditor(newWrapped);
                        GUIUtility.ExitGUI();
                    }
                }

                using (new EditorGUI.DisabledScope(wrapped == null))
                {
                    if (GUILayout.Button("Remove Step Payload"))
                    {
                        if (EditorUtility.DisplayDialog(
                                "Remove Step Payload",
                                $"Remove the embedded payload from step {i}?",
                                "Remove",
                                "Cancel"))
                        {
                            RemoveEmbeddedStepPayload(skill, composite, i, wrapped);
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }

            if (wrapped != null)
            {
                liveWrappedPayloads.Add(wrapped);

                var issues = new List<string>();
                wrapped.CollectValidationIssues(issues);
                if (issues.Count > 0)
                    EditorGUILayout.HelpBox(string.Join("\n", issues), MessageType.Error);

                UnityEditor.Editor nestedEditor = RefreshStepPayloadEditor(wrapped);
                if (nestedEditor != null)
                {
                    EditorGUILayout.Space(2f);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    nestedEditor.OnInspectorGUI();
                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndVertical();
        }

        PruneStepPayloadEditors(liveWrappedPayloads);
    }

    private SkillPayloadDef CreateEmbeddedStepPayload(
        SkillGemDefinition skill,
        CompositeSkillPayloadDef composite,
        int stepIndex,
        Type payloadType,
        SkillPayloadDef previousWrapped)
    {
        string skillPath = AssetDatabase.GetAssetPath(skill);
        if (string.IsNullOrEmpty(skillPath))
            return null;

        Undo.RecordObject(composite, "Replace Composite Step Payload");

        var newPayload = ScriptableObject.CreateInstance(payloadType) as SkillPayloadDef;
        if (newPayload == null)
            return null;

        newPayload.name = $"{SkillPayloadAssetUtility.GetPayloadDisplayName(payloadType)} Step Execution";
        newPayload.hideFlags = HideFlags.None;

        Undo.RegisterCreatedObjectUndo(newPayload, "Create Composite Step Payload");
        AssetDatabase.AddObjectToAsset(newPayload, skill);
        composite.SetStepPayload(stepIndex, newPayload);
        EditorUtility.SetDirty(newPayload);
        EditorUtility.SetDirty(composite);

        if (previousWrapped != null &&
            previousWrapped != newPayload &&
            SkillPayloadAssetUtility.IsEmbedded(skill, previousWrapped))
        {
            Undo.DestroyObjectImmediate(previousWrapped);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(skillPath, ImportAssetOptions.ForceUpdate);
        return newPayload;
    }

    private void RemoveEmbeddedStepPayload(
        SkillGemDefinition skill,
        CompositeSkillPayloadDef composite,
        int stepIndex,
        SkillPayloadDef wrapped)
    {
        string skillPath = AssetDatabase.GetAssetPath(skill);

        Undo.RecordObject(composite, "Remove Composite Step Payload");
        composite.SetStepPayload(stepIndex, null);
        EditorUtility.SetDirty(composite);

        if (wrapped != null && SkillPayloadAssetUtility.IsEmbedded(skill, wrapped))
            Undo.DestroyObjectImmediate(wrapped);

        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(skillPath))
            AssetDatabase.ImportAsset(skillPath, ImportAssetOptions.ForceUpdate);
    }

    private UnityEditor.Editor RefreshStepPayloadEditor(SkillPayloadDef payload)
    {
        if (payload == null)
            return null;

        if (stepPayloadEditors.TryGetValue(payload, out UnityEditor.Editor existing) && existing != null)
            return existing;

        UnityEditor.Editor created = CreateEditor(payload);
        stepPayloadEditors[payload] = created;
        return created;
    }

    private void PruneStepPayloadEditors(HashSet<SkillPayloadDef> live)
    {
        List<SkillPayloadDef> stale = null;
        foreach (KeyValuePair<SkillPayloadDef, UnityEditor.Editor> kv in stepPayloadEditors)
        {
            if (live.Contains(kv.Key))
                continue;

            stale ??= new List<SkillPayloadDef>();
            stale.Add(kv.Key);
        }

        if (stale == null)
            return;

        for (int i = 0; i < stale.Count; i++)
        {
            SkillPayloadDef key = stale[i];
            if (stepPayloadEditors[key] != null)
                DestroyImmediate(stepPayloadEditors[key]);
            stepPayloadEditors.Remove(key);
        }
    }

    private void DestroyStepPayloadEditors()
    {
        foreach (UnityEditor.Editor editor in stepPayloadEditors.Values)
        {
            if (editor != null)
                DestroyImmediate(editor);
        }

        stepPayloadEditors.Clear();
    }

    private static bool ConfirmPayloadReplacement(SkillPayloadDef currentPayload, Type nextType)
    {
        if (currentPayload == null || currentPayload.GetType() == nextType)
            return false;

        return EditorUtility.DisplayDialog(
            "Change Skill Execution",
            $"Replace '{SkillPayloadAssetUtility.GetPayloadDisplayName(currentPayload.GetType())}' with " +
            $"'{SkillPayloadAssetUtility.GetPayloadDisplayName(nextType)}'? The current embedded execution data will be deleted.",
            "Replace",
            "Cancel");
    }
}
#endif
