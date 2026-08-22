#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using Animancer;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode safety net for <see cref="CharacterAnimBrain"/> playback lifecycle and command
/// admission. The suite builds a real Animancer graph (the graph initialises outside Play Mode)
/// but the graph never advances time here, so every assertion is driven either by a cast point of
/// <c>0</c> (which the chain poll satisfies on the first tick) or by invoking the state's own
/// end-of-clip callback the way Animancer would.
///
/// Play Mode still owns anything that needs real clip time: skill cast-point events (they are
/// Animancer events, not polled), the chain watchdog, fade weights, and root-motion delta.
/// </summary>
public sealed class CharacterAnimBrainSmokeTests
{
    const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

    readonly List<Object> createdObjects = new();
    TimeSlowManager preExistingTimeSlowManager;

    [SetUp]
    public void SetUp()
    {
        preExistingTimeSlowManager = Object.FindAnyObjectByType<TimeSlowManager>();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();

        // The Brain's world-slow clock lazily spawns a TimeSlowManager. Do not leave it behind in
        // whatever scene the test runner happened to have open.
        if (preExistingTimeSlowManager == null)
        {
            TimeSlowManager spawned = Object.FindAnyObjectByType<TimeSlowManager>();
            if (spawned != null)
                Object.DestroyImmediate(spawned.gameObject);
        }

        preExistingTimeSlowManager = null;
    }

    // ---- Skill playback -----------------------------------------------------------------------

    [Test]
    public void SkillPlaybackStartsAndPublishesItsRootMotionPolicy()
    {
        Rig rig = CreateRig();

        bool started = rig.Brain.TryPlaySkill(101, null, 0f, null, usePlanarRootMotion: true);

        Assert.That(started, Is.True);
        Assert.That(rig.Brain.IsSkillPlaybackActive, Is.True);
        Assert.That(rig.Brain.RootMotionActive, Is.True, "Skill playback always drives root motion.");
        Assert.That(rig.Brain.RootMotionPlanarOnly, Is.True);
        Assert.That(rig.Brain.RootMotionYawActive, Is.True);
        Assert.That(rig.Signals, Is.EqualTo(new[] { "Skill:Started:101" }));
    }

    [Test]
    public void SkillCompletionEmitsExactlyOneTerminalAndReleasesLocomotion()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlaySkill(101, null, 0f);
        rig.ClearSignals();

        rig.EndSkillClip();

