#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TestStageAuthoringTool
{
    const string BaseConfigPath = "Assets/Data/Map/Test_Map Run Config SO.asset";
    const string LevelTablePath = "Assets/Scripts/Player/LevelSystem/Level Character Table SO.asset";
    const string OutputFolder = "Assets/Data/Map/TestStages";
    const string ProfileFolder = "Assets/Data/Map/Profiles";
    const string StatsFolder = "Assets/Data/EnemyStats/TestStage";
    const string PortalPath = "Assets/Prefab/MAP/TestStage/Stage Exit Cyan.prefab";
    const string HealRoomDefinitionPath = "Assets/Data/Map/RoomDefinition.Heal.asset";
    const string BasementPath = "Assets/Scenes/Basement/Basement.unity";
    const string UndoName = "Apply Test Stage Content";
    const string TestStagePaginationName = "TestStagePagination";
    const string ExistingMapsPageName = "ExistingMapsPage";
    const string TestStagePageName = "TestStagePage";
    const string PreviousPageButtonName = "PreviousPage";
    const string NextPageButtonName = "NextPage";

    static readonly string[] CombatRoomPaths =
    {
        "Assets/Prefab/MAP/Combat/Combat.Up.DeadEnd.Up.prefab",
        "Assets/Prefab/MAP/Combat/Combat.Intersection.prefab",
        "Assets/Prefab/MAP/Combat/Combat.Turn.Up.Right.prefab",
        "Assets/Prefab/MAP/Combat/Combat.Up.Down.prefab",
        "Assets/Prefab/MAP/Combat/Combat.Tjunction.Left.Right.Up.prefab",
    };

    [MenuItem("Tools/RB Project/Map/Validate Basement Board (Dry Run)")]
    public static void ValidateBasementBoard()
    {
        var report = new List<string>();
        ConfigureBasement(true, report, StageConfigPath(1), StageConfigPath(2), StageConfigPath(3));
        Debug.Log($"[TestStageAuthoringTool] Basement board dry run:\n{string.Join("\n", report)}");
    }

    [MenuItem("Tools/RB Project/Map/Apply Test Stage Content")]
    public static void ApplyAll()
    {
        EnsureFolder(OutputFolder);
        EnsureFolder(ProfileFolder);
        EnsureFolder(StatsFolder);
        EnsureFolder("Assets/Prefab/MAP/TestStage");

        GameObject portal = CreateStageExitPrefab();
        RoomDefinitionSO healRoom = LoadHealRoomDefinition();
        CharacterStats m1Stats = CreateEnemyStats(
            "Enemy_M_GR_01", "Assets/Scripts/ScriptableObject/Mons/Mons_GR01_MaleData.asset",
            380f, 7f, 15f, 3f, 3f, 0.25f, 4.5f);
        CharacterStats m2Stats = CreateEnemyStats(
            "Enemy_M_GR_02", "Assets/Scripts/ScriptableObject/Mons/Mons_GR01_MaleData.asset",
            550f, 10f, 22f, 3.5f, 7f, 0.35f, 4.5f);
        CharacterStats eliteStats = CreateEnemyStats(
            "Enemy_E_GR_01", "Assets/Character/Mons/Mons.GR004/M_GR04.Def.asset",
            1010f, 16f, 40f, 5f, 15f, 0.50f, 4.5f);
        CharacterStats bossStats = CreateEnemyStats(
            "Enemy_B_GR_01", "Assets/Scripts/CharacterStats/Enemy/Stats_NB_GR_02_Rector.asset",
            6330f, 100f, 80f, 7f, 25f, 0.75f, 5f);

        GunConfig enemyWeapon = AssetDatabase.LoadAssetAtPath<GunConfig>(
            "Assets/Scripts/ScriptableObject/Weapon/SMG/Enemy_SMG_GR01.asset");
        if (enemyWeapon == null)
            throw new InvalidOperationException("Test Stage enemy weapon asset is missing.");

        EnemyDropProfile m1DropProfile = AssetDatabase.LoadAssetAtPath<EnemyDropProfile>(
            "Assets/Scripts/ItemDrop/TestStage.DropProfile.M1.asset");
        EnemyDropProfile m2DropProfile = AssetDatabase.LoadAssetAtPath<EnemyDropProfile>(
            "Assets/Scripts/ItemDrop/TestStage.DropProfile.M2.asset");
        EnemyDropProfile eliteDropProfile = AssetDatabase.LoadAssetAtPath<EnemyDropProfile>(
            "Assets/Scripts/ItemDrop/TestStage.DropProfile.Elite.asset");
        EnemyDropProfile bossDropProfile = AssetDatabase.LoadAssetAtPath<EnemyDropProfile>(
            "Assets/Scripts/ItemDrop/TestStage.DropProfile.Boss.asset");
        if (m1DropProfile == null || m2DropProfile == null || eliteDropProfile == null || bossDropProfile == null)
            throw new InvalidOperationException("One or more Test Stage enemy drop profiles are missing.");

        GameObject m1 = ConfigureEnemyPrefab("Assets/Prefab/GameEnemy/Enemy_M_GR_01 Variant.prefab", m1Stats, enemyWeapon, m1DropProfile);
        GameObject m2 = ConfigureEnemyPrefab("Assets/Prefab/GameEnemy/Enemy_M_GR_02 Variant.prefab", m2Stats, enemyWeapon, m2DropProfile);
        GameObject elite = ConfigureEnemyPrefab("Assets/Prefab/GameEnemy/Enemy_E_GR_01 Variant.prefab", eliteStats, enemyWeapon, eliteDropProfile);
        GameObject boss = ConfigureEnemyPrefab("Assets/Prefab/GameEnemy/Enemy_B_GR_01 Variant.prefab", bossStats, enemyWeapon, bossDropProfile);

        EncounterDefinitionSO stage1Combat = CreateEncounter("Test Stage 01 Combat", false,
            new[] { new[] { m1, m2 } }, new[] { 4 });
        EncounterDefinitionSO stage1Boss = CreateEncounter("Test Stage 01 Boss", true,
            new[] { new[] { boss } }, new[] { 1 });
        EncounterDefinitionSO stage2Combat = CreateEncounter("Test Stage 02 Combat", false,
            new[] { new[] { m1, m2 }, new[] { elite } }, new[] { 4, 2 });
        EncounterDefinitionSO stage2Boss = CreateEncounter("Test Stage 02 Boss", true,
            new[] { new[] { boss } }, new[] { 1 });
        EncounterDefinitionSO stage3Combat = CreateEncounter("Test Stage 03 Combat", false,
            new[] { new[] { m1, m2, elite }, new[] { elite } }, new[] { 4, 4 });
        EncounterDefinitionSO stage3Boss = CreateEncounter("Test Stage 03 Boss", true,
            new[] { new[] { boss } }, new[] { 1 });

        CreateStageConfig(1, "test_stage_01", "TEST STAGE 01", 1, 11, 2,
            new[] { 5, 10 }, portal, healRoom, stage1Combat, stage1Boss);
        CreateStageConfig(2, "test_stage_02", "TEST STAGE 02", 11, 20, 3,
            new[] { 13, 17, 20 }, portal, healRoom, stage2Combat, stage2Boss);
        CreateStageConfig(3, "test_stage_03", "TEST STAGE 03", 20, 30, 5,
            new[] { 22, 24, 26, 28, 30 }, portal, healRoom, stage3Combat, stage3Boss);

        for (int i = 0; i < CombatRoomPaths.Length; i++)
            ConfigureCombatRoom(CombatRoomPaths[i]);
        ConfigureBossRoom("Assets/Prefab/MAP/Boss/BossTest.DeadEnd.Up.prefab");
        // Saved before the Basement scene opens: opening a scene in Single mode unloads
        // unreferenced assets, and unsaved edits on the freshly created configs would be lost.
        AssetDatabase.SaveAssets();

        var report = new List<string>();
        ConfigureBasement(false, report, StageConfigPath(1), StageConfigPath(2), StageConfigPath(3));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TestStageAuthoringTool] Test Stage content authoring completed. Basement board:\n{string.Join("\n", report)}");
    }

    static CharacterStats CreateEnemyStats(
        string id,
        string presentationSourcePath,
        float hp,
        float hpScaling,
        float damage,
        float damageScaling,
        float armor,
        float armorScaling,
        float speed)
    {
        string path = $"{StatsFolder}/{id} Stats.asset";
        CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(path);
        if (stats == null)
        {
            stats = ScriptableObject.CreateInstance<CharacterStats>();
            AssetDatabase.CreateAsset(stats, path);
        }

        stats.characterId = id;
        stats.characterName = id;
        CopyCharacterPresentation(
            AssetDatabase.LoadAssetAtPath<CharacterStats>(presentationSourcePath),
            stats,
            presentationSourcePath);
        stats.maxHP = hp;
        stats.MAXHPScaling = hpScaling;
        stats.Damage = damage;
        stats.DamageScaling = damageScaling;
        stats.armor = armor;
        stats.ArmorScaling = armorScaling;
        stats.speed = speed;
        stats.critRate = 0f;
        stats.CritrateScaling = 0f;
        stats.CritDamageScaling = 0f;
        stats.StaminaScaling = 0f;
        stats.EnagyScaling = 0f;
        stats.SpeedScaling = 0f;
        EditorUtility.SetDirty(stats);
        return stats;
    }

    static void CopyCharacterPresentation(CharacterStats source, CharacterStats destination, string sourcePath)
    {
        if (source == null)
            throw new InvalidOperationException($"Character presentation source is missing at '{sourcePath}'.");

        destination.icon = source.icon;
        destination.CharacterPrefab = source.CharacterPrefab;
        destination.CharacterPrefabBasement = source.CharacterPrefabBasement;
        destination.characterAvatar = source.characterAvatar;
        destination.controller = source.controller;
        destination.animProfile = source.animProfile;
        destination.behaviorSubtree = source.behaviorSubtree;
        destination.weaponHandMode = source.weaponHandMode;
        destination.combatRole = source.combatRole;
    }

    static GameObject ConfigureEnemyPrefab(
        string path,
        CharacterStats stats,
        GunConfig enemyWeapon,
        EnemyDropProfile dropProfile)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            EnemyContext context = root.GetComponentInChildren<EnemyContext>(true);
            if (context == null)
                throw new InvalidOperationException($"Enemy prefab '{path}' has no EnemyContext.");

            context.baseStats = stats;
            context.currentWeapon = enemyWeapon;
            WeaponSystem weaponSystem = context.GetComponent<WeaponSystem>();
            if (weaponSystem != null)
            {
                SerializedObject weaponSerialized = new SerializedObject(weaponSystem);
                weaponSerialized.FindProperty("currentWeapon").objectReferenceValue = enemyWeapon;
                weaponSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(weaponSystem);
            }
            EnemyDropper dropper = context.GetComponent<EnemyDropper>();
            if (dropper == null)
                throw new InvalidOperationException($"Enemy prefab '{path}' has no EnemyDropper.");
            dropper.profile = dropProfile;
            EditorUtility.SetDirty(dropper);
            EnemyLevelSystem enemyLevel = context.GetComponent<EnemyLevelSystem>();
            if (enemyLevel == null)
                enemyLevel = context.gameObject.AddComponent<EnemyLevelSystem>();
            context.EnemyLevelSystem = enemyLevel;
            EditorUtility.SetDirty(context);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    static EncounterDefinitionSO CreateEncounter(string displayName, bool boss, GameObject[][] wavePools, int[] spawnCounts)
    {
        string path = $"{OutputFolder}/{displayName}.asset";
        EncounterDefinitionSO encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinitionSO>(path);
        if (encounter == null)
        {
            encounter = ScriptableObject.CreateInstance<EncounterDefinitionSO>();
            AssetDatabase.CreateAsset(encounter, path);
        }

        SerializedObject serialized = new SerializedObject(encounter);
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("nodeType").enumValueIndex = (int)(boss ? MapNodeType.Boss : MapNodeType.Combat);
        serialized.FindProperty("weight").floatValue = 1f;
        serialized.FindProperty("bossEncounter").boolValue = boss;
        serialized.FindProperty("completeWhenNoEnemies").boolValue = true;
        SerializedProperty waves = serialized.FindProperty("waves");
        waves.arraySize = wavePools.Length;
        for (int waveIndex = 0; waveIndex < wavePools.Length; waveIndex++)
        {
            SerializedProperty wave = waves.GetArrayElementAtIndex(waveIndex);
            SerializedProperty prefabs = wave.FindPropertyRelative("enemyPrefabs");
            prefabs.arraySize = wavePools[waveIndex].Length;
            for (int prefabIndex = 0; prefabIndex < wavePools[waveIndex].Length; prefabIndex++)
                prefabs.GetArrayElementAtIndex(prefabIndex).objectReferenceValue = wavePools[waveIndex][prefabIndex];
            wave.FindPropertyRelative("spawnCount").intValue = spawnCounts[waveIndex];
            wave.FindPropertyRelative("initialDelay").floatValue = 0f;
            wave.FindPropertyRelative("spawnInterval").floatValue = 0.25f;
            wave.FindPropertyRelative("waitForWaveClear").boolValue = true;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(encounter);
        return encounter;
    }

    static RoomDefinitionSO LoadHealRoomDefinition()
    {
        RoomDefinitionSO definition = AssetDatabase.LoadAssetAtPath<RoomDefinitionSO>(HealRoomDefinitionPath);
        if (definition == null)
            throw new InvalidOperationException($"Heal room definition is missing at '{HealRoomDefinitionPath}'.");
        if (definition.NodeType != MapNodeType.Heal || definition.Weight <= 0f ||
            definition.RoomPrefab == null || definition.MaxExitCount < 2)
        {
            throw new InvalidOperationException(
                $"Heal room definition at '{HealRoomDefinitionPath}' must be enabled, use Heal, have a prefab, and support at least two exits.");
        }

        return definition;
    }

    static MapRunConfigSO CreateStageConfig(int number, string id, string displayName, int startLevel, int targetLevel,
        int runCount, int[] enemyLevels, GameObject portal, RoomDefinitionSO healRoom,
        EncounterDefinitionSO combat, EncounterDefinitionSO boss)
    {
        string path = StageConfigPath(number);
        MapRunConfigSO config = AssetDatabase.LoadAssetAtPath<MapRunConfigSO>(path);
        if (config == null)
        {
            if (!AssetDatabase.CopyAsset(BaseConfigPath, path))
                throw new InvalidOperationException($"Could not create stage config at '{path}'.");
            config = AssetDatabase.LoadAssetAtPath<MapRunConfigSO>(path);
        }

        // The config is a copy of the base one, so it starts out pointing at the base stage's
        // profiles. Each stage gets its own, or all three would overwrite each other's tuning.
        MapRunConfigSO baseConfig = AssetDatabase.LoadAssetAtPath<MapRunConfigSO>(BaseConfigPath);
        if (baseConfig == null)
            throw new InvalidOperationException($"Base run config is missing at '{BaseConfigPath}'.");

        ClearInheritedBaseProfiles(config, baseConfig);

        // Reuse whatever the config already points at. Creating a second profile beside an
        // existing one would repoint the config and orphan the first.
        MapGenerationProfileSO generation = CreateStageGenerationProfile(number, config);
        MapContentPoolSO pool = CreateStageContentPool(number, displayName, config, baseConfig, healRoom, combat, boss);
        StageProgressionProfileSO progression = CreateStageProgressionProfile(
            number, config, startLevel, targetLevel, runCount, enemyLevels, portal);

        SerializedObject serialized = new SerializedObject(config);
        serialized.FindProperty("stageId").stringValue = id;
        serialized.FindProperty("stageDisplayName").stringValue = displayName;
        serialized.FindProperty("generationProfile").objectReferenceValue = generation;
        serialized.FindProperty("contentPool").objectReferenceValue = pool;
        serialized.FindProperty("progressionProfile").objectReferenceValue = progression;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        return config;
    }

    /// <summary>
    /// A config copied from the base asset starts out pointing at the base stage's profiles. Those
    /// belong to the base stage, so the reference is dropped and this stage authors its own.
    /// </summary>
    static void ClearInheritedBaseProfiles(MapRunConfigSO config, MapRunConfigSO baseConfig)
    {
        SerializedObject serialized = new SerializedObject(config);
        bool changed = false;
        changed |= ClearIfSameAsBase(serialized, "generationProfile", baseConfig.GenerationProfile);
        changed |= ClearIfSameAsBase(serialized, "contentPool", baseConfig.ContentPool);
        changed |= ClearIfSameAsBase(serialized, "progressionProfile", baseConfig.ProgressionProfile);
        if (changed)
            serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static bool ClearIfSameAsBase(SerializedObject serialized, string propertyPath, UnityEngine.Object baseValue)
    {
        SerializedProperty property = serialized.FindProperty(propertyPath);
        if (baseValue == null || property.objectReferenceValue != baseValue)
            return false;

        property.objectReferenceValue = null;
        return true;
    }

    /// <summary>
    /// The Test Stage map shape: a six-node critical path that always offers a Heal room before the
    /// Boss, and rolls nothing but Combat in between.
    /// </summary>
    static MapGenerationProfileSO CreateStageGenerationProfile(int number, MapRunConfigSO config)
    {
        MapGenerationProfileSO profile = config.GenerationProfile ?? CreateOrLoadAsset<MapGenerationProfileSO>(
            $"{ProfileFolder}/Test Stage {number:00} Generation Profile.asset");

        SerializedObject serialized = new SerializedObject(profile);
        serialized.FindProperty("randomizeSeed").boolValue = true;
        serialized.FindProperty("seed").intValue = 0;
        serialized.FindProperty("criticalPathNodeCount").intValue = 6;
        serialized.FindProperty("minBranchCount").intValue = 1;
        serialized.FindProperty("maxBranchCount").intValue = 3;
        serialized.FindProperty("maxOutgoingPerNode").intValue = 3;
        serialized.FindProperty("forceBlueBeforeBoss").boolValue = true;
        serialized.FindProperty("pitySystem").FindPropertyRelative("forcedBlueType").enumValueIndex = (int)MapNodeType.Heal;
        SetSingleWeight(serialized.FindProperty("mainPathWeights"), MapNodeType.Combat);
        SetSingleWeight(serialized.FindProperty("blueWeights"), MapNodeType.Heal);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
        return profile;
    }

    static MapContentPoolSO CreateStageContentPool(
        int number,
        string displayName,
        MapRunConfigSO config,
        MapRunConfigSO baseConfig,
        RoomDefinitionSO healRoom,
        EncounterDefinitionSO combat,
        EncounterDefinitionSO boss)
    {
        MapContentPoolSO pool = config.ContentPool ?? CreateOrLoadAsset<MapContentPoolSO>(
            $"{ProfileFolder}/Test Stage {number:00} Content Pool.asset");

        SerializedObject serialized = new SerializedObject(pool);
        serialized.FindProperty("displayName").stringValue = displayName;

        // Rooms come from the base stage's pool so every Test Stage shares the same room set, plus
        // the Heal room the pre-Boss blue node needs.
        SerializedProperty rooms = serialized.FindProperty("roomDefinitions");
        RoomDefinitionSO[] baseRooms = baseConfig.RoomDefinitions;
        rooms.arraySize = baseRooms != null ? baseRooms.Length : 0;
        for (int i = 0; i < rooms.arraySize; i++)
            rooms.GetArrayElementAtIndex(i).objectReferenceValue = baseRooms[i];
        EnsureArrayContainsObjectReference(rooms, healRoom);

        SerializedProperty encounters = serialized.FindProperty("encounterDefinitions");
        encounters.arraySize = 2;
        encounters.GetArrayElementAtIndex(0).objectReferenceValue = combat;
        encounters.GetArrayElementAtIndex(1).objectReferenceValue = boss;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pool);
        return pool;
    }

    static StageProgressionProfileSO CreateStageProgressionProfile(
        int number,
        MapRunConfigSO config,
        int startLevel,
        int targetLevel,
        int runCount,
        int[] enemyLevels,
        GameObject portal)
    {
        StageProgressionProfileSO profile = config.ProgressionProfile ?? CreateOrLoadAsset<StageProgressionProfileSO>(
            $"{ProfileFolder}/Test Stage {number:00} Progression Profile.asset");

        SerializedObject serialized = new SerializedObject(profile);
        serialized.FindProperty("levelTable").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<LevelTableSO>(LevelTablePath);
        serialized.FindProperty("startLevel").intValue = startLevel;
        serialized.FindProperty("targetLevel").intValue = targetLevel;
        serialized.FindProperty("targetRunCount").intValue = runCount;
        SerializedProperty tiers = serialized.FindProperty("enemyLevelTiers");
        tiers.arraySize = enemyLevels.Length;
        for (int i = 0; i < enemyLevels.Length; i++)
            tiers.GetArrayElementAtIndex(i).intValue = enemyLevels[i];
        serialized.FindProperty("regularEnemyXpShare").floatValue = 0.6f;
        serialized.FindProperty("bossXpShare").floatValue = 0.2f;
        serialized.FindProperty("stageExitPrefab").objectReferenceValue = portal;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
        return profile;
    }

    static void SetSingleWeight(SerializedProperty weights, MapNodeType type)
    {
        weights.arraySize = 1;
        SerializedProperty entry = weights.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("type").enumValueIndex = (int)type;
        entry.FindPropertyRelative("weight").floatValue = 1f;
    }

    static T CreateOrLoadAsset<T>(string assetPath) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (existing != null)
            return existing;

        var created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, assetPath);
        return created;
    }

    static string StageConfigPath(int number)
    {
        return $"{OutputFolder}/Test Stage {number:00} Map Run Config.asset";
    }

    static void EnsureArrayContainsObjectReference(SerializedProperty array, UnityEngine.Object value)
    {
        if (array == null || !array.isArray)
            throw new InvalidOperationException("Expected a serialized object-reference array.");
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        for (int i = 0; i < array.arraySize; i++)
        {
            if (array.GetArrayElementAtIndex(i).objectReferenceValue == value)
                return;
        }

        int newIndex = array.arraySize;
        array.InsertArrayElementAtIndex(newIndex);
        array.GetArrayElementAtIndex(newIndex).objectReferenceValue = value;
    }

    static GameObject CreateStageExitPrefab()
    {
        GameObject root = new GameObject("Stage Exit Cyan");
        try
        {
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer < 0)
                throw new InvalidOperationException("The project has no 'Interactable' layer for the Test Stage exit.");

            root.layer = interactableLayer;
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.radius = 1.5f;
            collider.height = 3f;
            root.AddComponent<InteractableLink>();
            root.AddComponent<StageExitInteractable>();

            GameObject portalVfx = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/VFX/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/Variants/CFXR Portal (Cyan).prefab");
            if (portalVfx != null)
            {
                GameObject visual = PrefabUtility.InstantiatePrefab(portalVfx) as GameObject;
                visual.name = "CFXR Portal (Cyan)";
                visual.transform.SetParent(root.transform, false);
            }

            return PrefabUtility.SaveAsPrefabAsset(root, PortalPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void ConfigureCombatRoom(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            RoomController controller = root.GetComponentInChildren<RoomController>(true);
            if (controller == null)
                throw new InvalidOperationException($"Room prefab '{path}' has no RoomController.");

            SerializedObject serialized = new SerializedObject(controller);
            SerializedProperty pointsProperty = serialized.FindProperty("enemySpawnPoints");
            Vector3 center = controller.transform.position;
            if (pointsProperty.arraySize > 0 && pointsProperty.GetArrayElementAtIndex(0).objectReferenceValue is Transform existing)
                center = existing.position;

            Transform group = FindOrCreateChild(controller.transform, "TestStageEnemySpawnPoints");
            Vector3[] offsets =
            {
                new(-2.5f, 0f, -2.5f), new(2.5f, 0f, -2.5f),
                new(-2.5f, 0f, 2.5f), new(2.5f, 0f, 2.5f),
            };
            pointsProperty.arraySize = offsets.Length;
            for (int i = 0; i < offsets.Length; i++)
            {
                Transform point = FindOrCreateChild(group, $"EnemySpawnPoint_{i + 1:00}");
                point.position = center + offsets[i];
                point.rotation = controller.transform.rotation;
                pointsProperty.GetArrayElementAtIndex(i).objectReferenceValue = point;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ConfigureBossRoom(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            RoomController controller = root.GetComponentInChildren<RoomController>(true);
            if (controller == null)
                throw new InvalidOperationException($"Boss room prefab '{path}' has no RoomController.");

            SerializedObject serialized = new SerializedObject(controller);
            Transform bossPoint = FindOrCreateChild(controller.transform, "BossSpawnPoint");
            bossPoint.localPosition = new Vector3(0f, 0f, 3f);
            bossPoint.localRotation = Quaternion.identity;
            SerializedProperty enemies = serialized.FindProperty("enemySpawnPoints");
            enemies.arraySize = 1;
            enemies.GetArrayElementAtIndex(0).objectReferenceValue = bossPoint;

            Transform exitPoint = FindOrCreateChild(controller.transform, "StageExitSpawnPoint");
            exitPoint.localPosition = new Vector3(0f, 0f, -3f);
            exitPoint.localRotation = Quaternion.identity;
            serialized.FindProperty("stageExitSpawnPoint").objectReferenceValue = exitPoint;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ConfigureBasement(bool dryRun, List<string> report, params string[] stageConfigPaths)
    {
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        Scene scene = EditorSceneManager.OpenScene(BasementPath, OpenSceneMode.Single);
        try
        {
            GameObject mapUi = FindSceneObject(scene, "MapUI");
            if (mapUi == null)
                throw new InvalidOperationException("Basement scene has no GameObject named 'MapUI'.");

            // Stage configs are resolved after the scene is open. Opening a scene in Single mode
            // unloads unreferenced assets, so a reference resolved earlier becomes a destroyed
            // object and assigning it silently writes null.
            List<MapRunConfigSO> stages = LoadStageConfigs(stageConfigPaths, report);

            Transform pagination = mapUi.transform.Find(TestStagePaginationName);
            if (pagination == null && dryRun)
            {
                report.Add($"CREATE   MapUI/{TestStagePaginationName} and every object the tool owns under it.");
                return;
            }

            if (pagination == null)
            {
                pagination = CreateOwnedUiObject(TestStagePaginationName, mapUi.transform);
                report.Add($"CREATE   MapUI/{TestStagePaginationName}");
            }
            else
            {
                report.Add($"KEEP     MapUI/{TestStagePaginationName} and every page it already holds");
            }

            Stretch(Record((RectTransform)pagination));

            Transform existingPage = EnsureOwnedPage(pagination, ExistingMapsPageName, dryRun, report);
            if (existingPage != null && !dryRun)
                Stretch(Record((RectTransform)existingPage));

            // Placards authored directly under MapUI belong to the original board and are adopted
            // once into ExistingMapsPage. Placards that already sit inside a pagination page --
            // including hand-authored pages such as BossRushPage -- are never touched.
            AdoptLooseExistingPlacards(mapUi.transform, pagination, existingPage, dryRun, report);

            Transform testStagePage = EnsureOwnedPage(pagination, TestStagePageName, dryRun, report);
            if (testStagePage != null && !dryRun)
            {
                RectTransform pageRect = Record((RectTransform)testStagePage);
                pageRect.anchorMin = new Vector2(0.12f, 0.10f);
                pageRect.anchorMax = new Vector2(0.88f, 0.86f);
                pageRect.offsetMin = Vector2.zero;
                pageRect.offsetMax = Vector2.zero;
            }

            Vector2[] positions = { new(0.20f, 0.52f), new(0.50f, 0.52f), new(0.80f, 0.52f) };
            var ownedPlacards = new List<Transform>();
            for (int i = 0; i < stages.Count && i < positions.Length; i++)
            {
                Transform placard = EnsureStagePlacard(testStagePage, stages[i], positions[i], dryRun, report);
                if (placard != null)
                    ownedPlacards.Add(placard);
            }

            if (stages.Count > positions.Length)
            {
                report.Add(
                    $"SKIP     {stages.Count - positions.Length} stage config(s). {TestStagePageName} " +
                    $"has room for exactly {positions.Length} placards; author the rest as their own page.");
            }

            RemoveStaleStagePlacards(testStagePage, ownedPlacards, dryRun, report);

            Button previous = EnsureArrow(pagination, PreviousPageButtonName, "<", new Vector2(0.07f, 0.48f), dryRun, report);
            Button next = EnsureArrow(pagination, NextPageButtonName, ">", new Vector2(0.93f, 0.48f), dryRun, report);

            ConfigurePager(pagination, existingPage, testStagePage, previous, next, dryRun, report);

            if (dryRun)
                return;

            if (testStagePage != null)
                testStagePage.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (previousSetup != null && previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }

    static List<MapRunConfigSO> LoadStageConfigs(string[] stageConfigPaths, List<string> report)
    {
        var stages = new List<MapRunConfigSO>();
        if (stageConfigPaths == null)
            return stages;

        for (int i = 0; i < stageConfigPaths.Length; i++)
        {
            MapRunConfigSO stage = AssetDatabase.LoadAssetAtPath<MapRunConfigSO>(stageConfigPaths[i]);
            if (stage == null)
                report.Add($"MISSING  stage config at {stageConfigPaths[i]}. Its placard is skipped.");
            else
                stages.Add(stage);
        }

        return stages;
    }

    static Transform EnsureOwnedPage(Transform pagination, string pageName, bool dryRun, List<string> report)
    {
        Transform page = pagination != null ? pagination.Find(pageName) : null;
        if (page != null)
        {
            report.Add($"UPDATE   {TestStagePaginationName}/{pageName}");
            return page;
        }

        report.Add($"CREATE   {TestStagePaginationName}/{pageName}");
        if (dryRun || pagination == null)
            return null;

        return CreateOwnedUiObject(pageName, pagination);
    }

    static void AdoptLooseExistingPlacards(
        Transform mapUi,
        Transform pagination,
        Transform existingPage,
        bool dryRun,
        List<string> report)
    {
        if (mapUi == null || pagination == null)
            return;

        UIButtonHoverOutline[] placards = mapUi.GetComponentsInChildren<UIButtonHoverOutline>(true);
        for (int i = 0; i < placards.Length; i++)
        {
            UIButtonHoverOutline placard = placards[i];
            if (placard == null || placard.transform.IsChildOf(pagination))
                continue;

            report.Add($"ADOPT    MapUI/{placard.name} into {ExistingMapsPageName}");
            if (dryRun || existingPage == null)
                continue;

            Undo.SetTransformParent(placard.transform, existingPage, UndoName);
        }
    }

    static Transform EnsureStagePlacard(
        Transform page,
        MapRunConfigSO stage,
        Vector2 anchor,
        bool dryRun,
        List<string> report)
    {
        string placardName = stage.StageDisplayName;
        Transform placard = page != null ? page.Find(placardName) : null;
        if (placard == null)
        {
            report.Add($"CREATE   {TestStagePageName}/{placardName}");
            if (dryRun || page == null)
                return null;

            placard = CreateOwnedUiObject(placardName, page);
        }
        else
        {
            report.Add($"UPDATE   {TestStagePageName}/{placardName}");
            if (dryRun)
                return placard;
        }

        RectTransform rect = Record((RectTransform)placard);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.sizeDelta = new Vector2(220f, 150f);
        rect.anchoredPosition = Vector2.zero;

        Image image = Record(EnsureComponent<Image>(placard.gameObject));
        image.color = new Color(0.72f, 0.73f, 0.76f, 1f);
        Shadow shadow = Record(EnsureComponent<Shadow>(placard.gameObject));
        shadow.effectColor = new Color(0.18f, 0.17f, 0.20f, 0.8f);
        shadow.effectDistance = new Vector2(8f, -12f);
        Button button = Record(EnsureComponent<Button>(placard.gameObject));
        button.targetGraphic = image;

        StagePlacardButton stageButton = EnsureComponent<StagePlacardButton>(placard.gameObject);
        SerializedObject stageSerialized = new SerializedObject(stageButton);
        stageSerialized.FindProperty("runConfig").objectReferenceValue = stage;
        stageSerialized.ApplyModifiedPropertiesWithoutUndo();
        EnsurePersistentListener(
            button.onClick,
            stageButton,
            stageButton.EnterStage,
            nameof(StagePlacardButton.EnterStage));

        TMP_Text label = EnsureLabel(
            placard,
            $"{stage.StageDisplayName}\nLV.{stage.StartLevel}–{stage.TargetLevel}",
            26f);
        label.color = Color.black;
        return placard;
    }

    static void RemoveStaleStagePlacards(
        Transform page,
        List<Transform> ownedPlacards,
        bool dryRun,
        List<string> report)
    {
        if (page == null)
            return;

        var stale = new List<GameObject>();
        for (int i = 0; i < page.childCount; i++)
        {
            Transform child = page.GetChild(i);
            if (child.GetComponent<StagePlacardButton>() == null || ownedPlacards.Contains(child))
                continue;

            stale.Add(child.gameObject);
        }

        for (int i = 0; i < stale.Count; i++)
        {
            report.Add($"REMOVE   stale placard {TestStagePageName}/{stale[i].name}");
            if (!dryRun)
                Undo.DestroyObjectImmediate(stale[i]);
        }
    }

    static Button EnsureArrow(
        Transform pagination,
        string name,
        string text,
        Vector2 anchor,
        bool dryRun,
        List<string> report)
    {
        Transform arrow = pagination != null ? pagination.Find(name) : null;
        if (arrow == null)
        {
            report.Add($"CREATE   {TestStagePaginationName}/{name}");
            if (dryRun || pagination == null)
                return null;

            arrow = CreateOwnedUiObject(name, pagination);
        }
        else
        {
            report.Add($"UPDATE   {TestStagePaginationName}/{name}");
            if (dryRun)
                return arrow.GetComponent<Button>();
        }

        RectTransform rect = Record((RectTransform)arrow);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.sizeDelta = new Vector2(90f, 90f);
        rect.anchoredPosition = Vector2.zero;

        Image image = Record(EnsureComponent<Image>(arrow.gameObject));
        image.color = new Color(0.68f, 0.55f, 0.22f, 1f);
        Button button = Record(EnsureComponent<Button>(arrow.gameObject));
        button.targetGraphic = image;

        TMP_Text label = EnsureLabel(arrow, text, 54f);
        label.color = Color.black;
        return button;
    }

    static void ConfigurePager(
        Transform pagination,
        Transform existingPage,
        Transform testStagePage,
        Button previous,
        Button next,
        bool dryRun,
        List<string> report)
    {
        if (pagination == null)
            return;

        MobilizBoardPager pager = pagination.GetComponent<MobilizBoardPager>();
        if (pager == null)
        {
            report.Add($"CREATE   MobilizBoardPager on {TestStagePaginationName}");
            if (dryRun)
                return;

            pager = Undo.AddComponent<MobilizBoardPager>(pagination.gameObject);
        }
        else
        {
            report.Add($"UPDATE   MobilizBoardPager on {TestStagePaginationName}");
        }

        SerializedObject pagerSerialized = new SerializedObject(pager);
        SerializedProperty pagesProperty = pagerSerialized.FindProperty("pages");

        var registeredPages = new List<GameObject>();
        for (int i = 0; i < pagesProperty.arraySize; i++)
            registeredPages.Add(pagesProperty.GetArrayElementAtIndex(i).objectReferenceValue as GameObject);

        List<GameObject> pages = BuildPagerPageOrder(
            existingPage != null ? existingPage.gameObject : null,
            testStagePage != null ? testStagePage.gameObject : null,
            registeredPages);

        for (int i = 0; i < pages.Count; i++)
            report.Add($"PAGE[{i}]  {pages[i].name}");

        if (dryRun)
            return;

        pagesProperty.arraySize = pages.Count;
        for (int i = 0; i < pages.Count; i++)
            pagesProperty.GetArrayElementAtIndex(i).objectReferenceValue = pages[i];

        if (previous != null)
            pagerSerialized.FindProperty("previousButton").objectReferenceValue = previous;
        if (next != null)
            pagerSerialized.FindProperty("nextButton").objectReferenceValue = next;
        pagerSerialized.ApplyModifiedPropertiesWithoutUndo();

        if (previous != null)
        {
            EnsurePersistentListener(
                previous.onClick,
                pager,
                pager.ShowPreviousPage,
                nameof(MobilizBoardPager.ShowPreviousPage));
        }

        if (next != null)
        {
            EnsurePersistentListener(
                next.onClick,
                pager,
                pager.ShowNextPage,
                nameof(MobilizBoardPager.ShowNextPage));
        }
    }

    /// <summary>
    /// The tool owns page index 0 and index 1 only. Every other registered page is hand authored,
    /// so it keeps both its identity and its relative order, and empty slots are dropped.
    /// </summary>
    public static List<GameObject> BuildPagerPageOrder(
        GameObject existingPage,
        GameObject testStagePage,
        IReadOnlyList<GameObject> registeredPages)
    {
        var pages = new List<GameObject>();
        if (existingPage != null)
            pages.Add(existingPage);
        if (testStagePage != null)
            pages.Add(testStagePage);

        if (registeredPages == null)
            return pages;

        for (int i = 0; i < registeredPages.Count; i++)
        {
            GameObject page = registeredPages[i];
            if (page == null || pages.Contains(page))
                continue;

            pages.Add(page);
        }

        return pages;
    }

    static void EnsurePersistentListener(
        UnityEvent target,
        UnityEngine.Object listenerTarget,
        UnityAction call,
        string methodName)
    {
        if (target == null || listenerTarget == null)
            return;

        for (int i = 0; i < target.GetPersistentEventCount(); i++)
        {
            if (target.GetPersistentTarget(i) == listenerTarget &&
                string.Equals(target.GetPersistentMethodName(i), methodName, StringComparison.Ordinal))
            {
                return;
            }
        }

        UnityEventTools.AddPersistentListener(target, call);
    }

    static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
            component = Undo.AddComponent<T>(target);

        return component;
    }

    static T Record<T>(T target) where T : UnityEngine.Object
    {
        if (target != null)
            Undo.RecordObject(target, UndoName);

        return target;
    }

    static Transform CreateOwnedUiObject(string name, Transform parent)
    {
        GameObject created = CreateUiObject(name, parent);
        Undo.RegisterCreatedObjectUndo(created, UndoName);
        return created.transform;
    }

    static TMP_Text EnsureLabel(Transform parent, string text, float size)
    {
        Transform labelTransform = parent.Find("Label");
        if (labelTransform == null)
            labelTransform = CreateOwnedUiObject("Label", parent);

        Stretch(Record((RectTransform)labelTransform));
        TextMeshProUGUI label = Record(EnsureComponent<TextMeshProUGUI>(labelTransform.gameObject));
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (font != null)
            label.font = font;
        return label;
    }

    static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static Transform FindOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
            return child;

        GameObject childObject = new GameObject(name);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    static GameObject FindSceneObject(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                if (transforms[j].name == name)
                    return transforms[j].gameObject;
            }
        }
        return null;
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
