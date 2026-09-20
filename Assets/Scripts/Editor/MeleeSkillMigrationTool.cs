#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Animancer;
using UnityEditor;
using UnityEngine;

public static class MeleeSkillMigrationTool
{
    const string Destination = "Assets/Data/Combat/MeleeSkills";

    [MenuItem("Tools/RB/Melee/Preview Skill Migration")]
    public static void PreviewMenu() => Debug.Log(Preview());

    public static string Preview()
    {
        return string.Join("\n", Combos().Select(c =>
            $"{AssetDatabase.GetAssetPath(c)}: {c.Count} steps, {c.Steps.Count(s => s.executionSkill == null)} to migrate"));
    }

    [MenuItem("Tools/RB/Melee/Migrate All Combos To Skills")]
    public static void MigrateAllMenu() => Debug.Log(MigrateAll());

    public static string MigrateAll()
    {
        var results = new List<string>();
        foreach (var combo in Combos())
        {
            try { results.Add(MigrateCombo(combo)); }
            catch (InvalidOperationException ex) { results.Add($"BLOCKED: {ex.Message}"); }
        }
        return string.Join("\n", results);
    }

    public static string MigrateCombo(MeleeComboSO combo)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Migrate in Edit Mode.");
        string comboPath = AssetDatabase.GetAssetPath(combo);
        if (string.IsNullOrEmpty(comboPath)) throw new InvalidOperationException("Save the combo before migration.");
        // Preflight the whole combo before creating anything.
        foreach (var step in combo.Steps)
        {
            if (step.executionSkill != null) continue;
            if (step.clip == null || !step.clip.IsValid || CountEvents(step.clip, MeleeComboSO.HitStartEventName) == 0 ||
                CountEvents(step.clip, MeleeComboSO.HitStartEventName) != CountEvents(step.clip, MeleeComboSO.HitEndEventName))
                throw new InvalidOperationException($"{comboPath}: invalid legacy clip or unbalanced hit windows.");
        }
        if (combo.Steps.All(s => s.executionSkill != null)) return $"Unchanged: {comboPath}";
        Backup(comboPath);
        Undo.RecordObject(combo, "Migrate Melee Combo To Skills");
        combo.EnsureUniqueStepEntryIds();
        EnsureFolder(Destination);
        string guid = AssetDatabase.AssetPathToGUID(comboPath);
        int created = 0;
        for (int i = 0; i < combo.Count; i++)
        {
            var step = combo.Steps[i];
            if (step.executionSkill != null) continue;
            string path = $"{Destination}/{guid}_{step.EntryId}.asset";
            var skill = AssetDatabase.LoadAssetAtPath<SkillGemDefinition>(path);
            if (skill == null)
            {
                skill = CreateSkill(step, $"melee.{guid}.{step.EntryId}", $"{combo.name} / Step {i + 1}");
                AssetDatabase.CreateAsset(skill, path);
                AssetDatabase.AddObjectToAsset(skill.payload, skill);
                EditorUtility.SetDirty(skill.payload);
                EditorUtility.SetDirty(skill);
                AssetDatabase.SaveAssetIfDirty(skill);
                created++;
            }
            // Existing generated assets are reused without overwriting designer changes.
            var serialized = new SerializedObject(combo);
            serialized.FindProperty("steps").GetArrayElementAtIndex(i).FindPropertyRelative("executionSkill").objectReferenceValue = skill;
            serialized.ApplyModifiedProperties();
        }
        EditorUtility.SetDirty(combo);
        AssetDatabase.SaveAssetIfDirty(combo);
        return $"Migrated: {comboPath}; created {created} skills";
    }

    public static SkillGemDefinition CreateSkill(MeleeComboSO.Step step, string id, string displayName)
    {
        var skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
        skill.name = displayName;
        skill.displayName = displayName;
        skill.skillId = id;
        skill.tags = SkillTag.Melee;
        skill.baseDamage = 0f;
        skill.damageCoefficient = 1f;
        skill.baseStaggerPower = step.staggerPower;
        skill.baseCritChance = 0f;
        skill.baseManaCost = 0f;
        skill.baseCooldown = 0f;
        skill.baseCastTime = step.duration;
        skill.castPointNormalized = 0f;
        skill.skillClip = JsonUtility.FromJson<ClipTransition>(JsonUtility.ToJson(step.clip));
        var payload = ScriptableObject.CreateInstance<PrefabHitboxSkillPayloadDef>();
        payload.name = "Melee Hitboxes";
        skill.payload = payload;
        var data = new SerializedObject(payload);
        data.FindProperty("anchorMode").enumValueIndex = (int)PrefabHitboxSkillPayloadDef.HitboxAnchorMode.CasterRoot;
        var steps = data.FindProperty("steps");
        steps.arraySize = CountEvents(step.clip, MeleeComboSO.HitStartEventName);
        for (int i = 0; i < steps.arraySize; i++)
        {
            var hit = steps.GetArrayElementAtIndex(i);
            var keys = hit.FindPropertyRelative("groupKeys");
            keys.arraySize = 1;
            keys.GetArrayElementAtIndex(0).stringValue = "Strike";
            hit.FindPropertyRelative("damageMultiplier").floatValue = 1f;
            hit.FindPropertyRelative("hitPolicy").enumValueIndex = (int)PrefabHitboxSkillPayloadDef.HitPolicy.OncePerStep;
            hit.FindPropertyRelative("clearHitCacheOnEnter").boolValue = true;
            hit.FindPropertyRelative("overrideKnockback").boolValue = step.applyKnockback;
            hit.FindPropertyRelative("knockbackDistance").floatValue = step.knockbackDistance;
            hit.FindPropertyRelative("knockbackDuration").floatValue = step.knockbackDuration;
            hit.FindPropertyRelative("knockbackProgressCurve").animationCurveValue = step.knockbackProgressCurve;
            hit.FindPropertyRelative("knockbackReaction").enumValueIndex = (int)step.knockbackReaction;
            hit.FindPropertyRelative("knockbackInterruptsActions").boolValue = step.knockbackInterruptsActions;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        // Geometry is filled by the character-hitbox migration (or by the authoring tool).
        // A starter here would be mistaken for designer-authored data by the second migration.
        if (step.AnimationVfxTrack != null)
            new SkillVfxTimelineSource(skill).ReplaceCues(step.AnimationVfxTrack.Cues);
        return skill;
    }

    public static List<SkillHitboxLayoutData.HitBoxGroupData> CreateStarterLayout()
    {
        var group = new SkillHitboxLayoutData.HitBoxGroupData {
            GroupKey = "Strike", Anchor = SkillHitboxLayoutData.AnchorSpace.CasterRoot
        };
        group.Shapes.Add(new SkillHitboxLayoutData.HitBoxShapeData {
            ShapeName = "FrontStrike", Type = SkillHitboxLayoutData.HitBoxType.Box,
            LocalPosition = new Vector3(0f, 1f, 1f), Size = new Vector3(1.2f, 1.6f, 1.5f)
        });
        return new List<SkillHitboxLayoutData.HitBoxGroupData> { group };
    }

    public static string ValidateAll()
    {
        var issues = new List<string>();
        foreach (var combo in Combos())
        {
            if (!combo.IsValid(out string reason)) issues.Add($"{AssetDatabase.GetAssetPath(combo)}: {reason}");
            foreach (var step in combo.Steps)
            {
                var skill = step.executionSkill;
                if (skill == null) continue;
                var local = new List<string>();
                skill.payload?.CollectValidationIssues(local);
                skill.CollectRequiredTimelineValidationIssues(local);
                skill.CollectSkillVfxValidationIssues(local);
                if (skill.payload == null || AssetDatabase.GetAssetPath(skill.payload) != AssetDatabase.GetAssetPath(skill))
                    local.Add("Payload must be embedded in its skill.");
                foreach (string issue in local) issues.Add($"{skill.name}: {issue}");
            }
        }
        return issues.Count == 0 ? "PASS: all melee combos and execution skills valid" : string.Join("\n", issues);
    }

    static MeleeComboSO[] Combos() => AssetDatabase.FindAssets("t:MeleeComboSO", new[] { "Assets" })
        .Select(g => AssetDatabase.LoadAssetAtPath<MeleeComboSO>(AssetDatabase.GUIDToAssetPath(g)))
        .Where(c => c != null).ToArray();

    static int CountEvents(ClipTransition clip, CombatTimelineEventName eventName)
    {
        if (clip?.Events == null) return 0;
        int count = 0, index = -1;
        while ((index = clip.Events.IndexOf(CombatTimelineEventNames.ToStringReference(eventName), index + 1)) >= 0) count++;
        return count;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void Backup(string assetPath)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.codex-temp/melee-skill-baseline/Assets"));
        string destination = Path.Combine(root, assetPath.Substring("Assets/".Length));
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        if (!File.Exists(destination)) File.Copy(assetPath, destination);
        if (File.Exists(assetPath + ".meta") && !File.Exists(destination + ".meta")) File.Copy(assetPath + ".meta", destination + ".meta");
    }
}
#endif
