using System.Collections.Generic;
using UnityEditor;

internal sealed class SummonPayloadDesignerDescriptor : PayloadDesignerDescriptorBase<SummonSkillPayloadDef>
{
    public override string DisplayName => "Summon";
    public override string Description => "Spawns transient mobile or stationary actors owned by the caster.";
    public override string Category => "Summoning";

    protected override void ApplySafeDefaults(SummonSkillPayloadDef payload, PayloadDesignerContext context)
    {
    }

    protected override void DrawWizard(SummonSkillPayloadDef payload, PayloadDesignerContext context)
    {
        var serialized = new SerializedObject(payload);
        serialized.Update();
        EditorGUILayout.PropertyField(serialized.FindProperty("summonPrefab"));
        EditorGUILayout.PropertyField(serialized.FindProperty("mobility"));
        EditorGUILayout.PropertyField(serialized.FindProperty("fixedCount"));
        EditorGUILayout.PropertyField(serialized.FindProperty("useSkillProjectileCount"));
        EditorGUILayout.PropertyField(serialized.FindProperty("fixedLifetime"));
        EditorGUILayout.PropertyField(serialized.FindProperty("useSkillEffectDuration"));
        EditorGUILayout.PropertyField(serialized.FindProperty("perSkillCap"));
        EditorGUILayout.PropertyField(serialized.FindProperty("damageInheritance"));
        EditorGUILayout.PropertyField(serialized.FindProperty("despawnDelay"));
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("spawnOffsets"), true);
        EditorGUILayout.PropertyField(serialized.FindProperty("layoutOrientation"));
        EditorGUILayout.PropertyField(serialized.FindProperty("facingMode"));
        EditorGUILayout.PropertyField(serialized.FindProperty("resolveGround"));
        EditorGUILayout.PropertyField(serialized.FindProperty("requireNavMesh"));
        EditorGUILayout.PropertyField(serialized.FindProperty("groundMask"));
        EditorGUILayout.PropertyField(serialized.FindProperty("clearanceMask"));
        EditorGUILayout.PropertyField(serialized.FindProperty("groundRaycastHeight"));
        EditorGUILayout.PropertyField(serialized.FindProperty("groundRaycastDistance"));
        EditorGUILayout.PropertyField(serialized.FindProperty("navMeshSampleRadius"));
        EditorGUILayout.PropertyField(serialized.FindProperty("placementPadding"));
        serialized.ApplyModifiedProperties();
    }

    protected override PayloadGameplaySummary BuildSummary(SummonSkillPayloadDef payload, PayloadDesignerContext context)
    {
        string mobility = payload.Mobility == SummonMobility.Mobile ? "mobile" : "stationary";
        string lifetime = payload.UseSkillEffectDuration
            ? "the skill's effect duration"
            : $"{payload.FixedLifetime:0.#}s";
        var summary = PayloadGameplaySummary.Of(
            $"Spawns {mobility} summon(s) for {lifetime}.");
        summary.AddDetail($"Per-skill cap: {payload.PerSkillCap}; total caster cap: {SummonController.TotalActiveCap}.");
        summary.AddDetail($"Inherited damage: {payload.DamageInheritance:P0} of the skill damage.");
        if (payload.SummonPrefab == null)
            summary.AddWarning("Assign a summon prefab before this payload can be created.");
        return summary;
    }

    protected override void CollectAuthoringIssues(
        SummonSkillPayloadDef payload,
        PayloadDesignerContext context,
        List<PayloadAuthoringIssue> issues)
    {
        CollectRuntimeValidationIssuesAsErrors(payload, issues);
    }
}
