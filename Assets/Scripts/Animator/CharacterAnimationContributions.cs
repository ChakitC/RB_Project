using System.Collections.Generic;
using Animancer;
using UnityEngine;
using UnityEngine.Playables;

// Per-character metadata; weights always come from the evaluated animation graph.
internal sealed class CharacterAnimationContributions
{
    private readonly Dictionary<AnimancerState, PairOffsetBasePose> poses = new();

    internal void Clear() => poses.Clear();
    internal void Forget(AnimancerState state) { if (state != null) poses.Remove(state); }

    internal void Register(AnimancerState state, ITransition transition, CharacterAnimProfileSO profile)
    {
        if (state == null) return;
        PairOffsetBasePose pose;
        if (ReferenceEquals(transition, profile.ResolveLocomotionMixer())) pose = PairOffsetBasePose.None;
        else if (ReferenceEquals(transition, profile.slide)) pose = PairOffsetBasePose.Slide;
        else if (ReferenceEquals(transition, profile.sprint)) pose = PairOffsetBasePose.Sprint;
        else if (ReferenceEquals(transition, profile.crouchIdle)) pose = PairOffsetBasePose.CrouchIdle;
        else if (ReferenceEquals(transition, profile.crouchMove)) pose = PairOffsetBasePose.CrouchMove;
        else { poses.Remove(state); return; }
        poses[state] = pose;
    }

    internal void Collect(AnimancerLayer layer, List<PairOffsetBasePoseWeight> output)
    {
        output.Clear();
        if (layer == null || !layer.Playable.IsValid()) return;
        for (int i = 0; i < layer.ChildCount; i++)
        {
            var state = layer.GetChild(i);
            if (state == null || !state.Playable.IsValid() || !poses.TryGetValue(state, out var pose)) continue;
            float weight = Mathf.Clamp01(layer.Weight) * Mathf.Clamp01(state.Weight);
            if (weight <= 0f) continue;
            if (pose != PairOffsetBasePose.None)
                output.Add(new PairOffsetBasePoseWeight(pose, weight, state.Clip));
            else if (state is Vector2MixerState mixer)
            {
                for (int childIndex = 0; childIndex < mixer.ChildCount; childIndex++)
                {
                    var child = mixer.GetChild(childIndex);
                    float childWeight = weight * Mathf.Clamp01(child.Weight);
                    if (childWeight > 0f)
                        output.Add(new PairOffsetBasePoseWeight(PoseAt(mixer.GetThreshold(childIndex)), childWeight, child.Clip));
                }
            }
        }
    }

    private static PairOffsetBasePose PoseAt(Vector2 threshold)
    {
        if (threshold.sqrMagnitude < 0.0001f) return PairOffsetBasePose.Idle;
        int octant = (Mathf.RoundToInt(Mathf.Atan2(threshold.y, threshold.x) / (Mathf.PI * 0.25f)) + 8) % 8;
        return octant switch
        {
            0 => PairOffsetBasePose.Right,
            1 => PairOffsetBasePose.ForwardRight,
            2 => PairOffsetBasePose.Forward,
            3 => PairOffsetBasePose.ForwardLeft,
            4 => PairOffsetBasePose.Left,
            5 => PairOffsetBasePose.BackwardLeft,
            6 => PairOffsetBasePose.Backward,
            _ => PairOffsetBasePose.BackwardRight,
        };
    }
}
