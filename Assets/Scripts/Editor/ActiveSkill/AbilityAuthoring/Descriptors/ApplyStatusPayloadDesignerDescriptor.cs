using System.Collections.Generic;
using UnityEditor;

// Descriptor for ApplyStatusSkillPayloadDef. See PayloadDesignerDescriptorBase for the contract.
internal sealed class ApplyStatusPayloadDesignerDescriptor : PayloadDesignerDescriptorBase<ApplyStatusSkillPayloadDef>
{
    public override string DisplayName => "Apply Status to Self";
    public override string Description => "Applies one or more status effects directly to the caster.";
    public override string Category => "Buffs";

    protected override void ApplySafeDefaults(ApplyStatusSkillPayloadDef payload, PayloadDesignerContext context)
    {
        // No fabricated references -- at least one Status Effect must be chosen by the designer.
    }

    protected override void DrawWizard(ApplyStatusSkillPayloadDef payload, PayloadDesignerContext context)
    {
        var serialized = new SerializedObject(payload);
        serialized.Update();

        EditorGUILayout.LabelField("Status Effects", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("applications"), true);
        EditorGUILayout.PropertyField(serialized.FindProperty("preferCasterRoot"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Conditional Status Effects", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("conditionalStatuses"), true);

        serialized.ApplyModifiedProperties();
    }

    protected override PayloadGameplaySummary BuildSummary(ApplyStatusSkillPayloadDef payload, PayloadDesignerContext context)
    {
        var effectNames = new List<string>();
        IReadOnlyList<ApplyStatusSkillPayloadDef.StatusApplication> applications = payload.Applications;
        for (int i = 0; i < applications.Count; i++)
        {
            StatusEffectDef effect = applications[i]?.spec?.effect;
            if (effect != null)
                effectNames.Add(effect.name);
        }

        PayloadGameplaySummary summary = effectNames.Count > 0
            ? PayloadGameplaySummary.Of($"Applies {string.Join(", ", effectNames)} to the caster.")
            : PayloadGameplaySummary.Of("Applies no status effect yet.");

        if (effectNames.Count == 0)
            summary.AddWarning("Add at least one Status Effect for this ability to do anything.");

        int conditionalCount = payload.ConditionalStatuses?.Applications.Count ?? 0;
        if (conditionalCount > 0)
            summary.AddDetail($"{conditionalCount} additional status effect(s) unlock via upgrades.");

        return summary;
    }

    protected override void CollectAuthoringIssues(
        ApplyStatusSkillPayloadDef payload,
        PayloadDesignerContext context,
        List<PayloadAuthoringIssue> issues)
    {
        CollectRuntimeValidationIssuesAsErrors(payload, issues);
    }
}
