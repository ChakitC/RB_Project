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

    [Test]
    public void SprintUsesRomaLoopAndReturnsToDirectionalLocomotion()
    {
        var roma = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterAnimProfileSO>(
            "Assets/Data/Animation/Roma/Roma_AnimProfile.asset");
        Assert.That(roma.sprint, Is.Not.Null);
        Assert.That(roma.sprint.Clip.name, Is.EqualTo("Roma_Run"));
        Assert.That(roma.sprint.Clip.isLooping, Is.True);
        Rig rig = CreateRig(profile => profile.sprint = roma.sprint);
        rig.Brain.IsSprinting = true;
        rig.Brain.MoveSpeed01 = 1f;
        rig.Brain.MoveDirLocal = Vector2.up;
        rig.Tick();
        var graph = rig.Brain.GetComponent<AnimancerComponent>();
        Assert.That(graph.Layers[0].CurrentState.Clip, Is.EqualTo(roma.sprint.Clip));
        Assert.That(rig.Brain.RootMotionActive, Is.False);
        var runState = graph.Layers[0].CurrentState;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4f;
            rig.Brain.MoveDirLocal = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            rig.Tick();
            Assert.That(graph.Layers[0].CurrentState, Is.SameAs(runState),
                "Turning during Sprint must neither select directional locomotion nor restart the run.");
        }
        rig.Brain.IsSprinting = false;
        rig.Tick();
        Assert.That(graph.Layers[0].CurrentState, Is.InstanceOf<Vector2MixerState>());
    }

    [Test]
    public void CrouchSlideClipsAndFallbackRetainLocomotionOwnership()
    {
        Rig rig = CreateRig();
        // Preserve a complete locomotion profile; only add the optional in-place transitions.
        var roma = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterAnimProfileSO>(
            "Assets/Data/Animation/Roma/Roma_AnimProfile.asset");
        var profile = Object.Instantiate(roma);
        createdObjects.Add(profile);
        profile.crouchIdle = rig.CutsceneA;
        profile.crouchMove = rig.CutsceneB;
        profile.slide = roma.sprint;
        rig.Brain.SetAnimProfileOverride(profile);
        var graph = rig.Brain.GetComponent<AnimancerComponent>();
        rig.Brain.IsCrouching = true;
        rig.Tick();
        Assert.That(graph.Layers[0].CurrentState.Clip, Is.EqualTo(profile.crouchIdle.Clip));
        rig.Brain.MoveSpeed01 = 1f;
        rig.Tick();
        Assert.That(graph.Layers[0].CurrentState.Clip, Is.EqualTo(profile.crouchMove.Clip));
        rig.Brain.IsSliding = true;
        rig.Tick();
        Assert.That(graph.Layers[0].CurrentState.Clip, Is.EqualTo(profile.slide.Clip));
        var slideState = graph.Layers[0].CurrentState;
        rig.Tick();
        Assert.That(graph.Layers[0].CurrentState, Is.SameAs(slideState));
        Assert.That(rig.Brain.RootMotionActive, Is.False);
        profile.slide = null;
        rig.Tick();
        Assert.That(graph.Layers[0].CurrentState, Is.InstanceOf<Vector2MixerState>());
        rig.Brain.IsSliding = false;
        rig.Brain.IsCrouching = false;
        rig.Tick();
        Assert.That(graph.Layers[0].CurrentState, Is.InstanceOf<Vector2MixerState>());
    }

    [Test]
    public void CutscenePhaseKeepsRequestAndRejectsOldCallbacks()
    {
        Rig rig = CreateRig(); var definition = CreateSkillDefinition(rig);
        typeof(SkillGemDefinition).GetField("isCutsceneSkill", Hidden).SetValue(definition, true);
        definition.CutsceneDef.characterCutsceneClip = rig.CutsceneA;
        rig.CutsceneA.Events.NormalizedEndTime = .4f;
        Assert.That(rig.Brain.TryPlaySkill(301, definition, .5f), Is.True);
        var graph = rig.Brain.GetComponent<AnimancerComponent>();
        var cutscene = graph.Layers[0].CurrentState;
        var oldEnd = cutscene.SharedEvents.OnEnd;
        oldEnd();
        var main = graph.Layers[0].CurrentState;
        Assert.That(main, Is.Not.SameAs(cutscene));
        Assert.That(rig.Signals, Does.Contain("Skill:Started:301"));
        Assert.That(rig.SkillCompletedCount, Is.Zero);
        oldEnd(); Assert.That(graph.Layers[0].CurrentState, Is.SameAs(main));
        main.SharedEvents.OnEnd();
        Assert.That(rig.SkillCompletedCount, Is.EqualTo(1));
        Assert.That(rig.Brain.RootMotionActive, Is.False);
    }

    [Test]
    public void DeadAndStageIntroHoldTheLastFrameWithTheirOwnExitRules()
    {
        Rig rig = CreateRig(); var graph = rig.Brain.GetComponent<AnimancerComponent>();
        Assert.That(rig.Brain.TryPlayStageIntro(), Is.True);
        var intro = graph.Layers[0].CurrentState;
        Assert.That(intro.Weight, Is.EqualTo(1f));
        intro.SharedEvents.OnEnd();
        Assert.That(intro.Speed, Is.Zero); Assert.That(intro.Time, Is.EqualTo(intro.Length));
        rig.Brain.StopStageIntro();
        Assert.That(rig.Brain.IsExclusiveLocomotionActive, Is.False);
        rig.Brain.PlayDead();
        var dead = graph.Layers[0].CurrentState;
        dead.SharedEvents.OnEnd();
        Assert.That(dead.Speed, Is.Zero); Assert.That(dead.Time, Is.EqualTo(dead.Length));
        Assert.That(rig.Brain.TryPlayStageIntro(), Is.False);
        Assert.That(rig.Brain.CurrentAnimationMode, Is.EqualTo(CharacterAnimationMode.Dead));
    }

    [Test]
    public void DashAndKnockbackKeepDurationDirectionAndGameplayCompletion()
    {
        Rig rig = CreateRig(profile =>
        {
            profile.dashF = MakeClipTransition(createdObjects, "forwardDash");
            profile.dashB = MakeClipTransition(createdObjects, "backwardDash");
            profile.knockback = MakeClipTransition(createdObjects, "knockback");
        });
        var graph = rig.Brain.GetComponent<AnimancerComponent>();
        rig.Brain.PlayDash(.2f, Vector2.down);
        var dash = graph.Layers[0].CurrentState;
        Assert.That(dash.Clip.name, Is.EqualTo("backwardDash"));
        Assert.That(dash.Speed, Is.EqualTo(dash.Length / .2f).Within(.001));
        graph.Evaluate(.05f);
        Assert.That(dash.Weight, Is.EqualTo(1f).Within(.001));
        dash.SharedEvents.OnEnd();
        Assert.That(rig.Brain.CurrentAnimationMode, Is.EqualTo(CharacterAnimationMode.Locomotion));
        Assert.That(rig.Brain.PlayKnockback(new KnockbackData(Vector3.forward, 1f, .4f, Vector3.zero)), Is.True);
        var knockback = graph.Layers[0].CurrentState;
        Assert.That(knockback.Speed, Is.EqualTo(knockback.Length / .4f).Within(.001));
        knockback.SharedEvents.OnEnd();
        Assert.That(knockback.Speed, Is.Zero);
        Assert.That(rig.Brain.CurrentAnimationMode, Is.EqualTo(CharacterAnimationMode.Knockback));
        rig.Brain.StopKnockbackPlayback();
        Assert.That(rig.Brain.IsExclusiveLocomotionActive, Is.False);
    }

    [Test]
    public void ExplicitCancelCallbackCanStartTheNextSkill()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlaySkill(101, null, 0f);
        bool accepted = false;
        rig.Brain.SkillCastInterrupted += id =>
        {
            if (id == 101) accepted = rig.Brain.TryPlaySkill(102, null, 0f);
        };
        rig.Brain.CancelSkillCastRequest(101);
        rig.Brain.CancelSkillCastRequest(101);
        Assert.That(accepted, Is.True);
        Assert.That(rig.Brain.IsSkillPlaybackActive, Is.True);
        Assert.That(rig.SkillInterruptedIds, Is.EqualTo(new[] { 101 }));
    }

    [Test]
    public void ThrowingPlaybackObserverDoesNotDropLegacyTerminal()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlaySkill(101, null, 0f);
        System.Action<CharacterAnimBrain.PlaybackSignal> broken = signal =>
        {
            if (signal.Phase == CharacterAnimBrain.PlaybackPhase.Interrupted)
                throw new System.InvalidOperationException("animation observer probe");
        };
        rig.Brain.PlaybackEvent += broken;
        UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,
            new System.Text.RegularExpressions.Regex("InvalidOperationException: animation observer probe"));
        try { Assert.DoesNotThrow(() => rig.Brain.CancelSkillCastRequest(101)); }
        finally { rig.Brain.PlaybackEvent -= broken; }
        Assert.That(rig.SkillInterruptedIds, Is.EqualTo(new[] { 101 }));
        Assert.That(rig.Brain.RootMotionActive, Is.False);
    }

    [Test]
    public void OldEndCallbackCannotFinishReusedRequestId()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlaySkill(101, null, 0f);
        var graph = rig.Brain.GetComponent<AnimancerComponent>();
        var oldEnd = graph.Layers[0].CurrentState.SharedEvents.OnEnd;
        rig.Brain.CancelSkillCastRequest(101);
        Assert.That(rig.Brain.TryPlaySkill(101, null, 0f), Is.True);
        oldEnd();
        Assert.That(rig.Brain.IsSkillPlaybackActive, Is.True);
    }

    [Test]
    public void UtilityCancelCallbackCanStartNextRequest()
    {
        Rig rig = CreateRig();
        Assert.That(rig.Brain.TryPlayUtilityWarpOut(41), Is.True);
        bool accepted = false;
        rig.Brain.SkillCastInterrupted += id => { if (id == 41) accepted = rig.Brain.TryPlayUtilityWarpOut(42); };
        rig.Brain.CancelUtilityCastRequest(40);
        Assert.That(rig.SkillInterruptedIds, Is.Empty);
        rig.Brain.CancelUtilityCastRequest(41);
        rig.Brain.CancelUtilityCastRequest(41);
        Assert.That(accepted, Is.True);
        Assert.That(rig.SkillInterruptedIds, Is.EqualTo(new[] { 41 }));
        Assert.That(rig.CurrentStateName, Is.EqualTo("Locomotion_Utility"));
    }

    [Test]
    public void ChainCancelCallbackCanStartNextRequest()
    {
        Rig rig = CreateRig();
        Assert.That(rig.Brain.TryPlayChainCutscene(41, rig.CutsceneA), Is.True);
        bool accepted = false;
        rig.Brain.ChainPlaybackInterrupted += id => { if (id == 41) accepted = rig.Brain.TryPlayChainCutscene(42, rig.CutsceneB); };
        rig.Brain.CancelChainPlaybackRequest(41);
        rig.Brain.CancelChainPlaybackRequest(41);
        Assert.That(accepted, Is.True);
        Assert.That(rig.ChainInterruptedIds, Is.EqualTo(new[] { 41 }));
        Assert.That(rig.Signals, Does.Contain("ChainCutscene:Started:42"));
    }

    [Test]
    public void ReentrantCancellationDrainsTerminalSignalsInOrder()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlaySkill(41, null, 0f);
        rig.ClearSignals();
        rig.Brain.PlaybackEvent += signal =>
        {
            if (signal.RequestId == 41 && signal.Phase == CharacterAnimBrain.PlaybackPhase.Interrupted)
            {
                rig.Brain.TryPlaySkill(42, null, 0f);
                rig.Brain.CancelSkillCastRequest(42);
            }
        };
        rig.Brain.CancelSkillCastRequest(41);
        Assert.That(rig.Signals, Is.EqualTo(new[] { "Skill:Interrupted:41", "Skill:Started:42", "Skill:Interrupted:42" }));
        Assert.That(rig.SkillInterruptedIds, Is.EqualTo(new[] { 41, 42 }));
        Assert.That(rig.Brain.IsSkillPlaybackActive, Is.False);
    }

    [Test]
    public void AuthoredEndCannotCompletePlaybackStartedByItsCallback()
    {
        CharacterAnimBrain target = null;
        Rig rig = CreateRig(profile => profile.skillClip.Events.OnEnd = () =>
        {
            target.CancelSkillCastRequest(41);
            target.TryPlaySkill(41, null, 0f);
        });
        target = rig.Brain;
        Assert.That(target.TryPlaySkill(41, null, 0f), Is.True);
        var oldEnd = target.GetComponent<AnimancerComponent>().Layers[0].CurrentState.SharedEvents.OnEnd;
        oldEnd();
        Assert.That(target.IsSkillPlaybackActive, Is.True);
        Assert.That(rig.SkillInterruptedIds, Is.EqualTo(new[] { 41 }));
        Assert.That(rig.SkillCompletedCount, Is.Zero);
    }

    [Test]
    public void SameReferenceInvalidationAdvancesBindingAndInvalidatesCallbacks()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlaySkill(41, null, 0f);
        var oldEnd = rig.Brain.GetComponent<AnimancerComponent>().Layers[0].CurrentState.SharedEvents.OnEnd;
        int generation = Diagnostic<int>(rig.Brain, "BindingGeneration");
        rig.Brain.InvalidateAnimationBinding();
        rig.Tick();
        Assert.That(Diagnostic<int>(rig.Brain, "BindingGeneration"), Is.GreaterThan(generation));
        Assert.That(rig.Brain.TryPlaySkill(41, null, 0f), Is.True);
        oldEnd();
        Assert.That(rig.Brain.IsSkillPlaybackActive, Is.True);
        Assert.That(rig.SkillInterruptedIds, Is.EqualTo(new[] { 41 }));
    }

    [Test]
    public void DisableRejectsReentrantPlaybackAndDrainsCleanup()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlaySkill(41, null, 0f);
        bool accepted = true;
        rig.Brain.SkillCastInterrupted += id => { if (id == 41) accepted = rig.Brain.TryPlaySkill(42, null, 0f); };
        rig.Disable();
        Assert.That(accepted, Is.False);
        Assert.That(rig.Brain.IsSkillPlaybackActive, Is.False);
        Assert.That(rig.Brain.RootMotionActive, Is.False);
        Assert.That(Diagnostic<int>(rig.Brain, "PendingCleanup"), Is.Zero);
        Assert.That(Diagnostic<int>(rig.Brain, "PendingSignals"), Is.Zero);
        Assert.That(Diagnostic<int>(rig.Brain, "CallbackCount"), Is.Zero);
        Assert.That(Diagnostic<int>(rig.Brain, "VfxSessions"), Is.Zero);
    }

    [Test]
    public void DeathTerminalObserversSeeTheNewPriority()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlaySkill(41, null, 0f);
        bool accepted = true;
        CharacterAnimationMode observed = default;
        rig.Brain.SkillCastInterrupted += id =>
        {
            observed = rig.Brain.CurrentAnimationMode;
            accepted = rig.Brain.TryPlaySkill(42, null, 0f);
        };
        rig.Brain.PlayDead();
        Assert.That(observed, Is.EqualTo(CharacterAnimationMode.Dead));
        Assert.That(accepted, Is.False);
    }

    [Test]
    public void ThrowingRootMotionObserverDoesNotDropTheCompletePolicy()
    {
        Rig rig = CreateRig();
        var seen = new List<RootMotionPolicy>();
        rig.Brain.RootMotionPolicyChanged += policy => { if (policy.Active) throw new System.InvalidOperationException("root policy probe"); };
        rig.Brain.RootMotionPolicyChanged += seen.Add;
        UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,
            new System.Text.RegularExpressions.Regex("InvalidOperationException: root policy probe"));
        rig.Brain.TryPlaySkill(41, null, 0f, null, true);
        Assert.That(seen.Exists(policy => policy.Active && policy.PlanarOnly && policy.ApplyYaw), Is.True);
        Assert.That(seen.Exists(policy => policy.Active && !policy.PlanarOnly), Is.False);
    }

    [Test]
    public void VfxSignalRetainsOldIdentityWhenObserverStartsAnotherSkill()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlaySkill(41, null, 0f);
        var seen = new List<int>();
        rig.Brain.SkillAnimationVfxCueRaised += cue =>
        {
            rig.Brain.CancelSkillCastRequest(41);
            rig.Brain.TryPlaySkill(42, null, 0f);
        };
        rig.Brain.SkillTimelineEventRaised += (id, name) => seen.Add(id);
        typeof(CharacterAnimBrain).GetMethod("RaiseSkillVfxTimelineEvent", Hidden).Invoke(rig.Brain, new object[] { 0 });
        Assert.That(seen, Is.EqualTo(new[] { 41 }));
        Assert.That(rig.Brain.IsSkillPlaybackActive, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReloadSharesSpeedAndRejectsOldCompletionAfterReplay(bool fullBody)
    {
        Rig rig = CreateRig(profile =>
        {
            profile.reload = MakeClipTransition(createdObjects, "reload");
            profile.reloadBodyMode = fullBody ? CharacterAnimProfileSO.ReloadBodyMode.FullBody : CharacterAnimProfileSO.ReloadBodyMode.UpperBody;
        });
        rig.Brain.PlayReload(0.5f);
        var state = rig.Brain.GetComponent<AnimancerComponent>().Layers[fullBody ? 0 : 1].CurrentState;
        var oldEnd = state.SharedEvents.OnEnd;
        Assert.That(state.Speed, Is.EqualTo(state.Length / 0.5f).Within(0.001));
        rig.Brain.StopReloadAction();
        rig.Brain.PlayReload(0.25f);
        oldEnd();
        Assert.That(state.Speed, Is.EqualTo(state.Length / 0.25f).Within(0.001));
        Assert.That(Diagnostic<int>(rig.Brain, "CallbackCount"), Is.GreaterThan(0));
        state.SharedEvents.OnEnd();
        Assert.That(Diagnostic<int>(rig.Brain, "CallbackCount"), Is.Zero);
        Assert.That(rig.Brain.IsExclusiveLocomotionActive, Is.False);
    }

    [Test]
    public void DestroyedModelReleasesSessionBeforeReplacementArrives()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlaySkill(41, null, 0f);
        var model = new GameObject("Replaceable model");
        model.transform.SetParent(rig.Brain.transform, false);
        rig.Brain.GetComponent<AnimancerComponent>().Animator = model.AddComponent<Animator>();
        rig.Brain.InvalidateAnimationBinding();
        rig.Tick();
        rig.ClearSignals();
        rig.Brain.TryPlaySkill(41, null, 0f);
        Object.DestroyImmediate(model);
        UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
            new System.Text.RegularExpressions.Regex("Initialization failed"));
        rig.Tick();
        Assert.That(rig.SkillInterruptedIds, Is.EqualTo(new[] { 41 }));
        Assert.That(rig.Brain.RootMotionActive, Is.False);
        Assert.That(Diagnostic<int>(rig.Brain, "CallbackCount"), Is.Zero);
        Assert.That(Diagnostic<int>(rig.Brain, "VfxSessions"), Is.Zero);
    }

    [Test]
    public void SerializedExecutionOrderMatchesMovementContract()
    {
        Rig rig = CreateRig();
        var driver = rig.Brain.gameObject.AddComponent<CharacterAnimDriver>();
        Assert.That(UnityEditor.MonoImporter.GetExecutionOrder(UnityEditor.MonoScript.FromMonoBehaviour(driver)), Is.EqualTo(1000));
        Assert.That(UnityEditor.MonoImporter.GetExecutionOrder(UnityEditor.MonoScript.FromMonoBehaviour(rig.Brain)), Is.EqualTo(1100));
    }

    // Driven by the Play Mode test assembly: an actual Update writer runs before Driver/Brain.
    public System.Collections.IEnumerator RuntimeMovementIntentOrder()
    {
        Rig rig = CreateRig(profile =>
        {
            profile.crawlMixer = profile.ResolveLocomotionMixer();
            profile.crawling = null;
        });
        rig.Tick();
        var hub = rig.Brain.gameObject.AddComponent<StateHub>();
        var driver = rig.Brain.gameObject.AddComponent<CharacterAnimDriver>();
        typeof(CharacterAnimDriver).GetField("hub", Hidden).SetValue(driver, hub);
        typeof(CharacterAnimDriver).GetField("brain", Hidden).SetValue(driver, rig.Brain);
        var type = System.Type.GetType("AnimationFrameTestDriver, StateHub.PlayMode.Tests", true);
        var probe = rig.Brain.gameObject.AddComponent(type);
        int samples = 0;
        Vector2 expected = default, actual = default, previous = default, sampledExpected = default, sampledPrevious = default;
        bool crawl = false;
        var graph = rig.Brain.GetComponent<AnimancerComponent>();
        type.GetField("BeforeUpdate").SetValue(probe, (System.Action)(() =>
        {
            previous = ((Vector2MixerState)graph.Layers[0].CurrentState).Parameter;
            Vector2 direction = (Time.frameCount & 1) == 0 ? Vector2.right : Vector2.up;
            hub.SetMoveSpeed01(0.8f);
            hub.SetMoveDirLocal(direction);
            float scale = crawl ? 0.35f : 1f;
            float sharpness = crawl ? 10f : 14f;
            float dt = (float)typeof(CharacterAnimBrain).GetProperty("AnimationDeltaTime", Hidden).GetValue(rig.Brain);
            expected = Vector2.Lerp(previous, direction * 0.8f * scale, 1f - Mathf.Exp(-sharpness * dt));
        }));
        type.GetField("AfterLateUpdate").SetValue(probe, (System.Action)(() =>
        {
            actual = ((Vector2MixerState)graph.Layers[0].CurrentState).Parameter;
            sampledExpected = expected;
            sampledPrevious = previous;
            samples++;
        }));
        for (int phase = 0; phase < 2; phase++)
        {
            int start = samples;
            while (samples < start + 3) yield return null;
            Assert.That(Vector2.Distance(sampledExpected, actual), Is.LessThan(0.0001f),
                crawl ? "Crawl did not consume this frame's movement exactly once." : "Live did not consume this frame's movement exactly once.");
            Assert.That(Vector2.Distance(actual, sampledPrevious), Is.GreaterThan(0.00001f));
            if (phase == 0) { crawl = true; rig.Brain.SetDowned(true); }
        }
        type.GetField("BeforeUpdate").SetValue(probe, null);
        type.GetField("AfterLateUpdate").SetValue(probe, null);
    }

    [Test]
    public void UtilityReplacementClosesOldSessionBeforeArmingNewOne()
    {
        Rig rig = CreateRig();
        rig.Brain.TryPlayUtilityWarpOut(41);
        var oldEnd = rig.Brain.GetComponent<AnimancerComponent>().Layers[0].CurrentState.SharedEvents.OnEnd;
        rig.ClearSignals();
        Assert.That(rig.Brain.TryPlayUtilityWarpOut(42), Is.True);
        oldEnd();
        Assert.That(rig.Signals, Is.EqualTo(new[] { "UtilityWarpOut:Interrupted:41", "UtilityWarpOut:Started:42" }));
        rig.EndUtilityClip();
        Assert.That(rig.Signals, Does.Contain("UtilityWarpOut:Completed:42"));
    }

    [Test]
    public void SpecialReactionCancelReportsItsReasonAfterRelease()
    {
        Rig rig = CreateRig(profile => profile.miniStune = MakeClipTransition(createdObjects, "mini stun"));
        var reasons = new List<CharacterAnimBrain.SpecialReactionInterruptReason>();
        rig.Brain.SpecialReactionInterrupted += (id, reason) => reasons.Add(reason);
        Assert.That(rig.Brain.TryPlaySpecialReaction(41, 0.2f), Is.True);
        rig.Brain.CancelSpecialReaction(41);
        rig.Brain.CancelSpecialReaction(41);
        Assert.That(reasons, Is.EqualTo(new[] { CharacterAnimBrain.SpecialReactionInterruptReason.Cancelled }));
        Assert.That(rig.Brain.RootMotionActive, Is.False);
        Assert.That(rig.Brain.IsSpecialReactionPlaybackActive, Is.False);
    }

    [Test]
    public void SkillVfxLeaseEndsBeforePublicTerminalObserversRun()
    {
        Rig rig = CreateRig();
        var definition = CreateSkillDefinition(rig);
        definition.ReplaceSkillVfxEvents(new List<SkillVfxEvent>
        {
            new SkillVfxEvent { cueIndex = 0, action = SkillVfxAction.StopLoop, loopKey = "probe" },
        });
        Assert.That(rig.Brain.TryPlaySkill(41, definition, 0f), Is.True);
        typeof(CharacterAnimBrain).GetMethod("RaiseSkillVfxTimelineEvent", Hidden).Invoke(rig.Brain, new object[] { 0 });
        Assert.That(Diagnostic<int>(rig.Brain, "VfxSessions"), Is.EqualTo(1));
        int remaining = -1;
        rig.Brain.PlaybackEvent += signal =>
        {
            if (signal.RequestId == 41 && signal.Phase == CharacterAnimBrain.PlaybackPhase.Interrupted)
                remaining = Diagnostic<int>(rig.Brain, "VfxSessions");
        };
        rig.Brain.CancelSkillCastRequest(41);
        Assert.That(remaining, Is.Zero);
        Assert.That(Diagnostic<int>(rig.Brain, "VfxSessions"), Is.Zero);
    }

    static T Diagnostic<T>(CharacterAnimBrain brain, string field)
    {
        object snapshot = typeof(CharacterAnimBrain).GetMethod("GetDiagnosticSnapshot", Hidden).Invoke(brain, null);
        return (T)snapshot.GetType().GetField(field, Hidden).GetValue(snapshot);
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
        // Keep the historic test entry point; explicit cancellation now publishes one terminal.
        Rig rig = CreateRig();
        rig.Brain.TryPlaySkill(101, null, 0f);
        rig.ClearSignals();

        rig.Brain.CancelSkillCastRequest(101);

        Assert.That(rig.Signals, Is.EqualTo(new[] { "Skill:Interrupted:101" }));
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

    [Test]
    public void ActiveChainPlaybackReportsRequestScopedTiming()
    {
        Rig rig = CreateRig();
        Assert.That(rig.Brain.TryPlayChainCutscene(11, rig.CutsceneA), Is.True);

        MethodInfo timingMethod = typeof(CharacterAnimBrain).GetMethod(
            "TryGetActiveChainPlaybackTiming",
            Hidden);
        Assert.That(timingMethod, Is.Not.Null);

        object[] matchingRequest = { 11, 0f, 0f };
        Assert.That((bool)timingMethod.Invoke(rig.Brain, matchingRequest), Is.True);
        Assert.That((float)matchingRequest[1], Is.GreaterThanOrEqualTo(0f));
        Assert.That((float)matchingRequest[2], Is.GreaterThan(0f));

        object[] staleRequest = { 12, 0f, 0f };
        Assert.That((bool)timingMethod.Invoke(rig.Brain, staleRequest), Is.False,
            "A previous Chain request must not drive visibility for the active playback.");
    }

    [Test]
    public void ActiveSkillPlaybackReportsRequestScopedTiming()
    {
        Rig rig = CreateRig();
        Assert.That(rig.Brain.TryPlaySkill(21, null, 0f), Is.True);

        MethodInfo timingMethod = typeof(CharacterAnimBrain).GetMethod(
            "TryGetActiveSkillPlaybackTiming",
            Hidden);
        Assert.That(timingMethod, Is.Not.Null);

        object[] matchingRequest = { 21, 0f, 0f };
        Assert.That((bool)timingMethod.Invoke(rig.Brain, matchingRequest), Is.True);
        Assert.That((float)matchingRequest[1], Is.GreaterThanOrEqualTo(0f));
        Assert.That((float)matchingRequest[2], Is.GreaterThan(0f));

        object[] staleRequest = { 22, 0f, 0f };
        Assert.That((bool)timingMethod.Invoke(rig.Brain, staleRequest), Is.False,
            "A previous Helper skill request must not drive visibility for the active playback.");
    }

    [TestCase(0.17f, 0.18f, 0.10f, 0.18f, false)]
    [TestCase(0.18f, 0.18f, 0.30f, 0.18f, false)]
    [TestCase(0.18f, 0.18f, 0.18f, 0.18f, true)]
    public void ChainAutoHideWaitsForVisibilityAndRemainingPlayback(
        float elapsed,
        float minimumVisibleDuration,
        float remainingDuration,
        float disappearDuration,
        bool expected)
    {
        MethodInfo shouldBeginMethod = typeof(CharacterVisibilityController).GetMethod(
            "ShouldBeginAutoHide",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(shouldBeginMethod, Is.Not.Null);
        Assert.That(
            (bool)shouldBeginMethod.Invoke(
                null,
                new object[]
                {
                    elapsed,
                    minimumVisibleDuration,
                    remainingDuration,
                    disappearDuration,
                }),
            Is.EqualTo(expected));
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
        public void EndChainClip() => EndCurrentClip();

        public void EndSkillClip() => EndCurrentClip();

        public void EndUtilityClip() => EndCurrentClip();

        public CharacterAnimProfileSO CreateSecondProfile() => BuildProfile();

        void EndCurrentClip() => Brain.GetComponent<AnimancerComponent>().Layers[0].CurrentState.SharedEvents.OnEnd();

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
