using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Covers the encounter content guarantees: a wave with one usable prefab always spawns it, and a
/// pool with no usable prefab reports itself instead of silently spawning nothing.
/// </summary>
public sealed class EncounterContentTests
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
    public void SingleUsablePrefabInAMostlyEmptyPoolIsAlwaysPicked()
    {
        GameObject prefab = CreatePrefabStandIn("Enemy");
        EncounterWave wave = CreateWave(new[] { null, null, prefab, null, null, null, null, null });

        for (int attempt = 0; attempt < 200; attempt++)
            Assert.That(wave.GetRandomEnemyPrefab(), Is.SameAs(prefab), $"Attempt {attempt} returned no prefab.");
    }

    [Test]
    public void EveryUsablePrefabInAPoolCanBePicked()
    {
        GameObject first = CreatePrefabStandIn("EnemyA");
        GameObject second = CreatePrefabStandIn("EnemyB");
        EncounterWave wave = CreateWave(new[] { first, null, second, null });

        bool sawFirst = false;
        bool sawSecond = false;
        for (int attempt = 0; attempt < 500 && !(sawFirst && sawSecond); attempt++)
        {
            GameObject picked = wave.GetRandomEnemyPrefab();
            Assert.That(picked, Is.Not.Null);
            sawFirst |= picked == first;
            sawSecond |= picked == second;
        }

        Assert.That(sawFirst && sawSecond, Is.True, "Both usable prefabs should be reachable.");
    }

    [Test]
    public void PoolWithoutAnyUsablePrefabReturnsNull()
    {
        Assert.That(CreateWave(new GameObject[] { null, null }).GetRandomEnemyPrefab(), Is.Null);
        Assert.That(CreateWave(new GameObject[0]).GetRandomEnemyPrefab(), Is.Null);
    }

    /// <summary>
    /// EncounterWave is a serialized inner class, so the pool is written the same way the
    /// authoring tool writes it.
    /// </summary>
    EncounterWave CreateWave(GameObject[] prefabs)
    {
        var encounter = ScriptableObject.CreateInstance<EncounterDefinitionSO>();
        created.Add(encounter);

        SerializedObject serialized = new SerializedObject(encounter);
        SerializedProperty waves = serialized.FindProperty("waves");
        waves.arraySize = 1;
        SerializedProperty wave = waves.GetArrayElementAtIndex(0);
        SerializedProperty pool = wave.FindPropertyRelative("enemyPrefabs");
        pool.arraySize = prefabs.Length;
        for (int i = 0; i < prefabs.Length; i++)
            pool.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
        wave.FindPropertyRelative("spawnCount").intValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(encounter.Waves, Is.Not.Null.And.Length.EqualTo(1));
        return encounter.Waves[0];
    }

    GameObject CreatePrefabStandIn(string name)
    {
        var prefab = new GameObject(name);
        prefab.SetActive(false);
        created.Add(prefab);
        return prefab;
    }
}
