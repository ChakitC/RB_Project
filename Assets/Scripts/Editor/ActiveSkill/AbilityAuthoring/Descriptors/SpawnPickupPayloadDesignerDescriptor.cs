using System.Collections.Generic;
using UnityEditor;

// Descriptor for SpawnPickupSkillPayloadDef. See PayloadDesignerDescriptorBase for the contract.
internal sealed class SpawnPickupPayloadDesignerDescriptor : PayloadDesignerDescriptorBase<SpawnPickupSkillPayloadDef>
{
    public override string DisplayName => "Spawn Pickup";
    public override string Description => "Spawns pickup prefabs in front of the caster.";
    public override string Category => "World";

    protected override void ApplySafeDefaults(SpawnPickupSkillPayloadDef payload, PayloadDesignerContext context)
    {
        // Placement/spawn-count fields already default to usable values. Pickup Prefab is a
        // required author choice and is intentionally left blank.
    }

    protected override void DrawWizard(SpawnPickupSkillPayloadDef payload, PayloadDesignerContext context)
    {
        var serialized = new SerializedObject(payload);
        serialized.Update();

        EditorGUILayout.LabelField("Pickup", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("pickupPrefab"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Spawn Count", EditorStyles.boldLabel);
        SerializedProperty useSkillProjectileCount = serialized.FindProperty("useSkillProjectileCount");
        EditorGUILayout.PropertyField(useSkillProjectileCount);
        if (!useSkillProjectileCount.boolValue)
            EditorGUILayout.PropertyField(serialized.FindProperty("spawnCount"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("forwardOffset"));
        EditorGUILayout.PropertyField(serialized.FindProperty("spreadWidth"));
        EditorGUILayout.PropertyField(serialized.FindProperty("verticalOffset"));
        EditorGUILayout.PropertyField(serialized.FindProperty("maximumTargetDistance"));
        EditorGUILayout.PropertyField(serialized.FindProperty("groundMask"));

        serialized.ApplyModifiedProperties();
    }

    protected override PayloadGameplaySummary BuildSummary(SpawnPickupSkillPayloadDef payload, PayloadDesignerContext context)
    {
        string countSource = payload.UsesSkillProjectileCount
            ? "the skill's projectile count"
            : $"{payload.SpawnCount}";
        string prefabName = payload.PickupPrefab != null ? payload.PickupPrefab.name : "<no prefab assigned>";

        var summary = PayloadGameplaySummary.Of($"Spawns {countSource} '{prefabName}' pickup(s) in front of the caster.");

        if (payload.PickupPrefab == null)
            summary.AddWarning("No Pickup Prefab assigned -- this ability will do nothing.");
        else if (payload.PickupPrefabMissingSkillPickupComponent)
            summary.AddWarning("Pickup Prefab has no SkillPickup component.");

        return summary;
    }

    protected override void CollectAuthoringIssues(
        SpawnPickupSkillPayloadDef payload,
        PayloadDesignerContext context,
        List<PayloadAuthoringIssue> issues)
    {
        CollectRuntimeValidationIssuesAsErrors(payload, issues);
    }
}
