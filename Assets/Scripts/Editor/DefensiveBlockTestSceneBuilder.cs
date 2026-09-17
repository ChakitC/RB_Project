#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine.InputSystem;

public static class DefensiveBlockTestSceneBuilder
{
    public const string Folder = "Assets/Tests/DefensiveBlock";
    public const string ScenePath = Folder + "/RectorDefensiveBlock.unity";

    [MenuItem("Tools/RB/Defensive Block/Create Test Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring.");
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var originalScene = SceneManager.GetActiveScene();
        var existingTest = SceneManager.GetSceneByPath(ScenePath);
        if (existingTest.IsValid() && existingTest.isDirty)
            throw new InvalidOperationException("Save the open test scene before rebuilding it.");
        bool reopenTest = existingTest.IsValid();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        if (reopenTest) EditorSceneManager.CloseScene(existingTest, true);
        try
        {
            var skill = Copy<SkillGemDefinition>("Assets/Data/Skills/Enemies/Rector/Rector_Skill_1.asset", "RectorCharge.Test.asset");
            var skillData = new SerializedObject(skill);
            skillData.FindProperty("skillId").stringValue = "test.rector.defensive_charge";
            skillData.ApplyModifiedPropertiesWithoutUndo();
            var airesStats = Copy<CharacterStats>("Assets/Scripts/CharacterStats/Asosiation/ChaDef.Aires.asset", "Aires.Test.asset");
            var rectorStats = Copy<CharacterStats>("Assets/Scripts/CharacterStats/Enemy/Stats_NB_GR_02_Rector.asset", "Rector.Test.asset");
            var profile = AssetDatabase.LoadAssetAtPath<BlockAnimationProfile>(Folder + "/BlockAnimation.Test.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<BlockAnimationProfile>();
                AssetDatabase.CreateAsset(profile, Folder + "/BlockAnimation.Test.asset");
            }
            profile.beginClip = Clip("Assets/Animation/Ch_Aires/Aires_Block.fbx");
            profile.impactClip = Clip("Assets/Animation/Ch_Aires/Aires_Block_Root.Test.fbx");
            EditorUtility.SetDirty(profile);
            var player = Prepare("Assets/Prefab/Player/Player.prefab", "Player.Test", airesStats);
            var input = player.GetComponent<PlayerInput>();
            string inputPath = Folder + "/InputActions.Test.asset";
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputPath);
            if (actions == null)
            {
                actions = UnityEngine.Object.Instantiate(input.actions);
                AssetDatabase.CreateAsset(actions, inputPath);
            }
            actions.FindAction("Block", true).ChangeBinding(0).WithPath("<Keyboard>/space");
            actions.FindAction("Dash", true).ChangeBinding(0).WithPath("<Keyboard>/leftShift");
            actions.FindAction("Relord", true).ChangeBinding(0).WithPath("<Keyboard>/t");
            input.actions = actions;
            EditorUtility.SetDirty(actions);
            player.AddComponent<DefensiveBlockReadyCue>().cuePrefab = CreateReadyCuePrefab();
            var ally = Prepare("Assets/Prefab/Player/Ally_Stryker.prefab", "Aires.Test", airesStats);
            var rector = Prepare("Assets/Prefab/GameEnemy/Enemy_B_GR_01 Variant.prefab", "Rector.Test", rectorStats);
            var block = ally.AddComponent<DefensiveBlockController>();
            block.animationProfile = profile;
            block.impactVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Electric/CFXR Lightning Impact.prefab");
            block.member = ally.GetComponentInChildren<FieldAllyMember>(true);
            var attack = rector.AddComponent<DefensiveBlockAttack>();
            attack.skill = skill;
            foreach (var ctx in new[] {player, ally, rector}.Select(x=>x.GetComponent<CharacteContext>())) ctx.ResolveReferences();
            var harness = new GameObject("Defensive Block Test Controls").AddComponent<DefensiveBlockTestHarness>();
            harness.playerPrefab = PrefabUtility.SaveAsPrefabAsset(player, Folder + "/Player.Test.prefab");
            harness.allyPrefab = PrefabUtility.SaveAsPrefabAsset(ally, Folder + "/Aires.Test.prefab");
            harness.rectorPrefab = PrefabUtility.SaveAsPrefabAsset(rector, Folder + "/Rector.Test.prefab");
            harness.chargeSkill = skill;
            UnityEngine.Object.DestroyImmediate(player); UnityEngine.Object.DestroyImmediate(ally); UnityEngine.Object.DestroyImmediate(rector);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Arena Floor"; floor.transform.position = new Vector3(0,-0.25f,3);
            floor.transform.localScale = new Vector3(24,0.5f,30);
            var surface = floor.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            if (surface.navMeshData != null && !AssetDatabase.Contains(surface.navMeshData))
            {
                string navPath = Folder + "/ArenaNavMesh.asset";
                var saved = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(navPath);
                if (saved == null) AssetDatabase.CreateAsset(surface.navMeshData, navPath);
                else
                {
                    var generated = surface.navMeshData;
                    EditorUtility.CopySerialized(generated, saved);
                    surface.RemoveData();
                    surface.navMeshData = saved;
                    UnityEngine.Object.DestroyImmediate(generated);
                    EditorUtility.SetDirty(saved);
                }
            }
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Knockback Test Wall (move behind Rector)";
            wall.transform.position = new Vector3(0,1.5f,14); wall.transform.localScale = new Vector3(12,3,0.5f);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(Unity.Cinemachine.CinemachineBrain)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.transform.position = new Vector3(0,5,-8);
            camera.transform.LookAt(new Vector3(0,1,5)); camera.farClipPlane = 100;
            harness.testCamera = camera;
            harness.blockCamera = camera.gameObject.AddComponent<DefensiveBlockCameraShot>();
            var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f;
            light.transform.rotation = Quaternion.Euler(45,-30,0);
            RenderSettings.ambientLight = new Color(0.5f,0.5f,0.5f);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save test scene.");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (reopenTest) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (originalScene.IsValid()) SceneManager.SetActiveScene(originalScene);
        }
        Debug.Log("Defensive block test scene created: " + ScenePath);
    }
    public static GameObject CreateReadyCuePrefab()
    {
        var shader = Shader.Find("RB/Defensive Block Ready Flare");
        if (shader == null) throw new InvalidOperationException("Import BlockReadyFlare.shader first.");
        string materialPath = Folder + "/BlockReadyFlare.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        try
        {
            quad.name = "BlockReadyFlare";
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return PrefabUtility.SaveAsPrefabAsset(quad, Folder + "/BlockReadyFlare.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(quad); }
    }

    static T Copy<T>(string source, string name) where T : UnityEngine.Object
    {
        string path = Folder + "/" + name;
        if (AssetDatabase.LoadAssetAtPath<T>(path) == null && !AssetDatabase.CopyAsset(source, path))
            throw new InvalidOperationException("Cannot copy " + source);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
    static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
    static GameObject Prepare(string path, string name, CharacterStats stats)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.name = name;
        var ctx = root.GetComponent<CharacteContext>(); ctx.baseStats = stats;
        foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (component != null && (component is CharacterContextPartyLoader || component is MeleeHitboxTrigger))
                UnityEngine.Object.DestroyImmediate(component);
        // Test actors must not load/save the user's party or spawn other party members.
        string[] disabled = {"CharacterContextPartyLoader", "FieldAllyManager", "AllyHelperManager", "AllyHelperProcController", "PartyFormationController", "BehaviorTree", "AgentMoveDriver", "EnemyDropper", "SpecialShootPointController", "PassiveController", "AllyInterruptionController"};
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (component != null && disabled.Contains(component.GetType().Name)) component.enabled = false;
        ctx.ResolveReferences();
        if (ctx.KnockbackMotor != null)
        {
            var motor = new SerializedObject(ctx.KnockbackMotor);
            motor.FindProperty("collisionMask").intValue |= 1; // Test floor/wall use Default.
            motor.ApplyModifiedPropertiesWithoutUndo();
        }
        var visual = ctx.Visual;
        if (visual != null)
        {
            var serialized = new SerializedObject(visual);
            serialized.FindProperty("IsSlot").boolValue = false;
            serialized.FindProperty("buildModelAutomatically").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        return root;
    }
}
#endif
