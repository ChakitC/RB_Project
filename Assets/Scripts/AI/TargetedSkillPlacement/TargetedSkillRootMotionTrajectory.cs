using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

internal readonly struct TargetedSkillRootMotionSample
{
    public readonly float normalizedTime;
    public readonly Vector3 localPosition;
    public readonly float localYaw;

    public TargetedSkillRootMotionSample(float normalizedTime, Vector3 localPosition, float localYaw)
    {
        this.normalizedTime = normalizedTime;
        this.localPosition = localPosition;
        this.localYaw = localYaw;
    }
}

internal sealed class TargetedSkillRootMotionTrajectory
{
    const float PositionEpsilon = 0.01f;
    const float YawEpsilon = 0.5f;

    readonly TargetedSkillRootMotionSample[] samples;

    public TargetedSkillRootMotionTrajectory(AnimationClip clip, TargetedSkillRootMotionSample[] samples)
    {
        Clip = clip;
        this.samples = samples ?? Array.Empty<TargetedSkillRootMotionSample>();
    }

    public AnimationClip Clip { get; }
    public IReadOnlyList<TargetedSkillRootMotionSample> Samples => samples;
    public bool HasSamples => samples.Length > 1;
    public bool HasMeaningfulMotion
    {
        get
        {
            for (int i = 0; i < samples.Length; i++)
            {
                TargetedSkillRootMotionSample sample = samples[i];
                if (sample.localPosition.sqrMagnitude > PositionEpsilon * PositionEpsilon ||
                    Mathf.Abs(sample.localYaw) > YawEpsilon)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool TrySample(float normalizedTime, out TargetedSkillRootMotionSample sample)
    {
        sample = default;
        if (samples.Length == 0)
            return false;

        float clampedTime = Mathf.Clamp01(normalizedTime);
        if (samples.Length == 1 || clampedTime <= samples[0].normalizedTime)
        {
            sample = samples[0];
            return true;
        }

        int lastIndex = samples.Length - 1;
        if (clampedTime >= samples[lastIndex].normalizedTime)
        {
            sample = samples[lastIndex];
            return true;
        }

        for (int i = 1; i < samples.Length; i++)
        {
            TargetedSkillRootMotionSample next = samples[i];
            if (clampedTime > next.normalizedTime)
                continue;

            TargetedSkillRootMotionSample previous = samples[i - 1];
            float duration = next.normalizedTime - previous.normalizedTime;
            float t = duration > 0.0001f
                ? Mathf.Clamp01((clampedTime - previous.normalizedTime) / duration)
                : 0f;

            sample = new TargetedSkillRootMotionSample(
                clampedTime,
                Vector3.Lerp(previous.localPosition, next.localPosition, t),
                Mathf.LerpUnclamped(previous.localYaw, next.localYaw, t));
            return true;
        }

        sample = samples[lastIndex];
        return true;
    }
}

internal static class TargetedSkillRootMotionTrajectoryCache
{
    const float SampleRate = 30f;
    const int MaxSampleCount = 240;

    static readonly Dictionary<CacheKey, TargetedSkillRootMotionTrajectory> Trajectories = new();
    static readonly Dictionary<int, SamplerInstance> Samplers = new();
    static TargetedSkillRootMotionTrajectoryHost host;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRuntimeState()
    {
        Trajectories.Clear();
        Samplers.Clear();
        host = null;
    }

    public static bool TryGet(
        AnimationClip clip,
        Animator sourceAnimator,
        out TargetedSkillRootMotionTrajectory trajectory,
        out string failureReason)
    {
        trajectory = null;
        failureReason = null;

        if (clip == null)
        {
            failureReason = "Missing attack AnimationClip.";
            return false;
        }

        if (sourceAnimator == null)
        {
            failureReason = "Missing source Animator.";
            return false;
        }

        CacheKey key = new(
            clip.GetInstanceID(),
            sourceAnimator.avatar != null ? sourceAnimator.avatar.GetInstanceID() : sourceAnimator.gameObject.GetInstanceID());

        if (Trajectories.TryGetValue(key, out trajectory) && trajectory != null)
            return true;

        SamplerInstance sampler = ResolveSampler(sourceAnimator, key.rigId, out failureReason);
        if (sampler == null)
            return false;

        if (!sampler.TrySample(clip, SampleRate, MaxSampleCount, out trajectory, out failureReason))
            return false;

        Trajectories[key] = trajectory;
        return true;
    }

    static SamplerInstance ResolveSampler(Animator sourceAnimator, int rigId, out string failureReason)
    {
        failureReason = null;

        if (Samplers.TryGetValue(rigId, out SamplerInstance existing) && existing != null && existing.IsValid)
            return existing;

        EnsureHost();
        if (host == null)
        {
            failureReason = "Failed to create root-motion sampler host.";
            return null;
        }

        SamplerInstance created = SamplerInstance.Create(sourceAnimator, host.transform, out failureReason);
        if (created == null)
            return null;

        Samplers[rigId] = created;
        host.Register(created);
        return created;
    }

    static void EnsureHost()
    {
        if (host != null)
            return;

        GameObject hostObject = new("[Targeted Skill Root Motion Samplers]")
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        UnityEngine.Object.DontDestroyOnLoad(hostObject);
        host = hostObject.AddComponent<TargetedSkillRootMotionTrajectoryHost>();
    }

    readonly struct CacheKey : IEquatable<CacheKey>
    {
        public readonly int clipId;
        public readonly int rigId;

        public CacheKey(int clipId, int rigId)
        {
            this.clipId = clipId;
            this.rigId = rigId;
        }

        public bool Equals(CacheKey other)
        {
            return clipId == other.clipId && rigId == other.rigId;
        }

        public override bool Equals(object obj)
        {
            return obj is CacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(clipId, rigId);
        }
    }

    sealed class SamplerInstance : IDisposable
    {
        readonly GameObject sampleRoot;
        readonly Animator animator;
        readonly TargetedSkillRootMotionCollector collector;
        readonly TransformPose[] restorePose;

        SamplerInstance(
            GameObject sampleRoot,
            Animator animator,
            TargetedSkillRootMotionCollector collector,
            TransformPose[] restorePose)
        {
            this.sampleRoot = sampleRoot;
            this.animator = animator;
            this.collector = collector;
            this.restorePose = restorePose;
        }

        public bool IsValid => sampleRoot != null && animator != null && collector != null;

        public static SamplerInstance Create(
            Animator sourceAnimator,
            Transform parent,
            out string failureReason)
        {
            failureReason = null;

            if (sourceAnimator == null)
            {
                failureReason = "Missing source Animator.";
                return null;
            }

            GameObject clone = CloneTransformHierarchy(sourceAnimator.transform, parent);
            clone.name = $"[Root Motion Sampler] {sourceAnimator.name}";
            clone.hideFlags = HideFlags.HideAndDontSave;

            Animator cloneAnimator = clone.AddComponent<Animator>();
            cloneAnimator.avatar = sourceAnimator.avatar;
            cloneAnimator.runtimeAnimatorController = null;
            cloneAnimator.applyRootMotion = true;
            cloneAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            cloneAnimator.updateMode = AnimatorUpdateMode.Normal;
            cloneAnimator.enabled = true;

            TargetedSkillRootMotionCollector cloneCollector = clone.AddComponent<TargetedSkillRootMotionCollector>();
            cloneCollector.Configure(cloneAnimator);

            Transform[] transforms = clone.GetComponentsInChildren<Transform>(true);
            TransformPose[] poses = new TransformPose[transforms.Length];
            for (int i = 0; i < transforms.Length; i++)
                poses[i] = new TransformPose(transforms[i]);

            clone.SetActive(true);
            return new SamplerInstance(clone, cloneAnimator, cloneCollector, poses);
        }

        public bool TrySample(
            AnimationClip clip,
            float sampleRate,
            int maxSampleCount,
            out TargetedSkillRootMotionTrajectory trajectory,
            out string failureReason)
        {
            trajectory = null;
            failureReason = null;

            if (!IsValid || clip == null || clip.length <= 0.0001f)
            {
                failureReason = "The sampler or AnimationClip is invalid.";
                return false;
            }

            RestorePose();
            collector.ResetMotion();
            animator.Rebind();
            animator.Update(0f);

            PlayableGraph graph = PlayableGraph.Create($"Root Motion Sample: {clip.name}");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            try
            {
                AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                playable.SetApplyPlayableIK(false);

                AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Root Motion", animator);
                output.SetSourcePlayable(playable);

                graph.Play();
                graph.Evaluate(0f);

                int stepCount = Mathf.Clamp(
                    Mathf.CeilToInt(clip.length * Mathf.Max(1f, sampleRate)),
                    1,
                    Mathf.Max(1, maxSampleCount - 1));
                float stepDuration = clip.length / stepCount;

                TargetedSkillRootMotionSample[] samples = new TargetedSkillRootMotionSample[stepCount + 1];
                samples[0] = new TargetedSkillRootMotionSample(0f, Vector3.zero, 0f);

                for (int i = 1; i <= stepCount; i++)
                {
                    graph.Evaluate(stepDuration);
                    float normalizedTime = i / (float)stepCount;
                    samples[i] = new TargetedSkillRootMotionSample(
                        normalizedTime,
                        collector.AccumulatedPosition,
                        collector.AccumulatedYaw);
                }

                if (collector.CallbackCount == 0 && clip.hasRootCurves)
                {
                    failureReason = $"Unity did not emit root-motion deltas while sampling clip '{clip.name}'.";
                    return false;
                }

                trajectory = new TargetedSkillRootMotionTrajectory(clip, samples);
                return true;
            }
            catch (Exception ex)
            {
                failureReason = $"Root-motion sampling failed for clip '{clip.name}': {ex.Message}";
                return false;
            }
            finally
            {
                if (graph.IsValid())
                    graph.Destroy();

                RestorePose();
                collector.ResetMotion();
            }
        }

        public void Dispose()
        {
            if (sampleRoot != null)
                UnityEngine.Object.Destroy(sampleRoot);
        }

        void RestorePose()
        {
            if (restorePose != null)
            {
                for (int i = 0; i < restorePose.Length; i++)
                    restorePose[i].Restore();
            }

            if (sampleRoot != null)
                sampleRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        static GameObject CloneTransformHierarchy(Transform source, Transform parent)
        {
            GameObject clone = new(source != null ? source.name : "Root Motion Sampler");
            clone.SetActive(false);
            clone.transform.SetParent(parent, false);

            if (source == null)
                return clone;

            clone.transform.localPosition = source.localPosition;
            clone.transform.localRotation = source.localRotation;
            clone.transform.localScale = source.localScale;

            for (int i = 0; i < source.childCount; i++)
            {
                GameObject childClone = CloneTransformHierarchy(source.GetChild(i), clone.transform);
                childClone.SetActive(source.GetChild(i).gameObject.activeSelf);
            }

            return clone;
        }
    }

    readonly struct TransformPose
    {
        readonly Transform transform;
        readonly Vector3 localPosition;
        readonly Quaternion localRotation;
        readonly Vector3 localScale;

        public TransformPose(Transform transform)
        {
            this.transform = transform;
            localPosition = transform != null ? transform.localPosition : Vector3.zero;
            localRotation = transform != null ? transform.localRotation : Quaternion.identity;
            localScale = transform != null ? transform.localScale : Vector3.one;
        }

        public void Restore()
        {
            if (transform == null)
                return;

            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            transform.localScale = localScale;
        }
    }
}

internal sealed class TargetedSkillRootMotionCollector : MonoBehaviour
{
    Animator animator;

    public Vector3 AccumulatedPosition { get; private set; }
    public float AccumulatedYaw { get; private set; }
    public int CallbackCount { get; private set; }

    public void Configure(Animator sourceAnimator)
    {
        animator = sourceAnimator;
    }

    public void ResetMotion()
    {
        AccumulatedPosition = Vector3.zero;
        AccumulatedYaw = 0f;
        CallbackCount = 0;
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    void OnAnimatorMove()
    {
        if (animator == null)
            return;

        Vector3 deltaPosition = animator.deltaPosition;
        deltaPosition.y = 0f;

        AccumulatedPosition += deltaPosition;
        AccumulatedYaw += Mathf.DeltaAngle(0f, animator.deltaRotation.eulerAngles.y);
        CallbackCount++;

        transform.SetPositionAndRotation(
            AccumulatedPosition,
            Quaternion.Euler(0f, AccumulatedYaw, 0f));
    }
}

internal sealed class TargetedSkillRootMotionTrajectoryHost : MonoBehaviour
{
    readonly List<IDisposable> disposables = new();

    public void Register(IDisposable disposable)
    {
        if (disposable != null)
            disposables.Add(disposable);
    }

    void OnDestroy()
    {
        for (int i = 0; i < disposables.Count; i++)
            disposables[i]?.Dispose();

        disposables.Clear();
    }
}
