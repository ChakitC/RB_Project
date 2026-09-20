#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class UnifiedHitboxMigrationTool
{
    const string LegacyScriptGuid = "33324f34cfcf7274dbafdc4f0b8b10ea";

    [MenuItem("Tools/RB/Melee/Migrate Character Hitboxes To Skill Layouts")]
    public static void MigrateMenu() => Debug.Log(MigrateAll());

    public static string MigrateAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Migrate in Edit Mode.");
        var layouts = new Dictionary<PrefabHitboxSkillPayloadDef, List<SkillHitboxLayoutData.HitBoxGroupData>>();
        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab", "Assets/Tests" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponentsInChildren<MonoBehaviour>(true).Any(IsLegacy))
            .ToArray();

        // Read every binding first. Conflicting shared skills require a deliberate authoring decision.
        foreach (string path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var legacy in prefab.GetComponentsInChildren<MonoBehaviour>(true).Where(IsLegacy))
            {
                var context = CharacterContextModuleLookup.ResolveContext(legacy.gameObject);
                context?.ResolveReferences();
                var profile = context != null && context.baseStats != null ? context.baseStats.animProfile : null;
                if (profile == null)
                {
                    var unbound = new SerializedObject(legacy);
                    if (ReadColliders(unbound, MeleeType.Light).Count > 0 || ReadColliders(unbound, MeleeType.Heavy).Count > 0)
                        throw new InvalidOperationException($"{path}: authored hitboxes have no animation profile to migrate.");
                    continue;
                }
                var animator = context.Visual != null ? context.Visual.ModelAnimator : null;
                if (animator == null && context.AnimBrain != null) animator = context.AnimBrain.BoundAnimator;
                var data = new SerializedObject(legacy);
                foreach (var type in new[] { MeleeType.Light, MeleeType.Heavy })
                {
                    var colliders = ReadColliders(data, type);
                    if (colliders.Count == 0) continue;
                    if (animator == null) throw new InvalidOperationException($"{path}: missing bound Animator.");
                    var groups = CaptureLayout(animator.transform, colliders);
                    var combo = type == MeleeType.Light ? profile.lightCombo : profile.heavyCombo;
                    if (combo == null) combo = profile.meleeCombo;
                    Register(combo, groups, layouts);
                    // The existing Rector fallback clip is its heavy attack.
                    if (type == MeleeType.Heavy && profile.meleeCombo != null)
                        Register(profile.meleeCombo, groups, layouts);
                }
            }
        }

        var skills = AssetDatabase.FindAssets("t:SkillGemDefinition", new[] { "Assets/Data/Combat/MeleeSkills" })
            .Select(g => AssetDatabase.LoadAssetAtPath<SkillGemDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        int migrated = 0, starters = 0;
        foreach (var skill in skills)
        {
            var payload = skill.payload as PrefabHitboxSkillPayloadDef;
            if (payload == null) throw new InvalidOperationException($"{skill.name}: expected hitbox payload.");
            if (payload.HasInlineHitboxLayout) continue; // Never overwrite subsequent designer edits.
            Backup(AssetDatabase.GetAssetPath(skill));
            bool starter = !layouts.TryGetValue(payload, out var groups);
            if (starter) { groups = MeleeSkillMigrationTool.CreateStarterLayout(); starters++; }
            payload.ReplaceHitboxLayoutGroups(groups);
            var serialized = new SerializedObject(payload);
            serialized.FindProperty("anchorMode").enumValueIndex = (int)PrefabHitboxSkillPayloadDef.HitboxAnchorMode.CasterRoot;
            var steps = serialized.FindProperty("steps");
            for (int i = 0; i < steps.arraySize; i++)
            {
                var keys = steps.GetArrayElementAtIndex(i).FindPropertyRelative("groupKeys");
                keys.arraySize = groups.Count;
                for (int j = 0; j < groups.Count; j++) keys.GetArrayElementAtIndex(j).stringValue = groups[j].GroupKey;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var issues = new List<string>();
            payload.CollectValidationIssues(issues);
            if (issues.Count > 0) throw new InvalidOperationException(string.Join("\n", issues));
            EditorUtility.SetDirty(payload);
            AssetDatabase.SaveAssetIfDirty(payload);
            migrated++;
        }

        // Derived prefabs first, so their old target masks are still available before base cleanup.
        paths = paths.OrderByDescending(p => DependencyDepth(p)).ToArray();
        int removed = 0;
        foreach (string path in paths)
        {
            Backup(path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var legacyComponents = root.GetComponentsInChildren<MonoBehaviour>(true).Where(IsLegacy).ToArray();
                var oldColliders = new HashSet<Collider>();
                foreach (var legacy in legacyComponents)
                {
                    var data = new SerializedObject(legacy);
                    var context = CharacterContextModuleLookup.ResolveContext(legacy.gameObject);
                    context?.ResolveReferences();
                    var controller = context != null ? context.MeleeController : root.GetComponentInChildren<MeleeController>(true);
                    if (controller == null) throw new InvalidOperationException($"{path}: missing combo controller.");
                    var controllerData = new SerializedObject(controller);
                    controllerData.FindProperty("targetMask").intValue = data.FindProperty("targetMask").intValue;
                    controllerData.ApplyModifiedPropertiesWithoutUndo();
                    foreach (var type in new[] { MeleeType.Light, MeleeType.Heavy })
                        foreach (var collider in ReadColliders(data, type)) oldColliders.Add(collider);
                    UnityEngine.Object.DestroyImmediate(legacy);
                    removed++;
                }
                // Keep transforms/bones. Only the obsolete attack collider components are removed.
                foreach (var collider in oldColliders) if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        string report = $"PASS: {migrated} layouts migrated ({starters} starter layouts); {removed} legacy components removed from {paths.Length} prefabs.\n" + MeleeSkillMigrationTool.ValidateAll();
        Directory.CreateDirectory("BuildArtifacts");
        File.WriteAllText("BuildArtifacts/unified-hitbox-migration.txt", report);
        return report;
    }

    static bool IsLegacy(MonoBehaviour component) => component != null &&
        AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(component))) == LegacyScriptGuid;

    static int DependencyDepth(string path)
    {
        int depth = 0;
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        while ((asset = PrefabUtility.GetCorrespondingObjectFromSource(asset)) != null) depth++;
        return depth;
    }

    static List<Collider> ReadColliders(SerializedObject data, MeleeType type)
    {
        var result = new List<Collider>();
        var array = data.FindProperty(type == MeleeType.Light ? "lightHitboxes" : "heavyHitboxes").FindPropertyRelative("colliders");
        for (int i = 0; i < array.arraySize; i++)
            if (array.GetArrayElementAtIndex(i).objectReferenceValue is Collider collider && !result.Contains(collider)) result.Add(collider);
        if (result.Count == 0)
            foreach (string field in new[] { "legacyHitboxR", "legacyHitboxL" })
                if (data.FindProperty(field)?.objectReferenceValue is Collider legacy && !result.Contains(legacy)) result.Add(legacy);
        return result;
    }

    public static List<SkillHitboxLayoutData.HitBoxGroupData> CaptureLayout(Transform animatorRoot, IReadOnlyList<Collider> colliders)
    {
        var groups = new List<SkillHitboxLayoutData.HitBoxGroupData>();
        foreach (var collider in colliders)
        {
            // Dedicated HitBox children are prefab authoring objects; bind to their model bone instead.
            Transform anchor = collider.name.StartsWith("Hitbox", StringComparison.OrdinalIgnoreCase) ? collider.transform.parent : collider.transform;
            if (anchor == null || !anchor.IsChildOf(animatorRoot)) throw new InvalidOperationException("Hitbox must belong to the bound Animator hierarchy.");
            string path = AnimationUtility.CalculateTransformPath(anchor, animatorRoot);
            var group = groups.FirstOrDefault(g => g.AnchorPath == path);
            if (group == null)
            {
                group = new SkillHitboxLayoutData.HitBoxGroupData { GroupKey = $"Strike{groups.Count + 1:D2}",
                    Anchor = SkillHitboxLayoutData.AnchorSpace.AnimatorRoot, AnchorPath = path };
                groups.Add(group);
            }
            if (!SetSkillHitBoxData.TryCreateShapeData(anchor, collider, out var shape))
                throw new InvalidOperationException($"Unsupported collider {collider.GetType().Name}.");
            group.Shapes.Add(shape);
        }
        return groups;
    }

    static void Register(MeleeComboSO combo, List<SkillHitboxLayoutData.HitBoxGroupData> groups,
        Dictionary<PrefabHitboxSkillPayloadDef, List<SkillHitboxLayoutData.HitBoxGroupData>> layouts)
    {
        if (combo == null) throw new InvalidOperationException("Authored colliders have no matching combo.");
        foreach (var step in combo.Steps)
        {
            if (!(step.executionSkill?.payload is PrefabHitboxSkillPayloadDef payload)) throw new InvalidOperationException($"{combo.name}: migrate combo to skills first.");
            if (layouts.TryGetValue(payload, out var previous))
            {
                var a = new SkillHitboxLayoutData(); a.CopyFrom(previous);
                var b = new SkillHitboxLayoutData(); b.CopyFrom(groups);
                if (JsonUtility.ToJson(a) != JsonUtility.ToJson(b)) throw new InvalidOperationException($"{combo.name}: conflicting prefab geometry.");
            }
            layouts[payload] = groups;
        }
    }

    static void Backup(string path)
    {
        string destination = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.codex-temp/unified-hitbox-assets", path));
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        if (!File.Exists(destination)) File.Copy(path, destination);
    }
}
#endif
