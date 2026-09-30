#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Animancer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SkillPlacementTimingTests
{
    readonly List<Object> created = new();

    [TearDown]
    public void Cleanup()
    {
        foreach (Object value in created)
            if (value != null) Object.DestroyImmediate(value);
        created.Clear();
    }

    SkillGemDefinition Skill(bool hitStart = false)
    {
        var skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
        created.Add(skill);
        skill.castPointNormalized = 0.2f;
        skill.placementPointNormalized = 0.6f;
        skill.skillClip = new ClipTransition();
        if (hitStart)
        {
            var marker = StringAsset.Find(CombatTimelineEventNames.ToStringReference(CombatTimelineEventName.HitStart), out _);
            Assert.That(marker, Is.Not.Null, "Project HitStart marker must exist.");
            skill.skillClip.SerializedEvents = new AnimancerEvent.Sequence.Serializable();
            skill.skillClip.SerializedEvents.AddEvent(0.45f, name: marker);
        }
        return skill;
    }

    [Test]
    public void ExistingSkillsPreserveChainAndInterruptionTiming()
    {
        var skill = Skill(hitStart: true);
        Assert.That(skill.GetPlacementPointNormalized(), Is.EqualTo(0.2f));
        Assert.That(skill.GetPlacementPointNormalized(true), Is.EqualTo(0.45f));
        var noMarker = Skill();
        Assert.That(noMarker.GetPlacementPointNormalized(true), Is.EqualTo(0.2f));
    }

    [Test]
    public void CustomPlacementStaysFixedWhenCastPointMoves()
    {
        var skill = Skill(hitStart: true);
        skill.placementTimingSource = SkillPlacementTimingSource.Custom;
        skill.castPointNormalized = 0.1f;
        Assert.That(skill.GetPlacementPointNormalized(), Is.EqualTo(0.6f));
        skill.castPointNormalized = 0.8f;
        Assert.That(skill.GetPlacementPointNormalized(true), Is.EqualTo(0.6f));
        Assert.That(skill.GetCastPointNormalized(), Is.EqualTo(0.8f));
        skill.placementPointNormalized = 0.7f;
        Assert.That(skill.GetCastPointNormalized(), Is.EqualTo(0.8f));
    }

    [Test]
    public void HitStartUsesTheSamePlacementForChainAndInterruption()
    {
        var skill = Skill(hitStart: true);
        skill.placementTimingSource = SkillPlacementTimingSource.HitStart;
        Assert.That(skill.GetPlacementPointNormalized(), Is.EqualTo(0.45f));
        Assert.That(skill.GetPlacementPointNormalized(true), Is.EqualTo(0.45f));
        Assert.That(skill.GetCastPointNormalized(), Is.EqualTo(0.2f));
    }

    [Test]
    public void MissingHitStartUsesIndependentAuthoredFallback()
    {
        var skill = Skill();
        skill.placementTimingSource = SkillPlacementTimingSource.HitStart;
        skill.castPointNormalized = 0.9f;
        Assert.That(skill.GetPlacementPointNormalized(), Is.EqualTo(0.6f));
        Assert.That(skill.GetPlacementPointNormalized(true), Is.EqualTo(0.6f));
    }

    [TestCase(-1f, 0f)]
    [TestCase(2f, 0.999f)]
    [TestCase(float.NaN, 0.35f)]
    [TestCase(float.PositiveInfinity, 0.35f)]
    public void InvalidCustomTimeIsClampedOrDefaulted(float authored, float expected)
    {
        var skill = Skill();
        skill.placementTimingSource = SkillPlacementTimingSource.Custom;
        skill.placementPointNormalized = authored;
        Assert.That(skill.GetPlacementPointNormalized(), Is.EqualTo(expected));
    }

    [Test]
    public void TimelinePlacementBindingUsesItsOwnTimingAndPreservesReleaseAndHitStart()
    {
        var skill = Skill(hitStart: true);
        skill.placementTimingSource = SkillPlacementTimingSource.HitStart;
        var source = new SkillVfxTimelineSource(skill);
        var cast = source.Lanes[0];
        var placement = source.Lanes[1];
        Assert.That(placement.Label, Is.EqualTo("Placement Point"));
        Assert.That(placement.GetPointValue(source), Is.EqualTo(.45f));
        placement.SetPointValue(source, .7f);
        Assert.That(skill.placementTimingSource, Is.EqualTo(SkillPlacementTimingSource.Custom));
        Assert.That(placement.GetPointValue(source), Is.EqualTo(.7f));
        Assert.That(cast.GetPointValue(source), Is.EqualTo(.2f));
        Assert.That(skill.TryGetPlacementHitStart(out float hitStart), Is.True);
        Assert.That(hitStart, Is.EqualTo(.45f));
        cast.SetPointValue(source, .3f);
        Assert.That(cast.GetPointValue(source), Is.EqualTo(.3f));
        Assert.That(placement.GetPointValue(source), Is.EqualTo(.7f));
    }

    [Test]
    public void TimelinePlacementUndoRestoresModeAndTimeTogether()
    {
        var skill = Skill();
        var source = new SkillVfxTimelineSource(skill);
        Undo.IncrementCurrentGroup();
        try
        {
            source.Lanes[1].SetPointValue(source, .8f);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(skill.placementTimingSource, Is.EqualTo(SkillPlacementTimingSource.Legacy));
            Assert.That(skill.placementPointNormalized, Is.EqualTo(.6f));
            Undo.PerformRedo();
            Assert.That(skill.placementTimingSource, Is.EqualTo(SkillPlacementTimingSource.Custom));
            Assert.That(skill.placementPointNormalized, Is.EqualTo(.8f));
            Assert.That(skill.GetCastPointNormalized(), Is.EqualTo(.2f));
        }
        finally { Undo.ClearUndo(skill); }
    }

    [Test]
    public void TimelinePlacementDisplaysLegacyAndMissingHitStartFallback()
    {
        var skill = Skill();
        var source = new SkillVfxTimelineSource(skill);
        var placement = source.Lanes[1];
        Assert.That(placement.GetPointValue(source), Is.EqualTo(.2f));
        StringAssert.Contains("Legacy", placement.GetPointLabel());
        skill.placementTimingSource = SkillPlacementTimingSource.HitStart;
        Assert.That(placement.GetPointValue(source), Is.EqualTo(.6f));
        StringAssert.Contains("Fallback", placement.GetPointLabel());
    }

    [Test]
    public void CombinedTimelinePlacementBindsMainSkillAndIsAbsentFromCutscene()
    {
        var skill = Skill();
        var source = new CombinedCutsceneSkillVfxTimelineSource(skill);
        source.Lanes[1].SetPointValue(source, .75f);
        Assert.That(skill.placementPointNormalized, Is.EqualTo(.75f));
        Assert.That(source.PointValue, Is.EqualTo(.2f));
        source.SetMode(true);
        foreach (var lane in source.Lanes)
            Assert.That(lane.Label, Is.Not.EqualTo("Placement Point"));
        source.SetMode(false);
        Assert.That(source.Lanes[1].GetPointValue(source), Is.EqualTo(.75f));
    }

    [Test]
    public void PlacementDiagnosticUsesObservedMotionAfterCrossingThePlacementBeat()
    {
        // A is at time .5; the next rendered frame is .6. Both translation and yaw after A
        // must be included, even when a Utility lead-in has displaced the actual attack origin.
        var trajectory = new TargetedSkillRootMotionTrajectory(null, new[]
        {
            new TargetedSkillRootMotionSample(0f, Vector3.zero, 0f),
            new TargetedSkillRootMotionSample(0.5f, new Vector3(0f, 0f, 2f), 30f),
            new TargetedSkillRootMotionSample(1f, new Vector3(0f, 0f, 5f), 60f),
        });
        Quaternion attackRotation = Quaternion.Euler(0f, 80f, 0f);
        var placement = TargetedSkillPlacementResult.RootMotion(Vector3.zero, Quaternion.identity,
            new Vector3(10f, 0f, 20f), attackRotation * Quaternion.Euler(0f, 30f, 0f), 0f);
        Type type = typeof(SkillGemDefinition).Assembly.GetType("TargetedSkillPlacementPoseCheck", true);
        MethodInfo evaluate = type.GetMethod("TryGetExpectedPose", BindingFlags.Static | BindingFlags.NonPublic);
        object[] args = { trajectory, placement, 0.5f, 0.6f, null, null };
        Assert.That(evaluate.Invoke(null, args), Is.True);
        Vector3 expected = placement.ImpactPosition + attackRotation * new Vector3(0f, 0f, 0.6f);
        Assert.That(Vector3.Distance((Vector3)args[4], expected), Is.LessThan(0.0001f));
        Assert.That(Quaternion.Angle((Quaternion)args[5], Quaternion.Euler(0f, 116f, 0f)), Is.LessThan(0.01f));
    }
}
#endif
