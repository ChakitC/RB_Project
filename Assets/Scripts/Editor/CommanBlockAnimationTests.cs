#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
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
    public void TemporaryPoseSurvivesFadeAndIsDestroyedAfterwards()
    {
        var state = PlayTemporary();
        Invoke("RetireTemporaryState", state);
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
        Invoke("RetireTemporaryState", state);
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
        Invoke("RetireTemporaryState", state);
        Assert.That(brain.TryPlayCommanBlock(), Is.True);
        Advance(0.05f);
        Invoke("OnDisable");
        Assert.That(state.Playable.IsValid(), Is.False);
        Assert.That(animancer.Layers[1].Weight, Is.Zero);
        Assert.That(animancer.Layers[2].Weight, Is.Zero);
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

    private ClipTransition NewTransition(string name, float fade)
    {
        var clip = new AnimationClip { name = name }; created.Add(clip);
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
        return new ClipTransition { Clip = clip, FadeDuration = fade };
    }

    private AnimancerState PlayUpper(ClipTransition transition, PairOffsetUpperAction action)
        => (AnimancerState)Invoke("PlayActionTransition", transition, action);

    private AnimancerState PlayTemporary()
    {
        var state = new ClipState(NewTransition("Temporary", 0f).Clip);
        PlayTemporaryState(state);
        return state;
    }

    private void PlayTemporaryState(AnimancerState state)
        => typeof(CharacterAnimBrain).GetMethod("PlayLocomotionTransition", Hidden, null,
            new[] { typeof(AnimancerState), typeof(float) }, null).Invoke(brain, new object[] { state, 0f });

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
            Invoke("UpdatePresentationTransitions");
        }
    }
}
#endif
