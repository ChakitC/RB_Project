#if UNITY_EDITOR
using System.Reflection;
using Animancer;
using NUnit.Framework;
using UnityEngine;

public sealed class DefensiveBlockImpactTests
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(30, .5f)]
    [TestCase(60, 1f)]
    [TestCase(144, 2f)]
    [TestCase(2, 1f)]
    public void ImpactUsesMotorProgressPreservesEventsAndRestoresSpeed(int fps, float speed)
    {
        var clip = new AnimationClip();
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 2f, 0f));
        var graph = new AnimancerGraph();
        try
        {
            graph.Layers = new AnimancerLayerMixerList(graph);
            var state = graph.Layers[0].Play(clip);
            state.NormalizedTime = .1f; state.Speed = speed;
            int release = 0, laterHit = 0;
            var events = new AnimancerEvent.Sequence();
            events.Add(.3f, () => release++);
            events.Add(.8f, () => laterHit++);
            state.SharedEvents = events;
            AnimancerState.UpdateEventsRecursive(state, true);
            var type = typeof(CharacterAnimBrain).GetNestedType("Locomotion_Skill", BindingFlags.NonPublic);
            var skill = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
            type.GetField("_state", Hidden).SetValue(skill, state);
            Assert.That(type.GetMethod("TryBeginImpactApproach", Hidden).Invoke(skill, new object[] { .7f }), Is.True);
            var advance = type.GetMethod("TryAdvanceImpactApproach", Hidden);
            int frames = Mathf.CeilToInt(.22f * fps);
            for (int frame = 1; frame <= frames; frame++)
            {
                float progress = Mathf.Min(1f, frame / (fps * .22f));
                Assert.That(advance.Invoke(skill, new object[] { progress, false }), Is.True);
                AnimancerState.UpdateEventsRecursive(state, true);
                AnimancerEvent.Invoker.InvokeAllAndClear();
                Assert.That(state.NormalizedTime, Is.EqualTo(Mathf.Lerp(.1f, .7f, progress)).Within(.00001f));
            }
            Assert.That(release, Is.EqualTo(1));
            Assert.That(laterHit, Is.Zero);
            Assert.That(type.GetMethod("TryHoldApproach", Hidden).Invoke(skill, null), Is.True);
            advance.Invoke(skill, new object[] { .25f, false }); // Stale updates must not rewind events.
            Assert.That(state.NormalizedTime, Is.EqualTo(.7f).Within(.00001f));
            Assert.That(state.Speed, Is.Zero);
            Assert.That(type.GetMethod("TryEndApproach", Hidden).Invoke(skill, null), Is.True);
            Assert.That(state.Speed, Is.EqualTo(speed));
            state.MoveTime(.9f, true);
            AnimancerState.UpdateEventsRecursive(state, true);
            AnimancerEvent.Invoker.InvokeAllAndClear();
            Assert.That(release, Is.EqualTo(1));
            Assert.That(laterHit, Is.EqualTo(1), "Continue resumes later timeline events exactly once.");
        }
        finally { graph.Destroy(); Object.DestroyImmediate(clip); }
    }

    [TestCase(.1f)]
    [TestCase(.05f)]
    [TestCase(.8f)]
    [TestCase(1f)]
    [TestCase(float.NaN)]
    public void ImpactRejectsPastFramesAndAuthoredClipEnd(float impact)
    {
        var clip = new AnimationClip();
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 2f, 0f));
        var state = new ClipState(clip);
        try
        {
            state.NormalizedTime = .1f; state.Speed = 2f;
            state.SharedEvents = new AnimancerEvent.Sequence { NormalizedEndTime = .8f };
            var type = typeof(CharacterAnimBrain).GetNestedType("Locomotion_Skill", BindingFlags.NonPublic);
            var skill = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
            type.GetField("_state", Hidden).SetValue(skill, state);
            Assert.That(type.GetMethod("TryBeginImpactApproach", Hidden).Invoke(skill, new object[] { impact }), Is.False);
            Assert.That(state.Speed, Is.EqualTo(2f));
        }
        finally { state.Destroy(); Object.DestroyImmediate(clip); }
    }
}
#endif
