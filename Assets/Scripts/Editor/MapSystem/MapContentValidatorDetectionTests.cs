using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Proves that each <see cref="MapContentValidator"/> check actually fires. A validator that
/// reports clean on real data is only trustworthy if its rules are known to detect broken data.
/// </summary>
public sealed class MapContentValidatorDetectionTests
{
    readonly List<Object> created = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null)
                Object.DestroyImmediate(created[i]);
        }

        created.Clear();
    }

    // ------------------------------------------------------------------ encounters

    [Test]
    public void BossFlagThatDisagreesWithNodeTypeIsAnError()
    {
        EncounterDefinitionSO encounter = CreateEncounter(MapNodeType.Combat, bossEncounter: true, waves: 1, usablePrefabs: 1);

        AssertError(Validate(encounter), "Boss Encounter");
    }

    [Test]
    public void EncounterWithoutWavesIsAnError()
    {
        EncounterDefinitionSO encounter = CreateEncounter(MapNodeType.Combat, bossEncounter: false, waves: 0, usablePrefabs: 0);

        AssertError(Validate(encounter), "no waves");
    }

    [Test]
    public void WaveWithoutAnyUsableEnemyPrefabIsAnError()
    {
        EncounterDefinitionSO encounter = CreateEncounter(MapNodeType.Combat, bossEncounter: false, waves: 1, usablePrefabs: 0);

        AssertError(Validate(encounter), "no usable enemy prefab");
    }

    [Test]
    public void EnemyPrefabWithoutEnemyContextIsAnError()
    {
        EncounterDefinitionSO encounter = CreateEncounter(MapNodeType.Combat, bossEncounter: false, waves: 1, usablePrefabs: 0);
        SetWavePrefab(encounter, CreateGameObject("PlainEnemy"));

        AssertError(Validate(encounter), "no EnemyContext");
    }

    [Test]
    public void EnemyPrefabWithoutHealthSystemIsAnError()
    {
        EncounterDefinitionSO encounter = CreateEncounter(MapNodeType.Combat, bossEncounter: false, waves: 1, usablePrefabs: 0);
        GameObject enemy = CreateGameObject("EnemyWithoutHealth");
        enemy.AddComponent<EnemyContext>();
        SetWavePrefab(encounter, enemy);

        AssertError(Validate(encounter), "no HealthSystem");
    }

    // ------------------------------------------------------------------ room prefabs

    [Test]
    public void RoomPrefabWithoutRoomControllerIsAnError()
    {
        RoomDefinitionSO definition = CreateRoomDefinition(MapNodeType.Combat, RoomExitMask.Up, addController: false);

        AssertError(Validate(definition), "no RoomController");
    }

    [Test]
    public void RoomPrefabWithoutNavMeshSurfaceIsAnError()
    {
        RoomDefinitionSO definition = CreateRoomDefinition(MapNodeType.Combat, RoomExitMask.Up, addController: true);

        AssertError(Validate(definition), "no NavMeshSurface");
    }

    [Test]
    public void ExitMaskDirectionWithoutAnExitSocketIsAnError()
    {
        RoomDefinitionSO definition = CreateRoomDefinition(
            MapNodeType.Combat,
            RoomExitMask.Up | RoomExitMask.Left,
            addController: true);
        AddExit(definition.RoomPrefab, RoomExitDirection.Up);

        AssertError(Validate(definition), "Exit Mask includes Left");
    }

    [Test]
    public void DuplicateExitSocketsInOneDirectionAreAnError()
    {
        RoomDefinitionSO definition = CreateRoomDefinition(MapNodeType.Combat, RoomExitMask.Up, addController: true);
        AddExit(definition.RoomPrefab, RoomExitDirection.Up);
        AddExit(definition.RoomPrefab, RoomExitDirection.Up);

        AssertError(Validate(definition), "exit sockets authored as Up");
    }

    [Test]
    public void CombatRoomWithoutEnemySpawnPointsIsAnError()
    {
        RoomDefinitionSO definition = CreateRoomDefinition(MapNodeType.Combat, RoomExitMask.Up, addController: true);
        AddExit(definition.RoomPrefab, RoomExitDirection.Up);

        AssertError(Validate(definition), "no enemy spawn points");
    }

    [Test]
    public void ExitSocketOutsideTheExitMaskIsAWarning()
    {
        RoomDefinitionSO definition = CreateRoomDefinition(MapNodeType.Reward, RoomExitMask.Up, addController: true);
        AddExit(definition.RoomPrefab, RoomExitDirection.Up);
        AddExit(definition.RoomPrefab, RoomExitDirection.Down);

        List<MapContentIssue> issues = Validate(definition);

        Assert.That(Find(issues, "can never be used"), Is.Not.Null, Describe(issues));
        Assert.That(Find(issues, "can never be used").IsError, Is.False);
    }

    // ------------------------------------------------------------------ helpers

    static List<MapContentIssue> Validate(EncounterDefinitionSO encounter)
    {
        var issues = new List<MapContentIssue>();
        MapContentValidator.ValidateEncounter(encounter, issues);
        return issues;
    }

    static List<MapContentIssue> Validate(RoomDefinitionSO definition)
    {
        var issues = new List<MapContentIssue>();
        MapContentValidator.ValidateRoomDefinition(definition, issues);
        return issues;
    }

    static void AssertError(List<MapContentIssue> issues, string expectedFragment)
    {
        MapContentIssue match = Find(issues, expectedFragment);
        Assert.That(match, Is.Not.Null, $"Expected an issue containing '{expectedFragment}'.{Describe(issues)}");
        Assert.That(match.IsError, Is.True, $"Expected '{expectedFragment}' to be an error.{Describe(issues)}");
    }

    static MapContentIssue Find(List<MapContentIssue> issues, string fragment)
    {
        for (int i = 0; i < issues.Count; i++)
        {
            if (issues[i].Message.Contains(fragment))
                return issues[i];
        }

        return null;
    }

    static string Describe(List<MapContentIssue> issues)
    {
        if (issues.Count == 0)
            return " No issues were reported.";

        var lines = new List<string>();
        for (int i = 0; i < issues.Count; i++)
            lines.Add(issues[i].ToString());

        return $" Reported:\n{string.Join("\n", lines)}";
    }

    GameObject CreateGameObject(string name)
    {
        var instance = new GameObject(name);
        instance.SetActive(false);
        created.Add(instance);
        return instance;
    }

    EncounterDefinitionSO CreateEncounter(MapNodeType nodeType, bool bossEncounter, int waves, int usablePrefabs)
    {
        var encounter = ScriptableObject.CreateInstance<EncounterDefinitionSO>();
        encounter.name = $"Encounter.{nodeType}";
        created.Add(encounter);

        SerializedObject serialized = new SerializedObject(encounter);
        serialized.FindProperty("nodeType").enumValueIndex = (int)nodeType;
        serialized.FindProperty("bossEncounter").boolValue = bossEncounter;
        serialized.FindProperty("weight").floatValue = 1f;

        SerializedProperty waveArray = serialized.FindProperty("waves");
        waveArray.arraySize = waves;
        for (int i = 0; i < waves; i++)
        {
            SerializedProperty wave = waveArray.GetArrayElementAtIndex(i);
            SerializedProperty pool = wave.FindPropertyRelative("enemyPrefabs");
            pool.arraySize = Mathf.Max(1, usablePrefabs);
            for (int j = 0; j < usablePrefabs; j++)
                pool.GetArrayElementAtIndex(j).objectReferenceValue = CreateValidEnemyPrefab($"Enemy_{i}_{j}");
            wave.FindPropertyRelative("spawnCount").intValue = 1;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return encounter;
    }

    GameObject CreateValidEnemyPrefab(string name)
    {
        GameObject enemy = CreateGameObject(name);
        EnemyContext context = enemy.AddComponent<EnemyContext>();
        enemy.AddComponent<HealthSystem>();

        SerializedObject serialized = new SerializedObject(context);
        serialized.FindProperty("baseStats").objectReferenceValue = CreateStats(name);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return enemy;
    }

    CharacterStats CreateStats(string name)
    {
        var stats = ScriptableObject.CreateInstance<CharacterStats>();
        stats.name = $"{name} Stats";
        created.Add(stats);
        return stats;
    }

    static void SetWavePrefab(EncounterDefinitionSO encounter, GameObject prefab)
    {
        SerializedObject serialized = new SerializedObject(encounter);
        SerializedProperty pool = serialized.FindProperty("waves").GetArrayElementAtIndex(0).FindPropertyRelative("enemyPrefabs");
        pool.arraySize = 1;
        pool.GetArrayElementAtIndex(0).objectReferenceValue = prefab;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    RoomDefinitionSO CreateRoomDefinition(MapNodeType nodeType, RoomExitMask exitMask, bool addController)
    {
        GameObject prefab = CreateGameObject($"Room.{nodeType}");
        if (addController)
            prefab.AddComponent<RoomController>();

        var definition = ScriptableObject.CreateInstance<RoomDefinitionSO>();
        definition.name = $"RoomDefinition.{nodeType}";
        created.Add(definition);

        SerializedObject serialized = new SerializedObject(definition);
        serialized.FindProperty("nodeType").enumValueIndex = (int)nodeType;
        serialized.FindProperty("weight").floatValue = 1f;
        serialized.FindProperty("roomPrefab").objectReferenceValue = prefab;
        serialized.FindProperty("maxExitCount").intValue = 4;
        serialized.FindProperty("exitMask").intValue = (int)exitMask;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    static void AddExit(GameObject roomPrefab, RoomExitDirection direction)
    {
        var exitObject = new GameObject($"Exit.{direction}");
        exitObject.transform.SetParent(roomPrefab.transform, false);
        RoomExitInteractable exit = exitObject.AddComponent<RoomExitInteractable>();

        SerializedObject serialized = new SerializedObject(exit);
        serialized.FindProperty("direction").enumValueIndex = (int)direction;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
