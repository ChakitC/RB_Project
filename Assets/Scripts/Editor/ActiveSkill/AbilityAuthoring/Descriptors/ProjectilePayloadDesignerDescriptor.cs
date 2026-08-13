using System.Collections.Generic;
using UnityEditor;

// Descriptor for ProjectileSkillPayloadDef. See PayloadDesignerDescriptorBase for the contract.
internal sealed class ProjectilePayloadDesignerDescriptor : PayloadDesignerDescriptorBase<ProjectileSkillPayloadDef>
{
    public override string DisplayName => "Fire Projectile";
    public override string Description => "Fires one or more projectiles from the skill's cast origin.";
    public override string Category => "Damage";

    protected override void ApplySafeDefaults(ProjectileSkillPayloadDef payload, PayloadDesignerContext context)
    {
        // Spread angle already defaults to a usable value. Projectile Prefab is a required
        // author choice and is intentionally left blank.
    }

    protected override void DrawWizard(ProjectileSkillPayloadDef payload, PayloadDesignerContext context)
    {
        var serialized = new SerializedObject(payload);
        serialized.Update();

        EditorGUILayout.LabelField("Execution", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("projectilePrefab"));
        EditorGUILayout.PropertyField(serialized.FindProperty("projectileConfigOverride"));
        EditorGUILayout.PropertyField(serialized.FindProperty("spreadAngle"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Presentation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("projectileTrailVfxPrefab"));
        EditorGUILayout.PropertyField(serialized.FindProperty("projectileHitVfxPrefab"));
        EditorGUILayout.PropertyField(serialized.FindProperty("projectileHitVfxScale"));

        serialized.ApplyModifiedProperties();
    }

    protected override PayloadGameplaySummary BuildSummary(ProjectileSkillPayloadDef payload, PayloadDesignerContext context)
    {
        string prefabName = payload.HasExplicitProjectilePrefab
            ? payload.GetResolvedProjectilePrefab().name
            : "<no projectile prefab assigned>";

        var summary = PayloadGameplaySummary.Of(
            $"Fires the skill's configured projectile count of '{prefabName}' with a {payload.SpreadAngle:0.#} degree spread.");

        if (!payload.HasExplicitProjectilePrefab)
            summary.AddWarning("No Projectile Prefab assigned -- this ability will do nothing.");

        if (payload.HasProjectilePresentationAssets)
            summary.AddDetail("Has trail and/or hit VFX configured.");

        if (payload.HasHitVfxScaleWithoutHitVfx)
            summary.AddWarning("Hit VFX Scale is set, but there is no Projectile Hit VFX assigned.");

        return summary;
    }

    protected override void CollectAuthoringIssues(
        ProjectileSkillPayloadDef payload,
        PayloadDesignerContext context,
        List<PayloadAuthoringIssue> issues)
    {
        CollectRuntimeValidationIssuesAsErrors(payload, issues);
    }
}
