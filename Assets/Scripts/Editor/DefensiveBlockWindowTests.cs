#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class DefensiveBlockWindowTests
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);
    static DefensiveBlockWindow[] Pair() => new[]
    {
        new DefensiveBlockWindow { startNormalized = .1f, endNormalized = .3f, hitboxSteps = new[] { 0 }, onSuccess = DefensiveBlockOutcome.ContinueSkill },
        new DefensiveBlockWindow { startNormalized = .5f, endNormalized = .8f, hitboxSteps = new[] { 1 } }
    };

    [Test] public void WindowBoundariesGapsAndLegacyFallback()
    {
        var profile = new SkillDefensiveBlockSettings();
        try
        {
            Assert.IsTrue(profile.IsConfigured); Assert.AreEqual(0, profile.FindWindow(0));
            Assert.AreEqual(DefensiveBlockOutcome.InterruptSkill, profile.Outcome(0));
            profile.windows = Pair(); Assert.IsTrue(profile.IsConfigured);
            Assert.AreEqual(0, profile.FindWindow(.1f)); Assert.AreEqual(0, profile.FindWindow(.3f));
            Assert.AreEqual(-1, profile.FindWindow(.4f)); Assert.AreEqual(1, profile.FindWindow(.5f));
            Assert.AreEqual(1, profile.FindWindow(.8f)); Assert.AreEqual(-1, profile.FindWindow(.81f));
            Assert.IsFalse(profile.AllowsStep(1, 0)); Assert.IsTrue(profile.AllowsStep(1, 1));
            Assert.AreEqual(DefensiveBlockOutcome.ContinueSkill, profile.Outcome(0));
        }
        finally {  }
    }

    [Test] public void InvalidWindowsFailClosed()
    {
        var profile = new SkillDefensiveBlockSettings();
        try
        {
            profile.windows = Pair(); profile.windows[1].startNormalized = .3f; Assert.IsFalse(profile.IsConfigured);
            profile.windows = Pair(); profile.windows[1].hitboxSteps = new[] { 0 }; Assert.IsFalse(profile.IsConfigured);
            profile.windows = Pair(); profile.windows[0].endNormalized = float.NaN; Assert.IsFalse(profile.IsConfigured);
            profile.windows = Pair(); profile.windows[0].onSuccess = (DefensiveBlockOutcome)99; Assert.IsFalse(profile.IsConfigured);
        }
        finally {  }
    }

    [Test] public void EarlierDamageDoesNotRejectLaterWindowAndStaleRequestsDoNotMarkIt()
    {
        var casterObject = new GameObject("Window test caster");
        var victimObject = new GameObject("Window test victim");
        var profile = new SkillDefensiveBlockSettings();
        try
        {
            var caster = casterObject.AddComponent<EnemyContext>();
            var attack = casterObject.AddComponent<DefensiveBlockAttack>();
            var runtime = casterObject.AddComponent<SkillHitboxSequenceRuntime>();
            var victim = victimObject.AddComponent<PlayerContext>();
            profile.windows = Pair();
            Set(attack, "ctx", caster); Set(attack, "execution", runtime); Set(attack, "profile", profile);
            Set(attack, "requestId", 12); Set(attack, "life", caster.LifeGeneration);
            var passed = typeof(DefensiveBlockAttack).GetMethod("HasPassedWindow", Hidden);
            bool Hit(int window) => (bool)passed.Invoke(attack, new object[] { victim, window });
            attack.NotifyDamageApplied(runtime, 12, caster.LifeGeneration, victim, 0);
            Assert.IsTrue(Hit(0)); Assert.IsFalse(Hit(1));
            attack.NotifyDamageApplied(runtime, 11, caster.LifeGeneration, victim, 1);
            attack.NotifyDamageApplied(runtime, 12, caster.LifeGeneration + 1, victim, 1);
            Assert.IsFalse(Hit(1));
            attack.NotifyDamageApplied(runtime, 12, caster.LifeGeneration, victim, 1);
            Assert.IsTrue(Hit(1));
            attack.ResetExecution(); Assert.IsFalse(Hit(0)); Assert.IsFalse(Hit(1));
        }
        finally { Object.DestroyImmediate(casterObject); Object.DestroyImmediate(victimObject);  }
    }

    [Test] public void StepSuppressionIsRequestScopedAndLeavesFollowingStepPlayable()
    {
        var go = new GameObject("Window suppression");
        try
        {
            var runtime = go.AddComponent<SkillHitboxSequenceRuntime>();
            Set(runtime, "_initialized", true); Set(runtime, "_requestId", 8); Set(runtime, "_casterLife", 3);
            var type = typeof(SkillHitboxSequenceRuntime).GetNestedType("StepRuntimeState", BindingFlags.NonPublic);
            var steps = (IList)typeof(SkillHitboxSequenceRuntime).GetField("_steps", Hidden).GetValue(runtime);
            for (int i = 0; i < 2; i++)
            {
                var step = Activator.CreateInstance(type, true);
                type.GetField("StepIndex").SetValue(step, i);
                type.GetField("Definition").SetValue(step, new PrefabHitboxSkillPayloadDef.HitboxStep());
                steps.Add(step);
            }
            var activate = typeof(SkillHitboxSequenceRuntime).GetMethod("ActivateStep", Hidden);
            bool Active(int i) => (bool)type.GetField("IsActive").GetValue(steps[i]);
            activate.Invoke(runtime, new[] { steps[0] }); Assert.IsTrue(Active(0));
            Assert.IsFalse(runtime.SuppressSteps(7, 3, new[] { 0 })); Assert.IsTrue(Active(0));
            Assert.IsFalse(runtime.SuppressSteps(8, 4, new[] { 0 }));
            Assert.IsTrue(runtime.SuppressSteps(8, 3, new[] { 0 })); Assert.IsFalse(Active(0));
            Assert.IsTrue(runtime.SuppressSteps(8, 3, new[] { 0 }));
            activate.Invoke(runtime, new[] { steps[0] }); Assert.IsFalse(Active(0));
            activate.Invoke(runtime, new[] { steps[1] }); Assert.IsTrue(Active(1));
        }
        finally { Object.DestroyImmediate(go); }
    }

    public static string Run()
    {
        var test = new DefensiveBlockWindowTests();
        test.WindowBoundariesGapsAndLegacyFallback(); test.InvalidWindowsFailClosed();
        test.EarlierDamageDoesNotRejectLaterWindowAndStaleRequestsDoNotMarkIt();
        test.StepSuppressionIsRequestScopedAndLeavesFollowingStepPlayable();
        return "4 multi-window tests passed";
    }
}
#endif
