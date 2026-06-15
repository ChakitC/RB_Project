using System;
using System.Collections.Generic;
using UnityEngine;

public enum AnimationVfxAction
{
    OneShot = 0,
    StartLoop = 1,
    StopLoop = 2,
}

public enum AnimationVfxAnchor
{
    CasterRoot = 0,
    CastOrigin = 1,
    AimTransform = 2,
    CustomChildPath = 3,
    HumanoidBone = 4,
}

public enum AnimationVfxAnchorMode
{
    WorldSpace = 0,
    FollowAnchor = 1,
}

public interface IAnimationVfxCue
{
    int CueIndex { get; }
    AnimationVfxAction Action { get; }
    GameObject Prefab { get; }
    AnimationVfxAnchor Anchor { get; }
    AnimationVfxAnchorMode AnchorMode { get; }
    string CustomAnchorPath { get; }
    HumanBodyBones HumanoidBone { get; }
    Vector3 LocalPosition { get; }
    Vector3 LocalEulerAngles { get; }
    Vector3 LocalScale { get; }
    string LoopKey { get; }
    float ExtraLife { get; }
    bool AllowParticlesToFinish { get; }
}

public interface IAnimationVfxCueSource
{
    int CueCount { get; }
    IAnimationVfxCue GetCue(int index);
}

[Serializable]
public sealed class AnimationVfxCue : IAnimationVfxCue
{
    [Min(0)] public int cueIndex;
    public AnimationVfxAction action;
    public GameObject prefab;
    public AnimationVfxAnchor anchor = AnimationVfxAnchor.CastOrigin;
    public string customAnchorPath;
    public HumanBodyBones humanoidBone = HumanBodyBones.RightHand;
    public Vector3 localPosition;
    public Vector3 localEulerAngles;
    public Vector3 localScale = Vector3.one;
    public AnimationVfxAnchorMode anchorMode;
    public string loopKey;
    [Min(0f)] public float extraLife;
    public bool allowParticlesToFinish = true;

    int IAnimationVfxCue.CueIndex => cueIndex;
    AnimationVfxAction IAnimationVfxCue.Action => action;
    GameObject IAnimationVfxCue.Prefab => prefab;
    AnimationVfxAnchor IAnimationVfxCue.Anchor => anchor;
    AnimationVfxAnchorMode IAnimationVfxCue.AnchorMode => anchorMode;
    string IAnimationVfxCue.CustomAnchorPath => customAnchorPath;
    HumanBodyBones IAnimationVfxCue.HumanoidBone => humanoidBone;
    Vector3 IAnimationVfxCue.LocalPosition => localPosition;
    Vector3 IAnimationVfxCue.LocalEulerAngles => localEulerAngles;
    Vector3 IAnimationVfxCue.LocalScale => localScale;
    string IAnimationVfxCue.LoopKey => loopKey;
    float IAnimationVfxCue.ExtraLife => extraLife;
    bool IAnimationVfxCue.AllowParticlesToFinish => allowParticlesToFinish;
}

[Serializable]
public sealed class AnimationVfxTrack : IAnimationVfxCueSource
{
    [SerializeField] private List<AnimationVfxCue> cues = new List<AnimationVfxCue>();

    public IReadOnlyList<AnimationVfxCue> Cues => cues;
    public int CueCount => cues != null ? cues.Count : 0;

    public IAnimationVfxCue GetCue(int index)
    {
        return cues != null && index >= 0 && index < cues.Count ? cues[index] : null;
    }

    public void ReplaceCues(IEnumerable<AnimationVfxCue> values)
    {
        List<AnimationVfxCue> replacement = values != null
            ? new List<AnimationVfxCue>(values)
            : null;
        cues ??= new List<AnimationVfxCue>();
        cues.Clear();
        if (replacement == null)
            return;

        for (int i = 0; i < replacement.Count; i++)
        {
            AnimationVfxCue cue = replacement[i];
            if (cue != null)
                cues.Add(cue);
        }
    }

    public void MoveCueIndex(int oldCueIndex, int newCueIndex)
    {
        if (cues == null || oldCueIndex < 0 || newCueIndex < 0 || oldCueIndex == newCueIndex)
            return;

        for (int i = 0; i < cues.Count; i++)
        {
            AnimationVfxCue cue = cues[i];
            if (cue == null)
                continue;

            if (cue.cueIndex == oldCueIndex)
                cue.cueIndex = newCueIndex;
            else if (oldCueIndex < newCueIndex && cue.cueIndex > oldCueIndex && cue.cueIndex <= newCueIndex)
                cue.cueIndex--;
            else if (newCueIndex < oldCueIndex && cue.cueIndex >= newCueIndex && cue.cueIndex < oldCueIndex)
                cue.cueIndex++;
        }
    }

    public void RemoveCueIndex(int cueIndex)
    {
        if (cues == null || cueIndex < 0)
            return;

        for (int i = cues.Count - 1; i >= 0; i--)
        {
            AnimationVfxCue cue = cues[i];
            if (cue == null || cue.cueIndex == cueIndex)
                cues.RemoveAt(i);
            else if (cue.cueIndex > cueIndex)
                cue.cueIndex--;
        }
    }
}

