using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public static class PartySpawnFeatureSmokeTests
{
    static readonly string[] GameplayScenePaths =
    {
        "Assets/Scenes/MapPlayMode GR_01 Prototy/MapRun.unity",
        "Assets/Scenes/Map_TestAI/Map_TestAI.unity",
        "Assets/Scenes/Map_Play_Pototype/State_1.unity",
        "Assets/Scenes/MapBossTest/BoosTest.unity",
    };

    [MenuItem("Tools/RB/Party/Run Party Spawn Smoke Tests")]
    public static void RunFromMenu()
    {
        RunFromCommandLine();
        EditorUtility.DisplayDialog("Party Spawn Tests", "All party spawn smoke tests passed.", "OK");
    }

    public static void RunFromCommandLine()
    {
        PartySpawnConfigSO config =
            AssetDatabase.LoadAssetAtPath<PartySpawnConfigSO>(PartySpawnMigrationTool.ConfigPath);
        Expect(config != null, "Default party spawn config must exist.");
        Expect(config.TryValidate(out string configError), configError);

        TestRoleContract(config);
        TestPlayerUIPrefab(config);
        TestRuntimeComposition(config);
        TestMigratedScenes();

        Debug.Log("[PartySpawnTests] All party spawn smoke tests passed.");
    }

    static void TestRoleContract(PartySpawnConfigSO config)
    {
        PartySpawnEntry player = config.GetMember(ChainActorRole.Player);
        PartySpawnEntry ally1 = config.GetMember(ChainActorRole.PartySlot1);
        PartySpawnEntry ally2 = config.GetMember(ChainActorRole.PartySlot2);
        PartySpawnEntry helper = config.GetMember(ChainActorRole.Helper);

        Expect(player != null && player.PartyIndex == 0, "Player must use party index 0.");
        Expect(ally1 != null && ally1.PartyIndex == 1, "PartySlot1 must use party index 1.");
        Expect(ally2 != null && ally2.PartyIndex == 2, "PartySlot2 must use party index 2.");
        Expect(helper != null && helper.PartyIndex == 3, "Helper must use party index 3.");
        Expect(ReferenceEquals(ally1.Prefab, ally2.Prefab),
            "PartySlot1 and PartySlot2 must instantiate the same Ally_Stryker prefab asset.");
    }

    static void TestPlayerUIPrefab(PartySpawnConfigSO config)
    {
        Expect(config.PlayerUIPrefab != null, "Player UI prefab must be assigned.");
        Expect(config.PlayerUIPrefab.GetComponentInChildren<PlayerUIRuntimeBinder>(true) != null,
            "Player UI prefab must contain PlayerUIRuntimeBinder.");
    }

    static void TestRuntimeComposition(PartySpawnConfigSO config)
    {
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        GameObject markerObject = null;
        PartyRuntime party = null;

        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            config = AssetDatabase.LoadAssetAtPath<PartySpawnConfigSO>(PartySpawnMigrationTool.ConfigPath);
            Expect(config != null, "Party spawn config must remain loadable after changing scenes.");
            markerObject = new GameObject("PartySpawnPoint_Test");
            PartySpawnPoint marker = markerObject.AddComponent<PartySpawnPoint>();
            AssignConfig(marker, config);

            Expect(marker.TrySpawnNow(out string error), error);
            party = marker.CurrentParty;
            Expect(party != null, "PartySpawnPoint must expose its created PartyRuntime.");
            Equal(4, party.Actors.Count, "Runtime party must contain four actor roles.");
            Expect(party.Root != null && party.Root.name == "PartyRuntimeRoot",
                "Runtime party must use PartyRuntimeRoot instead of PlayerSquad.");
            Expect(party.PlayerUIRoot != null && party.PlayerUIContext != null,
                "Runtime party must create and bind Player UI.");

            var roles = new HashSet<ChainActorRole>();
            for (int i = 0; i < party.Actors.Count; i++)
            {
                PartyRuntimeActor actor = party.Actors[i];
                Expect(roles.Add(actor.Role), $"Role '{actor.Role}' must not be duplicated.");
                Equal(actor.PartyIndex, actor.PartyLoader.PartyIndex,
                    $"Role '{actor.Role}' loader must receive its configured party index.");
                Equal(actor.Role, actor.FieldMember.ActorRole,
                    $"Role '{actor.Role}' FieldAllyMember must receive its configured actor role.");
            }

            FieldAllyManager manager = party.Player.fieldAllyManager;
            Expect(manager != null, "Player must expose FieldAllyManager.");
            foreach (ChainActorRole role in roles)
                Expect(manager.TryGetMember(role, out _), $"FieldAllyManager must register role '{role}'.");

            PartyFormationController formation = party.Player.partyFormation;
            Expect(formation != null, "Player must expose PartyFormationController.");
            Equal(3, formation.RegisteredMemberCount,
                "PartyFormationController must bind all three companion party indices.");
            for (int partyIndex = 1; partyIndex <= 3; partyIndex++)
            {
                Expect(formation.TryGetRegisteredMember(partyIndex, out AllyContext formationMember),
                    $"Formation member for party index {partyIndex} must be registered.");
                Equal(partyIndex, formationMember.CharacterLoad.PartyIndex,
                    $"Formation member {partyIndex} must retain its configured party index.");
            }
        }
        finally
        {
            if (party?.PlayerUIRoot != null)
                UnityEngine.Object.DestroyImmediate(party.PlayerUIRoot);
            if (party?.Root != null)
                UnityEngine.Object.DestroyImmediate(party.Root);
            if (markerObject != null)
                UnityEngine.Object.DestroyImmediate(markerObject);
            if (previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }

    static void TestMigratedScenes()
    {
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            for (int i = 0; i < GameplayScenePaths.Length; i++)
            {
                Scene scene = EditorSceneManager.OpenScene(GameplayScenePaths[i], OpenSceneMode.Single);
                int markerCount = 0;
                int legacyCount = 0;
                PartySpawnPoint marker = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    PartySpawnPoint[] rootMarkers =
                        roots[rootIndex].GetComponentsInChildren<PartySpawnPoint>(true);
                    markerCount += rootMarkers.Length;
                    if (rootMarkers.Length > 0)
                        marker = rootMarkers[0];
                    if (string.Equals(roots[rootIndex].name, "PlayerSquad", StringComparison.Ordinal))
                        legacyCount++;
                }

                Equal(1, markerCount, $"Scene '{scene.path}' must contain one PartySpawnPoint.");
                Equal(0, legacyCount, $"Scene '{scene.path}' must not contain legacy PlayerSquad.");
                Expect(marker != null && marker.Config != null,
                    $"Scene '{scene.path}' PartySpawnPoint must reference a config asset.");
                Expect(marker.Config.TryValidate(out string configError),
                    $"Scene '{scene.path}' PartySpawnPoint config is invalid: {configError}");
            }
        }
        finally
        {
            if (previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }

    internal static void AssignConfig(PartySpawnPoint marker, PartySpawnConfigSO config)
    {
        var serialized = new SerializedObject(marker);
        serialized.FindProperty("config").objectReferenceValue = config;
        serialized.FindProperty("spawnOnAwake").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message} Expected {expected}, got {actual}.");
    }
}

