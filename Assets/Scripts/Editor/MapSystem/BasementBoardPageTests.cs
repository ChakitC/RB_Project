using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Guards the Basement board ownership contract: the Test Stage authoring tool owns page 0 and
/// page 1, and every hand-authored page such as BossRushPage survives a re-run untouched.
/// </summary>
public sealed class BasementBoardPageTests
{
    const string BasementPath = "Assets/Scenes/Basement/Basement.unity";
    const string BossRushConfigPath = "Assets/Data/Map/TestStages/Boss Rush 01 Map Run Config.asset";
    const string PaginationName = "TestStagePagination";
    const string ExistingMapsPageName = "ExistingMapsPage";
    const string TestStagePageName = "TestStagePage";
    const string BossRushPageName = "BossRushPage";

    [Test]
    public void PageOrderKeepsHandAuthoredPagesAfterTheToolOwnedPages()
    {
        var existingPage = new GameObject(ExistingMapsPageName);
        var testStagePage = new GameObject(TestStagePageName);
        var bossRushPage = new GameObject(BossRushPageName);
        var anotherPage = new GameObject("AnotherHandAuthoredPage");
        try
        {
            // What the pager holds before the tool runs, including an already-deleted slot.
            var registered = new List<GameObject>
            {
                existingPage,
                testStagePage,
                bossRushPage,
                null,
                anotherPage,
            };

            List<GameObject> ordered = TestStageAuthoringTool.BuildPagerPageOrder(
                existingPage,
                testStagePage,
                registered);

            Assert.That(ordered, Is.EqualTo(new[] { existingPage, testStagePage, bossRushPage, anotherPage }));
        }
        finally
        {
            Object.DestroyImmediate(anotherPage);
            Object.DestroyImmediate(bossRushPage);
            Object.DestroyImmediate(testStagePage);
            Object.DestroyImmediate(existingPage);
        }
    }

    [Test]
    public void PageOrderIsStableWhenTheToolRunsAgain()
    {
        var existingPage = new GameObject(ExistingMapsPageName);
        var testStagePage = new GameObject(TestStagePageName);
        var bossRushPage = new GameObject(BossRushPageName);
        try
        {
            List<GameObject> first = TestStageAuthoringTool.BuildPagerPageOrder(
                existingPage,
                testStagePage,
                new List<GameObject> { existingPage, testStagePage, bossRushPage });
            List<GameObject> second = TestStageAuthoringTool.BuildPagerPageOrder(
                existingPage,
                testStagePage,
                first);

            Assert.That(second, Is.EqualTo(first));
        }
        finally
        {
            Object.DestroyImmediate(bossRushPage);
            Object.DestroyImmediate(testStagePage);
            Object.DestroyImmediate(existingPage);
        }
    }

    [Test]
    public void BasementSceneStillHoldsTheHandAuthoredBossRushPage()
    {
        Scene scene = EditorSceneManager.OpenScene(BasementPath, OpenSceneMode.Additive);
        try
        {
            Transform pagination = FindInScene(scene, PaginationName);
            Assert.That(pagination, Is.Not.Null, $"Basement has no MapUI/{PaginationName}.");

            var pager = pagination.GetComponent<MobilizBoardPager>();
            Assert.That(pager, Is.Not.Null, $"{PaginationName} has no MobilizBoardPager.");

            SerializedProperty pages = new SerializedObject(pager).FindProperty("pages");
            var pageNames = new List<string>();
            for (int i = 0; i < pages.arraySize; i++)
            {
                var page = pages.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                pageNames.Add(page != null ? page.name : "<missing>");
            }

            Assert.That(pageNames.Count, Is.GreaterThanOrEqualTo(3), "Expected at least three board pages.");
            Assert.That(pageNames[0], Is.EqualTo(ExistingMapsPageName));
            Assert.That(pageNames[1], Is.EqualTo(TestStagePageName));
            Assert.That(pageNames[2], Is.EqualTo(BossRushPageName));

            Transform bossRushPage = pagination.Find(BossRushPageName);
            Assert.That(bossRushPage, Is.Not.Null, $"{BossRushPageName} is no longer a child of {PaginationName}.");

            var placard = bossRushPage.GetComponentInChildren<StagePlacardButton>(true);
            Assert.That(placard, Is.Not.Null, $"{BossRushPageName} has no StagePlacardButton.");

            var expected = AssetDatabase.LoadAssetAtPath<MapRunConfigSO>(BossRushConfigPath);
            Assert.That(expected, Is.Not.Null, $"Missing Boss Rush config at '{BossRushConfigPath}'.");
            Assert.That(placard.RunConfig, Is.SameAs(expected), "The BOSS RUSH placard lost its run config.");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    static Transform FindInScene(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                if (transforms[j].name == name)
                    return transforms[j];
            }
        }

        return null;
    }
}
