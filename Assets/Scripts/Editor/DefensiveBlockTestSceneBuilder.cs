#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

public static class DefensiveBlockTestSceneBuilder
{
    public const string Folder = "Assets/Tests/DefensiveBlock";
    public const string ScenePath = Folder + "/RectorDefensiveBlock.unity";

    [MenuItem("Tools/RB/Defensive Block/Create or Upgrade Test Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring.");
        DefensiveBlockProductionAuthoring.Configure();
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = scene.IsValid();
        var original = SceneManager.GetActiveScene();
        if (opened && scene.isDirty) throw new InvalidOperationException("Save the test scene before upgrading it.");
        if (!opened)
            scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var harness = Find<DefensiveBlockTestHarness>(scene) ?? new GameObject("Defensive Block Test Controls").AddComponent<DefensiveBlockTestHarness>();
            var point = Find<PartySpawnPoint>(scene) ?? new GameObject("PartySpawnPoint").AddComponent<PartySpawnPoint>();
            var spawn = new SerializedObject(point);
            spawn.FindProperty("config").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PartySpawnConfigSO>("Assets/Data/Party/DefaultPartySpawnConfig.asset");
            spawn.FindProperty("spawnOnAwake").boolValue = false;
            var roster = spawn.FindProperty("definitionOverrides"); roster.arraySize = 4;
            var aires = AssetDatabase.LoadAssetAtPath<CharacterStats>(DefensiveBlockProductionAuthoring.AiresPath);
            for (int i = 0; i < 4; i++) roster.GetArrayElementAtIndex(i).objectReferenceValue = aires;
            spawn.ApplyModifiedPropertiesWithoutUndo();
            harness.partySpawn = point;
            harness.playerPrefab = null; harness.allyPrefab = null;
            harness.rectorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefensiveBlockProductionAuthoring.RectorPath);
            harness.chargeSkill = AssetDatabase.LoadAssetAtPath<SkillGemDefinition>(DefensiveBlockProductionAuthoring.SkillPath);
            harness.pauseAutomaticCombat = true;
            var camera = Find<Camera>(scene);
            if (camera == null)
                camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(Unity.Cinemachine.CinemachineBrain)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            var legacy = camera.GetComponent<DefensiveBlockCameraShot>();
            if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
            if (camera.GetComponent<GameplayCameraController>() == null) camera.gameObject.AddComponent<GameplayCameraController>();
            harness.testCamera = camera; harness.blockCamera = null;
            if (Find<NavMeshSurface>(scene) == null)
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Arena Floor"; floor.transform.position = new Vector3(0, -0.25f, 3);
                floor.transform.localScale = new Vector3(24, 0.5f, 30);
                var surface = floor.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();
                AssetDatabase.CreateAsset(surface.navMeshData, Folder + "/ArenaNavMesh.asset");
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Knockback Test Wall (move behind Rector)";
                wall.transform.position = new Vector3(0, 1.5f, 14); wall.transform.localScale = new Vector3(12, 3, 0.5f);
                var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.3f;
                sun.transform.rotation = Quaternion.Euler(45, -30, 0);
                RenderSettings.ambientLight = Color.gray;
            }
            EditorUtility.SetDirty(harness);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save test scene.");
        }
        finally
        {
            if (!opened) EditorSceneManager.CloseScene(scene, true);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
        }
        Debug.Log("Defensive Block test scene now uses production party, skills, input and camera.");
    }
    static T Find<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }
}
#endif
