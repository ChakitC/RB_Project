#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Project-wide content check for the map system. It discovers every <see cref="MapRunConfigSO"/>
/// in the project and validates the whole chain a run depends on: stage identity, node-type
/// coverage, encounter content, enemy prefabs, room prefabs, and the Stage Exit portal.
///
/// Errors mean a run would break, soft-lock, or silently produce wrong content. Warnings mean the
/// authoring is degraded but runtime still has a working fallback.
/// </summary>
public static class MapContentValidator
{
    const string InteractableLayerName = "Interactable";

    [MenuItem("Tools/RB Project/Map/Validate Map Content")]
    public static void ValidateProjectAndLog()
    {
        List<MapContentIssue> issues = ValidateProject();
        int errors = 0;
        for (int i = 0; i < issues.Count; i++)
        {
            if (issues[i].IsError)
                errors++;
        }

        if (issues.Count == 0)
        {
            Debug.Log("[MapContentValidator] Map content is clean.");
            return;
        }

        var report = new System.Text.StringBuilder();
        report.AppendLine($"[MapContentValidator] {errors} error(s), {issues.Count - errors} warning(s).");
        for (int i = 0; i < issues.Count; i++)
            report.AppendLine(issues[i].ToString());

        if (errors > 0)
            Debug.LogError(report.ToString());
        else
            Debug.LogWarning(report.ToString());
    }

    public static List<MapContentIssue> ValidateProject()
    {
        var issues = new List<MapContentIssue>();
        List<MapRunConfigSO> configs = LoadAllRunConfigs();

        ValidateStageIds(configs, issues);

        // Room and encounter definitions are shared between configs. Each asset is reported once.
        var validatedAssets = new HashSet<Object>();
        HashSet<RoomDefinitionSO> testStageRooms = CollectTestStageRoomDefinitions(configs);
        for (int i = 0; i < configs.Count; i++)
            ValidateConfig(configs[i], issues, validatedAssets, testStageRooms);

        issues.AddRange(StageCatalogValidator.ValidateProject());
        return issues;
    }

