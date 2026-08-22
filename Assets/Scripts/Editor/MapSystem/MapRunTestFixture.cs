using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds an in-memory map run — config, room definitions, room templates, and a controller — so
/// the room-transition transaction can be exercised in Edit Mode. The party warp is replaced with
/// <see cref="MapRunController.PartyWarpOverride"/>, so no party, NavMesh, or physics is needed.
/// </summary>
public sealed class MapRunTestFixture : IDisposable
{
    readonly List<UnityEngine.Object> created = new();

    public MapRunController Controller { get; }
    public MapRunConfigSO Config { get; }

    /// <summary>Set to false to make every party warp fail.</summary>
    public bool WarpSucceeds { get; set; } = true;

    public int CommittedCount { get; private set; }
    public int RolledBackCount { get; private set; }

    /// <param name="asTestStage">
    /// Builds the config as a Test Stage, so stage progression, the Stage Exit portal, and
    /// completion rewards are active.
    /// </param>
    public MapRunTestFixture(bool asTestStage = false)
    {
        Config = BuildConfig(asTestStage);

        var controllerObject = new GameObject("MapRunController");
        created.Add(controllerObject);
        Controller = controllerObject.AddComponent<MapRunController>();

        // Rooms are spawned under the controller so disposing the fixture takes them with it.
        var roomParent = new GameObject("Rooms").transform;
        roomParent.SetParent(controllerObject.transform, false);

        SerializedObject serialized = new SerializedObject(Controller);
        serialized.FindProperty("runConfig").objectReferenceValue = Config;
        serialized.FindProperty("roomParent").objectReferenceValue = roomParent;
        serialized.FindProperty("startRunOnStart").boolValue = false;
        serialized.FindProperty("logLifecycle").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Controller.PartyWarpOverride = (room, direction) => WarpSucceeds;
        Controller.RoomTransitionCommitted += () => CommittedCount++;
        Controller.RoomTransitionRolledBack += () => RolledBackCount++;
    }

    public string StartNodeId => Controller.CurrentGraph != null ? Controller.CurrentGraph.StartNodeId : null;

    /// <summary>First outgoing node of the room the party currently stands in.</summary>
    public string FirstOutgoingNodeId()
    {
        MapNode node = Controller.CurrentNode;
        return node != null && node.OutgoingIds.Count > 0 ? node.OutgoingIds[0] : null;
    }

