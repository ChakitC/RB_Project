#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CooperativeBlockTestAuthoring
{
    public const string Folder = DefensiveBlockTestSceneBuilder.Folder + "/Cooperative";
    public const string SkillPath = Folder + "/Rector_Skill_1_Cooperative.asset";

    [MenuItem("Tools/RB/Defensive Block/Add Cooperative Test Skill")]
    public static void Build()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(DefensiveBlockTestSceneBuilder.Folder, "Cooperative");
        if (AssetDatabase.LoadAssetAtPath<SkillGemDefinition>(SkillPath) == null &&
            !AssetDatabase.CopyAsset(DefensiveBlockProductionAuthoring.SkillPath, SkillPath))
            throw new InvalidOperationException("Cannot copy Rector Skill 1 and its embedded payloads.");
        var skill = AssetDatabase.LoadAssetAtPath<SkillGemDefinition>(SkillPath);
        if (AssetDatabase.GetAssetPath(skill.payload) != SkillPath)
            throw new InvalidOperationException("Cooperative sample must own its payload.");
        skill.defensiveBlock.mode = DefensiveBlockMode.Cooperative;
        skill.defensiveBlock.cooperative = new CooperativeBlockSettings();
        foreach (var window in skill.defensiveBlock.windows) window.onSuccess = DefensiveBlockOutcome.InterruptSkill;
        var serialized = new SerializedObject(skill);
        serialized.FindProperty("displayName").stringValue = "Rector Skill 1 — Cooperative Block";
        serialized.FindProperty("skillId").stringValue = "test_rector_skill_1_cooperative";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill); AssetDatabase.SaveAssetIfDirty(skill);
        var playerProfile = Profile("PlayerCooperativeGuard.asset");
        var allyProfile = Profile("AiresCooperativeGuard.asset");
        var scene = SceneManager.GetSceneByPath(DefensiveBlockTestSceneBuilder.ScenePath);
        bool opened = scene.IsValid() && scene.isLoaded;
        if (opened && scene.isDirty) throw new InvalidOperationException("Save the test scene before upgrading it.");
        if (!opened) scene = EditorSceneManager.OpenScene(DefensiveBlockTestSceneBuilder.ScenePath, OpenSceneMode.Additive);
        try
        {
            var harness = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DefensiveBlockTestHarness>(true)).First();
            var choice = harness.testEnemies.First(e => e.prefab == AssetDatabase.LoadAssetAtPath<GameObject>(DefensiveBlockProductionAuthoring.RectorPath));
            choice.skills = choice.skills.Concat(new[] { skill }).Distinct().ToArray();
            harness.cooperativePlayerProfile = playerProfile; harness.cooperativeAllyProfile = allyProfile;
            EditorUtility.SetDirty(harness); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally { if (!opened) EditorSceneManager.CloseScene(scene, true); }
    }

    static DefensiveBlockActorProfile Profile(string name)
    {
        string path = Folder + "/" + name;
        var asset = AssetDatabase.LoadAssetAtPath<DefensiveBlockActorProfile>(path);
        if (asset == null)
        {
            var original = AssetDatabase.LoadAssetAtPath<CharacterStats>(DefensiveBlockProductionAuthoring.AiresPath).defensiveBlock;
            asset = UnityEngine.Object.Instantiate(original); asset.name = name.Replace(".asset", "");
            AssetDatabase.CreateAsset(asset, path);
        }
        asset.cooperativeEnabled = true; EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);
        return asset;
    }
}
#endif