public readonly struct AnimationVfxAnchorContext
{
    public Transform CharacterRoot { get; }
    public Transform CastOrigin { get; }
    public Transform AimTransform { get; }
    public Animator Animator { get; }

    public AnimationVfxAnchorContext(
        Transform characterRoot,
        Transform castOrigin,
        Transform aimTransform,
        Animator animator)
    {
        CharacterRoot = characterRoot;
        CastOrigin = castOrigin;
        AimTransform = aimTransform;
        Animator = animator;
    }

    public AnimationVfxAnchorContext WithFallbackRoot(Transform fallbackRoot)
    {
        return CharacterRoot != null
            ? this
            : new AnimationVfxAnchorContext(fallbackRoot, CastOrigin, AimTransform, Animator);
    }
}

public static class AnimationVfxAnchorResolver
{
    public static Transform Resolve(AnimationVfxAnchorContext context, IAnimationVfxCue cue)
    {
        Transform root = context.CharacterRoot;
        if (cue == null)
            return root;

        switch (cue.Anchor)
        {
            case AnimationVfxAnchor.CastOrigin:
                return context.CastOrigin != null ? context.CastOrigin : root;

            case AnimationVfxAnchor.AimTransform:
                return context.AimTransform != null ? context.AimTransform : root;

            case AnimationVfxAnchor.CustomChildPath:
                if (root == null || string.IsNullOrWhiteSpace(cue.CustomAnchorPath))
                    return root;

                Transform custom = root.Find(cue.CustomAnchorPath.Trim());
                return custom != null ? custom : root;

            case AnimationVfxAnchor.HumanoidBone:
                if (context.Animator != null && context.Animator.isHuman)
                {
                    Transform bone = context.Animator.GetBoneTransform(cue.HumanoidBone);
                    if (bone != null)
                        return bone;
                }

                return root;

            case AnimationVfxAnchor.CasterRoot:
            default:
                return root;
        }
    }

    public static void ResolvePose(
        Transform anchor,
        IAnimationVfxCue cue,
        out Vector3 position,
        out Quaternion rotation)
    {
        if (anchor == null)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return;
        }

        position = anchor.TransformPoint(cue != null ? cue.LocalPosition : Vector3.zero);
        rotation = anchor.rotation * Quaternion.Euler(cue != null ? cue.LocalEulerAngles : Vector3.zero);
    }
}

public static class AnimationVfxValidation
{
    public static void CollectIssues(
        IAnimationVfxCueSource source,
        int markerCount,
        string clipLabel,
        List<string> issues)
    {
        if (source == null || issues == null)
            return;

        var orderedIndices = new List<int>(source.CueCount);
        for (int i = 0; i < source.CueCount; i++)
        {
            IAnimationVfxCue cue = source.GetCue(i);
            if (cue == null)
            {
                issues.Add($"Timeline VFX entry {i + 1} is null.");
                continue;
            }

            CollectCueIssues(cue, i, issues);
            if (markerCount >= 0 && cue.CueIndex >= markerCount)
            {
                string label = string.IsNullOrWhiteSpace(clipLabel) ? "animation clip" : clipLabel;
                issues.Add(
                    $"Timeline VFX entry {i + 1} targets cue {cue.CueIndex + 1}, but the {label} has only {markerCount} Vfx marker(s).");
            }

            orderedIndices.Add(i);
        }

        orderedIndices.Sort((left, right) =>
        {
            IAnimationVfxCue leftCue = source.GetCue(left);
            IAnimationVfxCue rightCue = source.GetCue(right);
            int cueComparison = leftCue.CueIndex.CompareTo(rightCue.CueIndex);
            return cueComparison != 0 ? cueComparison : left.CompareTo(right);
        });

        var activeLoopKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < orderedIndices.Count; i++)
        {
            int sourceIndex = orderedIndices[i];
            IAnimationVfxCue cue = source.GetCue(sourceIndex);
            if (string.IsNullOrWhiteSpace(cue.LoopKey))
                continue;

            string key = cue.LoopKey.Trim();
            if (cue.Action == AnimationVfxAction.StartLoop)
            {
                activeLoopKeys.Add(key);
            }
            else if (cue.Action == AnimationVfxAction.StopLoop && !activeLoopKeys.Remove(key))
            {
                issues.Add(
                    $"Timeline VFX entry {sourceIndex + 1} stops loop '{key}' before a matching active Start Loop cue.");
            }
        }
    }

    public static void CollectCueIssues(IAnimationVfxCue cue, int index, List<string> issues)
    {
        if (cue == null || issues == null)
            return;

        string label = $"Timeline VFX entry {index + 1}";
        if (cue.CueIndex < 0)
            issues.Add($"{label} requires a non-negative cue index.");

        if (cue.Action != AnimationVfxAction.StopLoop && cue.Prefab == null)
            issues.Add($"{label} requires a VFX prefab.");

        if (cue.Anchor == AnimationVfxAnchor.CustomChildPath && string.IsNullOrWhiteSpace(cue.CustomAnchorPath))
            issues.Add($"{label} uses Custom Child Path but has no path.");

        if ((cue.Action == AnimationVfxAction.StartLoop || cue.Action == AnimationVfxAction.StopLoop) &&
            string.IsNullOrWhiteSpace(cue.LoopKey))
        {
            issues.Add($"{label} requires a Loop Key for {cue.Action}.");
        }
    }
}
