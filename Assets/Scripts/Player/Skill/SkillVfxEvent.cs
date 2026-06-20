using System;
using System.Collections.Generic;
using UnityEngine;

public enum SkillVfxAction
{
    OneShot = (int)AnimationVfxAction.OneShot,
    StartLoop = (int)AnimationVfxAction.StartLoop,
    StopLoop = (int)AnimationVfxAction.StopLoop,
}

public enum SkillVfxAnchor
{
    CasterRoot = (int)AnimationVfxAnchor.CasterRoot,
    CastOrigin = (int)AnimationVfxAnchor.CastOrigin,
    AimTransform = (int)AnimationVfxAnchor.AimTransform,
    CustomChildPath = (int)AnimationVfxAnchor.CustomChildPath,
    HumanoidBone = (int)AnimationVfxAnchor.HumanoidBone,
    GenericBone = (int)AnimationVfxAnchor.GenericBone,
}

public enum SkillVfxAnchorMode
{
    WorldSpace = (int)AnimationVfxAnchorMode.WorldSpace,
    FollowAnchor = (int)AnimationVfxAnchorMode.FollowAnchor,
}

[Serializable]
public sealed class SkillVfxEvent : IAnimationVfxCue
{
    [Min(0)]
    public int cueIndex;
    public SkillVfxAction action;
    public GameObject prefab;
    public SkillVfxAnchor anchor = SkillVfxAnchor.CastOrigin;
    public string customAnchorPath;
    public HumanBodyBones humanoidBone = HumanBodyBones.RightHand;
    public Vector3 localPosition;
    public Vector3 localEulerAngles;
    public Vector3 localScale = Vector3.one;
    public bool parentToAnchor;
    public string loopKey;
    [Min(0f)]
    public float extraLife;
    public bool allowParticlesToFinish = true;
    public AnimationClip animClip;

    public bool RequiresPrefab => action != SkillVfxAction.StopLoop;

    int IAnimationVfxCue.CueIndex => cueIndex;
    AnimationVfxAction IAnimationVfxCue.Action => (AnimationVfxAction)action;
    GameObject IAnimationVfxCue.Prefab => prefab;
    AnimationVfxAnchor IAnimationVfxCue.Anchor => (AnimationVfxAnchor)anchor;
    AnimationVfxAnchorMode IAnimationVfxCue.AnchorMode => parentToAnchor
        ? AnimationVfxAnchorMode.FollowAnchor
        : AnimationVfxAnchorMode.WorldSpace;
    string IAnimationVfxCue.CustomAnchorPath => customAnchorPath;
    HumanBodyBones IAnimationVfxCue.HumanoidBone => humanoidBone;
    Vector3 IAnimationVfxCue.LocalPosition => localPosition;
    Vector3 IAnimationVfxCue.LocalEulerAngles => localEulerAngles;
    Vector3 IAnimationVfxCue.LocalScale => localScale;
    string IAnimationVfxCue.LoopKey => loopKey;
    float IAnimationVfxCue.ExtraLife => extraLife;
    bool IAnimationVfxCue.AllowParticlesToFinish => allowParticlesToFinish;
    AnimationClip IAnimationVfxCue.AnimClip => animClip;

    public void CollectValidationIssues(List<string> issues, int index)
    {
        AnimationVfxValidation.CollectCueIssues(this, index, issues);
    }
}

public static class SkillVfxAnchorResolver
{
    public static AnimationVfxAnchorContext CreateContext(Transform source)
    {
        Transform root = ResolveCharacterRoot(source);
        ISkillUser skillUser = ResolveSkillUser(root);
        Animator animator = root != null ? root.GetComponentInChildren<Animator>(true) : null;
        return new AnimationVfxAnchorContext(
            root,
            skillUser?.CastOrigin,
            skillUser?.AimTransform,
            animator);
    }

    public static Transform Resolve(Transform source, SkillVfxEvent cue)
    {
        if (source == null)
            return null;

        return AnimationVfxAnchorResolver.Resolve(CreateContext(source), cue);
    }

    public static void ResolvePose(Transform anchor, SkillVfxEvent cue, out Vector3 position, out Quaternion rotation)
    {
        AnimationVfxAnchorResolver.ResolvePose(anchor, cue, out position, out rotation);
    }

    static Transform ResolveCharacterRoot(Transform source)
    {
        CharacteContext context = source.GetComponentInParent<CharacteContext>();
        if (context == null)
            context = source.GetComponentInChildren<CharacteContext>(true);

        return context != null ? context.transform : source;
    }

    static ISkillUser ResolveSkillUser(Transform root)
    {
        if (root == null)
            return null;

        CharacteContext context = root.GetComponent<CharacteContext>();
        if (context != null)
        {
            if (context.EnegySystem != null)
                return context.EnegySystem;
        }

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ISkillUser skillUser)
                return skillUser;
        }

        return null;
    }
}
