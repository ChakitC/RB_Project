#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SkillHitboxSetupTests
{
    readonly List<Object> owned = new();
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    [TearDown] public void Cleanup()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) { Undo.ClearUndo(owned[i]); Object.DestroyImmediate(owned[i]); }
    }

    [Test] public void BoneAndSiblingModuleResolveTheirOwnActorButPartyContainerIsRejected()
    {
        var party = Own(new GameObject("Party"));
        var actor = new GameObject("Actor"); actor.transform.SetParent(party.transform);
        var stats = new GameObject("Stats"); stats.transform.SetParent(actor.transform);
        var ctx = stats.AddComponent<IdentityProbeContext>();
        var bone = new GameObject("Hand"); bone.transform.SetParent(actor.transform);
        Assert.That(SkillHitboxCharacterSetup.ResolveCharacter(bone), Is.SameAs(ctx));
        var other = new GameObject("Other actor"); other.transform.SetParent(party.transform); other.AddComponent<IdentityProbeContext>();
        Assert.That(SkillHitboxCharacterSetup.ResolveCharacter(party), Is.Null);
        Assert.That(SkillHitboxCharacterSetup.ResolveCharacter(bone), Is.SameAs(ctx));
    }

    [Test] public void SetupIsIdempotentAndReusesNestedAuthoring()
    {
        var actor = Own(new GameObject("Actor")); var ctx = actor.AddComponent<IdentityProbeContext>();
        var child = new GameObject("Authoring"); child.transform.SetParent(actor.transform);
        var existing = child.AddComponent<SetAnimationVfxData>();
        Assert.That(SkillHitboxCharacterSetup.Prepare(ctx), Is.SameAs(existing));
        Assert.That(SkillHitboxCharacterSetup.Prepare(ctx), Is.SameAs(existing));
        Assert.That(actor.GetComponentsInChildren<SetAnimationVfxData>(true).Length, Is.EqualTo(1));
        Assert.That(existing.CharacterRoot, Is.SameAs(ctx.transform));
    }

    [Test] public void AddedAuthoringCanBeUndoneAndRedone()
    {
        var actor = Own(new GameObject("Actor")); var ctx = actor.AddComponent<IdentityProbeContext>();
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        SkillHitboxCharacterSetup.Prepare(ctx); Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(group);
        Undo.PerformUndo(); Assert.That(actor.GetComponent<SetAnimationVfxData>(), Is.Null);
        Undo.PerformRedo(); Assert.That(actor.GetComponent<SetAnimationVfxData>(), Is.Not.Null);
    }

    [Test] public void ParentAuthoringUsesContextModelInsteadOfPlaceholderAnimator()
    {
        var actor = Own(new GameObject("Actor wrapper")); actor.AddComponent<Animator>();
        var tool = actor.AddComponent<SetAnimationVfxData>();
        var stats = new GameObject("Stats"); stats.transform.SetParent(actor.transform);
        var ctx = stats.AddComponent<IdentityProbeContext>();
        ctx.Visual = actor.AddComponent<CharacterVisualController>();
        var modelRoot = new GameObject("Model Root"); modelRoot.transform.SetParent(actor.transform);
        var model = new GameObject("Actual model"); model.transform.SetParent(modelRoot.transform); var animator = model.AddComponent<Animator>();
        typeof(CharacterVisualController).GetField("modelRoot", Hidden).SetValue(ctx.Visual, modelRoot.transform);
        Assert.That(SkillHitboxCharacterSetup.Prepare(ctx), Is.SameAs(tool));
        Assert.That(SkillHitboxSceneHandles.AnchorRoot(tool, SkillHitboxLayoutData.AnchorSpace.AnimatorRoot), Is.SameAs(animator.transform));
    }

    [Test] public void SkillChoicesIncludeAllVariantsAndSerializedEnemySlotsWithoutCreatingRuntimeEntries()
    {
        var actor = Own(new GameObject("Actor")); var ctx = actor.AddComponent<IdentityProbeContext>();
        ctx.baseStats = Own(ScriptableObject.CreateInstance<CharacterStats>());
        var first = Own(ScriptableObject.CreateInstance<SkillGemDefinition>()); first.name = "First";
        var second = Own(ScriptableObject.CreateInstance<SkillGemDefinition>()); second.name = "Second";
        var third = Own(ScriptableObject.CreateInstance<SkillGemDefinition>()); third.name = "Enemy attack";
        ctx.baseStats.skillSlots.Add(new CharacterSkillLoadoutSlot { options = new List<CharacterSkillLoadoutOption> {
            new() { skillAsset = first, displayName = "Variant A" }, new() { skillAsset = second, displayName = "Variant B" } } });
        ctx.baseStats.helperCommandSlot.options.Add(new CharacterSkillLoadoutOption { skillAsset = first });
        ctx.SkillManager = actor.AddComponent<CharacterSkillManager>();
        var so = new SerializedObject(ctx.SkillManager); var slots = so.FindProperty("autonomousSlots"); slots.arraySize = 1;
        slots.GetArrayElementAtIndex(0).FindPropertyRelative("skillAsset").objectReferenceValue = third; so.ApplyModifiedPropertiesWithoutUndo();
        string before = EditorJsonUtility.ToJson(ctx.SkillManager);
        var choices = SkillHitboxCharacterSetup.CollectAttacks(ctx);
        Assert.That(choices.Select(a => a.Skill), Is.EquivalentTo(new[] { first, second, third }));
        Assert.That(choices[0].Label, Is.EqualTo("Variant A"));
        Assert.That(EditorJsonUtility.ToJson(ctx.SkillManager), Is.EqualTo(before));
    }

    [Test] public void RealCharacterSetupSelectsReadableLightComboAndActualModelAnimator()
    {
        foreach (string path in new[] { "Assets/Prefab/GameEnemy/Enemy_B_GR_01 Variant.prefab", "Assets/Prefab/GameEnemy/Enemy_E_GR_01 Variant.prefab", "Assets/Prefab/GameEnemy/Enemy_Base.prefab" })
        {
            string before = File.ReadAllText(path);
            var actor = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path)));
            var ctx = SkillHitboxCharacterSetup.ResolveCharacter(actor); ctx.ResolveReferences();
            var window = Own(ScriptableObject.CreateInstance<SkillAnimationVfxEditorWindow>());
            var type = window.GetType(); type.GetField("hitboxMode", Hidden).SetValue(window, true);
            SkillHitboxGroup.TryResolveAnchor(SkillHitboxLayoutData.AnchorSpace.AnimatorRoot, "", ctx.transform, ctx, out var model);
            Assert.That(model, Is.Not.Null, path);
            Assert.That(type.GetMethod("SetupHitboxCharacter", Hidden).Invoke(window, new object[] { model.gameObject }), Is.True);
            var tool = (SetAnimationVfxData)type.GetField("authoringTarget", Hidden).GetValue(window);
            Assert.That(tool, Is.Not.Null);
            Assert.That(type.GetMethod("GetPreviewAnimator", Hidden).Invoke(window, null), Is.SameAs(model.GetComponent<Animator>()));
            var choices = SkillHitboxCharacterSetup.CollectAttacks(ctx);
            Assert.That(choices.Any(a => a.Category == "Light" && a.Label.StartsWith("Step 1")), Is.True);
            Assert.That(choices.Any(a => a.Category == "Heavy"), Is.True);
            Assert.That(AnimationVfxTimelineSourceFactory.Create(tool.TimelineSourceAsset, tool.TimelineEntryId).SourceAsset, Is.SameAs(ctx.baseStats.animProfile.lightCombo.Steps[0].executionSkill));
            Assert.That(File.ReadAllText(path), Is.EqualTo(before));
        }
    }

    [Test] public void StandaloneRectorModelFindsAttacksWithoutAddingGameplayContext()
    {
        var gameplayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/GameEnemy/Enemy_B_GR_01 Variant.prefab");
        var stats = gameplayPrefab.GetComponentInChildren<CharacteContext>(true).baseStats;
        var model = Own(Object.Instantiate(stats.CharacterPrefab));
        Assert.That(model.GetComponentInChildren<CharacteContext>(true), Is.Null);
        var rig = SkillHitboxCharacterSetup.ResolveModel(model.GetComponentsInChildren<Transform>(true).Last().gameObject);
        Assert.That(rig, Is.SameAs(model));
        Assert.That(SkillHitboxCharacterSetup.FindModelStats(model), Does.Contain(stats));
        var window = Own(ScriptableObject.CreateInstance<SkillAnimationVfxEditorWindow>());
        var type = window.GetType(); type.GetField("hitboxMode", Hidden).SetValue(window, true);
        Assert.That(type.GetMethod("SetupHitboxCharacter", Hidden).Invoke(window, new object[] { model }), Is.True);
        var tool = (SetAnimationVfxData)type.GetField("authoringTarget", Hidden).GetValue(window);
        var attacks = (List<SkillHitboxCharacterSetup.Attack>)type.GetField("hitboxAttacks", Hidden).GetValue(window);
        Assert.That(attacks.Any(a => a.Category == "Light"), Is.True);
        Assert.That(attacks.Any(a => a.Category == "Heavy"), Is.True);
        Assert.That(attacks.Count(a => a.Category == "Light"), Is.EqualTo(stats.animProfile.lightCombo.Count));
        Assert.That(tool.PreviewAnimator, Is.SameAs(model.GetComponent<Animator>()));
        Assert.That(model.GetComponentInChildren<CharacteContext>(true), Is.Null);
    }

    public static string RunSmokeChecks()
    {
        var results = new List<string>();
        foreach (var method in typeof(SkillHitboxSetupTests).GetMethods().Where(m => m.GetCustomAttribute<TestAttribute>() != null))
        {
            var test = new SkillHitboxSetupTests();
            try { method.Invoke(test, null); results.Add("PASS " + method.Name); }
            catch (Exception ex) { results.Add("FAIL " + method.Name + ": " + (ex.InnerException ?? ex)); }
            finally { test.Cleanup(); }
        }
        string report = string.Join("\n", results);
        File.WriteAllText("../BuildArtifacts/hitbox-setup-tests.txt", report);
        return report;
    }
}
#endif
