using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PartyFollowUpPresentationTests
{
    GameObject root;
    TimeSlowManager slow;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("FollowUpPresentationTest");
        slow = root.AddComponent<TimeSlowManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
    }

    [Test]
    public void SupersededFollowUpCannotUpdateOrReleaseNewSlow()
    {
        int first = slow.BeginPresentationSlow(0.25f);
        int second = slow.BeginPresentationSlow(0.4f);
        slow.UpdatePresentationSlow(first, 0.1f);
        slow.EndPresentationSlow(first);
        Assert.That(slow.WorldTimeScale, Is.EqualTo(0.4f).Within(0.001f));
        slow.EndPresentationSlow(second);
        slow.EndPresentationSlow(second);
        Assert.That(slow.WorldTimeScale, Is.EqualTo(1f));
        Assert.That(slow.IsSlowing, Is.False);
    }

    [Test]
    public void LegacySlowTakesPriorityAndReleasesToCurrentFollowUpProgress()
    {
        int followUp = slow.BeginPresentationSlow(0.25f);
        int dash = slow.StartSlow(0.1f, 10f);
        slow.UpdatePresentationSlow(followUp, 0.7f);
        Assert.That(slow.WorldTimeScale, Is.EqualTo(0.1f).Within(0.001f));
        Assert.That(slow.StopSlow(followUp), Is.False);
        slow.StopSlow(dash);
        Assert.That(slow.WorldTimeScale, Is.EqualTo(0.7f).Within(0.001f));
        slow.EndPresentationSlow(followUp);
        Assert.That(slow.WorldTimeScale, Is.EqualTo(1f));
    }

    [Test]
    public void FollowUpCleanupDoesNotCancelAnActiveLegacySlow()
    {
        int followUp = slow.BeginPresentationSlow(0.25f);
        int cinematic = slow.StartSlow(0.05f, 10f);
        slow.EndPresentationSlow(followUp);
        Assert.That(slow.ActiveSlowHandle, Is.EqualTo(cinematic));
        Assert.That(slow.WorldTimeScale, Is.EqualTo(0.05f).Within(0.001f));
    }

    [Test]
    public void PausedSlowDoesNotExpireOrAdvanceWorldClock()
    {
        GlobalTimeScaleManager existing = Object.FindAnyObjectByType<GlobalTimeScaleManager>();
        GlobalTimeScaleManager global = GlobalTimeScaleManager.Instance;
        int pause = global.AcquirePauseToken();
        try
        {
            slow.StartSlow(0.2f, 1f);
            // An already-expired timer would be removed by Update without the pause guard.
            typeof(TimeSlowManager).GetField("_elapsed", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(slow, 2f);
            float before = slow.WorldTime;
            typeof(TimeSlowManager).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(slow, null);
            Assert.That(slow.IsSlowing, Is.True);
            Assert.That(slow.WorldTime, Is.EqualTo(before));
        }
        finally
        {
            global.ReleasePauseToken(pause);
            if (existing == null)
                Object.DestroyImmediate(global.gameObject);
        }
    }

    [Test]
    public void RecoveryReachesNormalAtCastPointEvenWithMalformedCurveEndpoint()
    {
        var profile = new PartyFollowUpPresentationProfile
        {
            recoveryCurve = AnimationCurve.Constant(0f, 1f, 0f)
        };
        Assert.That(profile.EvaluateWorldScale(0f), Is.EqualTo(0.25f));
        Assert.That(profile.EvaluateWorldScale(1f), Is.EqualTo(1f));
        Assert.That(profile.EvaluateWorldScale(2f), Is.EqualTo(1f));
    }

    [Test]
    public void DefaultRecoveryIsMonotonic()
    {
        var profile = new PartyFollowUpPresentationProfile();
        float previous = profile.EvaluateWorldScale(0f);
        for (int i = 1; i <= 20; i++)
        {
            float current = profile.EvaluateWorldScale(i / 20f);
            Assert.That(current, Is.GreaterThanOrEqualTo(previous));
            previous = current;
        }
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void ReleaseIsRequestScopedAndPreservesOtherExemptions(bool nestedManager, bool existingExemption)
    {
        AllyContext actor = root.AddComponent<AllyContext>();
        GameObject module = root;
        if (nestedManager)
        {
            module = new GameObject("GamePlayStats_System");
            module.transform.SetParent(root.transform);
        }
        CharacterSkillManager manager = module.AddComponent<CharacterSkillManager>();
        actor.ResolveReferences();
        Assert.That(actor.SkillManager, Is.SameAs(manager));
        if (existingExemption)
            actor.PushWorldSlowExemption();

        var cast = new ActiveSkillCastInfo(9, null, null, null, null, 0.5f, false, false, "test");
        System.Type scopeType = typeof(PartyFollowUpSkillExecutor).Assembly.GetType("PartyFollowUpPresentationScope");
        var scope = (System.IDisposable)System.Activator.CreateInstance(scopeType,
            new object[] { actor, null, new PartyFollowUpPresentationProfile(), cast });
        try
        {
            Assert.That(actor.UsesWorldSlow, Is.False);
            var released = (System.Action<ActiveSkillCastInfo>)typeof(CharacterSkillManager)
                .GetField("CastReleased", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            released(new ActiveSkillCastInfo(8, null, null, null, null, 0.5f, true, false, "other"));
            Assert.That(actor.UsesWorldSlow, Is.False);
            released(cast);
            scope.Dispose();
            Assert.That(actor.UsesWorldSlow, Is.EqualTo(!existingExemption));
        }
        finally
        {
            scope.Dispose();
            if (existingExemption)
                actor.PopWorldSlowExemption();
        }
    }
}