        // The graph never reached the cast point, so the state releases it on the way out. That
        // guarantee ("a completed request always saw its cast moment") is part of the contract.
        Assert.That(rig.Signals, Is.EqualTo(new[]
        {
            "Skill:CastMoment:101",
            "Skill:Completed:101",
        }));
        Assert.That(rig.SkillCompletedCount, Is.EqualTo(1));
        Assert.That(rig.Brain.IsSkillPlaybackActive, Is.False);
        Assert.That(rig.Brain.RootMotionActive, Is.False);
        Assert.That(rig.Brain.IsExclusiveLocomotionActive, Is.False);
    }

    [Test]
    public void CancelSkillCastRequestClearsStateWithoutATerminalEvent()
    {
        // Characterises the existing contract: the caller asked for the cancel, so it is not told
        // about an interruption it caused itself.
        Rig rig = CreateRig();
        rig.Brain.TryPlaySkill(101, null, 0f);
        rig.ClearSignals();

        rig.Brain.CancelSkillCastRequest(101);

        Assert.That(rig.Signals, Is.Empty);
        Assert.That(rig.Brain.IsSkillPlaybackActive, Is.False);
        Assert.That(rig.Brain.RootMotionActive, Is.False);
    }

    [Test]
    public void ExternalControlLossInterruptsAnActiveSkillExactlyOnce()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlaySkill(101, null, 0f);
        rig.ClearSignals();

        rig.Brain.InterruptActivePlaybackForExternalControlLoss();

        Assert.That(rig.Signals, Is.EqualTo(new[] { "Skill:Interrupted:101" }));
        Assert.That(rig.SkillInterruptedIds, Is.EqualTo(new[] { 101 }));
        Assert.That(rig.Brain.IsSkillPlaybackActive, Is.False);
    }

    [Test]
    public void SkillWithoutAValidClipFailsSafely()
    {
        Rig rig = CreateRig(profile => profile.skillClip = null);

        bool started = rig.Brain.TryPlaySkill(101, null, 0f);

        Assert.That(started, Is.False);
        Assert.That(rig.Brain.IsSkillPlaybackActive, Is.False);
        Assert.That(rig.Signals, Is.Empty);
    }

    [Test]
    public void BrainWithoutAnAnimProfileRefusesEveryPlaybackCommand()
    {
        Rig rig = CreateRig(configureBrain: false);

        Assert.That(rig.Brain.TryPlaySkill(101, null, 0f), Is.False);
        Assert.That(rig.Brain.TryPlayUtilityWarpOut(102), Is.False);
        Assert.That(rig.Brain.TryPlayChainCutscene(103, rig.CutsceneA), Is.False);
        Assert.That(rig.Brain.TryPlayStageIntro(), Is.False);
        Assert.That(rig.Signals, Is.Empty);
    }

    // ---- Utility playback ---------------------------------------------------------------------

    [Test]
    public void UtilityWarpOutRunsItsOwnLifecycle()
    {
        Rig rig = CreateRig();

        bool started = rig.Brain.TryPlayUtilityWarpOut(202);
        Assert.That(started, Is.True);
        Assert.That(rig.Signals, Is.EqualTo(new[] { "UtilityWarpOut:Started:202" }));

        rig.ClearSignals();
        rig.EndUtilityClip();

        Assert.That(rig.Signals, Is.EqualTo(new[]
        {
            "UtilityWarpOut:CastMoment:202",
            "UtilityWarpOut:Completed:202",
        }));
        Assert.That(rig.Brain.IsUtilityActive, Is.False);
    }

    // ---- Chain playback -----------------------------------------------------------------------

    [Test]
    public void ChainCutsceneEmitsStartedCastMomentThenCompletedExactlyOnce()
    {
        Rig rig = CreateRig();

        bool started = rig.Brain.TryPlayChainCutscene(1, rig.CutsceneA);
        Assert.That(started, Is.True);
        Assert.That(rig.Brain.IsChainPlaybackActive, Is.True);

        rig.Tick();
        rig.EndChainClip();

        Assert.That(rig.Signals, Is.EqualTo(new[]
        {
            "ChainCutscene:Started:1",
            "ChainCutscene:CastMoment:1",
            "ChainCutscene:Completed:1",
        }));
        Assert.That(rig.ChainCompletedIds, Is.EqualTo(new[] { 1 }));
        Assert.That(rig.ChainInterruptedIds, Is.Empty);
        Assert.That(rig.Brain.IsChainPlaybackActive, Is.False);
    }

    [Test]
    public void ChainCompletionCallbackCanStartTheNextChainImmediately()
    {
        // Regression guard for the deferred-frame workaround that used to live in
        // ChainAttackProcController: the terminal event must be delivered after the FSM has left
        // the chain state, so a handler can chain straight into the next playback.
        Rig rig = CreateRig();

        bool chainStillActiveInsideCallback = true;
        bool restarted = false;
        rig.Brain.ChainPlaybackCompleted += id =>
        {
            if (id != 1)
                return;

            chainStillActiveInsideCallback = rig.Brain.IsChainPlaybackActive;
            restarted = rig.Brain.TryPlayChainCutscene(2, rig.CutsceneB);
        };

        rig.Brain.TryPlayChainCutscene(1, rig.CutsceneA);
        rig.EndChainClip();

        Assert.That(chainStillActiveInsideCallback, Is.False,
            "IsChainPlaybackActive must already be false when ChainPlaybackCompleted is raised.");
        Assert.That(restarted, Is.True,
            "A completion handler must be able to start the next chain in the same frame.");
        Assert.That(rig.Brain.IsChainPlaybackActive, Is.True);
        Assert.That(rig.Signals, Does.Contain("ChainCutscene:Started:2"));
    }

    [Test]
    public void ChainPlaybackBlocksExternalAnimationCommands()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlayChainCutscene(1, rig.CutsceneA);
        rig.ClearSignals();

        rig.Brain.PlayDash(0.2f, Vector2.up);
        rig.Brain.PlayReload(0.5f);
        rig.Brain.NotifyShotFired();
        bool skillStarted = rig.Brain.TryPlaySkill(101, null, 0f);

        Assert.That(skillStarted, Is.False);
        Assert.That(rig.Brain.IsChainPlaybackActive, Is.True,
            "No external command may evict an active chain playback.");
        Assert.That(rig.Signals, Is.Empty);
    }

    [Test]
    public void ChainCannotStartWhileAnotherChainIsActive()
    {
        Rig rig = CreateRig();
        Assert.That(rig.Brain.TryPlayChainCutscene(1, rig.CutsceneA), Is.True);

        Assert.That(rig.Brain.TryPlayChainCutscene(2, rig.CutsceneB), Is.False);
        Assert.That(rig.Brain.TryPlayChainUtilityWarpOut(3), Is.False);
    }

    [Test]
    public void CancelChainPlaybackRequestEmitsExactlyOneInterrupted()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlayChainCutscene(1, rig.CutsceneA);
        rig.ClearSignals();

        rig.Brain.CancelChainPlaybackRequest(1);

        Assert.That(rig.Signals, Is.EqualTo(new[] { "ChainCutscene:Interrupted:1" }));
        Assert.That(rig.ChainInterruptedIds, Is.EqualTo(new[] { 1 }));
        Assert.That(rig.ChainCompletedIds, Is.Empty);
        Assert.That(rig.Brain.IsChainPlaybackActive, Is.False);
    }

    [Test]
    public void InterruptedChainSkillAlsoRaisesSkillCastInterrupted()
    {
        Rig rig = CreateRig();
        SkillGemDefinition skillDef = CreateSkillDefinition(rig);
        Assert.That(
            rig.Brain.TryPlayChainSkill(7, skillDef, 0f, requestAdvanceMoment: false, advancePointNormalized: 1f),
            Is.True);
        rig.ClearSignals();

        rig.Brain.CancelChainPlaybackRequest(7);

        Assert.That(rig.Signals, Is.EqualTo(new[] { "ChainSkill:Interrupted:7" }));
        Assert.That(rig.ChainInterruptedIds, Is.EqualTo(new[] { 7 }));
        Assert.That(rig.SkillInterruptedIds, Is.EqualTo(new[] { 7 }),
            "Chain skills mirror their interruption onto the skill event so skill runtimes unwind.");
    }

    [Test]
    public void ChainAdvanceMomentIsPolledOnceAtItsNormalizedPoint()
    {
        Rig rig = CreateRig();
        SkillGemDefinition skillDef = CreateSkillDefinition(rig);
        rig.Brain.TryPlayChainSkill(7, skillDef, 0f, requestAdvanceMoment: true, advancePointNormalized: 0f);
        rig.ClearSignals();

        rig.Tick();
        rig.Tick();

        Assert.That(rig.Signals, Is.EqualTo(new[]
        {
            "ChainSkill:CastMoment:7",
            "ChainSkill:AdvanceMoment:7",
        }));
    }

    // ---- Exclusive-state invariants -------------------------------------------------------------

    [Test]
    public void StageIntroRestoresApplyRootMotionWhenItExits()
    {
        Rig rig = CreateRig();
        rig.Tick();
        rig.Animator.applyRootMotion = true;

        Assert.That(rig.Brain.TryPlayStageIntro(), Is.True);
        Assert.That(rig.Animator.applyRootMotion, Is.False, "The intro pose is always root-motion free.");
        Assert.That(rig.Brain.IsStageIntroPlaybackActive, Is.True);

        rig.Brain.StopStageIntro();

        Assert.That(rig.Brain.IsStageIntroPlaybackActive, Is.False);
        Assert.That(rig.Animator.applyRootMotion, Is.True,
            "Every EnterExclusiveLocomotion must be paired with a restore when the state exits.");
        Assert.That(rig.Brain.IsExclusiveLocomotionActive, Is.False);
    }

    [Test]
    public void HardStatusLocomotionRestoresApplyRootMotionWhenItExits()
    {
        Rig rig = CreateRig();
        rig.Tick();
        rig.Animator.applyRootMotion = true;

        rig.SetStatusIntent(StatusLocomotionPose.Stun);
        Assert.That(rig.Brain.CurrentPlaybackKind, Is.EqualTo(CharacterAnimBrain.PlaybackKind.StatusEffect));
        Assert.That(rig.Animator.applyRootMotion, Is.False);

        rig.SetStatusIntent(StatusLocomotionPose.None);

        Assert.That(rig.Brain.CurrentPlaybackKind, Is.EqualTo(CharacterAnimBrain.PlaybackKind.None));
        Assert.That(rig.Animator.applyRootMotion, Is.True,
            "A hard status pose must hand root motion back to whoever owned it.");
        Assert.That(rig.Brain.IsExclusiveLocomotionActive, Is.False);
    }

    [Test]
    public void StageIntroInterruptsAnActiveSkillExactlyOnce()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlaySkill(101, null, 0f);
        rig.ClearSignals();

        rig.Brain.TryPlayStageIntro();

        Assert.That(rig.SkillInterruptedIds, Is.EqualTo(new[] { 101 }));
        Assert.That(rig.Signals, Does.Contain("Skill:Interrupted:101"));
        Assert.That(rig.Signals, Does.Contain("StageIntro:Started:0"));
    }

    // ---- Rebinding and teardown -------------------------------------------------------------------

    [Test]
    public void SwappingTheAnimProfileInterruptsActivePlaybackExactlyOnce()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlaySkill(101, null, 0f);
        rig.ClearSignals();

        rig.Brain.SetAnimProfileOverride(rig.CreateSecondProfile());
        rig.Tick();

        Assert.That(rig.SkillInterruptedIds, Is.EqualTo(new[] { 101 }));
        Assert.That(rig.Brain.IsSkillPlaybackActive, Is.False);

        // The rebound profile must still be able to drive playback.
        Assert.That(rig.Brain.TryPlaySkill(102, null, 0f), Is.True);
    }

    [Test]
    public void DisableInterruptsActivePlaybackWithoutDuplicateTerminals()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlaySkill(101, null, 0f);
        rig.Brain.TryPlayChainCutscene(1, rig.CutsceneA);
        rig.ClearSignals();

        // Edit Mode never calls OnDisable for a plain MonoBehaviour, so drive it directly.
        rig.Disable();

        Assert.That(rig.ChainInterruptedIds, Is.EqualTo(new[] { 1 }));
        Assert.That(rig.SkillInterruptedIds, Is.Empty,
            "The skill request was already interrupted when the chain took over.");
        Assert.That(rig.Brain.IsChainPlaybackActive, Is.False);

        rig.Disable();

        Assert.That(rig.ChainInterruptedIds, Is.EqualTo(new[] { 1 }),
            "A second teardown must not replay the terminal event.");
    }

    [Test]
    public void DisableReleasesAStateThatIsRefusingToExit()
    {
        // A full-body reload locks its own exit until CancelNow(). Teardown has to clear that lock,
        // or the FSM stays parked in reload and re-enabling never rebuilds it (the binding is
        // unchanged, so TryInitialize takes the fast path).
        Rig rig = CreateRig(profile =>
        {
            profile.reload = MakeClipTransition(createdObjects, "reload");
            profile.reloadBodyMode = CharacterAnimProfileSO.ReloadBodyMode.FullBody;
        });
        rig.Tick();

        rig.Brain.PlayReload(0.5f);
        Assert.That(rig.CurrentStateName, Is.EqualTo("Locomotion_Reload"),
            "Fixture problem: the full-body reload state did not take locomotion.");

        rig.Disable();

        Assert.That(rig.CurrentStateName, Is.Not.EqualTo("Locomotion_Reload"),
            "Disable must release a state that refuses to exit, not give up on the transition.");
        Assert.That(rig.Brain.IsExclusiveLocomotionActive, Is.False);
    }

    // ---- Root motion ownership ----------------------------------------------------------------

    [Test]
    public void SkillPublishesOneCoherentRootMotionPolicy()
    {
        Rig rig = CreateRig();

        rig.Brain.TryPlaySkill(101, null, 0f, null, usePlanarRootMotion: true);

        RootMotionPolicy policy = rig.Brain.RootMotion;
        Assert.That(policy.Active, Is.True);
        Assert.That(policy.PlanarOnly, Is.True);
        Assert.That(policy.ApplyYaw, Is.True, "A planar playback owns facing.");

        // The compatibility facade must never disagree with the policy it reads from.
        Assert.That(rig.Brain.RootMotionActive, Is.EqualTo(policy.Active));
        Assert.That(rig.Brain.RootMotionPlanarOnly, Is.EqualTo(policy.PlanarOnly));
        Assert.That(rig.Brain.RootMotionYawActive, Is.EqualTo(policy.ApplyYaw));
        Assert.That(rig.Brain.RootMotionIgnoresCharacterCollision,
            Is.EqualTo(policy.IgnoreCharacterCollision));
    }

    [Test]
    public void RootMotionPolicyIsClearedWhenThePlaybackEnds()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlaySkill(101, null, 0f, null, usePlanarRootMotion: true);

        rig.EndSkillClip();

        Assert.That(rig.Brain.RootMotion, Is.EqualTo(RootMotionPolicy.Inactive),
            "A finished playback must not leave a collision-ignore or planar flag behind.");
    }

    [Test]
    public void EveryPolicyChangeIsPublishedToAdapters()
    {
        Rig rig = CreateRig();

        var seen = new List<RootMotionPolicy>();
        System.Action<RootMotionPolicy> adapter = policy => seen.Add(policy);
        rig.Brain.RegisterRootMotionAdapter(adapter);

        Assert.That(seen.Count, Is.EqualTo(1),
            "Registering mid-flight must hand the adapter the current policy immediately.");
        Assert.That(seen[0], Is.EqualTo(RootMotionPolicy.Inactive));

        rig.Brain.TryPlaySkill(101, null, 0f, null, usePlanarRootMotion: true);
        Assert.That(seen[seen.Count - 1].Active, Is.True);
        Assert.That(seen[seen.Count - 1].PlanarOnly, Is.True);

        rig.EndSkillClip();
        Assert.That(seen[seen.Count - 1], Is.EqualTo(RootMotionPolicy.Inactive));

        rig.Brain.UnregisterRootMotionAdapter(adapter);
        int countAfterUnregister = seen.Count;

        rig.Brain.TryPlaySkill(102, null, 0f);
        Assert.That(seen.Count, Is.EqualTo(countAfterUnregister),
            "An unregistered adapter must stop receiving policy changes.");
    }

    [Test]
    public void ARegisteredAdapterTakesOverTheAnimatorFlagFromTheBrain()
    {
        Rig rig = CreateRig();

        // No adapter: the Brain writes the Animator itself, which is what an adapterless summon
        // or turret has always relied on.
        rig.Brain.TryPlaySkill(101, null, 0f);
        Assert.That(rig.Animator.applyRootMotion, Is.True);
        rig.EndSkillClip();
        Assert.That(rig.Animator.applyRootMotion, Is.False);

        System.Action<RootMotionPolicy> adapter = _ => { };
        rig.Brain.RegisterRootMotionAdapter(adapter);
        rig.Animator.applyRootMotion = false;

        rig.Brain.TryPlaySkill(102, null, 0f);

        Assert.That(rig.Brain.RootMotion.Active, Is.True, "The policy is still declared.");
        Assert.That(rig.Animator.applyRootMotion, Is.False,
            "With an adapter registered, only the adapter may write applyRootMotion.");

        rig.Brain.UnregisterRootMotionAdapter(adapter);
    }

    [Test]
    public void TheLastAdapterLeavingHandsTheAnimatorFlagBackToTheBrain()
    {
        Rig rig = CreateRig();

        System.Action<RootMotionPolicy> adapter = _ => { };
        rig.Brain.RegisterRootMotionAdapter(adapter);

        rig.Brain.TryPlaySkill(101, null, 0f);
        Assert.That(rig.Brain.RootMotion.Active, Is.True);
        Assert.That(rig.Animator.applyRootMotion, Is.False,
            "While an adapter owns the flag the Brain must not write it.");

        rig.Brain.UnregisterRootMotionAdapter(adapter);

        Assert.That(rig.Animator.applyRootMotion, Is.True,
            "Ownership came back to the Brain, so the Animator must match the declared policy "
            + "again instead of inheriting whatever the departing adapter left.");
    }

    [Test]
    public void TeardownClearsTheRootMotionPolicy()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlaySkill(101, null, 0f, null, usePlanarRootMotion: true);

        rig.Disable();

        Assert.That(rig.Brain.RootMotion, Is.EqualTo(RootMotionPolicy.Inactive));
    }

    // ---- Session lifecycle --------------------------------------------------------------------

    [Test]
    public void TwoTeardownPathsRacingTheSameRequestStillEmitOneTerminal()
    {
        // The session status is what makes this safe: an explicit interrupt and the state exiting
        // both try to close the same request in the same frame.
        Rig rig = CreateRig();
        rig.Brain.TryPlaySkill(101, null, 0f);
        rig.ClearSignals();

        rig.Brain.InterruptActivePlaybackForExternalControlLoss();
        rig.Brain.InterruptActivePlaybackForExternalControlLoss();

        Assert.That(rig.SkillInterruptedIds, Is.EqualTo(new[] { 101 }));
        Assert.That(rig.Signals, Is.EqualTo(new[] { "Skill:Interrupted:101" }));
    }

    [Test]
    public void ARequestNeverSeesBothCompletedAndInterrupted()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlayChainCutscene(1, rig.CutsceneA);
        rig.ClearSignals();

        rig.EndChainClip();
        rig.Brain.CancelChainPlaybackRequest(1);

        Assert.That(rig.ChainCompletedIds, Is.EqualTo(new[] { 1 }));
        Assert.That(rig.ChainInterruptedIds, Is.Empty,
            "Cancelling an already-completed request must not produce a second terminal.");
    }

    [Test]
    public void ChainCompletionDeliversEveryBeatItSkippedBeforeTheTerminal()
    {
        // The clip is torn down before its advance point, so both owed beats are delivered on the
        // way out, in clip order, and only then the terminal.
        Rig rig = CreateRig();
        SkillGemDefinition skillDef = CreateSkillDefinition(rig);
        rig.Brain.TryPlayChainSkill(7, skillDef, 0.5f, requestAdvanceMoment: true, advancePointNormalized: 0.9f);
        rig.ClearSignals();

        rig.EndChainClip();

        Assert.That(rig.Signals, Is.EqualTo(new[]
        {
            "ChainSkill:CastMoment:7",
            "ChainSkill:AdvanceMoment:7",
            "ChainSkill:Completed:7",
        }));
    }

    [Test]
    public void AnInterruptedChainIsNotOwedItsSkippedBeats()
    {
        Rig rig = CreateRig();
        SkillGemDefinition skillDef = CreateSkillDefinition(rig);
        rig.Brain.TryPlayChainSkill(7, skillDef, 0.5f, requestAdvanceMoment: true, advancePointNormalized: 0.9f);
        rig.ClearSignals();

        rig.Brain.CancelChainPlaybackRequest(7);

        Assert.That(rig.Signals, Is.EqualTo(new[] { "ChainSkill:Interrupted:7" }));
    }

    // ---- Binding / hot path -------------------------------------------------------------------

    [Test]
    public void BrainInitialisesFromTheContextBaseStatsAnimProfile()
    {
        ContextRig rig = CreateContextRig();

        Assert.That(rig.Brain.TryPlaySkill(101, null, 0f), Is.True,
            "A character with no inspector override must bind through ctx.baseStats.animProfile.");
        Assert.That(rig.Context.AnimBrain, Is.SameAs(rig.Brain),
            "Initialisation registers the Brain back into its context.");
    }

    [Test]
    public void SteadyStateTicksDoNotReResolveTheHierarchy()
    {
        // The whole point of the fast path: once bound, Update must not walk the hierarchy or call
        // ctx.ResolveReferences() again. Clearing the context's own back-reference is the cheapest
        // observable proof, because ResolveReferences() would immediately restore it.
        ContextRig rig = CreateContextRig();
        rig.Tick();

        rig.Context.AnimBrain = null;
        for (int i = 0; i < 5; i++)
            rig.Tick();

        Assert.That(rig.Context.AnimBrain, Is.Null,
            "A steady-state tick re-resolved the hierarchy; the binding fast path is not holding.");
    }

    [Test]
    public void InvalidatingTheBindingForcesOneFullResolve()
    {
        ContextRig rig = CreateContextRig();
        rig.Tick();
        rig.Context.AnimBrain = null;

        rig.Brain.InvalidateAnimationBinding();
        rig.Tick();

        Assert.That(rig.Context.AnimBrain, Is.SameAs(rig.Brain),
            "InvalidateAnimationBinding must force the next tick back onto the full resolve path.");

        rig.Context.AnimBrain = null;
        rig.Tick();

        Assert.That(rig.Context.AnimBrain, Is.Null,
            "The invalidation is consumed once; it must not pin the Brain to the slow path.");
    }

    [Test]
    public void SwappingTheBaseStatsAnimProfileRebindsWithoutExplicitInvalidation()
    {
        ContextRig rig = CreateContextRig();
        Assert.That(rig.Brain.TryPlaySkill(101, null, 0f), Is.True);

        rig.Stats.animProfile = rig.BuildSecondProfile();
        rig.Tick();

        Assert.That(rig.SkillInterruptedIds, Is.EqualTo(new[] { 101 }),
            "A profile swap must interrupt the playback that belonged to the old profile.");
        Assert.That(rig.Brain.TryPlaySkill(102, null, 0f), Is.True,
            "The rebound profile must still drive playback.");
    }

    [Test]
    public void AnimatorSwapRebindsWithoutExplicitInvalidation()
    {
        ContextRig rig = CreateContextRig();
        rig.Tick();
        Assert.That(rig.Brain.TryPlaySkill(101, null, 0f), Is.True);

        rig.RepointAnimancerAtANewAnimator();
        rig.Tick();

        Assert.That(rig.SkillInterruptedIds, Is.EqualTo(new[] { 101 }),
            "A rebuilt model must interrupt playback bound to the old Animator.");
        Assert.That(rig.Context.AnimBrain, Is.SameAs(rig.Brain));
    }

    // ---- Harness ------------------------------------------------------------------------------

    Rig CreateRig(System.Action<CharacterAnimProfileSO> configureProfile = null, bool configureBrain = true)
    {
        var rig = new Rig(createdObjects, configureProfile, configureBrain);
        return rig;
    }

    ContextRig CreateContextRig() => new(createdObjects);

    static SkillGemDefinition CreateSkillDefinition(Rig rig)
    {
        var def = ScriptableObject.CreateInstance<SkillGemDefinition>();
        def.name = "TestSkillGem";
        rig.Track(def);
        return def;
    }

    /// <summary>A minimal but real Brain: Animancer graph, authored profile, no character context.</summary>
    sealed class Rig
    {
        readonly List<Object> tracked;

        public readonly CharacterAnimBrain Brain;
        public readonly Animator Animator;
        public readonly ClipTransition CutsceneA;
        public readonly ClipTransition CutsceneB;

        public readonly List<string> Signals = new();
        public readonly List<int> ChainCompletedIds = new();
        public readonly List<int> ChainInterruptedIds = new();
        public readonly List<int> SkillInterruptedIds = new();
        public int SkillCompletedCount;

        public Rig(
            List<Object> tracked,
            System.Action<CharacterAnimProfileSO> configureProfile,
            bool configureBrain)
        {
            this.tracked = tracked;

            var go = new GameObject("CharacterAnimBrainSmokeTestRig");
            tracked.Add(go);

            Animator = go.AddComponent<Animator>();
            var animancer = go.AddComponent<AnimancerComponent>();
            animancer.Animator = Animator;

            Brain = go.AddComponent<CharacterAnimBrain>();
            typeof(CharacterAnimBrain).GetField("animancer", Hidden).SetValue(Brain, animancer);

            if (configureBrain)
            {
                CharacterAnimProfileSO profile = BuildProfile();
                configureProfile?.Invoke(profile);
                Brain.SetAnimProfileOverride(profile);
            }

            CutsceneA = MakeTransition("cutsceneA");
            CutsceneB = MakeTransition("cutsceneB");

            Brain.PlaybackEvent += s => Signals.Add($"{s.Kind}:{s.Phase}:{s.RequestId}");
            Brain.ChainPlaybackCompleted += id => ChainCompletedIds.Add(id);
            Brain.ChainPlaybackInterrupted += id => ChainInterruptedIds.Add(id);
            Brain.SkillCastInterrupted += id => SkillInterruptedIds.Add(id);
            Brain.SkillCompleted += () => SkillCompletedCount++;
        }

        public void Track(Object obj) => tracked.Add(obj);

        public void ClearSignals()
        {
            Signals.Clear();
            ChainCompletedIds.Clear();
            ChainInterruptedIds.Clear();
            SkillInterruptedIds.Clear();
            SkillCompletedCount = 0;
        }

        /// <summary>Runs one Brain frame. The Animancer graph does not advance; only polling does.</summary>
        public void Tick() => Invoke(Brain, "Update");

        /// <summary>Runs the Brain's teardown path.</summary>
        public void Disable() => Invoke(Brain, "OnDisable");

        /// <summary>Name of the active locomotion state, for assertions the public API cannot make.</summary>
        public string CurrentStateName
        {
            get
            {
                object stateMachine = typeof(CharacterAnimBrain).GetField("locomotionSM", Hidden).GetValue(Brain);
                object current = stateMachine.GetType().GetProperty("CurrentState").GetValue(stateMachine);
                return current == null ? "null" : current.GetType().Name;
            }
        }

        public void SetStatusIntent(StatusLocomotionPose pose) =>
            typeof(CharacterAnimBrain)
                .GetMethod("SetStatusLocomotionIntent", Hidden)
                .Invoke(Brain, new object[] { pose });

        /// <summary>Fires the end-of-clip callback the way the Animancer event sequence would.</summary>
        public void EndChainClip() => InvokeStateCallback("chain", "OnChainEnd");

        public void EndSkillClip() => InvokeStateCallback("skill", "OnSkillEnd");

        public void EndUtilityClip() => InvokeStateCallback("utility", "OnUtilityEnd");

        public CharacterAnimProfileSO CreateSecondProfile() => BuildProfile();

        void InvokeStateCallback(string stateField, string callback)
        {
            object state = typeof(CharacterAnimBrain).GetField(stateField, Hidden).GetValue(Brain);
            Assert.That(state, Is.Not.Null, $"Brain state '{stateField}' was never created.");
            Invoke(state, callback);
        }

        static void Invoke(object target, string method)
        {
            MethodInfo info = target.GetType().GetMethod(method, Hidden);
            Assert.That(info, Is.Not.Null, $"'{method}' is missing from {target.GetType().Name}.");
            info.Invoke(target, null);
        }

        CharacterAnimProfileSO BuildProfile() => BuildAnimProfile(tracked);

        ClipTransition MakeTransition(string clipName) => MakeClipTransition(tracked, clipName);
    }

    /// <summary>
    /// A Brain wired the way a real character prefab is: a concrete <see cref="CharacteContext"/>
    /// with <c>baseStats.animProfile</c>, no inspector override. Needed to observe whether the
    /// steady-state tick still walks the hierarchy.
    /// </summary>
    sealed class ContextRig
    {
        readonly List<Object> tracked;
        readonly AnimancerComponent animancer;

        public readonly CharacterAnimBrain Brain;
        public readonly EnemyContext Context;
        public readonly CharacterStats Stats;

        public readonly List<int> SkillInterruptedIds = new();

        public ContextRig(List<Object> tracked)
        {
            this.tracked = tracked;

            var go = new GameObject("CharacterAnimBrainContextRig");
            tracked.Add(go);

            Animator animator = go.AddComponent<Animator>();
            animancer = go.AddComponent<AnimancerComponent>();
            animancer.Animator = animator;

            Stats = ScriptableObject.CreateInstance<CharacterStats>();
            Stats.name = "SmokeTestCharacterStats";
            Stats.animProfile = BuildAnimProfile(tracked);
            tracked.Add(Stats);

            Context = go.AddComponent<EnemyContext>();
            Context.baseStats = Stats;

            Brain = go.AddComponent<CharacterAnimBrain>();
            typeof(CharacterAnimBrain).GetField("animancer", Hidden).SetValue(Brain, animancer);

            Brain.SkillCastInterrupted += id => SkillInterruptedIds.Add(id);
        }

        public void Tick() =>
            typeof(CharacterAnimBrain).GetMethod("Update", Hidden).Invoke(Brain, null);

        public CharacterAnimProfileSO BuildSecondProfile() => BuildAnimProfile(tracked);

        /// <summary>Stands in for <c>CharacterVisualController</c> rebuilding the model.</summary>
        public void RepointAnimancerAtANewAnimator()
        {
            var modelRoot = new GameObject("RebuiltModel");
            modelRoot.transform.SetParent(animancer.transform, false);
            tracked.Add(modelRoot);

            animancer.Animator = modelRoot.AddComponent<Animator>();
        }
    }

    static CharacterAnimProfileSO BuildAnimProfile(List<Object> tracked)
    {
        var profile = ScriptableObject.CreateInstance<CharacterAnimProfileSO>();
        profile.name = "SmokeTestAnimProfile";
        tracked.Add(profile);

        profile.locomotionDirectionalClips.idle = MakeClip(tracked, "idle");
        profile.locomotionDirectionalClips.forward = MakeClip(tracked, "forward");
        profile.locomotionDirectionalClips.backward = MakeClip(tracked, "backward");
        profile.locomotionDirectionalClips.left = MakeClip(tracked, "left");
        profile.locomotionDirectionalClips.right = MakeClip(tracked, "right");

        profile.skillClip = MakeClipTransition(tracked, "skill");
        profile.utilityWarpOutClip = MakeClipTransition(tracked, "warpOut");
        profile.utilityWarpOutCastPointNormalized = 0f;
        profile.utilityWarpInClip = MakeClipTransition(tracked, "warpIn");
        profile.utilityWarpInCastPointNormalized = 0f;
        profile.stageIntroClip = MakeClipTransition(tracked, "stageIntro");
        profile.stune = MakeClipTransition(tracked, "stun");
        profile.dead = MakeClipTransition(tracked, "dead");

        return profile;
    }

    static ClipTransition MakeClipTransition(List<Object> tracked, string clipName)
    {
        var transition = new ClipTransition();
        transition.Clip = MakeClip(tracked, clipName);
        return transition;
    }

    static AnimationClip MakeClip(List<Object> tracked, string clipName)
    {
        var clip = new AnimationClip { name = clipName };
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
        tracked.Add(clip);
        return clip;
    }
}
#endif
