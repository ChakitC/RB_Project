using System.Collections.Generic;
using UnityEditor;

// Descriptor for TauntSkillPayloadDef. See PayloadDesignerDescriptorBase for the contract.
internal sealed class TauntPayloadDesignerDescriptor : PayloadDesignerDescriptorBase<TauntSkillPayloadDef>
{
    public override string DisplayName => "Taunt Enemies";
    public override string Description =>
        "Spawns a runtime listener that taunts enemies in range when the TauntApply timeline event fires.";
    public override string Category => "Crowd Control";

    protected override void ApplySafeDefaults(TauntSkillPayloadDef payload, PayloadDesignerContext context)
    {
        // Field initializers on TauntSkillPayloadDef already give radius/duration/useSkillStats
        // reasonable non-zero values. The one required author choice -- Taunt Status -- cannot be
        // fabricated (must carry the Taunt tag), so it is left blank and reported as an error.
    }

    protected override void DrawWizard(TauntSkillPayloadDef payload, PayloadDesignerContext context)
    {
        var serialized = new SerializedObject(payload);
        serialized.Update();

        EditorGUILayout.LabelField("Range & Duration", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("useSkillStats"));
        EditorGUILayout.PropertyField(serialized.FindProperty("radius"));
        EditorGUILayout.PropertyField(serialized.FindProperty("duration"));
        EditorGUILayout.PropertyField(serialized.FindProperty("requireLineOfSight"));
        EditorGUILayout.PropertyField(serialized.FindProperty("targetLayers"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Taunt Status (required)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Must reference a StatusEffectDef tagged \"Taunt\" with separatePerSource = true and " +
            "StackMode.RefreshDuration.",
            MessageType.Info);
        EditorGUILayout.PropertyField(serialized.FindProperty("tauntStatus"), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Conditional Status Effects", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("conditionalStatuses"), true);

        serialized.ApplyModifiedProperties();
    }

    protected override PayloadGameplaySummary BuildSummary(TauntSkillPayloadDef payload, PayloadDesignerContext context)
    {
        string rangeSource = payload.UseSkillStats ? "the skill's area radius" : $"{payload.Radius:0.#}m";
        string durationSource = payload.UseSkillStats ? "the skill's effect duration" : $"{payload.Duration:0.#}s";
        var summary = PayloadGameplaySummary.Of($"Taunts enemies within {rangeSource} for {durationSource}.");

        if (payload.TauntStatus?.effect != null)
            summary.AddDetail($"Applies status '{payload.TauntStatus.effect.name}' to every taunted enemy.");
        else
            summary.AddWarning("No Taunt Status assigned yet -- taunted enemies will not be marked as taunted.");

        if (payload.RequireLineOfSight)
            summary.AddDetail("Only taunts enemies with line of sight to the caster.");

        int conditionalCount = payload.ConditionalStatuses?.Applications.Count ?? 0;
        if (conditionalCount > 0)
            summary.AddDetail($"{conditionalCount} additional status effect(s) unlock via upgrades.");

        return summary;
    }

    protected override void CollectAuthoringIssues(
        TauntSkillPayloadDef payload,
        PayloadDesignerContext context,
        List<PayloadAuthoringIssue> issues)
    {
        CollectRuntimeValidationIssuesAsErrors(payload, issues);
    }
}
