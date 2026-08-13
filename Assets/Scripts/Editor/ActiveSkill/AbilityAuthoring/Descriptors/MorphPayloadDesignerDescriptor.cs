using System.Collections.Generic;
using UnityEditor;

// Descriptor for MorphSkillPayloadDef. See PayloadDesignerDescriptorBase for the contract.
internal sealed class MorphPayloadDesignerDescriptor : PayloadDesignerDescriptorBase<MorphSkillPayloadDef>
{
    public override string DisplayName => "Morph Self";
    public override string Description => "Temporarily changes the caster's model, animation set, and/or status.";
    public override string Category => "Transformation";

    protected override void ApplySafeDefaults(MorphSkillPayloadDef payload, PayloadDesignerContext context)
    {
        // Change Mode / Duration already default to reasonable values. Model prefab and anim
        // profile are required author choices and are intentionally left blank.
    }

    protected override void DrawWizard(MorphSkillPayloadDef payload, PayloadDesignerContext context)
    {
        var serialized = new SerializedObject(payload);
        serialized.Update();

        EditorGUILayout.PropertyField(serialized.FindProperty("changeMode"));
        EditorGUILayout.PropertyField(serialized.FindProperty("duration"));

        if (payload.ChangesModel)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Model", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serialized.FindProperty("morphModelPrefab"));
            EditorGUILayout.PropertyField(serialized.FindProperty("morphController"));
            EditorGUILayout.PropertyField(serialized.FindProperty("morphAvatar"));
        }

        if (payload.ChangesAnimation)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serialized.FindProperty("morphAnimProfile"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Status Effects (while morphed)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("statusApplications"), true);

        serialized.ApplyModifiedProperties();
    }

    protected override PayloadGameplaySummary BuildSummary(MorphSkillPayloadDef payload, PayloadDesignerContext context)
    {
        var changeParts = new List<string>();
        if (payload.ChangesModel)
            changeParts.Add(payload.MorphModelPrefab != null ? $"model '{payload.MorphModelPrefab.name}'" : "model");
        if (payload.ChangesAnimation)
            changeParts.Add(payload.MorphAnimProfile != null ? $"animation set '{payload.MorphAnimProfile.name}'" : "animation set");

        string changeDescription = changeParts.Count > 0 ? string.Join(" and ", changeParts) : "nothing visual";
        var summary = PayloadGameplaySummary.Of($"Morphs the caster's {changeDescription} for {payload.Duration:0.#}s.");

        if (payload.ChangesModel && payload.MorphModelPrefab == null)
            summary.AddWarning("Change Mode includes Model, but no Morph Model Prefab is assigned.");
        if (payload.ChangesAnimation && payload.MorphAnimProfile == null)
            summary.AddWarning("Change Mode includes Animation, but no Morph Anim Profile is assigned.");

        if (payload.ChangesStatus)
            summary.AddDetail($"Applies {payload.StatusApplications.Count} status effect(s) while morphed.");

        return summary;
    }

    protected override void CollectAuthoringIssues(
        MorphSkillPayloadDef payload,
        PayloadDesignerContext context,
        List<PayloadAuthoringIssue> issues)
    {
        CollectRuntimeValidationIssuesAsErrors(payload, issues);
    }
}
