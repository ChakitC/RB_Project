using System.Collections.Generic;
using UnityEditor;

// Descriptor for PrefabHitboxSkillPayloadDef. See PayloadDesignerDescriptorBase for the contract.
internal sealed class PrefabHitboxPayloadDesignerDescriptor : PayloadDesignerDescriptorBase<PrefabHitboxSkillPayloadDef>
{
    public override string DisplayName => "Melee Hitbox Sequence";
    public override string Description =>
        "Drives a sequence of inline hitbox groups from Hit Start/Hit End timeline events.";
    public override string Category => "Damage";

    protected override void ApplySafeDefaults(PrefabHitboxSkillPayloadDef payload, PayloadDesignerContext context)
    {
        // Timeline event names and anchor mode already default to usable values. The inline
        // hitbox layout (at least one group with a group key) and at least one hitbox step are
        // required author choices and are intentionally left blank.
    }

    protected override void DrawWizard(PrefabHitboxSkillPayloadDef payload, PayloadDesignerContext context)
    {
        var serialized = new SerializedObject(payload);
        serialized.Update();

        EditorGUILayout.LabelField("Anchor", EditorStyles.boldLabel);
        SerializedProperty anchorMode = serialized.FindProperty("anchorMode");
        EditorGUILayout.PropertyField(anchorMode);
        if (anchorMode.enumValueIndex == (int)PrefabHitboxSkillPayloadDef.HitboxAnchorMode.CasterChildPath)
            EditorGUILayout.PropertyField(serialized.FindProperty("anchorChildPath"));
        EditorGUILayout.PropertyField(serialized.FindProperty("followAnchor"));
        EditorGUILayout.PropertyField(serialized.FindProperty("localPositionOffset"));
        EditorGUILayout.PropertyField(serialized.FindProperty("localEulerOffset"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Timeline", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("hitboxStartEventName"));
        EditorGUILayout.PropertyField(serialized.FindProperty("hitboxEndEventName"));
        EditorGUILayout.PropertyField(serialized.FindProperty("maxSequenceLifetime"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Targeting", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("targetMask"));
        EditorGUILayout.PropertyField(serialized.FindProperty("queryTriggers"));
        EditorGUILayout.PropertyField(serialized.FindProperty("showDamageNumbers"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hitbox Layout (required)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("hitboxLayout"), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Steps (required, at least one)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("steps"), true);

        serialized.ApplyModifiedProperties();
    }

    protected override PayloadGameplaySummary BuildSummary(PrefabHitboxSkillPayloadDef payload, PayloadDesignerContext context)
    {
        int stepCount = payload.Steps?.Count ?? 0;
        var summary = PayloadGameplaySummary.Of(
            stepCount > 0
                ? $"Runs a {stepCount}-step melee hitbox sequence between the {payload.HitboxStartEventName} and {payload.HitboxEndEventName} timeline events."
                : "Runs a melee hitbox sequence, but no steps are configured yet.");

        if (!payload.HasInlineHitboxLayout)
            summary.AddWarning("No inline hitbox layout is configured -- this ability will do nothing.");

        if (stepCount == 0)
            summary.AddWarning("Add at least one hitbox step referencing a group in the layout.");

        if (!payload.HasHitboxTimelineEvents || payload.HitboxStartEventName == payload.HitboxEndEventName)
            summary.AddWarning("Hitbox start/end timeline events must be valid and different from each other.");

        return summary;
    }

    protected override void CollectAuthoringIssues(
        PrefabHitboxSkillPayloadDef payload,
        PayloadDesignerContext context,
        List<PayloadAuthoringIssue> issues)
    {
        CollectRuntimeValidationIssuesAsErrors(payload, issues);
    }
}
