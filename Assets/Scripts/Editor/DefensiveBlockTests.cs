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
        tests.BlockOwnsNormalCommandsButYieldsToLifeAndControlLoss();
        tests.WrongRequestCannotStopHitboxExecution();
        tests.NestedKnockbackMotorMovesContextRoot();
        var cameraTests = new DefensiveBlockCameraTests();
        cameraTests.CameraBlendsBackToExactPoseAndLensAfterBlock();
        cameraTests.ResetAndDisableDuringShotRestoreWithoutDrift();
        cameraTests.PendingOrRejectedWarpDoesNotTakeCamera();
        cameraTests.ShotStaysBehindPlayerWithGuardAhead();
        Debug.Log("DefensiveBlock: 9 smoke tests passed.");
    }
    [Test]
    public void NestedKnockbackMotorMovesContextRoot()
    {
        var root = new GameObject("Knockback actor");
        try
        {
            var context = root.AddComponent<EnemyContext>();
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
