#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.Reflection;

public sealed class DefensiveBlockTests
{
    [Test]
    public void FastChargeCrossesGuardInOneFrame()
    {
        Assert.IsTrue(DefensiveBlockGeometry.SweepsGuard(new Vector3(0,1,8), new Vector3(0,1,-3),
            1f, Vector3.up, Vector3.forward, 0.5f, 1f));
        // A large charge volume can already straddle the plane when its hit window activates.
        Assert.IsTrue(DefensiveBlockGeometry.SweepsGuard(new Vector3(0,1,-0.2f), new Vector3(0,1,-0.3f),
            2f, Vector3.up, Vector3.forward, 0.5f, 1f));
    }
    [Test]
    public void MissBehindAndMovingAwayDoNotBlock()
    {
        Assert.IsFalse(DefensiveBlockGeometry.SweepsGuard(new Vector3(4,1,8), new Vector3(4,1,-3), 1, Vector3.up, Vector3.forward, .5f, 1));
        Assert.IsFalse(DefensiveBlockGeometry.SweepsGuard(new Vector3(0,1,-2), new Vector3(0,1,-4), 1, Vector3.up, Vector3.forward, .5f, 1));
        Assert.IsFalse(DefensiveBlockGeometry.SweepsGuard(new Vector3(0,1,1), new Vector3(0,1,3), 1, Vector3.up, Vector3.forward, .5f, 1));
        Assert.IsFalse(DefensiveBlockGeometry.SweepsGuard(new Vector3(0,8,8), new Vector3(0,8,-3), 1, Vector3.up, Vector3.forward, .5f, 1));
    }

    [Test]
    public void LongNarrowHitboxDoesNotBlockAcrossLateralOrVerticalGaps()
    {
        Vector3 guard = new Vector3(0f, 1.2f, 0f);
        Vector3 extents = new Vector3(.2f, .2f, 3f);
        Assert.IsFalse(DefensiveBlockGeometry.TrySweepGuard(new Vector3(2f, 1.2f, 5f), new Vector3(2f, 1.2f, 2f),
            extents, guard, Vector3.forward, .65f, 1.2f, .05f, out _));
        Assert.IsFalse(DefensiveBlockGeometry.TrySweepGuard(new Vector3(0f, 3.2f, 5f), new Vector3(0f, 3.2f, 2f),
            extents, guard, Vector3.forward, .65f, 1.2f, .05f, out _));
        Assert.IsTrue(DefensiveBlockGeometry.TrySweepGuard(new Vector3(0f, 1.2f, 5f), new Vector3(0f, 1.2f, 2f),
            extents, guard, Vector3.forward, .65f, 1.2f, .05f, out float fraction));
        Assert.That(fraction, Is.EqualTo(.65f).Within(.0001f));
    }

    [Test]
    public void WideAndTallShallowHitboxesStillTouchGuard()
    {
        Assert.IsTrue(DefensiveBlockGeometry.TrySweepGuard(new Vector3(2f, 0f, 2f), new Vector3(2f, 0f, -2f),
            new Vector3(3f, .2f, .2f), Vector3.zero, Vector3.forward, .65f, 1.2f, .05f, out _));
        Assert.IsTrue(DefensiveBlockGeometry.TrySweepGuard(new Vector3(0f, 3f, 2f), new Vector3(0f, 3f, -2f),
            new Vector3(.2f, 3f, .2f), Vector3.zero, Vector3.forward, .65f, 1.2f, .05f, out _));
    }

    [Test]
    public void HitboxExtentsFollowRotatedGuardAxes()
    {
        Vector3 forward = new Vector3(.6f, 0f, .8f);
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        Vector3 extents = new Vector3(3f, .2f, .2f);
        Assert.IsTrue(DefensiveBlockGeometry.TrySweepGuard(forward * 5f + side * 2.9f, -forward * 3f + side * 2.9f,
            extents, Vector3.zero, forward, .65f, 1.2f, .05f, out float fraction));
        Assert.That(fraction, Is.EqualTo(.37375f).Within(.0001f));
        Assert.IsFalse(DefensiveBlockGeometry.TrySweepGuard(forward * 5f + side * 3.3f, -forward * 3f + side * 3.3f,
            extents, Vector3.zero, forward, .65f, 1.2f, .05f, out _));
    }

