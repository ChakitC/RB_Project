#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Animancer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SkillHitboxAuthoringTests
{
    readonly List<Object> objects = new();
    readonly List<string> assets = new();
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    T Track<T>(T value) where T : Object { objects.Add(value); return value; }

    [TearDown] public void Cleanup()
    {
        foreach (string path in assets) AssetDatabase.DeleteAsset(path);
        for (int i = objects.Count - 1; i >= 0; i--)
            if (objects[i] != null && !EditorUtility.IsPersistent(objects[i])) { Undo.ClearUndo(objects[i]); Object.DestroyImmediate(objects[i]); }
    }

    public static string RunSmokeChecks()
    {
        var results = new List<string>();
        foreach (var method in typeof(SkillHitboxAuthoringTests).GetMethods())
        {
            if (method.GetCustomAttribute<TestAttribute>() == null) continue;
            var fixture = new SkillHitboxAuthoringTests();
            try { method.Invoke(fixture, null); results.Add("PASS " + method.Name); }
            catch (Exception ex) { results.Add("FAIL " + method.Name + ": " + (ex.InnerException ?? ex)); }
            finally { fixture.Cleanup(); }
        }
        string report = string.Join("\n", results);
        File.WriteAllText("../BuildArtifacts/hitbox-authoring-tests.txt", report);
        return report;
    }

    SkillGemDefinition Skill(bool composite = false)
    {
        var skill = Track(ScriptableObject.CreateInstance<SkillGemDefinition>()); skill.name = "Hitbox Authoring Test";
        var clip = Track(new AnimationClip());
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 0));
        skill.skillClip = new ClipTransition { Clip = clip };
        AddEvent(skill, .1f, CombatTimelineEventName.HitStart); AddEvent(skill, .2f, CombatTimelineEventName.HitEnd);
        AddEvent(skill, .5f, CombatTimelineEventName.HitStart); AddEvent(skill, .6f, CombatTimelineEventName.HitEnd);
        AddEvent(skill, .35f, CombatTimelineEventName.Vfx);
        var first = Payload("First"); skill.payload = first;
        if (composite)
        {
            var root = Track(ScriptableObject.CreateInstance<CompositeSkillPayloadDef>()); skill.payload = root;
            foreach (var payload in new[] { first, Payload("Second") })
            { var step = new PayloadStep(); step.SetPayload(payload); root.AddStep(step); }
        }
        return skill;
    }
    PrefabHitboxSkillPayloadDef Payload(string name)
    {
        var payload = Track(ScriptableObject.CreateInstance<PrefabHitboxSkillPayloadDef>()); payload.name = name;
        payload.ReplaceHitboxLayoutGroups(MeleeSkillMigrationTool.CreateStarterLayout());
        var so = new SerializedObject(payload); var steps = so.FindProperty("steps"); steps.arraySize = 2;
        for (int i = 0; i < 2; i++)
        {
            var step = steps.GetArrayElementAtIndex(i);
            step.FindPropertyRelative("damageMultiplier").floatValue = 10 * (i + 1);
            step.FindPropertyRelative("hitPolicy").enumValueIndex = 1;
            step.FindPropertyRelative("clearHitCacheOnEnter").boolValue = true;
            step.FindPropertyRelative("knockbackDistance").floatValue = i + 2;
            var keys = step.FindPropertyRelative("groupKeys"); keys.arraySize = 1; keys.GetArrayElementAtIndex(0).stringValue = "Strike";
        }
        so.ApplyModifiedPropertiesWithoutUndo(); return payload;
    }
    static void AddEvent(SkillGemDefinition skill, float time, CombatTimelineEventName name)
    {
        var asset = StringAsset.Find(CombatTimelineEventNames.ToStringReference(name), out _);
        Assert.That(asset, Is.Not.Null);
        skill.skillClip.SerializedEvents ??= new AnimancerEvent.Sequence.Serializable();
        skill.skillClip.SerializedEvents.AddEvent(time, name: asset);
        skill.skillClip.SerializedEvents.Events = null;
    }
    SkillHitboxAuthoringSession Session(SkillGemDefinition skill) => Track(SkillHitboxAuthoringSession.Create(skill));

    string[] TimelineRows(SkillAnimationVfxEditorWindow window, IAnimationVfxTimelineSource source)
    {
        var type = typeof(SkillAnimationVfxEditorWindow);
        type.GetMethod("BuildTracks", Hidden).Invoke(window, new object[] { source });
        return ((System.Collections.IEnumerable)type.GetField("tracks", Hidden).GetValue(window))
            .Cast<object>().Select(t => (string)t.GetType().GetField("Label").GetValue(t)).ToArray();
    }

    [Test] public void TimelineHidesEmptyOptionalRowsAndKeepsUnmatchedEvents()
    {
        var skill = Skill();
        skill.skillClip.SerializedEvents = new AnimancerEvent.Sequence.Serializable();
        var window = Track(ScriptableObject.CreateInstance<SkillAnimationVfxEditorWindow>());
        CollectionAssert.AreEqual(new[] { "Animation", "Cast Point", "Hitbox" }, TimelineRows(window, new SkillVfxTimelineSource(skill)));
        AddEvent(skill, .3f, CombatTimelineEventName.Vfx);
        CollectionAssert.Contains(TimelineRows(window, new SkillVfxTimelineSource(skill)), "VFX");
        var unknown = Track(ScriptableObject.CreateInstance<StringAsset>()); unknown.name = "LegacyCustomEvent";
        skill.skillClip.SerializedEvents.AddEvent(.4f, name: unknown);
        skill.skillClip.SerializedEvents.Events = null;
        var rows = TimelineRows(window, new SkillVfxTimelineSource(skill));
        CollectionAssert.Contains(rows, "Other Events");
        skill.skillClip.SerializedEvents = new AnimancerEvent.Sequence.Serializable();
        CollectionAssert.AreEqual(new[] { "Animation", "Cast Point", "Hitbox" }, TimelineRows(window, new SkillVfxTimelineSource(skill)));
        skill.payload = null;
        CollectionAssert.AreEqual(new[] { "Animation", "Cast Point" }, TimelineRows(window, new SkillVfxTimelineSource(skill)));
        var menu = new GenericMenu();
        typeof(SkillAnimationVfxEditorWindow).GetMethod("AddTimelineBackgroundContextItems", Hidden)
            .Invoke(window, new object[] { menu, new SkillVfxTimelineSource(skill), .2f, false, 0f });
        Assert.That(menu.GetItemCount(), Is.GreaterThan(0), "Hidden rows must remain authorable from the background.");
    }

    [Test] public void TimelineBlockRowFollowsDraftAndRectorComboKeepsRequiredRows()
    {
        var skill = Skill();
        var draft = Track(DefensiveBlockTimelineSession.Create(skill));
        var window = Track(ScriptableObject.CreateInstance<SkillAnimationVfxEditorWindow>());
        var field = typeof(SkillAnimationVfxEditorWindow).GetField("blockSession", Hidden);
        field.SetValue(window, draft);
        try
        {
            Assert.That(TimelineRows(window, new SkillVfxTimelineSource(skill)).Any(r => r.StartsWith("Block Window")), Is.False);
            draft.AddBlock();
            CollectionAssert.Contains(TimelineRows(window, new SkillVfxTimelineSource(skill)), "Block Window *");
            draft.Reload();
            Assert.That(TimelineRows(window, new SkillVfxTimelineSource(skill)).Any(r => r.StartsWith("Block Window")), Is.False);
        }
        finally { field.SetValue(window, null); }
        var combo = AssetDatabase.LoadAssetAtPath<MeleeComboSO>("Assets/Character/Rector/Rector Melee Heavy.asset");
        string before = EditorJsonUtility.ToJson(combo);
        var rows = TimelineRows(window, new MeleeComboVfxTimelineSource(combo, combo.Steps[0].EntryId));
        CollectionAssert.AreEqual(new[] { "Animation", "Hitbox", "Chain Window", "VFX" }, rows);
        Assert.That(EditorJsonUtility.ToJson(combo), Is.EqualTo(before));
    }

    [Test] public void DraftEditsNeverMutateSourceBeforeSave()
    {
        var skill = Skill(); string before = JsonUtility.ToJson(skill.payload), clip = JsonUtility.ToJson(skill.skillClip);
        var session = Session(skill);
        session.Record("Edit shape"); session.payloads[0].hitboxLayout.Groups[0].Shapes[0].Radius = 5;
        session.MoveMarker(session.markers[0].id, .08f);
        Assert.That(session.IsDirty, Is.True);
        Assert.That(JsonUtility.ToJson(skill.payload), Is.EqualTo(before)); Assert.That(JsonUtility.ToJson(skill.skillClip), Is.EqualTo(clip));
        session.Reload(); Assert.That(session.IsDirty, Is.False);
    }

    [Test] public void RenameGroupUpdatesAllStepReferencesAndUndoRedo()
    {
        var session = Session(Skill());
        Undo.IncrementCurrentGroup();
        session.RenameGroup(session.payloads[0], 0, "Blade");
        Undo.FlushUndoRecordObjects();
        Assert.That(session.payloads[0].steps.All(s => s.GroupKeys[0] == "Blade"), Is.True);
        Undo.PerformUndo(); Assert.That(session.payloads[0].hitboxLayout.Groups[0].GroupKey, Is.EqualTo("Strike"));
        Assert.That(session.payloads[0].steps[0].GroupKeys[0], Is.EqualTo("Strike"));
        Undo.PerformRedo(); Assert.That(session.payloads[0].steps[1].GroupKeys[0], Is.EqualTo("Blade"));
    }

    [Test] public void MovingWindowsReordersTheirSettingsAcrossAllSharedPayloads()
    {
        var session = Session(Skill(true)); var first = session.Windows(session.payloads[0])[0];
        session.MoveMarker(first.Start.id, .7f); session.MoveMarker(first.End.id, .8f);
        Assert.That(session.Validate(), Is.Empty);
        foreach (var draft in session.payloads)
        {
            Assert.That(draft.steps[0].DamageMultiplier, Is.EqualTo(20)); Assert.That(draft.steps[1].DamageMultiplier, Is.EqualTo(10));
            Assert.That(draft.steps[1].KnockbackDistance, Is.EqualTo(2)); Assert.That(draft.startIds[1], Is.EqualTo(first.Start.id));
        }
    }

    [Test] public void SharedWindowCreationRequiresGroupsForEveryAffectedPayload()
    {
        var session = Session(Skill(true)); session.AddWindow(session.payloads[0], .75f, .85f);
        Assert.That(session.payloads.All(p => p.steps.Count == 3 && p.steps[2].GroupKeys.Count == 0), Is.True);
        Assert.That(session.Validate().Any(s => s.Contains("no group keys")), Is.True);
        Assert.That(session.markers.Count, Is.EqualTo(6), "Shared event names must not produce duplicate markers.");
        session.RemoveWindow(session.Windows(session.payloads[1])[2]);
        Assert.That(session.payloads.All(p => p.steps.Count == 2), Is.True); Assert.That(session.Validate(), Is.Empty);
    }

    [Test] public void WholeWindowDragPreservesDurationSharedSettingsAndUndo()
    {
        var session = Session(Skill(true));
        var first = session.Windows(session.payloads[0])[0];
        float duration = first.End.time - first.Start.time;
        string before = JsonUtility.ToJson(session);
        Undo.IncrementCurrentGroup();
        session.MoveWindow(first.Start.id, first.End.id, .75f, duration);
        Assert.That(first.End.time - first.Start.time, Is.EqualTo(duration).Within(.00001f));
        foreach (var draft in session.payloads)
        {
            Assert.That(draft.startIds[1], Is.EqualTo(first.Start.id));
            Assert.That(draft.steps[1].DamageMultiplier, Is.EqualTo(10));
            Assert.That(draft.steps[1].KnockbackDistance, Is.EqualTo(2));
        }
        Assert.That(session.Validate(), Is.Empty);
        Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
        Assert.That(JsonUtility.ToJson(session), Is.EqualTo(before));
        Undo.PerformRedo();
        Assert.That(session.markers.Find(m => m.id == first.Start.id).time, Is.EqualTo(.75f));
    }

    [Test] public void WholeWindowDragClampsAtClipEndsAndTimelineCoordinatesSupportContinuousScrubbing()
    {
        var session = Session(Skill()); var first = session.Windows(session.payloads[0])[0];
        session.MoveWindow(first.Start.id, first.End.id, 2, .1f);
        Assert.That(first.Start.time, Is.EqualTo(.9f).Within(.00001f));
        Assert.That(first.End.time, Is.EqualTo(1));
        session.MoveWindow(first.Start.id, first.End.id, -1, .1f);
        Assert.That(first.Start.time, Is.Zero); Assert.That(first.End.time, Is.EqualTo(.1f));
        var area = new Rect(20, 30, 912, 200);
        foreach (float time in new[] { 0f, .1f, .8f, .2f, 1f })
            Assert.That(SkillHitboxTimelineAdapter.TimelineTime(area, 120 + time * 800), Is.EqualTo(time).Within(.00001f));
        Assert.That(SkillHitboxTimelineAdapter.TimelineTime(area, -500), Is.Zero);
        Assert.That(SkillHitboxTimelineAdapter.TimelineTime(area, 9999), Is.EqualTo(1));
    }

    [Test] public void DuplicateSharedHitCopiesEachPayloadSettingsAndSupportsUndoRedo()
    {
        var skill = Skill(true); var session = Session(skill);
        session.RenameGroup(session.payloads[1], 0, "OtherBlade");
        var so = new SerializedObject(session);
        var secondStep = so.FindProperty("payloads").GetArrayElementAtIndex(1).FindPropertyRelative("steps").GetArrayElementAtIndex(0);
        secondStep.FindPropertyRelative("damageMultiplier").floatValue = 3.5f;
        secondStep.FindPropertyRelative("overrideKnockback").boolValue = true;
        secondStep.FindPropertyRelative("knockbackProgressCurve").animationCurveValue = AnimationCurve.Linear(0, 0, 1, 1);
        so.ApplyModifiedPropertiesWithoutUndo();
        string sourceBefore = JsonUtility.ToJson(skill.payload), clipBefore = JsonUtility.ToJson(skill.skillClip);
        string before = JsonUtility.ToJson(session);
        var expected = session.payloads.Select(p => JsonUtility.ToJson(p.steps[0])).ToArray();
        var original = session.Windows(session.payloads[0])[0];
        Undo.IncrementCurrentGroup();
        string copyId = session.DuplicateWindow(original);
        Assert.That(copyId, Is.Not.Null.And.Not.EqualTo(original.Start.id));
        Assert.That(session.Validate(), Is.Empty);
        for (int i = 0; i < session.payloads.Count; i++)
        {
            var draft = session.payloads[i]; int index = draft.startIds.IndexOf(copyId);
            Assert.That(JsonUtility.ToJson(draft.steps[index]), Is.EqualTo(expected[i]));
            Assert.That(draft.steps[index], Is.Not.SameAs(draft.steps[0]));
        }
        Assert.That(JsonUtility.ToJson(skill.payload), Is.EqualTo(sourceBefore));
        Assert.That(JsonUtility.ToJson(skill.skillClip), Is.EqualTo(clipBefore));
        string after = JsonUtility.ToJson(session);
        Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
        Assert.That(JsonUtility.ToJson(session), Is.EqualTo(before));
        Undo.PerformRedo(); Assert.That(JsonUtility.ToJson(session), Is.EqualTo(after));
        var copy = session.Windows(session.payloads[0]).Find(w => w.Start.id == copyId);
        session.MoveWindow(copy.Start.id, copy.End.id, .75f, copy.End.time - copy.Start.time);
        Assert.That(session.payloads[1].steps[2].DamageMultiplier, Is.EqualTo(3.5f));
        session.RemoveWindow(session.Windows(session.payloads[0]).Find(w => w.Start.id == copyId));
        Assert.That(session.payloads.All(p => p.steps.Count == 2), Is.True);
        Assert.That(session.Validate(), Is.Empty);
    }

    [Test] public void DuplicateHitRefusesNoSpaceOrIncompleteTimingWithoutChangingDraft()
    {
        var session = Session(Skill());
        var windows = session.Windows(session.payloads[0]);
        session.RemoveWindow(windows[1]);
        session.MoveWindow(windows[0].Start.id, windows[0].End.id, 0, 1);
        string before = JsonUtility.ToJson(session);
        Assert.That(session.DuplicateWindow(session.Windows(session.payloads[0])[0]), Is.Null);
        Assert.That(JsonUtility.ToJson(session), Is.EqualTo(before));
        session.Reload();
        session.AddWindow(session.payloads[0], .12f, .18f);
        before = JsonUtility.ToJson(session);
        Assert.That(session.DuplicateWindow(session.Windows(session.payloads[0])[0]), Is.Null);
        Assert.That(JsonUtility.ToJson(session), Is.EqualTo(before));
    }

    [Test] public void TimelineAddAssignsOnlySelectedPayloadAndUndoRemovesEntireAddition()
    {
        var session = Session(Skill(true)); var panel = new SkillHitboxTimelineAdapter();
        string before = JsonUtility.ToJson(session);
        panel.AddHit(session, session.payloads[0], .75f);
        Assert.That(session.payloads[0].steps[2].GroupKeys, Is.EqualTo(new[] { "Strike" }));
        Assert.That(session.payloads[1].steps[2].GroupKeys, Is.Empty);
        Assert.That(session.Validate().Any(s => s.Contains("no group keys")), Is.True);
        Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
        Assert.That(JsonUtility.ToJson(session), Is.EqualTo(before));
        Undo.PerformRedo(); Assert.That(session.payloads[0].steps[2].GroupKeys, Is.EqualTo(new[] { "Strike" }));
    }

    [Test] public void TimelineDeleteRequiresFocusAndDoesNotRunWhileEditingText()
    {
        var session = Session(Skill(true)); var panel = new SkillHitboxTimelineAdapter();
        panel.AddHit(session, session.payloads[0], .75f);
        var key = new Event { type = EventType.KeyDown, keyCode = KeyCode.Delete };
        Assert.That(panel.HandleHitDelete(session, key, false, false), Is.False);
        Assert.That(panel.HandleHitDelete(session, key, true, true), Is.False);
        Assert.That(session.payloads[0].steps.Count, Is.EqualTo(3));
        var validate = new Event { type = EventType.ValidateCommand, commandName = "Delete" };
        Assert.That(panel.HandleHitDelete(session, validate, true, false), Is.True);
        Assert.That(session.payloads[0].steps.Count, Is.EqualTo(3));
        Assert.That(panel.HandleHitDelete(session, key, true, false), Is.True);
        Assert.That(session.payloads.All(p => p.steps.Count == 2), Is.True);
        Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
        Assert.That(session.payloads.All(p => p.steps.Count == 3), Is.True);
    }

    [Test] public void ScrubbingIsStatelessAndMatchesStartInclusiveEndExclusive()
    {
        var session = Session(Skill()); var draft = session.payloads[0];
        foreach (float t in new[] { .15f, .55f, .1f, .5f, .15f }) Assert.That(session.IsGroupActive(draft, "Strike", t), Is.True);
        foreach (float t in new[] { .2f, .6f, .4f, 0f, .99f }) Assert.That(session.IsGroupActive(draft, "Strike", t), Is.False);
    }

    [Test] public void InvalidWindowsShapesAndMissingAnchorsBlockValidation()
    {
        var session = Session(Skill()); var pair = session.Windows(session.payloads[0])[0];
        session.MoveMarker(pair.End.id, .05f);
        session.payloads[0].hitboxLayout.Groups[0].Shapes[0].Size = Vector3.zero;
        var issues = session.Validate((p, g) => false);
        Assert.That(issues.Any(s => s.Contains("HitEnd without")), Is.True);
        Assert.That(issues.Any(s => s.Contains("positive size")), Is.True);
        Assert.That(issues.Any(s => s.Contains("missing")), Is.True);
    }

    [Test] public void ExternalPayloadAndTimelineChangesAreDetected()
    {
        var skill = Skill(); var session = Session(skill);
        ((PrefabHitboxSkillPayloadDef)skill.payload).HitboxLayout.Groups[0].Shapes[0].Radius += 1;
        Assert.That(session.HasConflict(), Is.True); session.Reload(); Assert.That(session.HasConflict(), Is.False);
        AddEvent(skill, .9f, CombatTimelineEventName.Vfx); Assert.That(session.HasConflict(), Is.True);
    }

    [Test] public void SavePersistsOnlyOwnedDataAndKeepsOtherTimelineEvents()
    {
        var skill = Skill(true);
        string path = "Assets/__HitboxAuthoringTest_" + Guid.NewGuid().ToString("N") + ".asset"; assets.Add(path);
        AssetDatabase.CreateAsset(skill, path);
        AssetDatabase.AddObjectToAsset(skill.payload, skill);
        foreach (var payload in SkillHitboxAuthoringSession.FindPayloads(skill.payload)) AssetDatabase.AddObjectToAsset(payload, skill);
        AssetDatabase.AddObjectToAsset(skill.skillClip.Clip, skill);
        EditorUtility.SetDirty(skill); AssetDatabase.SaveAssetIfDirty(skill);
        var session = Session(skill);
        var pair = session.Windows(session.payloads[0])[0]; session.MoveMarker(pair.Start.id, .08f);
        session.RenameGroup(session.payloads[1], 0, "SecondBlade");
        Assert.That(session.DuplicateWindow(pair), Is.Not.Null);
        Assert.That(session.Save(out string error), Is.True, error);
        Assert.That(session.IsDirty, Is.False);
        Assert.That(SkillHitboxAuthoringSession.FindPayloads(skill.payload)[1].Steps[0].GroupKeys[0], Is.EqualTo("SecondBlade"));
        Assert.That(session.payloads.All(p => p.steps.Count == 3), Is.True);
        Assert.That(session.payloads[1].steps[1].DamageMultiplier, Is.EqualTo(10));
        Assert.That(session.payloads[1].steps[1].KnockbackDistance, Is.EqualTo(2));
        var events = skill.skillClip.SerializedEvents;
        int vfx = Array.FindIndex(events.Names, a => a != null && a.name == "Vfx");
        Assert.That(events.NormalizedTimes[vfx], Is.EqualTo(.35f));
        Assert.That(File.ReadAllText(path), Does.Contain("SecondBlade"));
    }

    [Test] public void ModelBoneAnchorAndShapeBoundsDoNotCreatePhysicsObjects()
    {
        var root = Track(new GameObject("Hitbox preview actor")); var context = root.AddComponent<IdentityProbeContext>();
        var placeholder = root.AddComponent<Animator>();
        var visualRoot = new GameObject("Visual"); visualRoot.transform.SetParent(root.transform, false);
        var model = new GameObject("Model"); model.transform.SetParent(visualRoot.transform, false); model.AddComponent<Animator>();
        var hand = new GameObject("Hand"); hand.transform.SetParent(model.transform, false); hand.transform.localScale = new Vector3(2, 3, 4);
        context.Visual = root.AddComponent<CharacterVisualController>(); context.Visual.animator = placeholder;
        typeof(CharacterVisualController).GetField("modelRoot", Hidden).SetValue(context.Visual, visualRoot.transform);
        var target = root.AddComponent<SetAnimationVfxData>();
        var session = Session(Skill()); var draft = session.payloads[0]; var group = draft.hitboxLayout.Groups[0];
        group.Anchor = SkillHitboxLayoutData.AnchorSpace.AnimatorRoot; group.AnchorPath = "Hand";
        Assert.That(SkillHitboxSceneHandles.TryBasis(target, draft, group, out var basis), Is.True);
        var shape = group.Shapes[0]; shape.LocalPosition = Vector3.zero; shape.Size = Vector3.one;
        Assert.That(SkillHitboxSceneHandles.BoundsFor(basis, shape).size, Is.EqualTo(new Vector3(2, 3, 4)));
        Assert.That(root.GetComponentsInChildren<Collider>().Length, Is.Zero);
        Assert.That(root.GetComponentsInChildren<SkillHitboxSequenceRuntime>().Length, Is.Zero);
    }

    [Test] public void ShapeAndAnchorEditsUndoRedoThenRevert()
    {
        var actor = Track(new GameObject("Authoring anchor test"));
        var target = actor.AddComponent<SetAnimationVfxData>();
        var bone = new GameObject("Bone"); bone.transform.SetParent(actor.transform, false);
        bone.transform.localPosition = new Vector3(0, 2, 1);
        var session = Session(Skill()); var draft = session.payloads[0]; var group = draft.hitboxLayout.Groups[0];
        SkillHitboxSceneHandles.TryBasis(target, draft, group, out var before);
        var originalBounds = SkillHitboxSceneHandles.BoundsFor(before, group.Shapes[0]);
        Undo.IncrementCurrentGroup();
        SkillHitboxTimelineAdapter.ChangeAnchor(session, target, draft, group, SkillHitboxLayoutData.AnchorSpace.CasterRoot, "Bone");
        SkillHitboxSceneHandles.TryBasis(target, draft, group, out var after);
        Assert.That(Vector3.Distance(originalBounds.center, SkillHitboxSceneHandles.BoundsFor(after, group.Shapes[0]).center), Is.LessThan(.0001));
        Undo.PerformUndo(); Assert.That(session.payloads[0].hitboxLayout.Groups[0].AnchorPath, Is.Empty);
        Undo.PerformRedo(); Assert.That(session.payloads[0].hitboxLayout.Groups[0].AnchorPath, Is.EqualTo("Bone"));
        Undo.IncrementCurrentGroup(); session.Record("Drag shape");
        session.payloads[0].hitboxLayout.Groups[0].Shapes[0].LocalPosition = Vector3.one * 9;
        Undo.PerformUndo(); Assert.That(session.payloads[0].hitboxLayout.Groups[0].Shapes[0].LocalPosition, Is.Not.EqualTo(Vector3.one * 9));
        Undo.PerformRedo(); Assert.That(session.payloads[0].hitboxLayout.Groups[0].Shapes[0].LocalPosition, Is.EqualTo(Vector3.one * 9));
        session.Reload(); Assert.That(session.IsDirty, Is.False);
    }

    [Test] public void BoneDropAcceptsHierarchyObjectsAndRejectsForeignOrAmbiguousPaths()
    {
        var root = Track(new GameObject("Drop root"));
        var bone = new GameObject("Hand"); bone.transform.SetParent(root.transform, false);
        var foreign = Track(new GameObject("Other actor"));
        Assert.That(SkillHitboxTimelineAdapter.TryBoneDropPath(root.transform, new Object[] { bone }, out var path), Is.True);
        Assert.That(path, Is.EqualTo("Hand"));
        Assert.That(SkillHitboxTimelineAdapter.TryBoneDropPath(root.transform, new Object[] { bone.transform }, out path), Is.True);
        Assert.That(SkillHitboxTimelineAdapter.TryBoneDropPath(root.transform, new Object[] { root }, out path), Is.True);
        Assert.That(path, Is.Empty);
        Assert.That(SkillHitboxTimelineAdapter.TryBoneDropPath(root.transform, new Object[] { foreign }, out _), Is.False);
        Assert.That(SkillHitboxTimelineAdapter.TryBoneDropPath(root.transform, new Object[] { bone, root }, out _), Is.False);
        var duplicate = new GameObject("Hand"); duplicate.transform.SetParent(root.transform, false);
        Assert.That(SkillHitboxTimelineAdapter.TryBoneDropPath(root.transform, new Object[] { duplicate }, out _), Is.False);
    }

    [Test] public void BoneDropPreservesWorldPoseAndSupportsUndoRedoWithoutChangingSource()
    {
        var actor = Track(new GameObject("Bone drop actor"));
        var target = actor.AddComponent<SetAnimationVfxData>();
        var bone = new GameObject("Hand"); bone.transform.SetParent(actor.transform, false);
        bone.transform.localPosition = new Vector3(1, 2, 3);
        bone.transform.localRotation = Quaternion.Euler(0, 40, 0);
        var session = Session(Skill()); var draft = session.payloads[0]; var group = draft.hitboxLayout.Groups[0];
        group.Anchor = SkillHitboxLayoutData.AnchorSpace.CasterRoot; group.AnchorPath = "";
        string source = JsonUtility.ToJson(draft.source), before = JsonUtility.ToJson(session);
        SkillHitboxSceneHandles.TryBasis(target, draft, group, out var basis);
        var world = SkillHitboxSceneHandles.BoundsFor(basis, group.Shapes[0]);
        Assert.That(SkillHitboxTimelineAdapter.AssignDroppedBone(session, target, draft, group, new Object[] { bone }), Is.True);
        Assert.That(group.AnchorPath, Is.EqualTo("Hand"));
        SkillHitboxSceneHandles.TryBasis(target, draft, group, out basis);
        Assert.That(Vector3.Distance(world.center, SkillHitboxSceneHandles.BoundsFor(basis, group.Shapes[0]).center), Is.LessThan(.0001f));
        Assert.That(JsonUtility.ToJson(draft.source), Is.EqualTo(source));
        Undo.PerformUndo(); Assert.That(JsonUtility.ToJson(session), Is.EqualTo(before));
        Undo.PerformRedo(); Assert.That(session.payloads[0].hitboxLayout.Groups[0].AnchorPath, Is.EqualTo("Hand"));
        session.Reload(); Assert.That(session.IsDirty, Is.False);
    }

    [Test] public void BonePickerCollectsSkeletonAncestorsAndSelectedSocketsWithoutForeignBones()
    {
        var root = Track(new GameObject("Picker actor"));
        var joint = new GameObject("Hand"); joint.transform.SetParent(root.transform, false);
        var socket = new GameObject("Attachment socket"); socket.transform.SetParent(joint.transform, false);
        var skin = new GameObject("Mesh"); skin.transform.SetParent(root.transform, false);
        var foreign = Track(new GameObject("Foreign bone"));
        skin.AddComponent<SkinnedMeshRenderer>().bones = new[] { joint.transform, foreign.transform };
        var bones = SkillHitboxBonePickerWindow.CollectBones(root.transform, false, null);
        Assert.That(bones.Contains(root.transform) && bones.Contains(joint.transform), Is.True);
        Assert.That(bones.Contains(socket.transform) || bones.Contains(foreign.transform), Is.False);
        CollectionAssert.Contains(SkillHitboxBonePickerWindow.CollectBones(root.transform, false, socket.transform), socket.transform);
        var all = SkillHitboxBonePickerWindow.CollectBones(root.transform, true, null);
        Assert.That(all.Contains(skin.transform) && all.Contains(socket.transform), Is.True);
        root.transform.position = new Vector3(5, 6, 7); root.transform.rotation = Quaternion.Euler(0, 35, 0);
        joint.transform.localPosition = new Vector3(1, 2, 0);
        Assert.That(Vector3.Distance(SkillHitboxBonePickerWindow.ProjectBone(root.transform, joint.transform, 0, 0), joint.transform.localPosition), Is.LessThan(.0001f));
    }

    [Test] public void BonePickerAppliesOnlyOnConfirmationAndRejectsStaleDraft()
    {
        var root = Track(new GameObject("Picker actor"));
        var target = root.AddComponent<SetAnimationVfxData>();
        var bone = new GameObject("Hand"); bone.transform.SetParent(root.transform, false);
        var session = Session(Skill()); var draft = session.payloads[0]; var group = draft.hitboxLayout.Groups[0];
        group.Anchor = SkillHitboxLayoutData.AnchorSpace.CasterRoot; group.AnchorPath = "";
        string before = JsonUtility.ToJson(session), source = JsonUtility.ToJson(draft.source);
        var picker = SkillHitboxBonePickerWindow.Open(session, target, draft, group, null);
        try
        {
            typeof(SkillHitboxBonePickerWindow).GetField("selected", Hidden).SetValue(picker, bone.transform);
            Assert.That(JsonUtility.ToJson(session), Is.EqualTo(before));
            Assert.That(picker.ApplySelection(), Is.True);
            Assert.That(group.AnchorPath, Is.EqualTo("Hand"));
            Assert.That(JsonUtility.ToJson(draft.source), Is.EqualTo(source));
            Undo.PerformUndo(); Assert.That(JsonUtility.ToJson(session), Is.EqualTo(before));
            session.Reload();
            Assert.That(picker.ApplySelection(), Is.False, "Reload replaces the group; the old picker must not edit stale data.");
        }
        finally { if (picker != null) picker.Close(); }
        draft = session.payloads[0]; group = draft.hitboxLayout.Groups[0];
        before = JsonUtility.ToJson(session);
        picker = SkillHitboxBonePickerWindow.Open(session, target, draft, group, null);
        typeof(SkillHitboxBonePickerWindow).GetField("selected", Hidden).SetValue(picker, bone.transform);
        picker.Close(); Assert.That(JsonUtility.ToJson(session), Is.EqualTo(before), "Cancel must not change the draft.");
    }

    [Test] public void BonePickerDiagramHidesFingersSeparatesWeaponsAndNeverChangesTransforms()
    {
        var actor = Track(new GameObject("Diagram actor"));
        foreach (string name in new[] { "root.x", "spine_01.x", "spine_02.x", "neck.x", "head.x", "hand.l", "thigh_twist.l", "leg_twist.l", "Weapon.L", "index1.l" })
        {
            var bone = new GameObject(name); bone.transform.SetParent(actor.transform, false);
            bone.transform.localPosition = Vector3.one * 30;
        }
        var finger = actor.transform.Find("index1.l");
        var tip = new GameObject("Tip"); tip.transform.SetParent(finger, false);
        var weapon = actor.transform.Find("Weapon.L");
        var before = actor.GetComponentsInChildren<Transform>().Select(t => t.localPosition).ToArray();
        var bones = SkillHitboxBonePickerWindow.CollectBones(actor.transform, true, null);
        Assert.That(bones.Contains(finger) || bones.Contains(tip.transform), Is.False);
        CollectionAssert.Contains(bones, weapon);
        var positions = SkillHitboxBonePickerLayout.BodyPositions(actor.transform, bones, false, 0, 0);
        Assert.That(positions.ContainsKey(weapon), Is.False);
        Assert.That(positions[actor.transform.Find("head.x")].y, Is.GreaterThan(1f));
        Assert.That(positions[actor.transform.Find("leg_twist.l")].y, Is.LessThan(-1f));
        CollectionAssert.AreEqual(before, actor.GetComponentsInChildren<Transform>().Select(t => t.localPosition).ToArray());
        Assert.That(SkillHitboxBonePickerLayout.Side(weapon), Is.EqualTo(-1));
    }

    [Test] public void BonePickerOverlapHitTestingKeepsCoincidentJointsAndCrossingBranchesSelectable()
    {
        var root = Track(new GameObject("Overlap root"));
        var a = new GameObject("Arm"); a.transform.SetParent(root.transform, false);
        var b = new GameObject("Twist"); b.transform.SetParent(root.transform, false);
        var points = new Dictionary<Transform, Vector2>
        { [root.transform] = Vector2.zero, [a.transform] = new Vector2(100, 0), [b.transform] = new Vector2(100, 0) };
        CollectionAssert.AreEquivalent(new[] { a.transform, b.transform }, SkillHitboxBonePickerLayout.HitCandidates(points, new Vector2(100, 0)));
        CollectionAssert.AreEquivalent(new[] { a.transform, b.transform }, SkillHitboxBonePickerLayout.HitCandidates(points, new Vector2(50, 0)));
        Assert.That(SkillHitboxBonePickerLayout.HitCandidates(points, new Vector2(50, 30)), Is.Empty);
        points[b.transform] = new Vector2(0, 100);
        CollectionAssert.AreEquivalent(new[] { a.transform }, SkillHitboxBonePickerLayout.HitCandidates(points, new Vector2(50, 0)));
        var reversed = points.Reverse().ToDictionary(p => p.Key, p => p.Value);
        CollectionAssert.AreEqual(SkillHitboxBonePickerLayout.HitCandidates(points, Vector2.zero), SkillHitboxBonePickerLayout.HitCandidates(reversed, Vector2.zero));
    }

    [Test] public void BonePickerTwistLanesSeparateBothEndsAndHitTargetsAtEveryZoom()
    {
        var joint = Track(new GameObject("arm_stretch.r"));
        var main = new GameObject("forearm_stretch.r"); main.transform.SetParent(joint.transform, false);
        var twist = new GameObject("arm_twist.r"); twist.transform.SetParent(joint.transform, false);
        var extra = new GameObject("arm_twist_offset.r"); extra.transform.SetParent(joint.transform, false);
        foreach (float zoom in new[] { .25f, 1f, 4f, 12f })
        {
            var original = new Dictionary<Transform, Vector2> { [joint.transform] = Vector2.zero,
                [main.transform] = new Vector2(100, 100) * zoom,
                [twist.transform] = new Vector2(50, 50) * zoom,
                [extra.transform] = new Vector2(50, 50) * zoom };
            var points = SkillHitboxBonePickerLayout.SeparateTwistLanes(original, out var starts);
            Assert.That(points[main.transform], Is.EqualTo(original[main.transform]));
            Assert.That(Vector2.Distance(starts[twist.transform], starts[main.transform]), Is.EqualTo(30).Within(.001f));
            Assert.That(Vector2.Distance(starts[extra.transform], starts[twist.transform]), Is.EqualTo(30).Within(.001f));
            CollectionAssert.AreEquivalent(new[] { main.transform }, SkillHitboxBonePickerLayout.HitCandidates(points, points[main.transform] * .5f, starts));
            foreach (var bone in new[] { twist.transform, extra.transform })
                CollectionAssert.AreEquivalent(new[] { bone }, SkillHitboxBonePickerLayout.HitCandidates(points, (points[bone] + starts[bone]) * .5f, starts));
        }
        Assert.That(twist.transform.localPosition, Is.EqualTo(Vector3.zero));
    }

    [Test] public void BonePickerWeaponAnchorsFollowTheSamePanZoomAndOrbitAsBody()
    {
        var actor = Track(new GameObject("Weapon diagram"));
        foreach (string name in new[] { "root.x", "spine_01.x", "neck.x", "head.x", "hand.l", "hand.r", "foot.l", "foot.r", "Weapon.L", "Weapon.R" })
        { var bone = new GameObject(name); bone.transform.SetParent(actor.transform, false); }
        var bones = actor.GetComponentsInChildren<Transform>();
        var left = actor.transform.Find("Weapon.L"); var right = actor.transform.Find("Weapon.R");
        var body = SkillHitboxBonePickerLayout.BodyPositions(actor.transform, bones, false, 0, 0);
        var weapons = SkillHitboxBonePickerLayout.WeaponPositions(actor.transform, bones, false, 0, 0);
        Assert.That(weapons[left].x, Is.LessThan(body.Values.Min(p => p.x)));
        Assert.That(weapons[right].x, Is.GreaterThan(body.Values.Max(p => p.x)));
        var rotated = SkillHitboxBonePickerLayout.WeaponPositions(actor.transform, bones, false, 35, 20);
        Assert.That(Vector3.Distance(rotated[left], Quaternion.Euler(20, 35, 0) * weapons[left]), Is.LessThan(.0001f));
        var center = new Vector2(.1f, -.2f); var size = new Vector2(730, 656); var pan = new Vector2(80, -45);
        var hand = body[actor.transform.Find("hand.l")];
        Vector2 Project(Vector3 p, float scale, Vector2 offset) => SkillHitboxBonePickerLayout.ToCanvas(p, center, scale, size, offset);
        foreach (float scale in new[] { 30f, 100f, 400f })
        {
            Assert.That(Vector2.Distance(Project(weapons[left], scale, pan) - Project(weapons[left], scale, Vector2.zero), pan), Is.LessThan(.001f));
            Vector2 relative = Project(weapons[left], scale, pan) - Project(hand, scale, pan);
            Vector2 baseline = Project(weapons[left], 1, Vector2.zero) - Project(hand, 1, Vector2.zero);
            Assert.That(Vector2.Distance(relative, baseline * scale), Is.LessThan(.02f));
        }
        Assert.That(left.localPosition, Is.EqualTo(Vector3.zero));
    }

    [Test] public void AddRemoveAndMoveWindowsUndoRedoPreservesSettings()
    {
        var session = Session(Skill(true));
        Undo.IncrementCurrentGroup(); session.AddWindow(session.payloads[0], .8f, 1f);
        Undo.PerformUndo(); Assert.That(session.payloads.All(p => p.steps.Count == 2), Is.True);
        Undo.PerformRedo(); Assert.That(session.payloads.All(p => p.steps.Count == 3), Is.True);
        Undo.IncrementCurrentGroup(); session.RemoveWindow(session.Windows(session.payloads[0])[2]);
        Undo.PerformUndo(); Assert.That(session.payloads.All(p => p.steps.Count == 3), Is.True);
        Undo.PerformRedo(); Assert.That(session.Validate(), Is.Empty);
        var pair = session.Windows(session.payloads[0])[0];
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
        session.MoveMarker(pair.Start.id, .7f); session.MoveMarker(pair.End.id, .8f); Undo.CollapseUndoOperations(undo);
        Undo.PerformUndo(); Assert.That(session.payloads[0].steps[0].DamageMultiplier, Is.EqualTo(10));
        Undo.PerformRedo(); Assert.That(session.payloads[0].steps[0].DamageMultiplier, Is.EqualTo(20));
    }

    [Test] public void SaveRoundTripsAllShapesWithoutSavingAnotherDirtyAsset()
    {
        var unrelated = Track(new AnimationClip()); unrelated.name = "Unrelated";
        string otherPath = "Assets/__HitboxUnrelated_" + Guid.NewGuid().ToString("N") + ".anim"; assets.Add(otherPath);
        AssetDatabase.CreateAsset(unrelated, otherPath); AssetDatabase.SaveAssetIfDirty(unrelated);
        string otherBytes = File.ReadAllText(otherPath); unrelated.name = "Unsaved change"; EditorUtility.SetDirty(unrelated);
        var skill = Skill(); string path = "Assets/__HitboxRoundTrip_" + Guid.NewGuid().ToString("N") + ".asset"; assets.Add(path);
        AssetDatabase.CreateAsset(skill, path); AssetDatabase.AddObjectToAsset(skill.payload, skill); AssetDatabase.AddObjectToAsset(skill.skillClip.Clip, skill);
        EditorUtility.SetDirty(skill); AssetDatabase.SaveAssetIfDirty(skill);
        var session = Session(skill); var group = session.payloads[0].hitboxLayout.Groups[0];
        group.Anchor = SkillHitboxLayoutData.AnchorSpace.AnimatorRoot; group.AnchorPath = "Hand"; group.Shapes.Clear();
        foreach (SkillHitboxLayoutData.HitBoxType type in Enum.GetValues(typeof(SkillHitboxLayoutData.HitBoxType)))
            group.Shapes.Add(new SkillHitboxLayoutData.HitBoxShapeData { Type = type, LocalScale = new Vector3(2, 3, 4),
                LocalPosition = new Vector3(1, 2, 3), LocalEulerAngles = new Vector3(20, 30, 40), Center = new Vector3(.1f, .2f, .3f), Size = new Vector3(4, 5, 6), Radius = .7f, Height = 2.3f, Direction = 2 });
        string expected = JsonUtility.ToJson(session.payloads[0].hitboxLayout);
        Assert.That(session.Save(out var error), Is.True, error);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        session.Reload(); Assert.That(JsonUtility.ToJson(session.payloads[0].hitboxLayout), Is.EqualTo(expected));
        Assert.That(File.ReadAllText(otherPath), Is.EqualTo(otherBytes));
        Assert.That(EditorUtility.IsDirty(unrelated), Is.True);
    }

    [Test] public void ExistingWindowPreviewRestoresPoseAndNeverCreatesAttackRuntime()
    {
        Assert.That(AnimationMode.InAnimationMode(), Is.False, "Stop an existing animation preview before this check.");
        var root = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/GameEnemy/Enemy_B_GR_01 Variant.prefab")));
        var context = root.GetComponentInChildren<CharacteContext>(true); context.ResolveReferences();
        var target = root.AddComponent<SetAnimationVfxData>(); var skill = context.baseStats.animProfile.lightCombo.Steps[0].executionSkill;
        target.SetTimelineSource(skill, "main");
        var transforms = root.GetComponentsInChildren<Transform>(true);
        var poses = transforms.Select(t => (t.localPosition, t.localRotation, t.localScale)).ToArray();
        int colliders = root.GetComponentsInChildren<Collider>(true).Length;
        var materials = root.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
        var materialJson = materials.Select(m => EditorJsonUtility.ToJson(m)).ToArray();
        var window = Track(ScriptableObject.CreateInstance<SkillAnimationVfxEditorWindow>());
        var type = typeof(SkillAnimationVfxEditorWindow);
        type.GetField("authoringTarget", Hidden).SetValue(window, target);
        type.GetField("hitboxMode", Hidden).SetValue(window, true);
        try
        {
            foreach (float time in new[] { .35f, .8f, .1f, .35f, 0f })
            {
                type.GetField("normalizedTime", Hidden).SetValue(window, time);
                type.GetMethod("SampleMainAtPlayhead", Hidden).Invoke(window, null);
            }
            Assert.That(root.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(colliders));
            Assert.That(root.GetComponentsInChildren<SkillHitboxSequenceRuntime>(true), Is.Empty);
        }
        finally { type.GetMethod("StopPreview", Hidden).Invoke(window, new object[] { true }); }
        for (int i = 0; i < transforms.Length; i++)
        {
            Assert.That(Vector3.Distance(transforms[i].localPosition, poses[i].localPosition), Is.LessThan(.0001f), transforms[i].name);
            Assert.That(Quaternion.Angle(transforms[i].localRotation, poses[i].localRotation), Is.LessThan(.05f), transforms[i].name);
            Assert.That(Vector3.Distance(transforms[i].localScale, poses[i].localScale), Is.LessThan(.0001f), transforms[i].name);
        }
        for (int i = 0; i < materials.Length; i++) Assert.That(EditorJsonUtility.ToJson(materials[i]), Is.EqualTo(materialJson[i]));
        Assert.That(AnimationMode.InAnimationMode(), Is.False);
    }

    [Test] public void RealCharacterLayoutsResolveAndValidate()
    {
        foreach (string path in new[] { "Assets/Prefab/GameEnemy/Enemy_B_GR_01 Variant.prefab", "Assets/Prefab/GameEnemy/Enemy_E_GR_01 Variant.prefab", "Assets/Prefab/GameEnemy/Enemy_Base.prefab" })
        {
            var root = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path)));
            var context = root.GetComponentInChildren<CharacteContext>(true); context.ResolveReferences();
            var target = root.AddComponent<SetAnimationVfxData>();
            foreach (var combo in new[] { context.baseStats.animProfile.lightCombo, context.baseStats.animProfile.heavyCombo })
                foreach (var step in combo.Steps)
                {
                    var session = Session(step.executionSkill);
                    Assert.That(session.Validate((p, g) => SkillHitboxSceneHandles.TryBasis(target, p, g, out _)), Is.Empty, path);
                }
        }
    }
}
#endif
