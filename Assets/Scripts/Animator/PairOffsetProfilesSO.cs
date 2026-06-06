using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PairOffsetProfiles", menuName = "Game/Characters/Pair Offset Profiles")]
public sealed class PairOffsetProfilesSO : ScriptableObject
{
    [SerializeField, Tooltip("Rotation offsets keyed by a locomotion pose and upper-body action.")]
    private List<PairOffsetProfile> pairOffsetProfiles = new List<PairOffsetProfile>();

    public IReadOnlyList<PairOffsetProfile> PairOffsetProfiles => pairOffsetProfiles;

    public PairOffsetProfile FindProfile(
        PairOffsetBasePose basePose,
        PairOffsetUpperAction upperAction,
        bool includeDisabled)
    {
        if (pairOffsetProfiles == null ||
            basePose == PairOffsetBasePose.None ||
            upperAction == PairOffsetUpperAction.None)
        {
            return null;
        }

        for (int i = 0; i < pairOffsetProfiles.Count; i++)
        {
            PairOffsetProfile profile = pairOffsetProfiles[i];
            if (profile == null)
                continue;

            if (!includeDisabled && !profile.Enabled)
                continue;

            if (profile.BasePose == basePose && profile.UpperAction == upperAction)
                return profile;
        }

        return null;
    }

    public PairOffsetProfile FindProfile(AnimationClip baseClip, AnimationClip upperBodyClip, bool includeDisabled)
    {
        if (pairOffsetProfiles == null)
            return null;

        for (int i = 0; i < pairOffsetProfiles.Count; i++)
        {
            PairOffsetProfile profile = pairOffsetProfiles[i];
            if (profile == null)
                continue;

            if (!includeDisabled && !profile.Enabled)
                continue;

            if (profile.BaseClip == baseClip && profile.UpperBodyClip == upperBodyClip)
                return profile;
        }

        return null;
    }

    public PairOffsetProfile UpsertProfile(
        bool enabled,
        string profileName,
        PairOffsetBasePose basePose,
        PairOffsetUpperAction upperAction,
        AnimationClip baseClip,
        AnimationClip upperBodyClip,
        float weight)
    {
        if (pairOffsetProfiles == null)
            pairOffsetProfiles = new List<PairOffsetProfile>();

        PairOffsetProfile profile = FindProfile(basePose, upperAction, true);
        if (profile == null)
            profile = FindProfile(baseClip, upperBodyClip, true);

        if (profile == null)
        {
            profile = new PairOffsetProfile();
            pairOffsetProfiles.Add(profile);
        }

        profile.Configure(enabled, profileName, basePose, upperAction, baseClip, upperBodyClip, weight);
        return profile;
    }

    public PairOffsetProfile UpsertProfile(
        bool enabled,
        string profileName,
        AnimationClip baseClip,
        AnimationClip upperBodyClip,
        float weight)
    {
        return UpsertProfile(
            enabled,
            profileName,
            PairOffsetBasePose.None,
            PairOffsetUpperAction.None,
            baseClip,
            upperBodyClip,
            weight);
    }

    public void NormalizeProfiles()
    {
        if (pairOffsetProfiles == null)
            pairOffsetProfiles = new List<PairOffsetProfile>();

        for (int i = 0; i < pairOffsetProfiles.Count; i++)
            pairOffsetProfiles[i]?.Normalize();
    }

    private void OnValidate()
    {
        NormalizeProfiles();
    }

    [Serializable]
    public sealed class PairOffsetProfile
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private string profileName;
        [SerializeField] private PairOffsetBasePose basePose;
        [SerializeField] private PairOffsetUpperAction upperAction;
        [SerializeField] private AnimationClip baseClip;
        [SerializeField] private AnimationClip upperBodyClip;
        [SerializeField, Range(0f, 1f)] private float weight = 1f;
        [SerializeField] private List<BoneRotationOffset> boneOffsets = new List<BoneRotationOffset>();

        public bool Enabled => enabled;
        public string ProfileName => profileName;
        public PairOffsetBasePose BasePose => basePose;
        public PairOffsetUpperAction UpperAction => upperAction;
        public AnimationClip BaseClip => baseClip;
        public AnimationClip UpperBodyClip => upperBodyClip;
        public float Weight => Mathf.Clamp01(weight);
        public IReadOnlyList<BoneRotationOffset> BoneOffsets => boneOffsets;

        public void Configure(
            bool enabled,
            string profileName,
            AnimationClip baseClip,
            AnimationClip upperBodyClip,
            float weight)
        {
            Configure(
                enabled,
                profileName,
                basePose,
                upperAction,
                baseClip,
                upperBodyClip,
                weight);
        }

        public void Configure(
            bool enabled,
            string profileName,
            PairOffsetBasePose basePose,
            PairOffsetUpperAction upperAction,
            AnimationClip baseClip,
            AnimationClip upperBodyClip,
            float weight)
        {
            this.enabled = enabled;
            this.profileName = profileName;
            if (basePose != PairOffsetBasePose.None)
                this.basePose = basePose;
            if (upperAction != PairOffsetUpperAction.None)
                this.upperAction = upperAction;
            this.baseClip = baseClip;
            this.upperBodyClip = upperBodyClip;
            this.weight = Mathf.Clamp01(weight);
        }

        public void ClearBoneOffsets()
        {
            if (boneOffsets == null)
                boneOffsets = new List<BoneRotationOffset>();
            else
                boneOffsets.Clear();
        }

        public void AddBoneOffset(bool enabled, string bonePath, Vector3 localEulerOffset, float weight)
        {
            if (boneOffsets == null)
                boneOffsets = new List<BoneRotationOffset>();

            var offset = new BoneRotationOffset();
            offset.Configure(enabled, bonePath, localEulerOffset, weight);
            boneOffsets.Add(offset);
        }

        internal void Normalize()
        {
            weight = Mathf.Clamp01(weight);

            if (boneOffsets == null)
                boneOffsets = new List<BoneRotationOffset>();

            for (int i = 0; i < boneOffsets.Count; i++)
                boneOffsets[i]?.Normalize();
        }
    }

    [Serializable]
    public sealed class BoneRotationOffset
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private string bonePath;
        [SerializeField] private Vector3 localEulerOffset;
        [SerializeField, Range(0f, 1f)] private float weight = 1f;

        public bool Enabled => enabled;
        public string BonePath => bonePath;
        public Vector3 LocalEulerOffset => localEulerOffset;
        public float Weight => Mathf.Clamp01(weight);

        public void Configure(bool enabled, string bonePath, Vector3 localEulerOffset, float weight)
        {
            this.enabled = enabled;
            this.bonePath = bonePath;
            this.localEulerOffset = localEulerOffset;
            this.weight = Mathf.Clamp01(weight);
        }

        internal void Normalize()
        {
            weight = Mathf.Clamp01(weight);
        }
    }
}
