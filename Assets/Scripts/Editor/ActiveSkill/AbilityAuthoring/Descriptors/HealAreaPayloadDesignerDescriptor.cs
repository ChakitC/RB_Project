using System.Collections.Generic;
using UnityEditor;

// Descriptor for HealAreaSkillPayloadDef. See PayloadDesignerDescriptorBase for the contract.
internal sealed class HealAreaPayloadDesignerDescriptor : PayloadDesignerDescriptorBase<HealAreaSkillPayloadDef>
{
    public override string DisplayName => "Heal Area";
    public override string Description => "Heals the caster or nearby allies and applies configured status effects.";
    public override string Category => "Support";

    protected override void ApplySafeDefaults(HealAreaSkillPayloadDef payload, PayloadDesignerContext context)
    {
        // Target already defaults to Self, which is valid with zero status effects configured
        // (a pure heal burst driven by the skill's Heal Power stat). Nothing to fabricate.
    }

    protected override void DrawWizard(HealAreaSkillPayloadDef payload, PayloadDesignerContext context)
    {
        var serialized = new SerializedObject(payload);
        serialized.Update();

        EditorGUILayout.PropertyField(serialized.FindProperty("target"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Status Effects", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("statusSpecApplications"), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Conditional Status Effects", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("conditionalStatuses"), true);

        serialized.ApplyModifiedProperties();
    }

    protected override PayloadGameplaySummary BuildSummary(HealAreaSkillPayloadDef payload, PayloadDesignerContext context)
    {
        string targetLabel = payload.Target == HealTargetMode.Allies
            ? "the caster and nearby allies"
            : "the caster";

        var summary = PayloadGameplaySummary.Of($"Heals {targetLabel} using the skill's Heal Power stat.");

        if (payload.Target == HealTargetMode.Allies)
            summary.AddDetail("Ally range uses the skill's area radius.");

        int statusCount = payload.StatusSpecApplications?.Count ?? 0;
        if (statusCount > 0)
            summary.AddDetail($"Applies {statusCount} status effect(s) alongside the heal.");

        int conditionalCount = payload.ConditionalStatuses?.Applications.Count ?? 0;
        if (conditionalCount > 0)
            summary.AddDetail($"{conditionalCount} additional status effect(s) unlock via upgrades.");

        return summary;
    }

    protected override void CollectAuthoringIssues(
        HealAreaSkillPayloadDef payload,
        PayloadDesignerContext context,
        List<PayloadAuthoringIssue> issues)
    {
        CollectRuntimeValidationIssuesAsErrors(payload, issues);
    }
}
