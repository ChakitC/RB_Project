#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Animancer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

public sealed class CommanBlockAnimationTests
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<Object> created = new();
    CharacterAnimBrain brain;
    AnimancerComponent animancer;
    CharacterAnimProfileSO profile;

    [SetUp]
    public void SetUp()
    {
        var go = new GameObject("CommanBlock test"); created.Add(go);
        var animator = go.AddComponent<Animator>();
        animancer = go.AddComponent<AnimancerComponent>(); animancer.Animator = animator;
        brain = go.AddComponent<CharacterAnimBrain>();
        typeof(CharacterAnimBrain).GetField("animancer", Hidden).SetValue(brain, animancer);
        profile = ScriptableObject.CreateInstance<CharacterAnimProfileSO>(); created.Add(profile);
        profile.upperBodyMask = new AvatarMask(); created.Add(profile.upperBodyMask);
        var clip = new AnimationClip { name = "CommanBlock test clip" }; created.Add(clip);
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 0));
        var dirs = profile.locomotionDirectionalClips;
        dirs.idle = dirs.forward = dirs.backward = dirs.left = dirs.right = clip;
        profile.commanBlock = new ClipTransition { Clip = clip };
        profile.block.beginClip = profile.block.impactClip = clip;
        brain.SetAnimProfileOverride(profile);
        Assert.That(Invoke("TryInitialize"), Is.True);
    }

    [TearDown]
    public void TearDown()
    {
        // Destroy the graph before the clips and profile it references.
        Object.DestroyImmediate(brain.gameObject);
        for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    object NewPlayback() => typeof(CharacterAnimBrain).GetMethod("CreatePlayback", Hidden).Invoke(brain, null);
    object PlaybackOptions(ClipTransition transition)
    {
        var type = typeof(CharacterAnimBrain).Assembly.GetType("AnimationPlaybackOptions", true);
        var options = System.Activator.CreateInstance(type);
        type.GetField("Transition", Hidden).SetValue(options, transition);
        type.GetField("InvokeAuthoredEnd", Hidden).SetValue(options, true);
        return options;
    }
    AnimancerState StartPlayback(object playback, object options, System.Action completed)
        => (AnimancerState)playback.GetType().GetMethod("Play", Hidden).Invoke(playback, new object[] { options, null, completed });
    void StopPlayback(object playback) => playback.GetType().GetMethod("Stop", Hidden).Invoke(playback, null);

    [Test]
    public void PlaybackSlotReplayPreservesTimingAndRejectsOldCallback()
    {
        var slot = NewPlayback();
        var clip = NewTransition("Slot replay", .1f); clip.Events.NormalizedEndTime = .35f;
        var options = PlaybackOptions(clip); int completions = 0;
        var first = StartPlayback(slot, options, () => completions++);
        var oldEnd = first.SharedEvents.OnEnd;
        StopPlayback(slot); StopPlayback(slot);
        var second = StartPlayback(slot, options, () => completions++);
        oldEnd();
        Assert.That(completions, Is.Zero);
        Assert.That(second.NormalizedEndTime, Is.EqualTo(.35f));
        second.SharedEvents.OnEnd();
        Assert.That(completions, Is.EqualTo(1));
        StopPlayback(slot);
    }

    [Test]
    public void StaleSlotStopCannotClearAnotherSlotOnTheSameState()
    {
        var oldSlot = NewPlayback(); var newSlot = NewPlayback();
        var options = PlaybackOptions(NewTransition("Shared state", .1f)); int completions = 0;
        var oldState = StartPlayback(oldSlot, options, null);
        var current = StartPlayback(newSlot, options, () => completions++);
        Assert.That(current, Is.SameAs(oldState));
        StopPlayback(oldSlot);
        Assert.That(current.SharedEvents, Is.Not.Null);
        current.SharedEvents.OnEnd();
        Assert.That(completions, Is.EqualTo(1));
        StopPlayback(newSlot);
    }

    [Test]
    public void AuthoredEndCanReplaySlotWithoutCompletingTheNewPlayback()
    {
        var slot = NewPlayback(); int oldComplete = 0, newComplete = 0;
        var next = PlaybackOptions(NewTransition("Next slot", .1f));
        AnimancerState replacement = null;
        var clip = NewTransition("Authored replay", .1f);
        clip.Events.OnEnd = () => replacement = StartPlayback(slot, next, () => newComplete++);
        var state = StartPlayback(slot, PlaybackOptions(clip), () => oldComplete++);
        state.SharedEvents.OnEnd();
        Assert.That(oldComplete, Is.Zero); Assert.That(newComplete, Is.Zero);
        replacement.SharedEvents.OnEnd(); Assert.That(newComplete, Is.EqualTo(1));
        StopPlayback(slot);
    }

    [Test]
    public void SharedTransitionDoesNotShareRuntimeCallbacksBetweenActors()
    {
        var other = new CommanBlockAnimationTests(); other.SetUp();
        try
        {
            var transition = NewTransition("Shared actor clip", .1f);
            var one = NewPlayback(); var two = other.NewPlayback(); int first = 0, second = 0;
            var a = StartPlayback(one, PlaybackOptions(transition), () => first++);
            var b = other.StartPlayback(two, other.PlaybackOptions(transition), () => second++);
            StopPlayback(one); b.SharedEvents.OnEnd();
            Assert.That(first, Is.Zero); Assert.That(second, Is.EqualTo(1));
            Assert.That(transition.Events.OnEnd, Is.Null);
            other.StopPlayback(two);
        }
        finally { other.TearDown(); }
    }

    [Test]
    public void TimelineReentrySkipsRemainingOldSubscribers()
    {
        var slot = NewPlayback(); int staleCalls = 0;
        var next = PlaybackOptions(NewTransition("Replacement", .1f));
        var clip = NewTransition("Reentrant timeline", .1f);
        System.Action callbacks = () => StartPlayback(slot, next, null);
        callbacks += () => staleCalls++;
        clip.Events.Add(.2f, callbacks);
        var state = StartPlayback(slot, PlaybackOptions(clip), null);
        var oldEvent = state.SharedEvents[0].callback;
        oldEvent(); oldEvent();
        Assert.That(staleCalls, Is.Zero);
        StopPlayback(slot);
    }

    [Test]
    public void ThrowingAuthoredSubscriberDoesNotDropCompletion()
    {
        var slot = NewPlayback(); int received = 0;
        var clip = NewTransition("Throwing end", .1f);
        clip.Events.OnEnd = () => throw new System.InvalidOperationException("slot observer failure");
        clip.Events.OnEnd += () => received++;
        UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,
            new System.Text.RegularExpressions.Regex("slot observer failure"));
        var state = StartPlayback(slot, PlaybackOptions(clip), () => received++);
        var end = state.SharedEvents.OnEnd;
        end(); end();
        Assert.That(received, Is.EqualTo(2));
        StopPlayback(slot);
    }

    [Test]
    public void SlotVfxCleanupIsIdempotentAndIsolatedBetweenActors()
    {
        var other = new CommanBlockAnimationTests(); other.SetUp();
        try
        {
            var track = new AnimationVfxTrack();
            track.ReplaceCues(new[] { new AnimationVfxCue { action = AnimationVfxAction.StopLoop, loopKey = "probe" } });
            var transition = NewTransition("Shared VFX clip", .1f);
            var options = PlaybackOptions(transition);
            options.GetType().GetField("Vfx", Hidden).SetValue(options, track);
            var one = NewPlayback(); var two = other.NewPlayback();
            StartPlayback(one, options, null); other.StartPlayback(two, options, null);
            Assert.That(VfxCount(), Is.EqualTo(1)); Assert.That(other.VfxCount(), Is.EqualTo(1));
            one.GetType().GetMethod("EndVfx", Hidden).Invoke(one, null);
            StopPlayback(one); StopPlayback(one);
            Assert.That(VfxCount(), Is.Zero); Assert.That(other.VfxCount(), Is.EqualTo(1));
            other.StopPlayback(two); Assert.That(other.VfxCount(), Is.Zero);
        }
        finally { other.TearDown(); }
    }

    int VfxCount()
    {
        object snapshot = Invoke("GetDiagnosticSnapshot");
        return (int)snapshot.GetType().GetField("VfxSessions", Hidden).GetValue(snapshot);
    }

    [Test]
    public void StartsImmediatelyAndFinishesWithoutTakingLocomotion()
    {
        bool rootMotion = animancer.Animator.applyRootMotion;
        Assert.That(brain.TryPlayCommanBlock(), Is.True);
        var fullBodyMask = animancer.Layers[2].Mask;
        Assert.That(fullBodyMask, Is.Not.SameAs(profile.upperBodyMask));
        // Every humanoid body part must be included by the command's owned mask.
        if (fullBodyMask != null)
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                Assert.That(fullBodyMask.GetHumanoidBodyPartActive((AvatarMaskBodyPart)i), Is.True);
        Assert.That(brain.CurrentAnimationMode, Is.EqualTo(CharacterAnimationMode.Locomotion));
        var state = animancer.Layers[2].CurrentState;
        Assert.That(state.Clip, Is.SameAs(profile.commanBlock.Clip));
        Assert.That(state.NormalizedTime, Is.EqualTo(0));
        Assert.That(animancer.Animator.applyRootMotion, Is.EqualTo(rootMotion));
        animancer.Evaluate(0.1f);
        state.Events(brain).OnEnd();
        var sm = typeof(CharacterAnimBrain).GetField("actionSM", Hidden).GetValue(brain);
        Assert.That(sm.GetType().GetProperty("CurrentState").GetValue(sm).GetType().Name, Is.EqualTo("Action_Empty"));
        Assert.That(animancer.Layers[1].Mask, Is.SameAs(profile.upperBodyMask));
        Assert.That(animancer.Layers[2].Mask, Is.SameAs(fullBodyMask));
        Assert.That(animancer.Layers[2].Weight, Is.GreaterThan(0f), "The outgoing full-body pose must survive completion.");
        Assert.That(animancer.Layers[2].TargetWeight, Is.Zero);
    }

    [Test]
    public void MissingClipDoesNotStartGesture()
    {
        profile.commanBlock = null;
        Assert.That(brain.TryPlayCommanBlock(), Is.False);
    }

    [Test]
    public void SelfGuardTakesPriorityOverCommandGesture()
    {
        Assert.That(brain.TryPlayCommanBlock(), Is.True);
        var begin = typeof(CharacterAnimBrain).GetMethod("TryBeginBlock", Hidden, null, new[] { typeof(int) }, null);
        Assert.That(begin.Invoke(brain, new object[] { 7 }), Is.True);
        Assert.That(brain.TryPlayCommanBlock(), Is.False);
        Assert.That(brain.CurrentAnimationMode, Is.EqualTo(CharacterAnimationMode.Block));
        Assert.That(animancer.Layers[1].Mask, Is.SameAs(profile.upperBodyMask), "Interruption must restore the action mask.");
    }

    [Test]
    public void RomaProfileUsesRequestedAsset()
    {
        var roma = AssetDatabase.LoadAssetAtPath<CharacterAnimProfileSO>("Assets/Data/Animation/Roma/Roma_AnimProfile.asset");
        Assert.That(roma.commanBlock?.Clip?.name, Is.EqualTo("Roma_CommanBlock"));
        Assert.That(AssetDatabase.GetAssetPath(roma.commanBlock.Clip), Is.EqualTo("Assets/Animation/Ch_Roma_Anim/Roma_CommanBlock.fbx"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MovementAndRotationLockReleasesWithoutClearingOtherOwners(bool disable)
    {
        var ctx = brain.gameObject.AddComponent<PlayerContext>();
        var hub = brain.gameObject.AddComponent<StateHub>();
        ctx.stateHub = hub;
        // Edit Mode does not invoke Awake; admission queries require initialization.
        typeof(StateHub).GetMethod("Awake", Hidden).Invoke(hub, null);
        typeof(CharacterAnimBrain).GetField("ctx", Hidden).SetValue(brain, ctx);
        int other = hub.AcquireExternalControlBlockToken(ControlBlockFlags.Rotate);
        Assert.That(brain.TryPlayCommanBlock(), Is.True);
        Assert.That(hub.ActiveControlBlockFlags, Is.EqualTo(ControlBlockFlags.Move | ControlBlockFlags.Rotate));
        Assert.That(hub.CanRotate(), Is.False);
        if (disable) typeof(CharacterAnimBrain).GetMethod("OnDisable", Hidden).Invoke(brain, null);
        else
        {
            animancer.Layers[2].CurrentState.Events(brain).OnEnd();
        }
        Assert.That(hub.ActiveControlBlockFlags, Is.EqualTo(ControlBlockFlags.Rotate));
        hub.ReleaseExternalControlBlockToken(other);
        Assert.That(hub.ActiveControlBlockFlags, Is.EqualTo(ControlBlockFlags.None));
        Assert.That(hub.CanRotate(), Is.True);
    }

    [Test]
    public void AuthoredEndTimeEndsGestureBeforeClipLength()
    {
        profile.commanBlock.Events.NormalizedEndTime = 0.35f;
        Assert.That(brain.TryPlayCommanBlock(), Is.True);
        Advance(0.25f);
        Assert.That(CurrentActionName(), Is.EqualTo("Action_CommanBlock"));
        Advance(0.2f);
        Assert.That(CurrentActionName(), Is.EqualTo("Action_Empty"));
    }

    [Test]
    public void ReplayedLocomotionReactionKeepsAuthoredEndTimeAfterCancel()
    {
        profile.miniStune = NewTransition("Reaction", 0.1f);
        profile.miniStune.Events.NormalizedEndTime = 0.35f;
        for (int replay = 0; replay < 3; replay++)
        {
            Assert.That(brain.TryPlaySpecialReaction(71, 0.6f), Is.True);
            var state = animancer.Layers[0].CurrentState;
            Assert.That(state.NormalizedEndTime, Is.EqualTo(0.35f).Within(0.001f));
            brain.CancelSpecialReaction(71);
        }
        Assert.That(profile.miniStune.Events.NormalizedEndTime, Is.EqualTo(0.35f));
    }

    [Test]
    public void ReplayedGestureKeepsAuthoredEndTimeAfterCleanup()
    {
        profile.commanBlock.Events.NormalizedEndTime = 0.35f;
        for (int replay = 0; replay < 3; replay++)
        {
            Assert.That(brain.TryPlayCommanBlock(), Is.True);
            var state = animancer.Layers[2].CurrentState;
            Assert.That(state.NormalizedEndTime, Is.EqualTo(0.35f).Within(0.001f),
                $"Playback {replay} must restore authored timing after the previous callbacks were released.");
            Advance(0.4f);
            Assert.That(CurrentActionName(), Is.EqualTo("Action_Empty"));
            // Alternate replay during the outgoing fade and after it has finished.
            if (replay == 1) Advance(0.3f);
        }
        Assert.That(profile.commanBlock.Events.NormalizedEndTime, Is.EqualTo(0.35f));
    }

    [Test]
    public void UpperActionStartsDuringFullBodyFadeWithoutChangingMasks()
    {
        Assert.That(brain.TryPlayCommanBlock(), Is.True);
        Advance(0.1f);
        var mask = animancer.Layers[2].Mask;
        animancer.Layers[2].CurrentState.Events(brain).OnEnd();
        var upper = PlayUpper(NewTransition("Upper", 0.3f), PairOffsetUpperAction.ShootHold);
        Advance(0.02f);
        Assert.That(animancer.Layers[2].Weight, Is.GreaterThan(0f));
        Assert.That(animancer.Layers[1].Weight, Is.GreaterThan(0f));
        Assert.That(upper.IsPlaying, Is.True);
        Assert.That(animancer.Layers[1].Mask, Is.SameAs(profile.upperBodyMask));
        Assert.That(animancer.Layers[2].Mask, Is.SameAs(mask));
        Advance(0.2f);
        Assert.That(animancer.Layers[2].Weight, Is.Zero);
        Assert.That(animancer.Layers[1].Weight, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void ReplayingGestureDuringFadeCancelsPreviousExit()
    {
        Assert.That(brain.TryPlayCommanBlock(), Is.True);
        Advance(0.1f);
        animancer.Layers[2].CurrentState.Events(brain).OnEnd();
        Assert.That(brain.TryPlayCommanBlock(), Is.True);
        Advance(0.15f);
        Assert.That(CurrentActionName(), Is.EqualTo("Action_CommanBlock"));
        Assert.That(animancer.Layers[2].Weight, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void PairOffsetsBlendBothActionsAndRespectFullBodyCoverage()
    {
        profile.pairOffsetProfiles = ScriptableObject.CreateInstance<PairOffsetProfilesSO>();
        created.Add(profile.pairOffsetProfiles);
        var oldState = PlayUpper(NewTransition("Hold", 0.4f), PairOffsetUpperAction.ShootHold);
        Advance(0.1f);
        var newState = PlayUpper(NewTransition("Reload", 0.4f), PairOffsetUpperAction.Reload);
        Advance(0.1f);
        var poses = new List<PairOffsetBasePoseWeight>();
        var actions = new List<PairOffsetActionWeight>();
        Assert.That(brain.TryGetPairOffsetContributions(poses, actions, out _), Is.True);
        Assert.That(actions.Count, Is.EqualTo(2));
        Assert.That(actions.Find(x => x.Action == PairOffsetUpperAction.ShootHold).Weight,
            Is.EqualTo(oldState.Weight * animancer.Layers[1].Weight).Within(0.001f));
        Assert.That(actions.Find(x => x.Action == PairOffsetUpperAction.Reload).Weight,
            Is.EqualTo(newState.Weight * animancer.Layers[1].Weight).Within(0.001f));
        Assert.That(brain.TryPlayCommanBlock(), Is.True);
        Advance(0.1f);
        Assert.That(brain.TryGetPairOffsetContributions(poses, actions, out _), Is.False,
            "Upper-body offsets cannot rotate bones over a fully weighted full-body pose.");
    }

    [Test]
    public void SlidePairOffsetsUseSavedClipProfileAndRestoreLocomotion()
    {
        profile.slide = NewTransition("Slide", 0f);
        profile.shootHoldLoop = NewTransition("Slide hold", 0f);
        profile.locomotionDirectionalClips.forward = NewTransition("Forward", 0f).Clip;
        profile.pairOffsetProfiles = ScriptableObject.CreateInstance<PairOffsetProfilesSO>();
        created.Add(profile.pairOffsetProfiles);
        var saved = profile.pairOffsetProfiles.UpsertProfile(true, "Saved slide",
            PairOffsetBasePose.None, PairOffsetUpperAction.None,
            profile.slide.Clip, profile.shootHoldLoop.Clip, 1f);
        saved.AddBoneOffset(true, "OffsetBone", new Vector3(0, 0, 35), 1f);
        profile.pairOffsetProfiles.UpsertProfile(true, "Forward",
            PairOffsetBasePose.Forward, PairOffsetUpperAction.ShootHold,
            profile.locomotionDirectionalClips.forward, profile.shootHoldLoop.Clip, 1f)
            .AddBoneOffset(true, "OffsetBone", new Vector3(0, 0, 5), 1f);
        var bone = new GameObject("OffsetBone");
        bone.transform.SetParent(brain.transform, false);
        PlayUpper(profile.shootHoldLoop, PairOffsetUpperAction.ShootHold);
        var locomotion = typeof(CharacterAnimBrain).GetField("locomotion", Hidden).GetValue(brain);
        var update = locomotion.GetType().GetMethod("Update");
        var enter = locomotion.GetType().GetMethod("OnEnterState");
        var poses = new List<PairOffsetBasePoseWeight>();
        var actions = new List<PairOffsetActionWeight>();
        var applier = brain.gameObject.AddComponent<CharacterPairOffsetApplier>();
        typeof(CharacterPairOffsetApplier).GetMethod("OnEnable", Hidden).Invoke(applier, null);
        var late = typeof(CharacterPairOffsetApplier).GetMethod("LateUpdate", Hidden);

        brain.IsSliding = true;
        update.Invoke(locomotion, null);
        Advance(0.2f);
        Assert.That(brain.TryGetPairOffsetContributions(poses, actions, out _), Is.True);
        Assert.That(poses.Count, Is.EqualTo(1));
        Assert.That(poses[0].Pose, Is.EqualTo(PairOffsetBasePose.Slide));
        Assert.That(profile.FindPairOffsetProfile(poses[0].Pose, actions[0].Action, false), Is.SameAs(saved));
        bone.transform.localRotation = Quaternion.identity;
        late.Invoke(applier, null);
        Assert.That(Mathf.DeltaAngle(35, bone.transform.localEulerAngles.z), Is.EqualTo(0).Within(0.01));

        // Re-entering locomotion during a slide must select Slide immediately too.
        enter.Invoke(locomotion, null);
        brain.TryGetPairOffsetContributions(poses, actions, out _);
        Assert.That(poses[0].Pose, Is.EqualTo(PairOffsetBasePose.Slide));
        brain.IsSliding = false;
        update.Invoke(locomotion, null);
        Advance(0.5f);
        brain.TryGetPairOffsetContributions(poses, actions, out _);
        Assert.That(poses.Exists(x => x.Pose == PairOffsetBasePose.Slide), Is.False);
        enter.Invoke(locomotion, null);
        ((Vector2MixerState)animancer.Layers[0].CurrentState).Parameter = Vector2.zero;
        Advance(0.5f);
        bone.transform.localRotation = Quaternion.identity;
        late.Invoke(applier, null);
        brain.TryGetPairOffsetContributions(poses, actions, out _);
        Assert.That(poses[0].Pose, Is.EqualTo(PairOffsetBasePose.Idle));
        Assert.That(Quaternion.Angle(Quaternion.identity, bone.transform.localRotation), Is.LessThan(0.01f));

        brain.IsSliding = true;
        profile.slide = new ClipTransition();
        enter.Invoke(locomotion, null);
        Advance(0.5f);
        brain.TryGetPairOffsetContributions(poses, actions, out _);
        Assert.That(poses[0].Pose, Is.EqualTo(PairOffsetBasePose.Idle), "Missing slide clip must keep mixer offsets.");
    }

    [Test]
    public void SlidePairOffsetsPreferExplicitKeysAndSupportPulseFallback()
    {
        profile.slide = NewTransition("Slide", 0f);
        profile.shootPulse = NewTransition("Slide pulse", 0f);
        profile.pairOffsetProfiles = ScriptableObject.CreateInstance<PairOffsetProfilesSO>();
        created.Add(profile.pairOffsetProfiles);
        var legacy = profile.pairOffsetProfiles.UpsertProfile(true, "Legacy slide",
            PairOffsetBasePose.None, PairOffsetUpperAction.None,
            profile.slide.Clip, profile.shootPulse.Clip, 1f);
        Assert.That(profile.FindPairOffsetProfile(PairOffsetBasePose.Slide, PairOffsetUpperAction.ShootPulse, false), Is.SameAs(legacy));
        var keyed = profile.pairOffsetProfiles.UpsertProfile(true, "Keyed slide",
            PairOffsetBasePose.Slide, PairOffsetUpperAction.ShootPulse, null, null, 1f);
        Assert.That(profile.FindPairOffsetProfile(PairOffsetBasePose.Slide, PairOffsetUpperAction.ShootPulse, false), Is.SameAs(keyed));
    }

    [Test]
    public void PairOffsetsUseNewBonePoseAndDoNotAccumulateBetweenEvaluations()
    {
        var bone = new GameObject("OffsetBone");
        bone.transform.SetParent(brain.transform, false);
        profile.pairOffsetProfiles = ScriptableObject.CreateInstance<PairOffsetProfilesSO>();
        created.Add(profile.pairOffsetProfiles);
        profile.pairOffsetProfiles.UpsertProfile(true, "Probe", PairOffsetBasePose.Idle,
            PairOffsetUpperAction.ShootHold, null, null, 1f)
            .AddBoneOffset(true, "OffsetBone", new Vector3(0, 0, 10), 1f);
        PlayUpper(NewTransition("Offset hold", 0.1f), PairOffsetUpperAction.ShootHold);
        Advance(0.2f);
        var applier = brain.gameObject.AddComponent<CharacterPairOffsetApplier>();
        typeof(CharacterPairOffsetApplier).GetMethod("OnEnable", Hidden).Invoke(applier, null);
        var late = typeof(CharacterPairOffsetApplier).GetMethod("LateUpdate", Hidden);
        animancer.Evaluate(0.01f);
        bone.transform.localRotation = Quaternion.Euler(0, 0, 20);
        late.Invoke(applier, null);
        Assert.That(Mathf.DeltaAngle(30, bone.transform.localEulerAngles.z), Is.EqualTo(0).Within(0.01));
        late.Invoke(applier, null);
        Assert.That(Mathf.DeltaAngle(30, bone.transform.localEulerAngles.z), Is.EqualTo(0).Within(0.01));
        animancer.Animator.updateMode = AnimatorUpdateMode.Fixed;
        animancer.Graph.PauseGraph();
        late.Invoke(applier, null);
        Assert.That(Mathf.DeltaAngle(30, bone.transform.localEulerAngles.z), Is.EqualTo(0).Within(0.01));
        // Manual evaluation while paused still produces a new pose and needs exactly one offset.
        animancer.Evaluate(0.01f);
        bone.transform.localRotation = Quaternion.Euler(0, 0, 40);
        late.Invoke(applier, null);
        Assert.That(Mathf.DeltaAngle(50, bone.transform.localEulerAngles.z), Is.EqualTo(0).Within(0.01));
    }

    [Test]
    public void TemporaryPoseSurvivesFadeAndIsDestroyedAfterwards()
    {
        var state = PlayTemporary();
        InvokePresentation("RetireTemporaryState", state);
        animancer.Layers[0].Play(NewTransition("Next", 0.2f));
        Advance(0.05f);
        Assert.That(state.Playable.IsValid(), Is.True);
        Assert.That(state.Weight, Is.GreaterThan(0f));
        Assert.That(state.Speed, Is.Zero);
        Advance(0.3f);
        Assert.That(state.Playable.IsValid(), Is.False);
    }

    [Test]
    public void ReclaimedTemporaryPoseIsNotDestroyedByOldCleanup()
    {
        var state = PlayTemporary();
        InvokePresentation("RetireTemporaryState", state);
        animancer.Layers[0].Play(NewTransition("Next", 0.2f));
        Advance(0.05f);
        PlayTemporaryState(state);
        Advance(0.3f);
        Assert.That(state.Playable.IsValid(), Is.True);
        Assert.That(state.Weight, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void DisableClearsFadesAndTemporaryPoses()
    {
        var state = PlayTemporary();
        InvokePresentation("RetireTemporaryState", state);
        Assert.That(brain.TryPlayCommanBlock(), Is.True);
        Advance(0.05f);
        Invoke("OnDisable");
        Assert.That(state.Playable.IsValid(), Is.False);
        Assert.That(animancer.Layers[1].Weight, Is.Zero);
        Assert.That(animancer.Layers[2].Weight, Is.Zero);
        Invoke("OnEnable");
        Assert.That(brain.TryPlayCommanBlock(), Is.True);
        Advance(0.1f);
        Assert.That(animancer.Layers[2].Weight, Is.GreaterThan(0f));
    }

    [Test]
    public void BlockExitKeepsSampledPoseFrozen()
    {
        var begin = typeof(CharacterAnimBrain).GetMethod("TryBeginBlock", Hidden, null, new[] { typeof(int) }, null);
        Assert.That(begin.Invoke(brain, new object[] { 7 }), Is.True);
        var state = animancer.Layers[0].CurrentState;
        state.NormalizedTime = 0.68f;
        Advance(0.1f);
        Invoke("EndBlock", 7, true);
        double time = state.Time;
        Advance(0.05f);
        Assert.That(state.Speed, Is.Zero);
        Assert.That(state.Time, Is.EqualTo(time).Within(0.0001));
    }

    [Test]
    public void FullBodyPoseBlendsAfterEndWithExcludedUpperMaskBone()
    {
        var leg = new GameObject("Leg");
        leg.transform.SetParent(brain.transform, false);
        profile.upperBodyMask.transformCount = 1;
        profile.upperBodyMask.SetTransformPath(0, "Leg");
        profile.upperBodyMask.SetTransformActive(0, false);
        var clip = new AnimationClip { name = "Leg gesture" }; created.Add(clip);
        clip.SetCurve("Leg", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 2f, 1f, 2f));
        profile.commanBlock = new ClipTransition { Clip = clip };
        profile.locomotionDirectionalClips.idle.SetCurve("Leg", typeof(Transform), "localPosition.x",
            AnimationCurve.Linear(0, 0, 1, 0));
        Assert.That(brain.TryPlayCommanBlock(), Is.True);
        Advance(0.1f);
        Assert.That(leg.transform.localPosition.x, Is.EqualTo(2f).Within(0.01f));
        animancer.Layers[2].CurrentState.Events(brain).OnEnd();
        Advance(0.02f);
        Assert.That(leg.transform.localPosition.x, Is.InRange(0.1f, 1.99f),
            "The excluded leg must blend toward Idle instead of snapping to it.");
        Advance(0.2f);
        Assert.That(leg.transform.localPosition.x, Is.EqualTo(0f).Within(0.01f));
    }

    [Test]
    public void RebindReleasesOldPresentationAndAllowsNewGesture()
    {
        Assert.That(brain.TryPlayCommanBlock(), Is.True);
        Advance(0.1f);
        var oldMask = animancer.Layers[2].Mask;
        var replacement = Object.Instantiate(profile); created.Add(replacement);
        brain.SetAnimProfileOverride(replacement);
        Assert.That(Invoke("TryInitialize"), Is.True);
        Assert.That(oldMask == null, Is.True);
        Assert.That(animancer.Layers[2].Weight, Is.Zero);
        Assert.That(brain.TryPlayCommanBlock(), Is.True);
        Advance(0.1f);
        Assert.That(animancer.Layers[2].Weight, Is.GreaterThan(0f));
    }

    [Test]
    public void AnimatedBoneKeepsCurrentPoseThroughOffsetsCoverageAndRebind()
    {
        var bone = new GameObject("OffsetBone");
        bone.transform.SetParent(brain.transform, false);
        var clip = new AnimationClip { name = "Moving offset bone" }; created.Add(clip);
        clip.SetCurve("OffsetBone", typeof(Transform), "localEulerAnglesRaw.z", AnimationCurve.Linear(0, 0, 1, 60));
        var pulseClip = Object.Instantiate(clip); created.Add(pulseClip);
        profile.pairOffsetProfiles = ScriptableObject.CreateInstance<PairOffsetProfilesSO>(); created.Add(profile.pairOffsetProfiles);
        profile.pairOffsetProfiles.UpsertProfile(true, "Hold", PairOffsetBasePose.Idle,
            PairOffsetUpperAction.ShootHold, profile.locomotionDirectionalClips.idle, clip, 1f).AddBoneOffset(true, "OffsetBone", new Vector3(0, 0, 10), 1f);
        profile.pairOffsetProfiles.UpsertProfile(true, "Pulse", PairOffsetBasePose.Idle,
            PairOffsetUpperAction.ShootPulse, profile.locomotionDirectionalClips.idle, pulseClip, 1f).AddBoneOffset(true, "OffsetBone", new Vector3(0, 0, 20), 1f);
        Assert.That(profile.pairOffsetProfiles.PairOffsetProfiles.Count, Is.EqualTo(2));
        PlayUpper(new ClipTransition { Clip = clip }, PairOffsetUpperAction.ShootHold);
        var applier = brain.gameObject.AddComponent<CharacterPairOffsetApplier>();
        var enable = typeof(CharacterPairOffsetApplier).GetMethod("OnEnable", Hidden);
        var late = typeof(CharacterPairOffsetApplier).GetMethod("LateUpdate", Hidden);
        enable.Invoke(applier, null);
        var poses = new List<PairOffsetBasePoseWeight>();
        var actions = new List<PairOffsetActionWeight>();
        float previousRaw = -1f;
        for (int i = 0; i < 6; i++)
        {
            animancer.Evaluate(0.05f);
            float raw = bone.transform.localEulerAngles.z;
            Assert.That(raw, Is.GreaterThan(previousRaw));
            previousRaw = raw;
            brain.TryGetPairOffsetContributions(poses, actions, out _);
            float expectedOffset = 0f;
            foreach (var action in actions) expectedOffset += action.Weight * (action.Action == PairOffsetUpperAction.ShootHold ? 10f : 20f);
            late.Invoke(applier, null);
            Assert.That(Mathf.DeltaAngle(raw + expectedOffset, bone.transform.localEulerAngles.z), Is.EqualTo(0).Within(0.1f));
            // No new graph evaluation: repeated render passes must leave the evaluated pose alone.
            late.Invoke(applier, null);
            Assert.That(Mathf.DeltaAngle(raw + expectedOffset, bone.transform.localEulerAngles.z), Is.EqualTo(0).Within(0.1f));
        }
        PlayUpper(new ClipTransition { Clip = pulseClip, FadeDuration = 0.2f }, PairOffsetUpperAction.ShootPulse);
        animancer.Evaluate(0.05f);
        float baseAngle = bone.transform.localEulerAngles.z;
        brain.TryGetPairOffsetContributions(poses, actions, out _);
        Assert.That(actions.Count, Is.EqualTo(2));
        float blend = 0f;
        foreach (var action in actions) blend += action.Weight * (action.Action == PairOffsetUpperAction.ShootHold ? 10f : 20f);
        late.Invoke(applier, null);
        Assert.That(Mathf.DeltaAngle(baseAngle + blend, bone.transform.localEulerAngles.z), Is.EqualTo(0).Within(0.1f));
        brain.TryPlayCommanBlock();
        animancer.Evaluate(0.2f);
        baseAngle = bone.transform.localEulerAngles.z;
        late.Invoke(applier, null);
        Assert.That(Mathf.DeltaAngle(baseAngle, bone.transform.localEulerAngles.z), Is.EqualTo(0).Within(0.1f));
        brain.InvalidateAnimationBinding();
        Assert.That(Invoke("TryInitialize"), Is.True);
        animancer.Evaluate(0f);
        float reboundPose = bone.transform.localEulerAngles.z;
        late.Invoke(applier, null);
        Assert.That(Mathf.DeltaAngle(reboundPose, bone.transform.localEulerAngles.z), Is.EqualTo(0).Within(0.01));
        Assert.That(brain.TryPlayCommanBlock(), Is.True);
    }

    [Test]
    public void PairOffsetFadeUsesEvaluatedStatesThroughInterruptionAndMissingProfiles()
    {
        profile.slide = NewTransition("Fade slide", 0.4f);
        profile.sprint = NewTransition("Fade sprint", 0.4f);
        profile.shootHoldLoop = NewTransition("Fade hold", 0f);
        profile.reload = NewTransition("Fade reload", 0.4f);
        profile.pairOffsetProfiles = ScriptableObject.CreateInstance<PairOffsetProfilesSO>();
        created.Add(profile.pairOffsetProfiles);
        profile.pairOffsetProfiles.UpsertProfile(true, "Idle hold", PairOffsetBasePose.Idle,
            PairOffsetUpperAction.ShootHold, null, null, 1).AddBoneOffset(true, "OffsetBone", new Vector3(0, 0, 10), 1);
        profile.pairOffsetProfiles.UpsertProfile(true, "Slide hold", PairOffsetBasePose.Slide,
            PairOffsetUpperAction.ShootHold, null, null, 1).AddBoneOffset(true, "OffsetBone", new Vector3(0, 0, 50), 1);
        profile.pairOffsetProfiles.UpsertProfile(true, "Slide reload", PairOffsetBasePose.Slide,
            PairOffsetUpperAction.Reload, null, null, 1).AddBoneOffset(true, "OffsetBone", new Vector3(0, 0, -20), 1);
        var bone = new GameObject("OffsetBone"); bone.transform.SetParent(brain.transform, false);
        var hold = PlayUpper(profile.shootHoldLoop, PairOffsetUpperAction.ShootHold);
        Advance(0.2f);
        var applier = brain.gameObject.AddComponent<CharacterPairOffsetApplier>();
        typeof(CharacterPairOffsetApplier).GetMethod("OnEnable", Hidden).Invoke(applier, null);
        var late = typeof(CharacterPairOffsetApplier).GetMethod("LateUpdate", Hidden);
        var play = Presentation.GetType().GetMethod("PlayLocomotionTransition", Hidden, null, new[] { typeof(ITransition) }, null);
        var idle = animancer.Layers[0].CurrentState;
        var slide = (AnimancerState)play.Invoke(Presentation, new object[] { profile.slide });
        AnimancerState reload = null;
        float Sample()
        {
            var poses = new List<PairOffsetBasePoseWeight>(); var actions = new List<PairOffsetActionWeight>();
            Assert.That(brain.TryGetPairOffsetContributions(poses, actions, out _), Is.True);
            Assert.That(poses.FindAll(x => x.Pose == PairOffsetBasePose.Slide).ConvertAll(x => x.Weight).Sum(), Is.EqualTo(slide.Weight).Within(0.0001f));
            float coverage = animancer.Layers[1].Weight * (1f - animancer.Layers[2].Weight);
            float expected = coverage * (hold.Weight * (idle.Weight * 10 + slide.Weight * 50) + (reload != null ? reload.Weight * slide.Weight * -20 : 0));
            bone.transform.localRotation = Quaternion.identity;
            late.Invoke(applier, null);
            Assert.That(Mathf.DeltaAngle(expected, bone.transform.localEulerAngles.z), Is.EqualTo(0).Within(0.01f));
            return expected;
        }
        Advance(0.01f);
        Assert.That(Sample(), Is.InRange(10f, 15f), "First fade sample must not jump straight to the slide correction.");
        Advance(0.08f); Sample();
        play.Invoke(Presentation, new object[] { profile.sprint }); // Third state has no profiles: identity contribution.
        reload = PlayUpper(profile.reload, PairOffsetUpperAction.Reload);
        for (int i = 0; i < 8; i++) { Advance(0.02f); Sample(); }
        play.Invoke(Presentation, new object[] { profile.slide }); // Reverse before either fade finishes.
        for (int i = 0; i < 12; i++) { Advance(0.02f); Sample(); }
        animancer.Graph.PauseGraph();
        float paused = bone.transform.localEulerAngles.z;
        late.Invoke(applier, null);
        Assert.That(Mathf.DeltaAngle(paused, bone.transform.localEulerAngles.z), Is.EqualTo(0).Within(0.01f));
    }

    [Test]
    public void PairOffsetEvaluatorIsOrderIndependentAndKeepsIdentityWeight()
    {
        var bone = brain.transform;
        var evaluator = new PairOffsetEvaluator();
        var first = new Vector3(25, 40, -10); var second = new Vector3(-35, 5, 50);
        evaluator.Add(bone, first, .3f); evaluator.Add(bone, second, .2f);
        var result = evaluator.GetRotation(bone);
        evaluator.Clear(); evaluator.Add(bone, second, .2f); evaluator.Add(bone, first, .3f);
        Assert.That(Quaternion.Angle(result, evaluator.GetRotation(bone)), Is.LessThan(.01f));
        evaluator.Clear(); evaluator.Add(bone, new Vector3(0, 0, 80), .25f);
        Assert.That(Quaternion.Angle(Quaternion.Euler(0, 0, 20), evaluator.GetRotation(bone)), Is.LessThan(.01f));
        evaluator.Clear(); evaluator.Add(bone, first, .5f);
        Assert.That(Quaternion.Angle(Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(first), .5f), evaluator.GetRotation(bone)), Is.LessThan(.01f));
    }

    [Test]
    public void RomaPairOffsetsFollowActualRigDuringSlideFade()
    {
        var source = AssetDatabase.LoadAssetAtPath<CharacterAnimProfileSO>("Assets/Data/Animation/Roma/Roma_AnimProfile.asset");
        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Character/Roma/Roma.fbx");
        Assert.That(source, Is.Not.Null); Assert.That(modelAsset, Is.Not.Null);
        var model = Object.Instantiate(modelAsset, brain.transform);
        var modelAnimator = model.GetComponentInChildren<Animator>();
        modelAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animancer.Animator = modelAnimator;
        profile = Object.Instantiate(source); created.Add(profile);
        brain.SetAnimProfileOverride(profile);
        Assert.That(Invoke("TryInitialize"), Is.True);
        var mixer = (Vector2MixerState)animancer.Layers[0].CurrentState;
        mixer.Parameter = Vector2.up;
        PlayUpper(profile.shootHoldLoop, PairOffsetUpperAction.ShootHold);
        Advance(.4f);
        var saved = profile.FindPairOffsetProfile(PairOffsetBasePose.Slide, PairOffsetUpperAction.ShootHold, false);
        Assert.That(saved, Is.Not.Null); Assert.That(saved.BoneOffsets.Count, Is.GreaterThan(0));
        var bone = modelAnimator.transform.Find(saved.BoneOffsets[0].BonePath);
        Assert.That(bone, Is.Not.Null, saved.BoneOffsets[0].BonePath);
        var applier = brain.gameObject.AddComponent<CharacterPairOffsetApplier>();
        typeof(CharacterPairOffsetApplier).GetMethod("OnEnable", Hidden).Invoke(applier, null);
        var late = typeof(CharacterPairOffsetApplier).GetMethod("LateUpdate", Hidden);
        var play = Presentation.GetType().GetMethod("PlayLocomotionTransition", Hidden, null, new[] { typeof(ITransition) }, null);
        play.Invoke(Presentation, new object[] { profile.slide });
        var poses = new List<PairOffsetBasePoseWeight>(); var actions = new List<PairOffsetActionWeight>();
        var expected = new PairOffsetEvaluator();
        var evidence = new System.Text.StringBuilder("frame,slideWeight,correctionAngle\n");
        for (int frame = 0; frame < 55; frame++)
        {
            if (frame == 12) play.Invoke(Presentation, new object[] { profile.ResolveLocomotionMixer() });
            if (frame == 18) play.Invoke(Presentation, new object[] { profile.slide });
            if (frame == 28)
            {
                animancer.Graph.Speed = .2f;
                play.Invoke(Presentation, new object[] { profile.ResolveLocomotionMixer() });
            }
            Advance(1f / 60f);
            var sampled = bone.localRotation;
            brain.TryGetPairOffsetContributions(poses, actions, out var profiles);
            expected.Clear();
            foreach (var pose in poses)
            foreach (var action in actions)
            {
                var pair = PairOffsetEvaluator.FindProfile(profiles, pose.Pose, action.Action, pose.Clip, action.Clip);
                if (pair == null) continue;
                foreach (var offset in pair.BoneOffsets)
                    if (offset.Enabled && offset.BonePath == saved.BoneOffsets[0].BonePath)
                        expected.Add(bone, offset.LocalEulerOffset, pose.Weight * action.Weight * pair.Weight * offset.Weight);
            }
            late.Invoke(applier, null);
            Assert.That(Quaternion.Angle(sampled * expected.GetRotation(bone), bone.localRotation), Is.LessThan(.05f), "Frame " + frame);
            evidence.AppendLine(frame + "," + poses.Where(x => x.Pose == PairOffsetBasePose.Slide).Sum(x => x.Weight).ToString("F5", System.Globalization.CultureInfo.InvariantCulture) + "," + Quaternion.Angle(sampled, bone.localRotation).ToString("F5", System.Globalization.CultureInfo.InvariantCulture));
        }
        var folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../BuildArtifacts/PairOffsets"));
        System.IO.Directory.CreateDirectory(folder);
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, Application.isPlaying ? "RomaPlayMode.csv" : "RomaEditMode.csv"), evidence.ToString());
    }

    [Test]
    public void PairOffsetMixerReadsActualChildWeights()
    {
        profile.pairOffsetProfiles = ScriptableObject.CreateInstance<PairOffsetProfilesSO>(); created.Add(profile.pairOffsetProfiles);
        PlayUpper(NewTransition("Mixer hold", 0f), PairOffsetUpperAction.ShootHold);
        var mixer = (Vector2MixerState)animancer.Layers[0].CurrentState;
        mixer.Parameter = new Vector2(.35f, .65f);
        Advance(.1f);
        var poses = new List<PairOffsetBasePoseWeight>(); var actions = new List<PairOffsetActionWeight>();
        brain.TryGetPairOffsetContributions(poses, actions, out _);
        var keys = new[] { PairOffsetBasePose.Idle, PairOffsetBasePose.Forward, PairOffsetBasePose.Backward, PairOffsetBasePose.Left, PairOffsetBasePose.Right };
        for (int i = 0; i < mixer.ChildCount; i++)
            Assert.That(poses.FindAll(x => x.Pose == keys[i]).ConvertAll(x => x.Weight).Sum(), Is.EqualTo(mixer.Weight * mixer.GetChild(i).Weight).Within(.0001f));
    }

    [Test]
    public void PairOffsetPreviewBlendsSavedProfilesAndProtectsCapture()
    {
        var go = new GameObject("Offset preview test"); created.Add(go);
        var animator = go.AddComponent<Animator>();
        var bone = new GameObject("OffsetBone"); bone.transform.SetParent(go.transform, false);
        var idle = NewTransition("Preview idle", 0).Clip;
        var slide = NewTransition("Preview slide", 0).Clip;
        var shoot = NewTransition("Preview shoot", 0).Clip;
        var offsets = ScriptableObject.CreateInstance<PairOffsetProfilesSO>(); created.Add(offsets);
        offsets.UpsertProfile(true, "Idle", idle, shoot, 1).AddBoneOffset(true, "OffsetBone", new Vector3(0, 0, 10), 1);
        offsets.UpsertProfile(true, "Slide", slide, shoot, 1).AddBoneOffset(true, "OffsetBone", new Vector3(0, 0, 50), 1);
        var preview = go.AddComponent<SoloRootMotionPreview>();
        void Set(string field, object value) => typeof(SoloRootMotionPreview).GetField(field, Hidden).SetValue(preview, value);
        Set("animator", animator); Set("previewMode", SoloRootMotionPreview.PreviewMode.UpperBody);
        Set("clip", slide); Set("upperBodyClip", shoot); Set("transitionFromBaseClip", idle);
        Set("baseTransitionWeight", .25f); Set("upperBodyWeight", .5f);
        Set("applyPairBoneOffsets", true); Set("pairOffsetProfileAsset", offsets);
        preview.Play(); preview.Pause();
        Assert.That(Mathf.DeltaAngle(10, bone.transform.localEulerAngles.z), Is.EqualTo(0).Within(.01f));
        Assert.That(preview.CaptureCurrentPoseAsActivePairOffsets(), Is.Zero);
        preview.Evaluate(0);
        Assert.That(Mathf.DeltaAngle(10, bone.transform.localEulerAngles.z), Is.EqualTo(0).Within(.01f), "Repeated preview samples cannot accumulate offsets.");
        Set("baseTransitionWeight", 1f); preview.Evaluate(0);
        Assert.That(Mathf.DeltaAngle(25, bone.transform.localEulerAngles.z), Is.EqualTo(0).Within(.01f));
        Object.DestroyImmediate(go);
    }

    private ClipTransition NewTransition(string name, float fade)
    {
        var clip = new AnimationClip { name = name }; created.Add(clip);
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
        return new ClipTransition { Clip = clip, FadeDuration = fade };
    }

    private AnimancerState PlayUpper(ClipTransition transition, PairOffsetUpperAction action)
        => (AnimancerState)Invoke("PlayActionPresentation", transition, action, false, null);

    private AnimancerState PlayTemporary()
    {
        var state = new ClipState(NewTransition("Temporary", 0f).Clip);
        PlayTemporaryState(state);
        return state;
    }

    private void PlayTemporaryState(AnimancerState state)
        => Presentation.GetType().GetMethod("PlayLocomotionTransition", Hidden, null,
            new[] { typeof(AnimancerState), typeof(float) }, null).Invoke(Presentation, new object[] { state, 0f });

    private object Presentation => typeof(CharacterAnimBrain).GetField("presentation", Hidden).GetValue(brain);
    private object InvokePresentation(string method, params object[] args)
        => Presentation.GetType().GetMethod(method, Hidden).Invoke(Presentation, args);

    private object Invoke(string method, params object[] args)
        => typeof(CharacterAnimBrain).GetMethod(method, Hidden).Invoke(brain, args);

    private string CurrentActionName()
    {
        var sm = typeof(CharacterAnimBrain).GetField("actionSM", Hidden).GetValue(brain);
        return sm.GetType().GetProperty("CurrentState").GetValue(sm).GetType().Name;
    }

    private void Advance(float duration)
    {
        int steps = Mathf.CeilToInt(duration / 0.01f);
        for (int i = 0; i < steps; i++)
        {
            animancer.Evaluate(duration / steps);
            InvokePresentation("UpdatePresentationTransitions");
        }
    }
}
#endif