    [TestCase(.5f, .8f, .5f)]
    [TestCase(1f, .8f, 1f)]
    [TestCase(2f, .8f, 2f)]
    [TestCase(.5f, .12f, .1818182f)]
    [TestCase(1f, .12f, .1818182f)]
    [TestCase(2f, .12f, .1818182f)]
    public void TimedApproachPreservesSpeedUnlessWindowRequiresSlowing(float originalSpeed, float windowEnd, float expectedSpeed)
    {
        var clip = new AnimationClip();
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 2f, 0f));
        var state = new Animancer.ClipState(clip);
        try
        {
            state.NormalizedTime = .1f;
            state.Speed = originalSpeed;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(CharacterAnimBrain).GetNestedType("Locomotion_Skill", BindingFlags.NonPublic);
            // Exercise the real playback methods without creating an actor or entering a scene state.
            var skill = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
            type.GetField("_state", flags).SetValue(skill, state);
            Assert.IsTrue((bool)type.GetMethod("TryBeginApproach", flags).Invoke(skill, new object[] { windowEnd, .22f }));
            Assert.That(state.Speed, Is.EqualTo(expectedSpeed).Within(.0001f));
            Assert.IsTrue((bool)type.GetMethod("TryEndApproach", flags).Invoke(skill, null));
            Assert.That(state.Speed, Is.EqualTo(originalSpeed).Within(.0001f));
        }
        finally
        {
            state.Destroy();
            Object.DestroyImmediate(clip);
        }
    }
    [Test]
    public void BlockOwnsNormalCommandsButYieldsToLifeAndControlLoss()
    {
        Assert.IsTrue(CharacterAnimationTransitionPolicy.CanStart(CharacterAnimationMode.Locomotion, CharacterAnimationMode.Block, CharacterAnimationTransitionReason.NormalCommand));
        foreach (var next in new[] {CharacterAnimationMode.Skill, CharacterAnimationMode.Dash, CharacterAnimationMode.Melee, CharacterAnimationMode.Utility, CharacterAnimationMode.Block})
            Assert.IsFalse(CharacterAnimationTransitionPolicy.CanStart(CharacterAnimationMode.Block, next, CharacterAnimationTransitionReason.NormalCommand));
        Assert.IsTrue(CharacterAnimationTransitionPolicy.CanStart(CharacterAnimationMode.Block, CharacterAnimationMode.Dead, CharacterAnimationTransitionReason.LifeStateOverride));
        Assert.IsTrue(CharacterAnimationTransitionPolicy.CanStart(CharacterAnimationMode.Block, CharacterAnimationMode.StageIntro, CharacterAnimationTransitionReason.CinematicOverride));
        Assert.IsTrue(CharacterAnimationTransitionPolicy.CanStart(CharacterAnimationMode.Block, CharacterAnimationMode.Knockback, CharacterAnimationTransitionReason.ExternalControlLoss));
        Assert.IsFalse(CharacterAnimationTransitionPolicy.CanStart(CharacterAnimationMode.Chain, CharacterAnimationMode.Block, CharacterAnimationTransitionReason.NormalCommand));
        Assert.IsFalse(CharacterAnimationTransitionPolicy.CanStart(CharacterAnimationMode.Locomotion, CharacterAnimationMode.Block, CharacterAnimationTransitionReason.NormalCommand, true));
    }
    [Test]
    public void WrongRequestCannotStopHitboxExecution()
    {
        var g = new GameObject("Block request isolation test");
        try
        {
            var runtime = g.AddComponent<SkillHitboxSequenceRuntime>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(SkillHitboxSequenceRuntime).GetField("_initialized", flags).SetValue(runtime, true);
            typeof(SkillHitboxSequenceRuntime).GetField("_requestId", flags).SetValue(runtime, 12);
            Assert.IsFalse(runtime.StopExecution(11));
            Assert.IsFalse((bool)typeof(SkillHitboxSequenceRuntime).GetField("_isShuttingDown", flags).GetValue(runtime));
            // A terminal execution must reject subsequent stops, including the matching id.
            typeof(SkillHitboxSequenceRuntime).GetField("_isShuttingDown", flags).SetValue(runtime, true);
            Assert.IsFalse(runtime.StopExecution(12));
        }
        finally { Object.DestroyImmediate(g); }
    }
    [MenuItem("Tools/RB/Defensive Block/Run Smoke Tests")]
    public static void RunSmokeTests()
    {
        var tests = new DefensiveBlockTests();
        tests.FastChargeCrossesGuardInOneFrame();
        tests.MissBehindAndMovingAwayDoNotBlock();
        tests.LongNarrowHitboxDoesNotBlockAcrossLateralOrVerticalGaps();
        tests.WideAndTallShallowHitboxesStillTouchGuard();
        tests.HitboxExtentsFollowRotatedGuardAxes();
        foreach (float speed in new[] { .5f, 1f, 2f })
        {
            tests.TimedApproachPreservesSpeedUnlessWindowRequiresSlowing(speed, .8f, speed);
            tests.TimedApproachPreservesSpeedUnlessWindowRequiresSlowing(speed, .12f, .1818182f);
        }
        tests.BlockOwnsNormalCommandsButYieldsToLifeAndControlLoss();
        tests.WrongRequestCannotStopHitboxExecution();
        tests.NestedKnockbackMotorMovesContextRoot();
        var cameraTests = new DefensiveBlockCameraTests();
        cameraTests.CameraBlendsBackToExactPoseAndLensAfterBlock();
        cameraTests.ResetAndDisableDuringShotRestoreWithoutDrift();
        cameraTests.PendingOrRejectedWarpDoesNotTakeCamera();
        cameraTests.ShotStaysBehindPlayerWithGuardAhead();
        Debug.Log("DefensiveBlock: 18 smoke tests passed.");
    }
    [Test]
    public void NestedKnockbackMotorMovesContextRoot()
    {
        var root = new GameObject("Knockback actor");
        try
        {
            var context = root.AddComponent<EnemyContext>();
            var body = root.AddComponent<CapsuleCollider>();
            context.ColliderRefs = root.AddComponent<CharacterColliderRefs>();
            context.ColliderRefs.CharacterPositionCollider = body;
            context.EnsureMovementMotor();
            var child = new GameObject("Movement_System"); child.transform.SetParent(root.transform);
            child.transform.localPosition = Vector3.up;
            var motor = child.AddComponent<CharacterKnockbackMotor>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(CharacterKnockbackMotor).GetField("ctx", flags).SetValue(motor, context);
            var applied = (Vector3)typeof(CharacterKnockbackMotor).GetMethod("ApplyDelta", flags).Invoke(motor, new object[] {Vector3.forward});
            Assert.That(applied, Is.EqualTo(Vector3.forward));
            Assert.That(root.transform.position, Is.EqualTo(Vector3.forward));
            Assert.That(child.transform.localPosition, Is.EqualTo(Vector3.up));
        }
        finally { Object.DestroyImmediate(root); }
    }
}
#endif
