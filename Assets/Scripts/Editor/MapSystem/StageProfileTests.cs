using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Profiles are the only source of tuning: a run config owns stage identity and nothing else.
/// These tests pin that a config reads its values from the profiles it points at, and that a config
/// missing one is rejected rather than quietly running on defaults.
/// </summary>
public sealed class StageProfileTests
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

    [Test]
    public void ShapeComesFromTheGenerationProfile()
    {
        MapRunConfigSO config = CreateConfig();
        MapGenerationProfileSO profile = CreateAsset<MapGenerationProfileSO>("Profile");
        SerializedObject serialized = new SerializedObject(profile);
        serialized.FindProperty("criticalPathNodeCount").intValue = 4;
        serialized.FindProperty("maxOutgoingPerNode").intValue = 3;
        serialized.FindProperty("forceBlueBeforeBoss").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assign(config, "generationProfile", profile);

        Assert.That(config.CriticalPathNodeCount, Is.EqualTo(4));
        Assert.That(config.MaxOutgoingPerNode, Is.EqualTo(3));
        Assert.That(config.ForceBlueBeforeBoss, Is.True);
    }

    [Test]
    public void ContentComesFromTheContentPool()
    {
        MapRunConfigSO config = CreateConfig();
        RoomDefinitionSO room = CreateAsset<RoomDefinitionSO>("PoolRoom");
        MapContentPoolSO pool = CreateAsset<MapContentPoolSO>("Pool");
        SerializedObject serialized = new SerializedObject(pool);
        SerializedProperty rooms = serialized.FindProperty("roomDefinitions");
        rooms.arraySize = 1;
        rooms.GetArrayElementAtIndex(0).objectReferenceValue = room;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assign(config, "contentPool", pool);

        Assert.That(config.RoomDefinitions, Is.EqualTo(new[] { room }));
    }

    [Test]
    public void ProgressionComesFromTheProgressionProfile()
    {
        MapRunConfigSO config = CreateConfig();
        StageProgressionProfileSO profile = CreateAsset<StageProgressionProfileSO>("Progression");
        SerializedObject serialized = new SerializedObject(profile);
        serialized.FindProperty("startLevel").intValue = 20;
        serialized.FindProperty("targetLevel").intValue = 30;
        serialized.FindProperty("targetRunCount").intValue = 5;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assign(config, "progressionProfile", profile);

        Assert.That(config.StartLevel, Is.EqualTo(20));
        Assert.That(config.TargetLevel, Is.EqualTo(30));
        Assert.That(config.TargetRunCount, Is.EqualTo(5));
    }

    /// <summary>Stage identity is per-stage and never served by a shared profile.</summary>
    [Test]
    public void StageIdentityStaysOnTheConfig()
    {
        MapRunConfigSO config = CreateConfig();
        SerializedObject serialized = new SerializedObject(config);
        serialized.FindProperty("stageId").stringValue = "stage_identity";
        SerializedProperty legacy = serialized.FindProperty("legacyStageIds");
        legacy.arraySize = 1;
        legacy.GetArrayElementAtIndex(0).stringValue = "old_stage_identity";
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assign(config, "progressionProfile", CreateAsset<StageProgressionProfileSO>("Progression"));

        Assert.That(config.StageId, Is.EqualTo("stage_identity"));
        Assert.That(config.IsTestStage, Is.True);
        Assert.That(config.LegacyStageIds, Is.EqualTo(new[] { "old_stage_identity" }));
    }

    [Test]
    public void ConfigWithoutAGenerationProfileIsRejected()
    {
        MapRunConfigSO config = CreateConfig();
        Assign(config, "contentPool", CreateAsset<MapContentPoolSO>("Pool"));

        Assert.That(MapRunConfigValidator.Validate(config, out string error), Is.False);
        Assert.That(error, Does.Contain("Generation Profile is missing"));
    }

    [Test]
    public void ConfigWithoutAContentPoolIsRejected()
    {
        MapRunConfigSO config = CreateConfig();
        Assign(config, "generationProfile", CreateAsset<MapGenerationProfileSO>("Profile"));

        Assert.That(MapRunConfigValidator.Validate(config, out string error), Is.False);
        Assert.That(error, Does.Contain("Content Pool is missing"));
    }

    [Test]
    public void TestStageWithoutAProgressionProfileIsRejected()
    {
        MapRunConfigSO config = CreateConfig();
        SerializedObject serialized = new SerializedObject(config);
        serialized.FindProperty("stageId").stringValue = "stage_needs_progression";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assign(config, "generationProfile", CreateAsset<MapGenerationProfileSO>("Profile"));
        Assign(config, "contentPool", CreateAsset<MapContentPoolSO>("Pool"));

        Assert.That(MapRunConfigValidator.Validate(config, out string error), Is.False);
        Assert.That(error, Does.Contain("Stage Progression Profile"));
    }

    /// <summary>
    /// A config with no profile must not crash on read. It is rejected by the validator, and the
    /// properties stay safe to touch while that error is being reported.
    /// </summary>
    [Test]
    public void ProfilelessConfigReadsSafeDefaultsInsteadOfThrowing()
    {
        MapRunConfigSO config = CreateConfig();

        Assert.That(config.MainPathWeights, Is.Empty);
        Assert.That(config.RoomDefinitions, Is.Empty);
        Assert.That(config.EncounterDefinitions, Is.Empty);
        Assert.That(config.PitySystem, Is.Not.Null);
        Assert.That(config.LevelTable, Is.Null);
        Assert.That(config.StageExitPrefab, Is.Null);
        Assert.That(config.GetXpBudgetPerRun(), Is.Zero);
        Assert.That(config.GetEnemyLevel(0), Is.EqualTo(config.StartLevel));
    }

    MapRunConfigSO CreateConfig()
    {
        return CreateAsset<MapRunConfigSO>("Config");
    }

    T CreateAsset<T>(string name) where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        asset.name = name;
        created.Add(asset);
        return asset;
    }

    static void Assign(MapRunConfigSO config, string propertyPath, Object value)
    {
        SerializedObject serialized = new SerializedObject(config);
        serialized.FindProperty(propertyPath).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
