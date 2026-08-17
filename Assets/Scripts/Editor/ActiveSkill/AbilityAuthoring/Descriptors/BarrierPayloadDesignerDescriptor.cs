using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal sealed class BarrierPayloadDesignerDescriptor : PayloadDesignerDescriptorBase<BarrierSkillPayloadDef>
{
    public override string DisplayName => "Barrier";

    public override string Description =>
        "Deploys a shell that absorbs hostile projectiles until its HP or lifetime runs out.";

    public override string Category => "Defense";

    protected override void ApplySafeDefaults(BarrierSkillPayloadDef payload, PayloadDesignerContext context)
    {
    }

    protected override void DrawWizard(BarrierSkillPayloadDef payload, PayloadDesignerContext context)
    {
        var serialized = new SerializedObject(payload);
        serialized.Update();
        EditorGUILayout.PropertyField(serialized.FindProperty("barrierPrefab"));
        EditorGUILayout.PropertyField(serialized.FindProperty("anchorMode"));
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Radius", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("useSkillAreaRadius"));
        EditorGUILayout.PropertyField(serialized.FindProperty("fixedRadius"));
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lifetime", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("useSkillEffectDuration"));
        EditorGUILayout.PropertyField(serialized.FindProperty("fixedLifetime"));
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Health", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("baseHealth"));
        EditorGUILayout.PropertyField(serialized.FindProperty("anchorMaxHealthShare"));
        serialized.ApplyModifiedProperties();
    }

    protected override PayloadGameplaySummary BuildSummary(
        BarrierSkillPayloadDef payload,
        PayloadDesignerContext context)
    {
        string anchor = payload.AnchorMode switch
        {
            BarrierAnchorMode.SpawnedEntitiesFromCurrentCast => "each entity this cast spawned",
            BarrierAnchorMode.CastPosition => "the cast position",
            _ => "the caster",
        };

        string radius = payload.UseSkillAreaRadius
            ? "the skill's area radius"
            : $"{payload.FixedRadius:0.#}m";

        string lifetime = payload.UseSkillEffectDuration
            ? "the skill's effect duration"
            : $"{payload.FixedLifetime:0.#}s";

        var summary = PayloadGameplaySummary.Of(
            $"Deploys a projectile barrier on {anchor}, sized to {radius}, lasting {lifetime}.");

        if (payload.AnchorMaxHealthShare > 0f)
        {
            summary.AddDetail(
                $"Barrier HP: {payload.BaseHealth:0.#} + {payload.AnchorMaxHealthShare:P0} of the anchor's max HP.");
        }
        else
        {
            summary.AddDetail($"Barrier HP: {payload.BaseHealth:0.#}.");
        }

        summary.AddDetail("Blocks hostile projectiles entering from outside; friendly fire and shots born inside pass through.");
        summary.AddDetail("Broken barriers do not regenerate — recovering one needs a fresh cast.");

        if (payload.AnchorMode == BarrierAnchorMode.SpawnedEntitiesFromCurrentCast)
        {
            summary.AddDetail(
                "Requires a spawning step earlier in the same composite; the barrier reads that cast's spawns directly.");
        }

        if (payload.BarrierPrefab == null)
            summary.AddWarning("Assign a barrier prefab before this payload can be created.");
        else if (payload.BarrierPrefab.layer != LayerMask.NameToLayer("Barrier"))
            summary.AddWarning("Barrier prefab is not on the 'Barrier' physics layer, so projectiles will not hit it.");

        return summary;
    }

    protected override void CollectAuthoringIssues(
        BarrierSkillPayloadDef payload,
        PayloadDesignerContext context,
        List<PayloadAuthoringIssue> issues)
    {
        CollectRuntimeValidationIssuesAsErrors(payload, issues);

        int barrierLayer = LayerMask.NameToLayer("Barrier");
        if (barrierLayer < 0)
        {
            issues.Add(PayloadAuthoringIssue.Error(
                "Physics layer 'Barrier' is not defined in Project Settings. Barriers will not block projectiles."));
            return;
        }

        if (payload.BarrierPrefab != null && payload.BarrierPrefab.layer != barrierLayer)
        {
            issues.Add(PayloadAuthoringIssue.Error(
                $"Barrier prefab '{payload.BarrierPrefab.name}' must be on the 'Barrier' layer."));
        }
    }
}