public sealed class PartySpawnUnityTests
{
    [Test]
    public void PartyAuthoringAndEditModeCompositionAreValid()
    {
        PartySpawnFeatureSmokeTests.RunFromCommandLine();
    }

    [UnityTest]
    public IEnumerator PartySpawnsWhenMarkerActivatesInPlayMode()
    {
        yield return new EnterPlayMode();

        PartySpawnConfigSO config =
            AssetDatabase.LoadAssetAtPath<PartySpawnConfigSO>(PartySpawnMigrationTool.ConfigPath);
        Assert.That(config, Is.Not.Null);

        GameObject markerObject = new("PartySpawnPoint_PlayModeTest");
        markerObject.SetActive(false);
        PartySpawnPoint marker = markerObject.AddComponent<PartySpawnPoint>();
        PartySpawnFeatureSmokeTests.AssignConfig(marker, config);
        markerObject.SetActive(true);

        yield return null;

        Assert.That(marker.CurrentParty, Is.Not.Null);
        Assert.That(marker.CurrentParty.Actors.Count, Is.EqualTo(4));
        Assert.That(marker.CurrentParty.Root.name, Is.EqualTo("PartyRuntimeRoot"));

        UnityEngine.Object.Destroy(marker.CurrentParty.PlayerUIRoot);
        UnityEngine.Object.Destroy(marker.CurrentParty.Root);
        UnityEngine.Object.Destroy(markerObject);

        yield return new ExitPlayMode();
    }
}
