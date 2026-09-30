#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

public sealed class CooperativeBlockTests
{
    [Test] public void DefaultsAndLegacyModesAreStable()
    {
        Assert.AreEqual(0, (int)DefensiveBlockMode.Contact); Assert.AreEqual(1, (int)DefensiveBlockMode.TimedApproach);
        var settings = new CooperativeBlockSettings(); Assert.IsTrue(settings.IsValid);
        Assert.AreEqual(.5f, settings.initialMeter); Assert.AreEqual(.1f, settings.gainPerPress);
        Assert.AreEqual(.2f, settings.drainPerSecond); Assert.AreEqual(3f, settings.duration);
        var profile = ScriptableObject.CreateInstance<DefensiveBlockActorProfile>();
        try { Assert.IsFalse(profile.cooperativeEnabled); } finally { Object.DestroyImmediate(profile); }
    }
    [Test] public void FiveImmediatePressesWinAndTerminalInputIsIgnored()
    {
        var meter = new CooperativeBlockMeter(new CooperativeBlockSettings());
        for (int i = 0; i < 5; i++) Assert.IsTrue(meter.Press());
        Assert.AreEqual(CooperativeBlockState.Success, meter.State);
        Assert.IsFalse(meter.Press()); meter.Advance(10); Assert.AreEqual(1f, meter.Value);
    }
    [Test] public void DepletionAndDeadlineLoseWithoutLateRescue()
    {
        var meter = new CooperativeBlockMeter(new CooperativeBlockSettings()); meter.Advance(2.5);
        Assert.AreEqual(CooperativeBlockState.Failure, meter.State); Assert.IsFalse(meter.Press());
        meter = new CooperativeBlockMeter(new CooperativeBlockSettings { drainPerSecond = .01f }); meter.Advance(3);
        Assert.Greater(meter.Value, 0); Assert.AreEqual(CooperativeBlockState.Failure, meter.State); Assert.IsFalse(meter.Press());
    }
    [Test] public void PausedClockDoesNotAdvanceAndSettingsAreSnapshot()
    {
        var settings = new CooperativeBlockSettings(); var meter = new CooperativeBlockMeter(settings);
        settings.drainPerSecond = 100; settings.duration = .01f;
        meter.Advance(0); meter.Advance(double.NaN); meter.Advance(-1);
        Assert.AreEqual(.5f, meter.Value); Assert.AreEqual(3, meter.Remaining);
        meter.Advance(1); Assert.That(meter.Value, Is.EqualTo(.3f).Within(.000001));
    }
    [Test] public void TimestampedInputsGiveSameOutcomeAt30_60_144Fps()
    {
        foreach (int fps in new[] { 30, 60, 144 })
        {
            var meter = new CooperativeBlockMeter(new CooperativeBlockSettings());
            double previous = 0, nextPress = .12; int presses = 0;
            for (int frame = 1; frame <= fps * 3 && meter.State == CooperativeBlockState.Struggling; frame++)
            {
                double now = (double)frame / fps;
                while (nextPress <= now && meter.State == CooperativeBlockState.Struggling)
                {
                    meter.Advance(nextPress - previous); previous = nextPress;
                    meter.Press(); presses++; nextPress += .12;
                }
                meter.Advance(now - previous); previous = now;
            }
            Assert.AreEqual(CooperativeBlockState.Success, meter.State, "FPS " + fps);
            Assert.AreEqual(7, presses, "FPS " + fps);
        }
    }
    [Test] public void CooperativeDraftUndoRevertAndRejectContinue()
    {
        var skill = ScriptableObject.CreateInstance<SkillGemDefinition>(); skill.defensiveBlock = new SkillDefensiveBlockSettings();
        var draft = DefensiveBlockTimelineSession.Create(skill);
        try
        {
            draft.SetMode(DefensiveBlockMode.Cooperative);
            Undo.RecordObject(draft, "Meter test"); draft.cooperative.gainPerPress = .25f;
            Undo.FlushUndoRecordObjects(); Assert.IsTrue(draft.IsDirty);
            Assert.AreEqual(.1f, skill.defensiveBlock.cooperative.gainPerPress);
            draft.windows[0].onSuccess = DefensiveBlockOutcome.ContinueSkill;
            StringAssert.Contains("Continue Skill", draft.ValidationError);
            draft.Reload(); Assert.IsFalse(draft.IsDirty); Assert.AreEqual(.1f, draft.cooperative.gainPerPress);
            skill.defensiveBlock.mode = DefensiveBlockMode.Cooperative;
            skill.defensiveBlock.windows = new[] { new DefensiveBlockWindow { onSuccess = DefensiveBlockOutcome.ContinueSkill } };
            Assert.IsFalse(skill.defensiveBlock.IsConfigured);
        }
        finally { Undo.ClearUndo(draft); Object.DestroyImmediate(draft); Object.DestroyImmediate(skill); }
    }
}
#endif
