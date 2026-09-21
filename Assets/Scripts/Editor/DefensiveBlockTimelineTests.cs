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

public sealed class DefensiveBlockTimelineTests
{
    readonly List<Object> owned = new();
    string folder;
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    DefensiveBlockTimelineSession Draft()
    {
        var skill = Own(ScriptableObject.CreateInstance<SkillGemDefinition>());
        skill.defensiveBlock = new SkillDefensiveBlockSettings();
        return Own(DefensiveBlockTimelineSession.Create(skill));
    }
    void Persist(DefensiveBlockTimelineSession draft)
    {
        folder = "Assets/__BlockTimelineTest_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
        AssetDatabase.CreateAsset(draft.skill, folder + "/Skill.asset");
        draft.Reload();
    }
    [TearDown] public void Cleanup()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null)
            {
                Undo.ClearUndo(owned[i]);
                if (!AssetDatabase.Contains(owned[i])) Object.DestroyImmediate(owned[i]);
            }
        if (folder != null)
        {
            foreach (string id in AssetDatabase.FindAssets("", new[] { folder }))
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(id))) Undo.ClearUndo(asset);
            AssetDatabase.DeleteAsset(folder);
        }
    }

    [Test] public void DraftSupportsUndoRedoRevertWithoutChangingProfile()
    {
        var draft = Draft();
        float original = draft.end;
        Undo.RecordObject(draft, "Block test"); draft.start = .2f; draft.end = .8f;
        Undo.FlushUndoRecordObjects();
        Assert.That(draft.IsDirty, Is.True);
        Assert.That(draft.profile.windowEndNormalized, Is.EqualTo(original));
        Undo.PerformUndo(); Assert.That(draft.IsDirty, Is.False);
        Undo.PerformRedo(); Assert.That(draft.start, Is.EqualTo(.2f));
        draft.Reload(); Assert.That(draft.IsDirty, Is.False);
        Assert.That(draft.end, Is.EqualTo(original));
    }

    [Test] public void SaveWritesOnlyTimingAndOtherAssetSavesCannotFlushTheDraft()
    {
        var draft = Draft(); Persist(draft);
        string skillBefore = EditorJsonUtility.ToJson(draft.skill);
        string profileBefore = JsonUtility.ToJson(draft.profile);
        draft.start = .2f; draft.end = .8f;
        EditorUtility.SetDirty(draft.skill); AssetDatabase.SaveAssetIfDirty(draft.skill);
        Assert.That(draft.profile.windowStartNormalized, Is.Zero);
        Assert.That(draft.Save(out var error), Is.True, error);
        string profilePath = folder + "/Skill.asset";
        AssetDatabase.ImportAsset(profilePath, ImportAssetOptions.ForceUpdate);
        var loaded = AssetDatabase.LoadAssetAtPath<SkillGemDefinition>(profilePath).defensiveBlock;
        Assert.That(loaded.windowStartNormalized, Is.EqualTo(.2f));
        Assert.That(loaded.windowEndNormalized, Is.EqualTo(.8f));
        var expectedSkill = Own(Object.Instantiate(draft.skill));
        expectedSkill.name = draft.skill.name;
        expectedSkill.defensiveBlock = JsonUtility.FromJson<SkillDefensiveBlockSettings>(profileBefore);
        Assert.That(EditorJsonUtility.ToJson(expectedSkill), Is.EqualTo(skillBefore));
        var expected = new SkillDefensiveBlockSettings();
        JsonUtility.FromJsonOverwrite(profileBefore, expected);
        expected.windowStartNormalized = .2f; expected.windowEndNormalized = .8f;
        Assert.That(JsonUtility.ToJson(loaded), Is.EqualTo(JsonUtility.ToJson(expected)));
    }

    [Test] public void ExternalProfileChangesAndRebindingBlockSaveUntilReload()
    {
        var draft = Draft(); Persist(draft); draft.end = .9f;
        draft.profile.knockbackDistance = 9f;
        Assert.That(draft.Save(out _), Is.False);
        draft.Reload(); draft.end = .9f;
        var other = new SkillDefensiveBlockSettings();
        draft.skill.defensiveBlock = other;
        Assert.That(draft.HasConflict, Is.True);
        Assert.That(draft.Save(out _), Is.False);
        draft.Reload(); Assert.That(JsonUtility.ToJson(draft.profile), Is.EqualTo(JsonUtility.ToJson(other)));
        Assert.That(draft.IsDirty, Is.False);
    }

    [Test] public void InvalidRangesAreRejectedAndEndpointsMatchRuntime()
    {
        var draft = Draft();
        foreach (var range in new[] { new Vector2(-.1f, .8f), new Vector2(.8f, .2f), new Vector2(0, 1.1f),
            new Vector2(float.NaN, .8f), new Vector2(0, float.PositiveInfinity) })
        { draft.start = range.x; draft.end = range.y; Assert.That(draft.ValidationError, Is.Not.Null); }
        draft.start = .2f; draft.end = .8f;
        foreach (float time in new[] { .2f, .8f, .5f, .8f, .2f }) Assert.That(draft.Contains(time), Is.True);
        foreach (float time in new[] { .19f, .81f, 0f, 1f }) Assert.That(draft.Contains(time), Is.False);
        draft.end = draft.start; Assert.That(draft.ValidationError, Is.Null);
    }

    [Test] public void MainClipCoordinatesIncludeCutsceneOffsetAndRoundTrip()
    {
        var window = Own(ScriptableObject.CreateInstance<SkillAnimationVfxEditorWindow>());
        var type = window.GetType();
        type.GetField("_cutsceneSkillFraction", Hidden).SetValue(window, .25f);
        Rect row = new Rect(120, 0, 800, 38);
        foreach (float time in new[] { 0f, .2f, .8f, 1f })
        {
            var marker = (Rect)type.GetMethod("GetMarkerRect", Hidden).Invoke(window, new object[] { row, time });
            Assert.That(marker.center.x, Is.EqualTo(120 + 800 * (.25f + time * .75f)).Within(.001f));
            float roundTrip = (float)type.GetMethod("MouseToNormalized", Hidden).Invoke(window, new object[] { row, marker.center.x });
            Assert.That(roundTrip, Is.EqualTo(time).Within(.001f));
        }
    }

    [Test] public void RectorBindingLoadsCurrentProfileAndCutsceneHasNoBlockTrack()
    {
        var skill = AssetDatabase.LoadAssetAtPath<SkillGemDefinition>("Assets/Data/Skills/Enemies/Rector/Rector_Skill_1.asset");
        Assert.That(skill, Is.Not.Null);
        var draft = Own(DefensiveBlockTimelineSession.Create(skill));
        Assert.That(skill.defensiveBlock, Is.Not.Null);
        Assert.That(draft.start, Is.EqualTo(draft.profile.windowStartNormalized));
        Assert.That(draft.end, Is.EqualTo(draft.profile.windowEndNormalized));
        var window = Own(ScriptableObject.CreateInstance<SkillAnimationVfxEditorWindow>());
        var type = window.GetType();
        type.GetField("blockSession", Hidden).SetValue(window, draft);
        var show = type.GetMethod("ShowBlockTrack", Hidden);
        Assert.That(show.Invoke(window, new object[] { new SkillVfxTimelineSource(skill) }), Is.True);
        Assert.That(show.Invoke(window, new object[] { new CutsceneSkillVfxTimelineSource(skill) }), Is.False);
        type.GetField("blockSession", Hidden).SetValue(window, null);
    }

    [Test] public void AddBlockIsUndoableAndSavesInlineWithoutCreatingASubAsset()
    {
        var draft = Draft(); draft.skill.defensiveBlock = null; Persist(draft);
        string before = File.ReadAllText(folder + "/Skill.asset");
        Assert.That(draft.IsEnabled, Is.False);
        draft.AddBlock(); Undo.FlushUndoRecordObjects();
        Assert.That(draft.IsDirty, Is.True);
        Assert.That(draft.skill.defensiveBlock, Is.Null);
        Assert.That(File.ReadAllText(folder + "/Skill.asset"), Is.EqualTo(before));
        Undo.PerformUndo(); Assert.That(draft.IsEnabled, Is.False);
        Undo.PerformRedo(); Assert.That(draft.IsEnabled, Is.True);
        draft.Reload(); Assert.That(draft.IsEnabled, Is.False);
        draft.AddBlock(); draft.start = .1f; draft.end = .4f;
        Assert.That(draft.Save(out var error), Is.True, error);
        Assert.That(draft.skill.defensiveBlock, Is.Not.Null);
        Assert.That(draft.profile.windowStartNormalized, Is.EqualTo(.1f));
        Assert.That(draft.IsDirty, Is.False);
        Assert.That(draft.Save(out error), Is.True, error);
        Assert.That(AssetDatabase.LoadAllAssetsAtPath(folder + "/Skill.asset").Length, Is.EqualTo(1));
    }

    [Test] public void ForeignProfileIsCopiedWithoutEditingItsOwnerOrNonTimingValues()
    {
        var draft = Draft(); Persist(draft);
        var first = draft.skill;
        string before = File.ReadAllText(folder + "/Skill.asset");
        var second = Own(ScriptableObject.CreateInstance<SkillGemDefinition>());
        second.defensiveBlock = first.defensiveBlock;
        AssetDatabase.CreateAsset(second, folder + "/Second.asset");
        var secondDraft = Own(DefensiveBlockTimelineSession.Create(second));
        Assert.That(second.defensiveBlock, Is.Not.SameAs(first.defensiveBlock));
        secondDraft.start = .25f;
        Assert.That(secondDraft.Save(out var error), Is.True, error);
        Assert.That(second.defensiveBlock, Is.Not.Null);
        Assert.That(second.defensiveBlock, Is.Not.SameAs(first.defensiveBlock));
        Assert.That(second.defensiveBlock.knockbackDistance, Is.EqualTo(first.defensiveBlock.knockbackDistance));
        Assert.That(second.defensiveBlock.hitboxSteps, Is.EqualTo(first.defensiveBlock.hitboxSteps));
        Assert.That(File.ReadAllText(folder + "/Skill.asset"), Is.EqualTo(before));
        Assert.That(first.defensiveBlock.windowStartNormalized, Is.Zero);
    }

    [Test] public void ModeAndApproachSettingsAreUndoableDraftAndSavedWithWindows()
    {
        var draft = Draft(); Persist(draft);
        draft.SetMode(DefensiveBlockMode.TimedApproach);
        draft.timedApproachSeconds = .37f; draft.approachStandOff = 1.25f;
        Undo.FlushUndoRecordObjects();
        Assert.That(draft.profile.mode, Is.EqualTo(DefensiveBlockMode.Contact));
        Undo.PerformUndo(); Assert.That(draft.IsDirty, Is.False);
        Undo.PerformRedo(); Assert.That(draft.IsDirty, Is.True);
        Assert.That(draft.Save(out var error), Is.True, error);
        Assert.That(draft.profile.mode, Is.EqualTo(DefensiveBlockMode.TimedApproach));
        Assert.That(draft.profile.ApproachDuration, Is.EqualTo(.37f));
        Assert.That(draft.profile.approachStandOff, Is.EqualTo(1.25f));
        draft.DisableBlock(); Assert.That(draft.profile, Is.Not.Null);
        Assert.That(draft.Save(out error), Is.True, error);
        Assert.That(draft.profile, Is.Null);
        draft.AddBlock(); Assert.That(draft.Save(out error), Is.True, error);
        Assert.That(draft.profile.ApproachDuration, Is.EqualTo(.37f));
        Assert.That(draft.profile.approachStandOff, Is.EqualTo(1.25f));
    }

    [Test] public void InvalidTimedApproachCannotSaveAndContactIgnoresUnusedDuration()
    {
        var draft = Draft(); Persist(draft); draft.SetMode(DefensiveBlockMode.TimedApproach);
        foreach (float duration in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
        {
            draft.timedApproachSeconds = duration;
            Assert.That(draft.Save(out _), Is.False);
            Assert.That(draft.profile.mode, Is.EqualTo(DefensiveBlockMode.Contact));
        }
        draft.SetMode(DefensiveBlockMode.Contact);
        draft.timedApproachSeconds = 0f;
        Assert.That(draft.Save(out var error), Is.True, error);
        Assert.That(draft.profile.IsConfigured, Is.True);
        Assert.That(draft.profile.ApproachDuration, Is.Zero);
    }
    [Test] public void DuplicatingSkillCopiesItsOwnProfileAndTimingsStayIndependent()
    {
        var draft = Draft(); Persist(draft);
        Assert.That(AssetDatabase.CopyAsset(folder + "/Skill.asset", folder + "/Copy.asset"), Is.True);
        var copy = AssetDatabase.LoadAssetAtPath<SkillGemDefinition>(folder + "/Copy.asset");
        Assert.That(copy.defensiveBlock, Is.Not.Null);
        Assert.That(copy.defensiveBlock, Is.Not.SameAs(draft.profile));
        var copyDraft = Own(DefensiveBlockTimelineSession.Create(copy));
        copyDraft.end = .9f;
        Assert.That(copyDraft.Save(out var error), Is.True, error);
        Assert.That(draft.profile.windowEndNormalized, Is.EqualTo(.62f));
        Assert.That(copy.defensiveBlock.windowEndNormalized, Is.EqualTo(.9f));
    }

    [Test] public void AddingBlockDetectsAnotherToolAssigningProfileBeforeSave()
    {
        var draft = Draft(); draft.skill.defensiveBlock = null; Persist(draft);
        draft.AddBlock();
        draft.skill.defensiveBlock = new SkillDefensiveBlockSettings();
        Assert.That(draft.HasConflict, Is.True);
        Assert.That(draft.Save(out _), Is.False);
        Assert.That(AssetDatabase.LoadAllAssetsAtPath(folder + "/Skill.asset").Length, Is.EqualTo(1));
    }

    [Test] public void MultipleWindowsSaveIndependentRangesStepsAndOutcomes()
    {
        var draft = Draft(); Persist(draft);
        draft.SetRange(0, .1f, .25f);
        draft.AddWindow(.5f);
        draft.windows[0].onSuccess = DefensiveBlockOutcome.ContinueSkill;
        Assert.That(draft.WindowCount, Is.EqualTo(2));
        Assert.That(draft.Contains(.35f), Is.False);
        Assert.That(draft.ValidationError, Is.Null);
        Assert.That(draft.Save(out var error), Is.True, error);
        Assert.That(draft.profile.windows.Length, Is.EqualTo(2));
        Assert.That(draft.profile.Outcome(0), Is.EqualTo(DefensiveBlockOutcome.ContinueSkill));
        Assert.That(draft.profile.FindWindow(.55f), Is.EqualTo(1));
        Assert.That(draft.profile.Steps(1), Is.EqualTo(new[] { 2 }));
        draft.RemoveWindow(0);
        Assert.That(draft.Save(out error), Is.True, error);
        Assert.That(draft.profile.windows.Length, Is.EqualTo(1));
        Assert.That(draft.profile.FindWindow(.55f), Is.EqualTo(0));
    }

    [Test] public void MultipleWindowsRejectOverlapAndSharedStepsWithoutSaving()
    {
        var draft = Draft(); Persist(draft);
        draft.AddWindow(.8f);
        draft.SetRange(1, .5f, .9f);
        Assert.That(draft.Save(out _), Is.False);
        draft.SetRange(1, .8f, .9f);
        draft.windows[1].hitboxSteps = new[] { 0 };
        Assert.That(draft.Save(out _), Is.False);
        Assert.That(draft.profile.HasMultipleWindowData, Is.False);
        draft.Reload(); Assert.That(draft.WindowCount, Is.EqualTo(1));
    }

    public static string RunSmokeChecks()
    {
        var results = new List<string>();
        foreach (var method in typeof(DefensiveBlockTimelineTests).GetMethods().Where(m => m.GetCustomAttribute<TestAttribute>() != null))
        {
            var test = new DefensiveBlockTimelineTests();
            try { method.Invoke(test, null); results.Add("PASS " + method.Name); }
            catch (Exception ex) { results.Add("FAIL " + method.Name + ": " + (ex.InnerException ?? ex)); }
            finally { test.Cleanup(); }
        }
        string report = string.Join("\n", results);
        Directory.CreateDirectory("../BuildArtifacts");
        File.WriteAllText("../BuildArtifacts/block-timeline-tests.txt", report);
        return report;
    }
}
#endif
