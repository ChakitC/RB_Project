using System.Collections.Generic;
using Animancer;
using UnityEngine;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class CharacterPairOffsetApplier : MonoBehaviour
{
    private readonly struct AppliedBoneRotation
    {
        public readonly Transform Bone;
        public readonly Quaternion LocalRotation;

        public AppliedBoneRotation(Transform bone)
        {
            Bone = bone;
            LocalRotation = bone.localRotation;
        }
    }

    [SerializeField] private CharacteContext ctx;
    [SerializeField] private CharacterAnimBrain brain;
    [SerializeField] private Animator animator;
    [SerializeField] private bool applyPairOffsets = true;
    [SerializeField] private bool showDebugState;
    [SerializeField, TextArea] private string debugState;

    private readonly List<AppliedBoneRotation> appliedRotations = new List<AppliedBoneRotation>();
    private readonly List<PairOffsetBasePoseWeight> basePoseWeights = new List<PairOffsetBasePoseWeight>(3);
    private readonly Dictionary<string, Transform> boneCache = new Dictionary<string, Transform>();
    private Animator cachedAnimator;
    private PairOffsetProfilesSO cachedProfiles;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (!applyPairOffsets ||
            brain == null ||
            animator == null ||
            !brain.TryGetActivePairOffsetBlend(
                basePoseWeights,
                out PairOffsetProfilesSO profiles,
                out PairOffsetUpperAction upperAction,
                out float actionWeight))
        {
            RestoreAppliedOffsets();
            SetDebugState("Inactive: missing toggle, brain, animator, or active pair state.");
            return;
        }

        if (cachedAnimator != animator || cachedProfiles != profiles)
        {
            RestoreAppliedOffsets();
            boneCache.Clear();
            cachedAnimator = animator;
            cachedProfiles = profiles;
        }

        RestoreAppliedOffsets();
        int appliedProfileCount = ApplyProfiles(
            basePoseWeights,
            upperAction,
            actionWeight,
            out int appliedBoneCount,
            out int missingBoneCount,
            out string firstMissingBonePath);

        if (appliedBoneCount > 0)
        {
            string missingSuffix = missingBoneCount > 0
                ? $" Missing {missingBoneCount} bone path(s), first: {firstMissingBonePath}."
                : string.Empty;
            SetDebugState($"Applying {appliedProfileCount} profile(s), {appliedBoneCount} bone(s) for {upperAction}, action weight {actionWeight:0.###}.{missingSuffix}");
        }
        else if (appliedProfileCount > 0 && missingBoneCount > 0)
        {
            SetDebugState($"Profile(s) matched for {upperAction}, but no bones resolved. Missing {missingBoneCount} path(s), first: {firstMissingBonePath}");
        }
        else
        {
            SetDebugState($"No profiles for blended {upperAction} poses.");
        }
    }

    private void OnDisable()
    {
        RestoreAppliedOffsets();
    }

    private void OnDestroy()
    {
        RestoreAppliedOffsets();
    }

    private void ResolveReferences()
    {
        if (ctx == null)
        {
            TryGetComponent(out ctx);
            if (ctx == null)
                ctx = GetComponentInParent<CharacteContext>();
        }

        ctx?.ResolveReferences();

        if (ctx != null && ctx.PairOffsetApplier != this)
            ctx.PairOffsetApplier = this;

        if (brain == null && ctx != null)
            brain = ctx.AnimBrain;
        if (brain == null)
            TryGetComponent(out brain);
        if (brain == null && ctx != null)
            brain = ctx.GetComponentInChildren<CharacterAnimBrain>(true);

        Animator animancerAnimator = ResolveAnimancerAnimator();
        if (animancerAnimator != null && animator != animancerAnimator)
        {
            RestoreAppliedOffsets();
            boneCache.Clear();
            animator = animancerAnimator;
        }

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        if (animator == null && ctx != null)
            animator = ctx.GetComponentInChildren<Animator>(true);
    }

    private Animator ResolveAnimancerAnimator()
    {
        if (brain == null)
            return null;

        AnimancerComponent animancer = brain.GetComponent<AnimancerComponent>();
        if (animancer != null && animancer.Animator != null)
            return animancer.Animator;

        animancer = brain.GetComponentInChildren<AnimancerComponent>(true);
        if (animancer != null && animancer.Animator != null)
            return animancer.Animator;

        return null;
    }

    private int ApplyProfiles(
        IReadOnlyList<PairOffsetBasePoseWeight> poseWeights,
        PairOffsetUpperAction upperAction,
        float actionWeight,
        out int appliedBoneCount,
        out int missingBoneCount,
        out string firstMissingBonePath)
    {
        int appliedProfileCount = 0;
        appliedBoneCount = 0;
        missingBoneCount = 0;
        firstMissingBonePath = null;

        if (poseWeights == null)
            return appliedProfileCount;

        for (int i = 0; i < poseWeights.Count; i++)
        {
            PairOffsetBasePoseWeight poseWeight = poseWeights[i];
            if (poseWeight.Pose == PairOffsetBasePose.None || poseWeight.Weight <= 0.001f)
                continue;

            PairOffsetProfilesSO.PairOffsetProfile profile =
                brain.FindPairOffsetProfile(poseWeight.Pose, upperAction, false);
            if (profile == null)
                continue;

            int profileAppliedBoneCount = ApplyProfile(
                profile,
                actionWeight * Mathf.Clamp01(poseWeight.Weight),
                out int profileMissingBoneCount,
                out string profileFirstMissingBonePath);

            appliedBoneCount += profileAppliedBoneCount;
            missingBoneCount += profileMissingBoneCount;
            if (firstMissingBonePath == null && profileFirstMissingBonePath != null)
                firstMissingBonePath = profileFirstMissingBonePath;

            appliedProfileCount++;
        }

        return appliedProfileCount;
    }

    private int ApplyProfile(
        PairOffsetProfilesSO.PairOffsetProfile profile,
        float actionWeight,
        out int missingBoneCount,
        out string firstMissingBonePath)
    {
        int appliedBoneCount = 0;
        missingBoneCount = 0;
        firstMissingBonePath = null;

        IReadOnlyList<PairOffsetProfilesSO.BoneRotationOffset> offsets = profile.BoneOffsets;
        if (offsets == null || offsets.Count == 0)
            return appliedBoneCount;

        Transform root = animator != null ? animator.transform : transform;
        for (int i = 0; i < offsets.Count; i++)
        {
            PairOffsetProfilesSO.BoneRotationOffset offset = offsets[i];
            if (offset == null || !offset.Enabled || offset.Weight <= 0f)
                continue;

            Transform bone = ResolveBone(root, offset.BonePath);
            if (bone == null)
            {
                missingBoneCount++;
                if (firstMissingBonePath == null)
                    firstMissingBonePath = $"{root.name}: {offset.BonePath}";
                continue;
            }

            float weight = Mathf.Clamp01(profile.Weight * offset.Weight * actionWeight);
            if (weight <= 0f)
                continue;

            appliedRotations.Add(new AppliedBoneRotation(bone));

            Quaternion offsetRotation = Quaternion.Euler(offset.LocalEulerOffset);
            if (weight < 1f)
                offsetRotation = Quaternion.Slerp(Quaternion.identity, offsetRotation, weight);

            bone.localRotation *= offsetRotation;
            appliedBoneCount++;
        }

        return appliedBoneCount;
    }

    private Transform ResolveBone(Transform root, string path)
    {
        if (root == null || string.IsNullOrWhiteSpace(path))
            return null;

        if (boneCache.TryGetValue(path, out Transform cachedBone))
            return cachedBone;

        Transform bone = ResolveRelativeTransform(root, path);
        boneCache[path] = bone;
        return bone;
    }

    private void RestoreAppliedOffsets()
    {
        for (int i = appliedRotations.Count - 1; i >= 0; i--)
        {
            AppliedBoneRotation applied = appliedRotations[i];
            if (applied.Bone != null)
                applied.Bone.localRotation = applied.LocalRotation;
        }

        appliedRotations.Clear();
    }

    private void SetDebugState(string message)
    {
        if (showDebugState)
            debugState = message;
    }

    private static Transform ResolveRelativeTransform(Transform root, string path)
    {
        if (root == null || string.IsNullOrWhiteSpace(path))
            return null;

        if (string.Equals(path, root.name, System.StringComparison.Ordinal))
            return root;

        string rootPrefix = $"{root.name}/";
        if (path.StartsWith(rootPrefix, System.StringComparison.Ordinal))
            path = path.Substring(rootPrefix.Length);

        return root.Find(path);
    }
}
