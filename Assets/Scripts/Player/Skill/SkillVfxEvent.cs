using System;
using System.Collections.Generic;
using UnityEngine;

public enum SkillVfxAction
{
    OneShot = 0,
    StartLoop = 1,
    StopLoop = 2,
}

public enum SkillVfxAnchor
{
    CasterRoot = 0,
    CastOrigin = 1,
    AimTransform = 2,
    CustomChildPath = 3,
    HumanoidBone = 4,
}

public enum SkillVfxAnchorMode
{
    WorldSpace = 0,
    FollowAnchor = 1,
}

[Serializable]
public sealed class SkillVfxEvent
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

    public bool RequiresPrefab => action != SkillVfxAction.StopLoop;

    public void CollectValidationIssues(List<string> issues, int index)
    {
        if (issues == null)
            return;

        string label = $"Timeline VFX entry {index + 1}";

        if (cueIndex < 0)
            issues.Add($"{label} requires a non-negative cue index.");

        if (RequiresPrefab && prefab == null)
            issues.Add($"{label} requires a VFX prefab.");

        if (anchor == SkillVfxAnchor.CustomChildPath && string.IsNullOrWhiteSpace(customAnchorPath))
            issues.Add($"{label} uses Custom Child Path but has no path.");

        if ((action == SkillVfxAction.StartLoop || action == SkillVfxAction.StopLoop) && string.IsNullOrWhiteSpace(loopKey))
            issues.Add($"{label} requires a Loop Key for {action}.");
    }
}

public static class SkillVfxAnchorResolver
{
    public static Transform Resolve(Transform source, SkillVfxEvent cue)
    {
        if (source == null)
            return null;

        Transform root = ResolveCharacterRoot(source);
        if (cue == null)
            return root;

        switch (cue.anchor)
        {
            case SkillVfxAnchor.CastOrigin:
                return ResolveSkillUser(root)?.CastOrigin ?? root;

            case SkillVfxAnchor.AimTransform:
                return ResolveSkillUser(root)?.AimTransform ?? root;

            case SkillVfxAnchor.CustomChildPath:
                if (string.IsNullOrWhiteSpace(cue.customAnchorPath))
                    return root;

                Transform custom = root.Find(cue.customAnchorPath.Trim());
                return custom != null ? custom : root;

            case SkillVfxAnchor.HumanoidBone:
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator != null && animator.isHuman)
                {
                    Transform bone = animator.GetBoneTransform(cue.humanoidBone);
                    if (bone != null)
                        return bone;
                }

                return root;

            case SkillVfxAnchor.CasterRoot:
            default:
                return root;
        }
    }

    public static void ResolvePose(Transform anchor, SkillVfxEvent cue, out Vector3 position, out Quaternion rotation)
    {
        if (anchor == null)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return;
        }

        position = anchor.TransformPoint(cue != null ? cue.localPosition : Vector3.zero);
        rotation = anchor.rotation * Quaternion.Euler(cue != null ? cue.localEulerAngles : Vector3.zero);
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