    public void Dispose()
    {
        if (Controller != null)
            Controller.PartyWarpOverride = null;

        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null)
                UnityEngine.Object.DestroyImmediate(created[i]);
        }

        created.Clear();
    }

    /// <summary>The Stage Exit prefab handed to a Test Stage config, or null otherwise.</summary>
    public GameObject StageExitPrefab { get; private set; }

    MapRunConfigSO BuildConfig(bool asTestStage)
    {
        var config = ScriptableObject.CreateInstance<MapRunConfigSO>();
        config.name = "TestMapRunConfig";
        created.Add(config);

        SerializedObject serialized = new SerializedObject(config);
        serialized.FindProperty("stageId").stringValue = asTestStage ? "map_run_test_stage" : string.Empty;
        serialized.FindProperty("stageDisplayName").stringValue = "MAP RUN TEST STAGE";
        serialized.FindProperty("generationProfile").objectReferenceValue = BuildGenerationProfile();
        serialized.FindProperty("contentPool").objectReferenceValue = BuildContentPool();
        if (asTestStage)
            serialized.FindProperty("progressionProfile").objectReferenceValue = BuildProgressionProfile();
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return config;
    }

    /// <summary>A three-node critical path — Start, Combat, Boss — with no branches.</summary>
    MapGenerationProfileSO BuildGenerationProfile()
    {
        var profile = ScriptableObject.CreateInstance<MapGenerationProfileSO>();
        profile.name = "TestGenerationProfile";
        created.Add(profile);

        SerializedObject serialized = new SerializedObject(profile);
        serialized.FindProperty("randomizeSeed").boolValue = false;
        serialized.FindProperty("seed").intValue = 4242;
        serialized.FindProperty("criticalPathNodeCount").intValue = 3;
        serialized.FindProperty("minBranchCount").intValue = 0;
        serialized.FindProperty("maxBranchCount").intValue = 0;
        serialized.FindProperty("maxOutgoingPerNode").intValue = 2;
        serialized.FindProperty("forceBlueBeforeBoss").boolValue = false;
        SetWeights(serialized.FindProperty("mainPathWeights"), MapNodeType.Combat);
        SetWeights(serialized.FindProperty("blueWeights"), MapNodeType.Reward);
        SetWeights(serialized.FindProperty("branchDeadEndWeights"), MapNodeType.Reward);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return profile;
    }

    MapContentPoolSO BuildContentPool()
    {
        var pool = ScriptableObject.CreateInstance<MapContentPoolSO>();
        pool.name = "TestContentPool";
        created.Add(pool);

        RoomDefinitionSO[] definitions =
        {
            BuildRoomDefinition("Room.Start", MapNodeType.Start),
            BuildRoomDefinition("Room.Combat", MapNodeType.Combat),
            BuildRoomDefinition("Room.Reward", MapNodeType.Reward),
            BuildRoomDefinition("Room.Boss", MapNodeType.Boss),
        };

        SerializedObject serialized = new SerializedObject(pool);
        SerializedProperty rooms = serialized.FindProperty("roomDefinitions");
        rooms.arraySize = definitions.Length;
        for (int i = 0; i < definitions.Length; i++)
            rooms.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
        serialized.FindProperty("encounterDefinitions").arraySize = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return pool;
    }

    StageProgressionProfileSO BuildProgressionProfile()
    {
        var levelTable = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelTableSO>(
            "Assets/Scripts/Player/LevelSystem/Level Character Table SO.asset");
        if (levelTable == null)
            throw new InvalidOperationException("The Test Stage fixture needs the shared LevelTable asset.");

        StageExitPrefab = new GameObject("StageExitPortal.Template");
        StageExitPrefab.SetActive(false);
        StageExitPrefab.AddComponent<StageExitInteractable>();
        created.Add(StageExitPrefab);

        var profile = ScriptableObject.CreateInstance<StageProgressionProfileSO>();
        profile.name = "TestProgressionProfile";
        created.Add(profile);

        SerializedObject serialized = new SerializedObject(profile);
        serialized.FindProperty("levelTable").objectReferenceValue = levelTable;
        serialized.FindProperty("startLevel").intValue = 1;
        serialized.FindProperty("targetLevel").intValue = 2;
        serialized.FindProperty("targetRunCount").intValue = 1;
        SerializedProperty tiers = serialized.FindProperty("enemyLevelTiers");
        tiers.arraySize = 1;
        tiers.GetArrayElementAtIndex(0).intValue = 1;
        serialized.FindProperty("stageExitPrefab").objectReferenceValue = StageExitPrefab;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return profile;
    }

    static void SetWeights(SerializedProperty weights, MapNodeType type)
    {
        weights.arraySize = 1;
        SerializedProperty entry = weights.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("type").enumValueIndex = (int)type;
        entry.FindPropertyRelative("weight").floatValue = 1f;
    }

    RoomDefinitionSO BuildRoomDefinition(string name, MapNodeType type)
    {
        GameObject template = new GameObject($"{name}.Template");
        template.SetActive(false);
        template.AddComponent<RoomController>();
        created.Add(template);

        var definition = ScriptableObject.CreateInstance<RoomDefinitionSO>();
        definition.name = name;
        created.Add(definition);

        SerializedObject serialized = new SerializedObject(definition);
        serialized.FindProperty("displayName").stringValue = name;
        serialized.FindProperty("nodeType").enumValueIndex = (int)type;
        serialized.FindProperty("weight").floatValue = 1f;
        serialized.FindProperty("roomPrefab").objectReferenceValue = template;
        serialized.FindProperty("maxExitCount").intValue = 4;
        serialized.FindProperty("exitMask").intValue = (int)(RoomExitMask.Up | RoomExitMask.Right | RoomExitMask.Down | RoomExitMask.Left);
        serialized.FindProperty("allowSupersetExitMask").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return definition;
    }
}
