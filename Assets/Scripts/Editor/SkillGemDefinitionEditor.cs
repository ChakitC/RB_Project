#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillGemDefinition))]
public sealed class SkillGemDefinitionEditor : OdinEditor
{
    private UnityEditor.Editor payloadEditor;
    private SkillPayloadDef cachedPayload;

    protected override void OnDisable()
    {
        DestroyPayloadEditor();
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
