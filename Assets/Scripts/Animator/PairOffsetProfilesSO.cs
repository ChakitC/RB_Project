using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PairOffsetProfiles", menuName = "Game/Characters/Pair Offset Profiles")]
public sealed class PairOffsetProfilesSO : ScriptableObject
{
    [SerializeField, Tooltip("รายการ rotation offset ที่เลือกตามคู่ Base Clip + Upper Body Clip")]
    private List<PairOffsetProfile> pairOffsetProfiles = new List<PairOffsetProfile>();

    public IReadOnlyList<PairOffsetProfile> PairOffsetProfiles => pairOffsetProfiles;

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
        AnimationClip baseClip,
        AnimationClip upperBodyClip,
        float weight)
    {
        if (pairOffsetProfiles == null)
            pairOffsetProfiles = new List<PairOffsetProfile>();

        PairOffsetProfile profile = FindProfile(baseClip, upperBodyClip, true);
        if (profile == null)
        {
            profile = new PairOffsetProfile();
            pairOffsetProfiles.Add(profile);
        }

        profile.Configure(enabled, profileName, baseClip, upperBodyClip, weight);
        return profile;
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
        [SerializeField, Tooltip("เปิดหรือปิด profile นี้")]
        private bool enabled = true;
        [SerializeField, Tooltip("ชื่อ profile สำหรับอ่านใน inspector และ browser")]
        private string profileName;
        [SerializeField, Tooltip("คลิป locomotion/base ที่ profile นี้ผูกอยู่")]
        private AnimationClip baseClip;
        [SerializeField, Tooltip("คลิปช่วงบนที่ profile นี้ผูกอยู่")]
        private AnimationClip upperBodyClip;
        [SerializeField, Range(0f, 1f), Tooltip("น้ำหนักรวมของ offset ทั้ง profile")]
        private float weight = 1f;
        [SerializeField, Tooltip("รายการกระดูกที่มี local rotation offset")]
        private List<BoneRotationOffset> boneOffsets = new List<BoneRotationOffset>();

        public bool Enabled => enabled;
        public string ProfileName => profileName;
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
            this.enabled = enabled;
            this.profileName = profileName;
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
        [SerializeField, Tooltip("เปิดหรือปิด offset ของกระดูกนี้")]
        private bool enabled = true;
        [SerializeField, Tooltip("path ของกระดูกเทียบจาก Animator root")]
        private string bonePath;
        [SerializeField, Tooltip("มุมหมุน local ที่จะทาเพิ่มหลัง animation sample")]
        private Vector3 localEulerOffset;
        [SerializeField, Range(0f, 1f), Tooltip("น้ำหนัก offset ของกระดูกนี้")]
        private float weight = 1f;

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
