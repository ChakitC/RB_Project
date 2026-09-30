#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BlockAnimationOwnershipTests
{
    [Test]
    public void MigrationPreservesAllFieldsSupportsUndoAndDoesNotOverwriteAuthoredData()
    {
        var legacy = AssetDatabase.LoadAssetAtPath<BlockAnimationProfile>("Assets/Data/DefensiveBlock/AiresBlockAnimation.asset");
        var profile = ScriptableObject.CreateInstance<CharacterAnimProfileSO>();
        try
        {
            Assert.That(BlockAnimationProfileMigration.TryMigrate(profile, legacy), Is.True);
            Undo.FlushUndoRecordObjects();
            Assert.That(JsonUtility.ToJson(profile.block), Is.EqualTo(JsonUtility.ToJson(BlockAnimationSettings.FromLegacy(legacy))));
            Undo.PerformUndo(); Assert.That(profile.block.HasClips, Is.False);
            Undo.PerformRedo(); Assert.That(profile.block.IsConfigured, Is.True);
            profile.block.beginSeconds = .9f;
            Assert.That(BlockAnimationProfileMigration.TryMigrate(profile, legacy), Is.False);
            Assert.That(profile.block.beginSeconds, Is.EqualTo(.9f));
        }
        finally { Undo.ClearUndo(profile); Object.DestroyImmediate(profile); }
    }

    [Test]
    public void CharacterAndOverrideProfilesOwnBlockEvenWhenGuardSettingsAreShared()
    {
        var root = new GameObject("Block animation ownership"); root.SetActive(false);
        var character = ScriptableObject.CreateInstance<CharacterStats>();
        var profile = ScriptableObject.CreateInstance<CharacterAnimProfileSO>();
        var other = ScriptableObject.CreateInstance<CharacterAnimProfileSO>();
        var guard = ScriptableObject.CreateInstance<DefensiveBlockActorProfile>();
        try
        {
            var context = root.AddComponent<EnemyContext>();
            var brain = root.AddComponent<CharacterAnimBrain>();
            var controller = root.AddComponent<DefensiveBlockController>();
            context.baseStats = character;
            character.animProfile = profile; character.defensiveBlock = guard;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(CharacterAnimBrain).GetField("ctx", flags).SetValue(brain, context);
            typeof(DefensiveBlockController).GetField("ctx", flags).SetValue(controller, context);
            context.ResolveReferences();
            Assert.That(brain.BlockAnimation, Is.SameAs(profile.block));
            Assert.That(controller.AnimationSettings, Is.SameAs(profile.block));
            brain.SetAnimProfileOverride(other);
            Assert.That(controller.AnimationSettings, Is.SameAs(other.block));
            brain.ClearAnimProfileOverride();
            character.animProfile = other;
            Assert.That(controller.AnimationSettings, Is.SameAs(other.block));
            Assert.That(controller.Settings, Is.SameAs(guard));
            var resolve = typeof(DefensiveBlockController).GetMethod("ResolveProfile", flags);
            Assert.That(resolve.Invoke(controller, null), Is.False, "Missing character Block clips must fail readiness.");
        }
        finally
        {
            Object.DestroyImmediate(root); Object.DestroyImmediate(character);
            Object.DestroyImmediate(profile); Object.DestroyImmediate(other); Object.DestroyImmediate(guard);
        }
    }

    [Test]
    public void ProductionProfilesHaveIndependentBlockSettings()
    {
        var aires = AssetDatabase.LoadAssetAtPath<CharacterAnimProfileSO>("Assets/Data/Animation/Aires/Aires_AnimProfile.asset");
        foreach (string name in new[] { "Aires", "Roma", "Feno", "Milano" })
        {
            var profile = AssetDatabase.LoadAssetAtPath<CharacterAnimProfileSO>($"Assets/Data/Animation/{name}/{name}_AnimProfile.asset");
            Assert.That(profile.block.IsConfigured, Is.True, name);
            if (profile != aires) Assert.That(profile.block, Is.Not.SameAs(aires.block));
        }
        var snapshot = aires.block.Copy();
        snapshot.beginSeconds += 1f;
        Assert.That(snapshot.beginSeconds, Is.Not.EqualTo(aires.block.beginSeconds));
    }
}
#endif
