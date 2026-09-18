#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DefensiveBlockContactOrderTests
{
    [Test]
    public void GuardBeforePlayerWinsAcrossOneLongFrame()
    {
        Vector3 previous = new Vector3(0, 1, 8), current = new Vector3(0, 1, -3);
        Assert.IsTrue(DefensiveBlockGeometry.TrySweepGuard(previous, current, 1,
            new Vector3(0, 1, 3), Vector3.forward, .65f, 1.2f, .05f, out float guard));
        var player = new Bounds(Vector3.up, Vector3.one);
        Assert.IsTrue(DefensiveBlockGeometry.TrySweepBounds(previous, current, Vector3.one,
            player.center, player, out float victim));
        Assert.IsTrue(DefensiveBlockGeometry.GuardContactWins(guard, victim));
    }

    [Test]
    public void PlayerAheadOfGuardWinsRegardlessOfCallbackOrder()
    {
        Vector3 previous = new Vector3(0, 1, 8), current = new Vector3(0, 1, -3);
        Assert.IsTrue(DefensiveBlockGeometry.TrySweepGuard(previous, current, 1,
            new Vector3(0, 1, 3), Vector3.forward, .65f, 1.2f, .05f, out float guard));
        var player = new Bounds(new Vector3(0, 1, 5), Vector3.one);
        Assert.IsTrue(DefensiveBlockGeometry.TrySweepBounds(previous, current, Vector3.one,
            player.center, player, out float victim));
        Assert.IsFalse(DefensiveBlockGeometry.GuardContactWins(guard, victim));
    }

    [Test]
    public void InitialOverlapAndTiesCannotRetroactivelyProtectPlayer()
    {
        var player = new Bounds(Vector3.zero, Vector3.one);
        Assert.IsTrue(DefensiveBlockGeometry.TrySweepBounds(Vector3.zero, Vector3.back * 4,
            Vector3.one, player.center, player, out float victim));
        Assert.AreEqual(0f, victim);
        Assert.IsFalse(DefensiveBlockGeometry.GuardContactWins(.25f, victim));
        Assert.IsFalse(DefensiveBlockGeometry.GuardContactWins(0f, victim));
        Assert.IsFalse(DefensiveBlockGeometry.GuardContactWins(.5f, .5f));
        Assert.IsTrue(DefensiveBlockGeometry.GuardContactWins(0f, 0f, 2f),
            "A ready guard ahead of Player can catch a wide hitbox on activation.");
        Assert.IsFalse(DefensiveBlockGeometry.GuardContactWins(0f, 0f, -2f),
            "Player ahead of the guard must still win the contact.");
    }

    [Test]
    public void MovingPlayerUsesRelativeSweepAndMissDoesNotVetoGuard()
    {
        var target = new Bounds(new Vector3(0, 1, 4), Vector3.one);
        Assert.IsTrue(DefensiveBlockGeometry.TrySweepBounds(new Vector3(0, 1, 4), new Vector3(0, 1, 4),
            Vector3.one, Vector3.up, target, out float victim));
        Assert.That(victim, Is.InRange(.6f, .7f));
        target.center += Vector3.right * 10;
        Assert.IsFalse(DefensiveBlockGeometry.TrySweepBounds(new Vector3(0, 1, 8), new Vector3(0, 1, -3),
            Vector3.one, target.center, target, out victim));
        Assert.IsTrue(DefensiveBlockGeometry.GuardContactWins(.5f, victim));
    }

    [Test]
    public void AppliedHitIsScopedToExecutionRequestLifeAndVictim()
    {
        var casterObject = new GameObject("Contact order caster");
        var victimObject = new GameObject("Contact order victim");
        var unrelatedObject = new GameObject("Unrelated execution");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            var caster = casterObject.AddComponent<EnemyContext>();
            var attack = casterObject.AddComponent<DefensiveBlockAttack>();
            var runtime = casterObject.AddComponent<SkillHitboxSequenceRuntime>();
            var otherRuntime = unrelatedObject.AddComponent<SkillHitboxSequenceRuntime>();
            var victim = victimObject.AddComponent<PlayerContext>();
            typeof(DefensiveBlockAttack).GetField("ctx", flags).SetValue(attack, caster);
            typeof(DefensiveBlockAttack).GetField("execution", flags).SetValue(attack, runtime);
            typeof(DefensiveBlockAttack).GetField("requestId", flags).SetValue(attack, 12);
            typeof(DefensiveBlockAttack).GetField("life", flags).SetValue(attack, caster.LifeGeneration);
            var passed = typeof(DefensiveBlockAttack).GetMethod("HasPassedTarget", flags);
            attack.NotifyDamageApplied(otherRuntime, 12, caster.LifeGeneration, victim);
            attack.NotifyDamageApplied(runtime, 11, caster.LifeGeneration, victim);
            attack.NotifyDamageApplied(runtime, 12, caster.LifeGeneration + 1, victim);
            Assert.IsFalse((bool)passed.Invoke(attack, new object[] { victim }));
            attack.NotifyDamageApplied(runtime, 12, caster.LifeGeneration, victim);
            Assert.IsTrue((bool)passed.Invoke(attack, new object[] { victim }));
            Assert.IsFalse((bool)passed.Invoke(attack, new object[] { caster }));
            typeof(CharacteContext).GetField("_lifeGeneration", flags).SetValue(victim, victim.LifeGeneration + 1);
            Assert.IsFalse((bool)passed.Invoke(attack, new object[] { victim }), "A respawned victim is a new life.");
            attack.NotifyDamageApplied(runtime, 12, caster.LifeGeneration, victim);
            Assert.IsTrue((bool)passed.Invoke(attack, new object[] { victim }));
            Assert.IsFalse((bool)typeof(SkillHitboxSequenceRuntime).GetField("_isShuttingDown", flags).GetValue(runtime),
                "A missed guard must not cancel the enemy's hitboxes.");
            attack.ResetExecution();
            Assert.IsFalse((bool)passed.Invoke(attack, new object[] { victim }));
        }
        finally
        {
            Object.DestroyImmediate(casterObject);
            Object.DestroyImmediate(victimObject);
            Object.DestroyImmediate(unrelatedObject);
        }
    }

    [MenuItem("Tools/RB/Defensive Block/Run Contact Order Tests")]
    public static void Run()
    {
        var tests = new DefensiveBlockContactOrderTests();
        tests.GuardBeforePlayerWinsAcrossOneLongFrame();
        tests.PlayerAheadOfGuardWinsRegardlessOfCallbackOrder();
        tests.InitialOverlapAndTiesCannotRetroactivelyProtectPlayer();
        tests.MovingPlayerUsesRelativeSweepAndMissDoesNotVetoGuard();
        tests.AppliedHitIsScopedToExecutionRequestLifeAndVictim();
        Debug.Log("DefensiveBlock: 5 contact order tests passed.");
    }
}
#endif
