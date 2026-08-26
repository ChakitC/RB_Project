using System;
using UnityEngine;

public sealed class CharacterPlacementAnimationInput
{
    public readonly struct Sample
    {
        public Sample(float normalizedTime, Vector3 localPosition, float localYaw)
        {
            NormalizedTime = Mathf.Clamp01(normalizedTime);
            LocalPosition = localPosition;
            LocalYaw = localYaw;
        }

        public float NormalizedTime { get; }
        public Vector3 LocalPosition { get; }
        public float LocalYaw { get; }
    }

    public readonly struct Segment
    {
        public Segment(
            string name,
            float startNormalized,
            float endNormalized,
            Sample[] samples)
        {
            Name = name ?? string.Empty;
            StartNormalized = Mathf.Clamp01(startNormalized);
            EndNormalized = Mathf.Clamp01(Mathf.Max(startNormalized, endNormalized));
            Samples = samples ?? Array.Empty<Sample>();
        }

        public string Name { get; }
        public float StartNormalized { get; }
        public float EndNormalized { get; }
        public Sample[] Samples { get; }
    }

    public CharacterPlacementAnimationInput(
        AnimationClip clip,
        Avatar avatar,
        bool planarRootMotionEnabled,
        Sample[] samples,
        Segment[] segments = null)
    {
        Clip = clip;
        Avatar = avatar;
        PlanarRootMotionEnabled = planarRootMotionEnabled;
        Samples = samples ?? Array.Empty<Sample>();
        Segments = segments ?? Array.Empty<Segment>();
    }

    public AnimationClip Clip { get; }
    public Avatar Avatar { get; }
    public bool PlanarRootMotionEnabled { get; }
    public Sample[] Samples { get; }
    public Segment[] Segments { get; }
    public bool HasSamples => Samples.Length > 0;

    public bool TrySample(float normalizedTime, out Sample sample)
    {
        sample = default;
        if (Samples.Length == 0)
            return false;

        float clampedTime = Mathf.Clamp01(normalizedTime);
        if (Samples.Length == 1 || clampedTime <= Samples[0].NormalizedTime)
        {
            sample = Samples[0];
            return true;
        }

        int lastIndex = Samples.Length - 1;
        if (clampedTime >= Samples[lastIndex].NormalizedTime)
        {
            sample = Samples[lastIndex];
            return true;
        }

        for (int i = 1; i < Samples.Length; i++)
        {
            Sample next = Samples[i];
            if (clampedTime > next.NormalizedTime)
                continue;

            Sample previous = Samples[i - 1];
            float duration = next.NormalizedTime - previous.NormalizedTime;
            float t = duration > 0.0001f
                ? Mathf.Clamp01((clampedTime - previous.NormalizedTime) / duration)
                : 0f;
            sample = new Sample(
                clampedTime,
                Vector3.Lerp(previous.LocalPosition, next.LocalPosition, t),
                Mathf.LerpAngle(previous.LocalYaw, next.LocalYaw, t));
            return true;
        }

        sample = Samples[lastIndex];
        return true;
    }
}
