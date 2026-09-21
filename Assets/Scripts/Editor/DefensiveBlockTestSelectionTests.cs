#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class DefensiveBlockTestSelectionTests
{
    [Test]
    public void TestControlsSuspendPlayerActionsAndRestoreOnlyPreviouslyActiveInput()
    {
        var host = new GameObject("Block cursor test");
        host.SetActive(false);
        var actor = new GameObject("Test player input");
        actor.SetActive(false);
        var actions = ScriptableObject.CreateInstance<InputActionAsset>();
        try
        {
            var map = actions.AddActionMap("Gameplay");
            map.AddAction("Fire", InputActionType.Button, "<Mouse>/leftButton");
            var input = actor.AddComponent<PlayerInput>();
            input.actions = actions;
            input.defaultActionMap = "Gameplay";
            // Edit Mode does not run PlayerInput.OnEnable; bind the map explicitly.
            input.currentActionMap = map;
            var player = actor.AddComponent<PlayerContext>();
            var harness = host.AddComponent<DefensiveBlockTestHarness>();
            typeof(DefensiveBlockTestHarness).GetField("<Player>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(harness, player);
            var sync = typeof(DefensiveBlockTestHarness).GetMethod("SyncTestControlsInput", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var release = typeof(DefensiveBlockTestHarness).GetMethod("ReleaseTestControls", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            input.ActivateInput();
            player.moveInput = Vector2.one; player.lookInput = Vector2.one;
            Assert.That(harness.TestControlsOpen, Is.True);
            sync.Invoke(harness, null);
            Assert.That(input.inputIsActive, Is.False);
            Assert.That(input.currentActionMap.enabled, Is.False);
            Assert.That(player.moveInput, Is.EqualTo(Vector2.zero));
            Assert.That(player.lookInput, Is.EqualTo(Vector2.zero));
            harness.SetTestControlsOpen(false);
            sync.Invoke(harness, null);
            Assert.That(input.inputIsActive, Is.True);
            Assert.That(input.currentActionMap.enabled, Is.True);
            input.DeactivateInput();
            harness.SetTestControlsOpen(true);
            sync.Invoke(harness, null);
            release.Invoke(harness, null);
            Assert.That(input.inputIsActive, Is.False, "Do not enable input that another system already disabled.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(actor);
            UnityEngine.Object.DestroyImmediate(actions);
        }
    }

    [Test]
    public void SwitchingEnemySelectsItsOwnSkillAndRejectsForeignOrInvalidChoices()
    {
        var host = new GameObject("Block selection test");
        host.SetActive(false);
        try
        {
            var harness = host.AddComponent<DefensiveBlockTestHarness>();
            harness.testEnemies = DefensiveBlockTestSceneBuilder.CollectEnemyChoices();
            int rector = Array.FindIndex(harness.testEnemies, entry => AssetDatabase.GetAssetPath(entry.prefab) == DefensiveBlockProductionAuthoring.RectorPath);
            int gr04 = Array.FindIndex(harness.testEnemies, entry => AssetDatabase.GetAssetPath(entry.prefab).EndsWith("Enemy_E_GR_01 Variant.prefab"));
            Assert.That(rector, Is.GreaterThanOrEqualTo(0));
            Assert.That(gr04, Is.GreaterThanOrEqualTo(0));
            Assert.That(harness.SelectEnemy(rector), Is.True);
            var oldSkill = harness.chargeSkill;
            Assert.That(harness.SelectEnemy(gr04), Is.True);
            Assert.That(harness.SelectedEnemy.skills, Does.Contain(harness.chargeSkill));
            Assert.That(harness.chargeSkill, Is.Not.SameAs(oldSkill));
            var selected = harness.chargeSkill;
            Assert.That(harness.SelectSkill(oldSkill), Is.False);
            Assert.That(harness.SelectSkill(null), Is.False);
            Assert.That(harness.SelectEnemy(-1), Is.False);
            Assert.That(harness.chargeSkill, Is.SameAs(selected));
            foreach (var skill in harness.SelectedEnemy.skills)
                Assert.That(harness.SelectSkill(skill), Is.True);
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    [Test]
    public void CatalogIncludesMeleeAndSkillsWithoutComboContainersAndPreservesCustomEntries()
    {
        var choices = DefensiveBlockTestSceneBuilder.CollectEnemyChoices();
        var rector = choices.First(entry => AssetDatabase.GetAssetPath(entry.prefab) == DefensiveBlockProductionAuthoring.RectorPath);
        Assert.That(rector.skills, Does.Contain(AssetDatabase.LoadAssetAtPath<SkillGemDefinition>(DefensiveBlockProductionAuthoring.SkillPath)));
        Assert.That(rector.skills.Any(skill => skill.SkillDefinitionDisplayName.Contains("Heavy")), Is.True);
        Assert.That(choices.All(entry => entry.skills.All(skill => skill != null && !skill.IsCombo)), Is.True);
        var custom = new DefensiveBlockTestEnemy { prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tests/DefensiveBlock/Rector.Test.prefab"), skills = rector.skills };
        Assert.That(custom.prefab, Is.Not.Null);
        Assert.That(DefensiveBlockTestSceneBuilder.CollectEnemyChoices(new[] { custom }).Any(entry => entry.prefab == custom.prefab), Is.True);
    }
}
#endif
