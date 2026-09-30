using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StateHubDebugMigration
{
    static readonly string[] Fields = { "debugInInspector", "logTransitions", "showOnScreen", "screenPos" };

    [MenuItem("Tools/RB/StateHub/Migrate Debug Views")]
    public static void RunMenu() => Debug.Log(Run());

    // Source prefabs precede their variants. Scenes retain their own legacy property overrides.
    public static string Run()
    {
        var changed = new List<string>();
        var skipped = new List<string>();
        string hubPath = AssetDatabase.GUIDToAssetPath("218e69c47a6d821479402caf781dea8d");
        var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => AssetDatabase.GetDependencies(p).Contains(hubPath))
            .OrderBy(p => AssetDatabase.GetDependencies(p).Count(d => d.EndsWith(".prefab"))).ToArray();
        foreach (var path in prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!Migrate(root)) continue;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed.Add(path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            if (!AssetDatabase.GetDependencies(path).Contains(hubPath)) continue;
            var loaded = SceneManager.GetSceneByPath(path);
            if (loaded.IsValid() && loaded.isDirty) { skipped.Add(path + " (unsaved scene)"); continue; }
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                bool dirty = false;
                foreach (var root in scene.GetRootGameObjects()) dirty |= Migrate(root);
                if (!dirty) continue;
                if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Could not save " + path);
                changed.Add(path);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        return $"Changed {changed.Count} assets. Skipped {skipped.Count}.\n" + string.Join("\n", changed.Concat(skipped));
    }

    static bool Migrate(GameObject root)
    {
        bool changed = false;
        var contexts = root.GetComponentsInChildren<CharacteContext>(true)
            .ToDictionary(c => c, EditorJsonUtility.ToJson);
        foreach (var hub in root.GetComponentsInChildren<StateHub>(true))
        {
            var legacy = new SerializedObject(hub);
            var contextBefore = legacy.FindProperty("ctx").objectReferenceValue;
            var view = hub.GetComponent<StateHubDebugView>();
            if (!view) { view = hub.gameObject.AddComponent<StateHubDebugView>(); changed = true; }
            var target = new SerializedObject(view);
            string signature = string.Join("|", Fields.Select(f => f == "screenPos"
                ? legacy.FindProperty(f).vector2Value.ToString("R")
                : legacy.FindProperty(f).boolValue.ToString()));
            if (target.FindProperty("legacyDebugSignature").stringValue == signature) continue;
            bool updated = false;
            foreach (string field in Fields)
            {
                var sourceProperty = legacy.FindProperty(field);
                var targetProperty = target.FindProperty(field);
                if (field == "screenPos")
                {
                    if (targetProperty.vector2Value == sourceProperty.vector2Value) continue;
                    targetProperty.vector2Value = sourceProperty.vector2Value;
                }
                else
                {
                    if (targetProperty.boolValue == sourceProperty.boolValue) continue;
                    targetProperty.boolValue = sourceProperty.boolValue;
                }
                updated = true;
            }
            target.FindProperty("legacyDebugSignature").stringValue = signature;
            updated = true;
            if (updated)
            {
                target.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.IsPartOfPrefabInstance(view)) PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                changed = true;
            }
            legacy.Update();
            if (legacy.FindProperty("ctx").objectReferenceValue != contextBefore)
                throw new InvalidOperationException("Debug migration changed a StateHub context binding.");
        }
        foreach (var pair in contexts)
            if (EditorJsonUtility.ToJson(pair.Key) != pair.Value)
                throw new InvalidOperationException("Debug migration changed context authoring: " + pair.Key.name);
        return changed;
    }
}
