#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TestStageAuthoringTool
{
    const string BaseConfigPath = "Assets/Data/Map/Test_Map Run Config SO.asset";
    const string LevelTablePath = "Assets/Scripts/Player/LevelSystem/Level Character Table SO.asset";
    const string OutputFolder = "Assets/Data/Map/TestStages";
    const string StatsFolder = "Assets/Data/EnemyStats/TestStage";
    const string PortalPath = "Assets/Prefab/MAP/TestStage/Stage Exit Cyan.prefab";
    const string HealRoomDefinitionPath = "Assets/Data/Map/RoomDefinition.Heal.asset";
    const string BasementPath = "Assets/Scenes/Basement/Basement.unity";

    static readonly string[] CombatRoomPaths =
    {
        "Assets/Prefab/MAP/Combat/Combat.Up.DeadEnd.Up.prefab",
        "Assets/Prefab/MAP/Combat/Combat.Intersection.prefab",
        "Assets/Prefab/MAP/Combat/Combat.Turn.Up.Right.prefab",
        "Assets/Prefab/MAP/Combat/Combat.Up.Down.prefab",
        "Assets/Prefab/MAP/Combat/Combat.Tjunction.Left.Right.Up.prefab",
    };

    [MenuItem("Tools/RB Project/Map/Apply Test Stage Content")]
    public static void ApplyAll()
    {
        EnsureFolder(OutputFolder);
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

        MapRunConfigSO stage1 = CreateStageConfig(1, "test_stage_01", "TEST STAGE 01", 1, 11, 2,
            new[] { 5, 10 }, portal, healRoom, stage1Combat, stage1Boss);
        MapRunConfigSO stage2 = CreateStageConfig(2, "test_stage_02", "TEST STAGE 02", 11, 20, 3,
            new[] { 13, 17, 20 }, portal, healRoom, stage2Combat, stage2Boss);
        MapRunConfigSO stage3 = CreateStageConfig(3, "test_stage_03", "TEST STAGE 03", 20, 30, 5,
            new[] { 22, 24, 26, 28, 30 }, portal, healRoom, stage3Combat, stage3Boss);

        for (int i = 0; i < CombatRoomPaths.Length; i++)
            ConfigureCombatRoom(CombatRoomPaths[i]);
        ConfigureBossRoom("Assets/Prefab/MAP/Boss/BossTest.DeadEnd.Up.prefab");
        ConfigureBasement(stage1, stage2, stage3);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TestStageAuthoringTool] Test Stage content authoring completed.");
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
        string path = $"{OutputFolder}/Test Stage {number:00} Map Run Config.asset";
        MapRunConfigSO config = AssetDatabase.LoadAssetAtPath<MapRunConfigSO>(path);
        if (config == null)
        {
            if (!AssetDatabase.CopyAsset(BaseConfigPath, path))
                throw new InvalidOperationException($"Could not create stage config at '{path}'.");
            config = AssetDatabase.LoadAssetAtPath<MapRunConfigSO>(path);
        }

        SerializedObject serialized = new SerializedObject(config);
        serialized.FindProperty("stageId").stringValue = id;
        serialized.FindProperty("stageDisplayName").stringValue = displayName;
        serialized.FindProperty("levelTable").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LevelTableSO>(LevelTablePath);
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
        serialized.FindProperty("randomizeSeed").boolValue = true;
        serialized.FindProperty("criticalPathNodeCount").intValue = 6;
        serialized.FindProperty("forceBlueBeforeBoss").boolValue = true;
        SerializedProperty pitySystem = serialized.FindProperty("pitySystem");
        pitySystem.FindPropertyRelative("forcedBlueType").enumValueIndex = (int)MapNodeType.Heal;
        SerializedProperty mainWeights = serialized.FindProperty("mainPathWeights");
        mainWeights.arraySize = 1;
        mainWeights.GetArrayElementAtIndex(0).FindPropertyRelative("type").enumValueIndex = (int)MapNodeType.Combat;
        mainWeights.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 1f;
        SerializedProperty blueWeights = serialized.FindProperty("blueWeights");
        blueWeights.arraySize = 1;
        blueWeights.GetArrayElementAtIndex(0).FindPropertyRelative("type").enumValueIndex = (int)MapNodeType.Heal;
        blueWeights.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 1f;
        EnsureArrayContainsObjectReference(serialized.FindProperty("roomDefinitions"), healRoom);
        SerializedProperty encounters = serialized.FindProperty("encounterDefinitions");
        encounters.arraySize = 2;
        encounters.GetArrayElementAtIndex(0).objectReferenceValue = combat;
        encounters.GetArrayElementAtIndex(1).objectReferenceValue = boss;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        return config;
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

    static void ConfigureBasement(params MapRunConfigSO[] stages)
    {
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        Scene scene = EditorSceneManager.OpenScene(BasementPath, OpenSceneMode.Single);
        try
        {
            GameObject mapUi = FindSceneObject(scene, "MapUI");
            if (mapUi == null)
                throw new InvalidOperationException("Basement scene has no GameObject named 'MapUI'.");

            Transform oldRoot = mapUi.transform.Find("TestStagePagination");
            if (oldRoot != null)
            {
                UIButtonHoverOutline[] previousPagePlacards = oldRoot.GetComponentsInChildren<UIButtonHoverOutline>(true);
                for (int i = 0; i < previousPagePlacards.Length; i++)
                    previousPagePlacards[i].transform.SetParent(mapUi.transform, true);
                UnityEngine.Object.DestroyImmediate(oldRoot.gameObject);
            }

            GameObject pagination = CreateUiObject("TestStagePagination", mapUi.transform);
            Stretch((RectTransform)pagination.transform);

            GameObject existingPage = CreateUiObject("ExistingMapsPage", pagination.transform);
            Stretch((RectTransform)existingPage.transform);
            UIButtonHoverOutline[] existingPlacards = mapUi.GetComponentsInChildren<UIButtonHoverOutline>(true);
            for (int i = 0; i < existingPlacards.Length; i++)
            {
                UIButtonHoverOutline placard = existingPlacards[i];
                if (placard != null && !placard.transform.IsChildOf(pagination.transform))
                    placard.transform.SetParent(existingPage.transform, true);
            }

            GameObject page = CreateUiObject("TestStagePage", pagination.transform);
            RectTransform pageRect = (RectTransform)page.transform;
            pageRect.anchorMin = new Vector2(0.12f, 0.10f);
            pageRect.anchorMax = new Vector2(0.88f, 0.86f);
            pageRect.offsetMin = Vector2.zero;
            pageRect.offsetMax = Vector2.zero;

            Vector2[] positions = { new(0.20f, 0.52f), new(0.50f, 0.52f), new(0.80f, 0.52f) };
            for (int i = 0; i < stages.Length; i++)
                CreateStagePlacard(page.transform, stages[i], positions[i]);

            Button previous = CreateArrow(pagination.transform, "PreviousPage", "<", new Vector2(0.07f, 0.48f));
            Button next = CreateArrow(pagination.transform, "NextPage", ">", new Vector2(0.93f, 0.48f));
            MobilizBoardPager pager = pagination.AddComponent<MobilizBoardPager>();
            SerializedObject pagerSerialized = new SerializedObject(pager);
            SerializedProperty pages = pagerSerialized.FindProperty("pages");
            pages.arraySize = 2;
            pages.GetArrayElementAtIndex(0).objectReferenceValue = existingPage;
            pages.GetArrayElementAtIndex(1).objectReferenceValue = page;
            pagerSerialized.FindProperty("previousButton").objectReferenceValue = previous;
            pagerSerialized.FindProperty("nextButton").objectReferenceValue = next;
            pagerSerialized.FindProperty("initialPage").intValue = 0;
            pagerSerialized.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(previous.onClick, pager.ShowPreviousPage);
            UnityEventTools.AddPersistentListener(next.onClick, pager.ShowNextPage);
            page.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (previousSetup != null && previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }

    static void CreateStagePlacard(Transform parent, MapRunConfigSO stage, Vector2 anchor)
    {
        GameObject placard = CreateUiObject(stage.StageDisplayName, parent);
        RectTransform rect = (RectTransform)placard.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.sizeDelta = new Vector2(220f, 150f);
        rect.anchoredPosition = Vector2.zero;
        Image image = placard.AddComponent<Image>();
        image.color = new Color(0.72f, 0.73f, 0.76f, 1f);
        Shadow shadow = placard.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.18f, 0.17f, 0.20f, 0.8f);
        shadow.effectDistance = new Vector2(8f, -12f);
        Button button = placard.AddComponent<Button>();
        button.targetGraphic = image;
        StagePlacardButton stageButton = placard.AddComponent<StagePlacardButton>();
        SerializedObject stageSerialized = new SerializedObject(stageButton);
        stageSerialized.FindProperty("runConfig").objectReferenceValue = stage;
        stageSerialized.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(button.onClick, stageButton.EnterStage);

        TMP_Text label = CreateLabel(placard.transform, $"{stage.StageDisplayName}\nLV.{stage.StartLevel}\u2013{stage.TargetLevel}", 26f);
        label.color = Color.black;
    }

    static Button CreateArrow(Transform parent, string name, string text, Vector2 anchor)
    {
        GameObject arrow = CreateUiObject(name, parent);
        RectTransform rect = (RectTransform)arrow.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.sizeDelta = new Vector2(90f, 90f);
        rect.anchoredPosition = Vector2.zero;
        Image image = arrow.AddComponent<Image>();
        image.color = new Color(0.68f, 0.55f, 0.22f, 1f);
        Button button = arrow.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text label = CreateLabel(arrow.transform, text, 54f);
        label.color = Color.black;
        return button;
    }

    static TMP_Text CreateLabel(Transform parent, string text, float size)
    {
        GameObject labelObject = CreateUiObject("Label", parent);
        Stretch((RectTransform)labelObject.transform);
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
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