    /// <summary>Every run config in the project, ordered by asset path so reports are stable.</summary>
    public static List<MapRunConfigSO> LoadAllRunConfigs()
    {
        var configs = new List<MapRunConfigSO>();
        string[] guids = AssetDatabase.FindAssets($"t:{nameof(MapRunConfigSO)}");
        var paths = new List<string>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
            paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));

        paths.Sort(System.StringComparer.Ordinal);
        for (int i = 0; i < paths.Count; i++)
        {
            var config = AssetDatabase.LoadAssetAtPath<MapRunConfigSO>(paths[i]);
            if (config != null)
                configs.Add(config);
        }

        return configs;
    }

    static void ValidateStageIds(List<MapRunConfigSO> configs, List<MapContentIssue> issues)
    {
        var byStageId = new Dictionary<string, List<MapRunConfigSO>>(System.StringComparer.Ordinal);
        for (int i = 0; i < configs.Count; i++)
        {
            MapRunConfigSO config = configs[i];
            string stageId = config.StageId;
            if (string.IsNullOrEmpty(stageId))
            {
                // Legal, but it turns off every Test Stage system, so it is always worth saying.
                Add(issues, MapContentIssueSeverity.Warning, config,
                    "Stage Id is empty, so this config is not a Test Stage: stage progression, " +
                    "enemy levels, run XP, and the Stage Exit portal are all inactive.");
                continue;
            }

            if (!byStageId.TryGetValue(stageId, out List<MapRunConfigSO> sharing))
            {
                sharing = new List<MapRunConfigSO>();
                byStageId[stageId] = sharing;
            }

            sharing.Add(config);
        }

        foreach (KeyValuePair<string, List<MapRunConfigSO>> pair in byStageId)
        {
            if (pair.Value.Count < 2)
                continue;

            var names = new List<string>();
            for (int i = 0; i < pair.Value.Count; i++)
                names.Add(AssetDatabase.GetAssetPath(pair.Value[i]));

            for (int i = 0; i < pair.Value.Count; i++)
            {
                Add(issues, MapContentIssueSeverity.Error, pair.Value[i],
                    $"Stage Id '{pair.Key}' is shared with: {string.Join(", ", names)}. " +
                    "Stage progress is saved per Stage Id, so the runs would overwrite each other.");
            }
        }
    }

    public static void ValidateConfig(MapRunConfigSO config, List<MapContentIssue> issues)
    {
        ValidateConfig(config, issues, new HashSet<Object>(), CollectTestStageRoomDefinitions(new List<MapRunConfigSO> { config }));
    }

    /// <summary>Room definitions any Test Stage can route a node through.</summary>
    static HashSet<RoomDefinitionSO> CollectTestStageRoomDefinitions(List<MapRunConfigSO> configs)
    {
        var rooms = new HashSet<RoomDefinitionSO>();
        for (int i = 0; i < configs.Count; i++)
        {
            MapRunConfigSO config = configs[i];
            if (config == null || !config.IsTestStage || config.RoomDefinitions == null)
                continue;

            for (int j = 0; j < config.RoomDefinitions.Length; j++)
            {
                if (config.RoomDefinitions[j] != null)
                    rooms.Add(config.RoomDefinitions[j]);
            }
        }

        return rooms;
    }

    static void ValidateConfig(
        MapRunConfigSO config,
        List<MapContentIssue> issues,
        HashSet<Object> validatedAssets,
        HashSet<RoomDefinitionSO> testStageRooms)
    {
        if (config == null)
            return;

        if (!MapRunConfigValidator.Validate(config, out string configError))
            Add(issues, MapContentIssueSeverity.Error, config, configError);

        ValidateNodeTypeCoverage(config, issues);
        ValidateEncounterDefinitions(config, issues, validatedAssets);
        ValidateRoomDefinitions(config, issues, validatedAssets, testStageRooms);
        ValidateStageExitPrefab(config, issues);
    }

    /// <summary>
    /// Every node type the generator can produce for this config needs both a usable room and,
    /// when the type fights, a usable encounter. A missing encounter completes the room instantly,
    /// which turns a Combat or Boss node into an empty corridor.
    /// </summary>
    static void ValidateNodeTypeCoverage(MapRunConfigSO config, List<MapContentIssue> issues)
    {
        var types = new HashSet<MapNodeType> { MapNodeType.Start, MapNodeType.Boss };
        CollectWeightedTypes(config.MainPathWeights, types);
        CollectWeightedTypes(config.BlueWeights, types);
        types.Add(config.PitySystem.ForcedBlueType);
        if (config.MaxBranchCount > 0)
            CollectWeightedTypes(config.BranchDeadEndWeights, types);

        foreach (MapNodeType type in types)
        {
            if (!HasUsableRoomDefinition(config, type))
            {
                Add(issues, MapContentIssueSeverity.Error, config,
                    $"Node type {type} can be generated, but no enabled room definition with a prefab supports it.");
            }

            if (!RequiresEncounter(type))
                continue;

            if (!HasUsableEncounterDefinition(config, type))
            {
                Add(issues, MapContentIssueSeverity.Error, config,
                    $"Node type {type} can be generated, but no enabled encounter definition supports it. " +
                    "The room would complete with no enemies.");
            }
        }
    }

    static void ValidateEncounterDefinitions(MapRunConfigSO config, List<MapContentIssue> issues, HashSet<Object> validatedAssets)
    {
        EncounterDefinitionSO[] encounters = config.EncounterDefinitions;
        if (encounters == null)
            return;

        for (int i = 0; i < encounters.Length; i++)
        {
            EncounterDefinitionSO encounter = encounters[i];
            if (encounter == null)
            {
                Add(issues, MapContentIssueSeverity.Error, config, $"EncounterDefinitions[{i}] is empty.");
                continue;
            }

            if (validatedAssets.Add(encounter))
                ValidateEncounter(encounter, issues);
        }
    }

    public static void ValidateEncounter(EncounterDefinitionSO encounter, List<MapContentIssue> issues)
    {
        bool shouldBeBoss = encounter.NodeType == MapNodeType.Boss;
        if (encounter.BossEncounter != shouldBeBoss)
        {
            Add(issues, MapContentIssueSeverity.Error, encounter,
                $"Boss Encounter is {encounter.BossEncounter} but Node Type is {encounter.NodeType}. " +
                "The flag decides which XP pool the spawns draw from, so the two must agree.");
        }

        EncounterWave[] waves = encounter.Waves;
        if (waves == null || waves.Length == 0)
        {
            Add(issues, MapContentIssueSeverity.Error, encounter,
                "The encounter has no waves. At runtime the room completes immediately.");
            return;
        }

        for (int i = 0; i < waves.Length; i++)
        {
            EncounterWave wave = waves[i];
            if (wave == null)
            {
                Add(issues, MapContentIssueSeverity.Error, encounter, $"Wave {i} is empty.");
                continue;
            }

            ValidateWave(encounter, i, wave, issues);
        }
    }

    static void ValidateWave(EncounterDefinitionSO encounter, int waveIndex, EncounterWave wave, List<MapContentIssue> issues)
    {
        GameObject[] prefabs = wave.EnemyPrefabs;
        int usable = 0;
        if (prefabs != null)
        {
            for (int i = 0; i < prefabs.Length; i++)
            {
                GameObject prefab = prefabs[i];
                if (prefab == null)
                {
                    Add(issues, MapContentIssueSeverity.Warning, encounter,
                        $"Wave {waveIndex} enemy prefab slot {i} is empty.");
                    continue;
                }

                usable++;
                ValidateEnemyPrefab(encounter, waveIndex, i, prefab, issues);
            }
        }

        if (usable == 0)
        {
            Add(issues, MapContentIssueSeverity.Error, encounter,
                $"Wave {waveIndex} has no usable enemy prefab, so its {wave.SpawnCount} spawn(s) never happen.");
        }
    }

    static void ValidateEnemyPrefab(
        EncounterDefinitionSO encounter,
        int waveIndex,
        int slotIndex,
        GameObject prefab,
        List<MapContentIssue> issues)
    {
        string where = $"Wave {waveIndex} enemy prefab slot {slotIndex} ('{prefab.name}')";

        EnemyContext enemyContext = prefab.GetComponentInChildren<EnemyContext>(true);
        if (enemyContext == null)
        {
            Add(issues, MapContentIssueSeverity.Error, encounter,
                $"{where} has no EnemyContext. Stage enemy level and XP cannot be assigned to it.");
            return;
        }

        if (enemyContext.baseStats == null)
        {
            Add(issues, MapContentIssueSeverity.Error, encounter,
                $"{where} has an EnemyContext with no base stats.");
        }

        if (prefab.GetComponentInChildren<HealthSystem>(true) == null)
        {
            Add(issues, MapContentIssueSeverity.Error, encounter,
                $"{where} has no HealthSystem. EncounterDirector cannot track its death, " +
                "so the room would never clear.");
        }
    }

    static void ValidateRoomDefinitions(
        MapRunConfigSO config,
        List<MapContentIssue> issues,
        HashSet<Object> validatedAssets,
        HashSet<RoomDefinitionSO> testStageRooms)
    {
        RoomDefinitionSO[] definitions = config.RoomDefinitions;
        if (definitions == null)
            return;

        for (int i = 0; i < definitions.Length; i++)
        {
            RoomDefinitionSO definition = definitions[i];
            if (definition == null)
            {
                Add(issues, MapContentIssueSeverity.Error, config, $"RoomDefinitions[{i}] is empty.");
                continue;
            }

            if (validatedAssets.Add(definition))
                ValidateRoomDefinition(definition, issues, testStageRooms != null && testStageRooms.Contains(definition));
        }
    }

    public static void ValidateRoomDefinition(
        RoomDefinitionSO definition,
        List<MapContentIssue> issues,
        bool usedByTestStage = false)
    {
        GameObject prefab = definition.RoomPrefab;
        if (prefab == null)
        {
            Add(issues, MapContentIssueSeverity.Error, definition, "Room Prefab is empty.");
            return;
        }

        RoomController controller = prefab.GetComponentInChildren<RoomController>(true);
        if (controller == null)
        {
            Add(issues, MapContentIssueSeverity.Error, definition,
                $"Room prefab '{prefab.name}' has no RoomController. Runtime adds one, but the added " +
                "component has no authored sockets, exits, or lockdown objects.");
            return;
        }

        ValidateRoomNavMesh(definition, prefab, issues);
        int directionalSpawns = ValidateRoomExits(definition, controller, issues);
        ValidateRoomSpawnSockets(definition, controller, directionalSpawns, issues);

        // Room-specific behaviour is authored as a lifecycle component on the prefab. Without it a
        // Test Stage Heal room is just an empty room: nothing configures the heal or ammo station.
        if (usedByTestStage &&
            definition.NodeType == MapNodeType.Heal &&
            prefab.GetComponentInChildren<TestStageRecoveryStations>(true) == null)
        {
            Add(issues, MapContentIssueSeverity.Error, definition,
                $"Room prefab \'{prefab.name}\' has no TestStageRecoveryStations. A Test Stage routed " +
                "through this Heal room would leave its heal and ammo stations unconfigured.");
        }
    }

    static void ValidateRoomNavMesh(RoomDefinitionSO definition, GameObject prefab, List<MapContentIssue> issues)
    {
        NavMeshSurface[] surfaces = prefab.GetComponentsInChildren<NavMeshSurface>(true);
        if (surfaces.Length == 0)
        {
            Add(issues, MapContentIssueSeverity.Error, definition,
                $"Room prefab '{prefab.name}' has no NavMeshSurface. Companions cannot be warped into it.");
            return;
        }

        for (int i = 0; i < surfaces.Length; i++)
        {
            if (surfaces[i] != null && surfaces[i].navMeshData == null)
            {
                Add(issues, MapContentIssueSeverity.Error, definition,
                    $"NavMeshSurface '{surfaces[i].name}' on room prefab '{prefab.name}' has no baked NavMeshData.");
            }
        }
    }

    /// <summary>
    /// The generator only ever asks a room for directions inside its <c>ExitMask</c>, so every
    /// direction in the mask must exist as an authored exit socket on the prefab.
    /// </summary>
    /// <returns>How many directions in the Exit Mask have an entrance spawn point.</returns>
    static int ValidateRoomExits(RoomDefinitionSO definition, RoomController controller, List<MapContentIssue> issues)
    {
        RoomExitInteractable[] exits = ResolveAuthoredExits(controller);
        var authored = new Dictionary<RoomExitDirection, int>();
        for (int i = 0; i < exits.Length; i++)
        {
            RoomExitInteractable exit = exits[i];
            if (exit == null)
                continue;

            RoomExitDirection direction = exit.AuthoredDirection;
            authored.TryGetValue(direction, out int count);
            authored[direction] = count + 1;
        }

        foreach (KeyValuePair<RoomExitDirection, int> pair in authored)
        {
            if (pair.Value > 1)
            {
                Add(issues, MapContentIssueSeverity.Error, definition,
                    $"Room prefab has {pair.Value} exit sockets authored as {pair.Key}. " +
                    "Only the first is ever configured.");
            }

            if ((definition.ExitMask & RoomExitDirectionUtility.ToMask(pair.Key)) == 0)
            {
                Add(issues, MapContentIssueSeverity.Warning, definition,
                    $"Room prefab has a {pair.Key} exit socket, but Exit Mask is {definition.ExitMask}, " +
                    "so that socket can never be used.");
            }
        }

        int directionalSpawns = 0;
        for (int i = 0; i < 4; i++)
        {
            var direction = (RoomExitDirection)i;
            if ((definition.ExitMask & RoomExitDirectionUtility.ToMask(direction)) == 0)
                continue;

            if (!authored.ContainsKey(direction))
            {
                Add(issues, MapContentIssueSeverity.Error, definition,
                    $"Exit Mask includes {direction}, but the room prefab has no RoomExitInteractable " +
                    "authored in that direction. A node routed through it would have no usable door.");
                continue;
            }

            if (HasDirectionalPlayerSpawn(controller, exits, direction))
            {
                directionalSpawns++;
                continue;
            }

            Add(issues, MapContentIssueSeverity.Warning, definition,
                $"No entrance spawn point for {direction}. The party falls back to the room's " +
                "generic player spawn point, so it can enter facing the wrong way.");
        }

        return directionalSpawns;
    }

    static void ValidateRoomSpawnSockets(
        RoomDefinitionSO definition,
        RoomController controller,
        int directionalSpawns,
        List<MapContentIssue> issues)
    {
        bool fights = MapPitySystem.IsRedNodeType(definition.NodeType) || definition.NodeType == MapNodeType.Boss;
        SerializedObject serialized = new SerializedObject(controller);

        if (fights && CountAssigned(serialized.FindProperty("enemySpawnPoints")) == 0)
        {
            Add(issues, MapContentIssueSeverity.Error, definition,
                $"A {definition.NodeType} room has no enemy spawn points. Every spawn would stack on " +
                "the room origin.");
        }

        // Rooms in this project author their entrance spawns per doorway, so the generic socket is
        // only worth reporting when there is no directional spawn either.
        if (directionalSpawns == 0 && serialized.FindProperty("playerSpawnPoint").objectReferenceValue == null)
        {
            Add(issues, MapContentIssueSeverity.Warning, definition,
                "No entrance spawn point at all. The party falls back to the room transform.");
        }

        if (definition.NodeType == MapNodeType.Boss &&
            serialized.FindProperty("stageExitSpawnPoint").objectReferenceValue == null)
        {
            Add(issues, MapContentIssueSeverity.Warning, definition,
                "A Boss room has no Stage Exit spawn point. The portal falls back to the first loot " +
                "spawn point, or to the room origin.");
        }
    }

    static void ValidateStageExitPrefab(MapRunConfigSO config, List<MapContentIssue> issues)
    {
        if (!config.IsTestStage)
            return;

        GameObject prefab = config.StageExitPrefab;
        if (prefab == null)
            return;

        if (prefab.GetComponentInChildren<StageExitInteractable>(true) == null)
        {
            Add(issues, MapContentIssueSeverity.Error, config,
                $"Stage Exit prefab '{prefab.name}' has no StageExitInteractable.");
        }

        if (prefab.GetComponentInChildren<InteractableLink>(true) == null)
        {
            Add(issues, MapContentIssueSeverity.Error, config,
                $"Stage Exit prefab '{prefab.name}' has no InteractableLink, so the Interactor " +
                "cannot resolve the portal from its collider.");
        }

        Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
        bool hasTrigger = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].isTrigger)
                hasTrigger = true;
        }

        if (!hasTrigger)
        {
            Add(issues, MapContentIssueSeverity.Error, config,
                $"Stage Exit prefab '{prefab.name}' has no trigger collider for the Interactor sphere cast.");
        }

        int interactableLayer = LayerMask.NameToLayer(InteractableLayerName);
        if (interactableLayer >= 0 && prefab.layer != interactableLayer)
        {
            Add(issues, MapContentIssueSeverity.Error, config,
                $"Stage Exit prefab '{prefab.name}' is on layer '{LayerMask.LayerToName(prefab.layer)}' " +
                $"instead of '{InteractableLayerName}'.");
        }
    }

    static RoomExitInteractable[] ResolveAuthoredExits(RoomController controller)
    {
        // Mirrors RoomController.ResolveExits: an empty serialized list is filled from the children.
        SerializedProperty exits = new SerializedObject(controller).FindProperty("exits");
        if (exits.arraySize == 0)
            return controller.GetComponentsInChildren<RoomExitInteractable>(true);

        var resolved = new List<RoomExitInteractable>(exits.arraySize);
        for (int i = 0; i < exits.arraySize; i++)
        {
            if (exits.GetArrayElementAtIndex(i).objectReferenceValue is RoomExitInteractable exit)
                resolved.Add(exit);
        }

        return resolved.ToArray();
    }

    static bool HasDirectionalPlayerSpawn(RoomController controller, RoomExitInteractable[] exits, RoomExitDirection direction)
    {
        SerializedProperty byDirection = new SerializedObject(controller).FindProperty("playerSpawnPointsByDirection");
        for (int i = 0; i < byDirection.arraySize; i++)
        {
            SerializedProperty entry = byDirection.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("direction").enumValueIndex == (int)direction &&
                entry.FindPropertyRelative("spawnPoint").objectReferenceValue != null)
            {
                return true;
            }
        }

        // RoomController also accepts a "SpawnPoint"-named child underneath the exit socket.
        for (int i = 0; i < exits.Length; i++)
        {
            RoomExitInteractable exit = exits[i];
            if (exit == null || exit.AuthoredDirection != direction)
                continue;

            Transform[] children = exit.GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < children.Length; j++)
            {
                if (children[j] != exit.transform &&
                    children[j].name.IndexOf("SpawnPoint", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    static int CountAssigned(SerializedProperty array)
    {
        int count = 0;
        for (int i = 0; i < array.arraySize; i++)
        {
            if (array.GetArrayElementAtIndex(i).objectReferenceValue != null)
                count++;
        }

        return count;
    }

    static void CollectWeightedTypes(WeightedMapNodeType[] weights, HashSet<MapNodeType> types)
    {
        if (weights == null)
            return;

        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i] != null && weights[i].weight > 0f)
                types.Add(weights[i].type);
        }
    }

    static bool RequiresEncounter(MapNodeType type)
    {
        return MapPitySystem.IsRedNodeType(type) || type == MapNodeType.Boss;
    }

    static bool HasUsableRoomDefinition(MapRunConfigSO config, MapNodeType type)
    {
        RoomDefinitionSO[] definitions = config.RoomDefinitions;
        if (definitions == null)
            return false;

        for (int i = 0; i < definitions.Length; i++)
        {
            RoomDefinitionSO definition = definitions[i];
            if (definition != null && definition.Weight > 0f && definition.NodeType == type && definition.RoomPrefab != null)
                return true;
        }

        return false;
    }

    static bool HasUsableEncounterDefinition(MapRunConfigSO config, MapNodeType type)
    {
        EncounterDefinitionSO[] encounters = config.EncounterDefinitions;
        if (encounters == null)
            return false;

        for (int i = 0; i < encounters.Length; i++)
        {
            EncounterDefinitionSO encounter = encounters[i];
            if (encounter != null && encounter.Weight > 0f && encounter.NodeType == type)
                return true;
        }

        return false;
    }

    static void Add(List<MapContentIssue> issues, MapContentIssueSeverity severity, Object context, string message)
    {
        issues.Add(new MapContentIssue(severity, context != null ? context.name : "<unknown>", message, context));
    }
}
#endif
