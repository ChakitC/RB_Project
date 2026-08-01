using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PartySpawnMigrationTool
{
    public const string ConfigPath = "Assets/Data/Party/DefaultPartySpawnConfig.asset";

    const string PlayerPrefabPath = "Assets/Prefab/Player/Player.prefab";
    const string AllyPrefabPath = "Assets/Prefab/Player/Ally_Stryker.prefab";
    const string HelperPrefabPath = "Assets/Prefab/Player/Ally_Helper.prefab";
    const string PlayerUIPrefabPath = "Assets/Prefab/User Interface/PlayerUI.prefab";
    const string LegacySquadPrefabPath = "Assets/Prefab/Player/PlayerSquad.prefab";

    static readonly string[] GameplayScenePaths =
    {
        "Assets/Scenes/MapPlayMode GR_01 Prototy/MapRun.unity",
        "Assets/Scenes/Map_TestAI/Map_TestAI.unity",
        "Assets/Scenes/Map_Play_Pototype/State_1.unity",
        "Assets/Scenes/MapBossTest/BoosTest.unity",
    };

    [MenuItem("Tools/RB/Party/Create Or Update Runtime Party Setup")]
    public static void RunFromMenu()
    {
        RunFromCommandLine();
        EditorUtility.DisplayDialog("Runtime Party", "Party setup and gameplay scenes were migrated.", "OK");
    }

    public static void RunFromCommandLine()
    {
        Debug.Log("[PartySpawnMigration] Refreshing per-scene runtime party references.");
        EnsureSafeEditorState();
        EnsurePlayerUIBinder();
        PartySpawnConfigSO config = CreateOrUpdateConfig();
        MigrateGameplayScenes(config);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        PartySpawnFeatureSmokeTests.RunFromCommandLine();
        Debug.Log("[PartySpawnMigration] Runtime party setup migration completed.");
    }

    static void EnsureSafeEditorState()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Party migration cannot run while entering or using Play Mode.");

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.IsValid() && scene.isDirty)
            {
                throw new InvalidOperationException(
                    $"Save or discard the open scene '{scene.path}' before running party migration.");
            }
        }
    }

    static void EnsurePlayerUIBinder()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PlayerUIPrefabPath);
        try
        {
            if (contents.GetComponentInChildren<PlayerUIRuntimeBinder>(true) == null)
                contents.AddComponent<PlayerUIRuntimeBinder>();

            PrefabUtility.SaveAsPrefabAsset(contents, PlayerUIPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    static PartySpawnConfigSO CreateOrUpdateConfig()
    {
        EnsureAssetFolder("Assets/Data/Party");

        GameObject player = LoadRequiredPrefab(PlayerPrefabPath);
        GameObject ally = LoadRequiredPrefab(AllyPrefabPath);
        GameObject helper = LoadRequiredPrefab(HelperPrefabPath);
        GameObject playerUI = LoadRequiredPrefab(PlayerUIPrefabPath);

        PartySpawnConfigSO config = AssetDatabase.LoadAssetAtPath<PartySpawnConfigSO>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<PartySpawnConfigSO>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }

        config.SetAuthoringData(
            new[]
            {
                new PartySpawnEntry(ChainActorRole.Player, 0, player, Vector3.zero, Vector3.zero),
                new PartySpawnEntry(
                    ChainActorRole.PartySlot1,
                    1,
                    ally,
                    new Vector3(2.18f, 0f, -3.86f),
                    Vector3.zero),
                new PartySpawnEntry(
                    ChainActorRole.PartySlot2,
                    2,
                    ally,
                    new Vector3(-1.82f, 0f, -4.06f),
                    Vector3.zero),
                new PartySpawnEntry(
                    ChainActorRole.Helper,
                    3,
                    helper,
                    new Vector3(-4.94f, 0f, 0.02f),
                    Vector3.zero),
            },
            playerUI);

        EditorUtility.SetDirty(config);
        if (!config.TryValidate(out string error))
            throw new InvalidOperationException($"Generated party config is invalid:\n{error}");

        return config;
    }

    static void MigrateGameplayScenes(PartySpawnConfigSO config)
    {
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            for (int i = 0; i < GameplayScenePaths.Length; i++)
            {
                PartySpawnConfigSO sceneConfig =
                    AssetDatabase.LoadAssetAtPath<PartySpawnConfigSO>(ConfigPath);
                if (sceneConfig == null)
                    throw new InvalidOperationException($"Party spawn config is missing: {ConfigPath}");

                MigrateScene(GameplayScenePaths[i], sceneConfig);
            }
        }
        finally
        {
            if (previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }

    static void MigrateScene(string scenePath, PartySpawnConfigSO config)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        config = AssetDatabase.LoadAssetAtPath<PartySpawnConfigSO>(ConfigPath);
        if (config == null)
            throw new InvalidOperationException($"Party spawn config is missing: {ConfigPath}");

        PartySpawnPoint[] existingMarkers = FindSceneComponents<PartySpawnPoint>(scene);
        GameObject legacyRoot = FindLegacySquadRoot(scene);

        if (existingMarkers.Length > 1)
            throw new InvalidOperationException($"Scene '{scenePath}' has more than one PartySpawnPoint.");

        PartySpawnPoint marker;
        if (existingMarkers.Length == 1)
        {
            marker = existingMarkers[0];
        }
        else
        {
            if (legacyRoot == null)
                throw new InvalidOperationException($"Scene '{scenePath}' has neither PlayerSquad nor PartySpawnPoint.");

            if (TryFindExternalLegacyReference(scene, legacyRoot.transform, out string referenceDescription))
            {
                throw new InvalidOperationException(
                    $"Scene '{scenePath}' has an external reference into PlayerSquad: {referenceDescription}. " +
                    "Move that reference into an IPartySpawnedReceiver before migrating.");
            }

            var markerObject = new GameObject("PartySpawnPoint");
            SceneManager.MoveGameObjectToScene(markerObject, scene);
            markerObject.transform.SetPositionAndRotation(
                legacyRoot.transform.position,
                legacyRoot.transform.rotation);
            marker = markerObject.AddComponent<PartySpawnPoint>();
        }

        var serializedMarker = new SerializedObject(marker);
        serializedMarker.FindProperty("config").objectReferenceValue = config;
        serializedMarker.FindProperty("spawnOnAwake").boolValue = true;
        serializedMarker.ApplyModifiedPropertiesWithoutUndo();

        if (legacyRoot != null)
            UnityEngine.Object.DestroyImmediate(legacyRoot);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static GameObject FindLegacySquadRoot(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        GameObject match = null;

        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(root);
            string sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
            if (!string.Equals(sourcePath, LegacySquadPrefabPath, StringComparison.Ordinal) &&
                !string.Equals(root.name, "PlayerSquad", StringComparison.Ordinal))
            {
                continue;
            }

            if (match != null)
                throw new InvalidOperationException($"Scene '{scene.path}' contains more than one legacy PlayerSquad.");

            match = root;
        }

        return match;
    }

    static bool TryFindExternalLegacyReference(Scene scene, Transform legacyRoot, out string description)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            MonoBehaviour[] behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
            for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
            {
                MonoBehaviour behaviour = behaviours[behaviourIndex];
                if (behaviour == null || behaviour.transform.IsChildOf(legacyRoot))
                    continue;

                var serialized = new SerializedObject(behaviour);
                SerializedProperty property = serialized.GetIterator();
                bool enterChildren = true;
                bool clearedReceiverReference = false;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (property.propertyType != SerializedPropertyType.ObjectReference ||
                        property.propertyPath == "m_Script")
                    {
                        continue;
                    }

                    UnityEngine.Object referenced = property.objectReferenceValue;
                    Transform referencedTransform = referenced switch
                    {
                        GameObject gameObject => gameObject.transform,
                        Component component => component.transform,
                        _ => null,
                    };

                    if (referencedTransform == null || !referencedTransform.IsChildOf(legacyRoot))
                        continue;

                    if (behaviour is IPartySpawnedReceiver ||
                        behaviour is CameraOcclusionCutoutFader)
                    {
                        property.objectReferenceValue = null;
                        clearedReceiverReference = true;
                        continue;
                    }

                    description = $"{GetHierarchyPath(behaviour.transform)}.{property.propertyPath}";
                    return true;
                }

                if (clearedReceiverReference)
                    serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        description = string.Empty;
        return false;
    }

    static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        var results = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            results.AddRange(roots[i].GetComponentsInChildren<T>(true));
        return results.ToArray();
    }

    static GameObject LoadRequiredPrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            throw new InvalidOperationException($"Required prefab is missing: {path}");
        return prefab;
    }

    static void EnsureAssetFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static string GetHierarchyPath(Transform target)
    {
        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }

        return path;
    }
}
