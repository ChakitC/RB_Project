using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Plays one AnimationClip or a masked upper-body overlay for scene preview, with an edit-mode root motion toggle.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Animation/Solo Root Motion Preview")]
[DefaultExecutionOrder(DefaultExecutionOrder)]
public sealed class SoloRootMotionPreview : MonoBehaviour, IAnimationClipSource
{
    public enum PreviewMode
    {
        SingleClip = 0,
        UpperBody = 1,
    }

    public const int DefaultExecutionOrder = -5000;
    private const float RootMotionScrubStep = 1f / 30f;
    private const int MaxRootMotionScrubSteps = 300;

    [Serializable]
    private struct TransformPose
    {
        public Transform transform;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;

        public TransformPose(Transform transform)
        {
            this.transform = transform;
            localPosition = transform.localPosition;
            localRotation = transform.localRotation;
            localScale = transform.localScale;
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

    [Serializable]
    private sealed class BoneRotationOffset
    {
        [Tooltip("เปิดหรือปิด offset ของกระดูกนี้")]
        public bool enabled = true;
        [Tooltip("Transform กระดูกที่ใช้ใน preview")]
        public Transform bone;
        [Tooltip("path ของกระดูกเทียบจาก Animator root")]
        public string bonePath;
        [Tooltip("มุมหมุน local ที่จะทาเพิ่มหลัง animation sample")]
        public Vector3 localEulerOffset;
        [Tooltip("น้ำหนัก offset ของกระดูกนี้")]
        [Range(0f, 1f)] public float weight = 1f;
    }

    [Serializable]
    private sealed class PairOffsetProfile
    {
        [Tooltip("เปิดหรือปิด profile นี้")]
        public bool enabled = true;
        [Tooltip("ชื่อ profile สำหรับอ่านใน inspector และ SO browser")]
        public string profileName;
        [Tooltip("Runtime locomotion pose key used by CharacterPairOffsetApplier")]
        public PairOffsetBasePose basePose;
        [Tooltip("Runtime upper-body action key used by CharacterPairOffsetApplier")]
        public PairOffsetUpperAction upperAction;
        [Tooltip("คลิป locomotion/base ที่ profile นี้ผูกอยู่")]
        public AnimationClip baseClip;
        [Tooltip("คลิปช่วงบนที่ profile นี้ผูกอยู่")]
        public AnimationClip upperBodyClip;
        [Tooltip("น้ำหนักรวมของ offset ทั้ง profile")]
        [Range(0f, 1f)] public float weight = 1f;
        [Tooltip("รายการกระดูกที่มี local rotation offset")]
        public List<BoneRotationOffset> boneOffsets = new List<BoneRotationOffset>();
    }

    private readonly struct AppliedBoneRotation
    {
        public readonly Transform bone;
        public readonly Quaternion localRotation;

        public AppliedBoneRotation(Transform bone)
        {
            this.bone = bone;
            localRotation = bone.localRotation;
        }
    }

    [SerializeField, Tooltip("Animator ของตัวละครหรือโมเดลที่ component นี้ใช้ควบคุม preview")]
    private Animator animator;

    [SerializeField, Tooltip("โหมดการสร้าง preview graph: เล่นคลิปเดียว หรือซ้อนคลิปช่วงบน")]
    private PreviewMode previewMode;

    [SerializeField, Tooltip("คลิปหลักที่ใช้ preview ถ้าเป็น Upper Body จะเป็น locomotion/base clip")]
    private AnimationClip clip;

    [SerializeField, Tooltip("คลิปช่วงบนที่จะซ้อนทับบน base clip ในโหมด Upper Body")]
    private AnimationClip upperBodyClip;

    [SerializeField, Tooltip("AvatarMask ของ layer ช่วงบน เว้นว่างได้เฉพาะกรณีต้องการให้ overlay กระทบทั้งตัว")]
    private AvatarMask upperBodyMask;

    [SerializeField, Range(0f, 1f), Tooltip("น้ำหนัก blend ของ layer ช่วงบน")]
    private float upperBodyWeight = 1f;

    [SerializeField, Tooltip("เปิดใช้ local rotation offset เฉพาะคู่ Base Clip + Upper Body Clip หลัง animation sample")]
    private bool applyPairBoneOffsets;

    [SerializeField, Tooltip("SO เป้าหมายสำหรับ save/load pair offset profile เพื่อเตรียมใช้กับระบบจริง")]
    private PairOffsetProfilesSO pairOffsetProfileAsset;

    [SerializeField, Tooltip("รายการ local profile ใน component นี้ แก้ปกติผ่าน Current Working Profile")]
    private List<PairOffsetProfile> pairOffsetProfiles = new List<PairOffsetProfile>();

    [SerializeField, Range(0f, 1f), Tooltip("เวลาเริ่มต้นของ preview ในช่วง 0 ถึง 1")]
    private float normalizedStartTime;

    [SerializeField, Tooltip("ความเร็วในการเล่น preview")]
    private float speed = 1f;

    [SerializeField, Tooltip("ให้ root motion ขยับ transform ของ Animator ระหว่าง preview")]
    private bool applyRootMotion;

    [SerializeField, Tooltip("เมื่อปิด component ให้หยุดและ rewind preview กลับจุดเริ่ม")]
    private bool stopOnDisable = true;

    private PlayableGraph graph;
    private AnimationClipPlayable playable;
    private AnimationClipPlayable upperBodyPlayable;
    private AnimationLayerMixerPlayable layerMixer;
    private bool isPlaying;
    private bool hasCachedAnimatorRootMotion;
    private bool cachedAnimatorRootMotion;
    private Animator cachedAnimator;
    private PreviewMode activePreviewMode;
    private AnimationClip activeClip;
    private AnimationClip activeUpperBodyClip;
    private AvatarMask activeUpperBodyMask;
    private bool hasPreviewOrigin;
    private Vector3 previewOriginPosition;
    private Quaternion previewOriginRotation;
    private Vector3 previewOriginScale;
    private readonly List<AppliedBoneRotation> appliedPairBoneRotations = new List<AppliedBoneRotation>();

    [SerializeField, HideInInspector]
    private bool hasRestorePose;

    [SerializeField, HideInInspector]
    private Transform restorePoseRoot;

    [SerializeField, HideInInspector]
    private TransformPose[] restorePose = Array.Empty<TransformPose>();

#if UNITY_EDITOR
    [SerializeField, Tooltip("ให้ clip ถูก apply อัตโนมัติใน Edit Mode")]
    private bool applyInEditMode = true;

    private double lastEditorUpdateTime;
#endif

    public Animator Animator
    {
        get => animator;
        set
        {
            if (animator == value)
                return;

            StopPreview(false);
            animator = value;
            Play();
        }
    }

    public PreviewMode Mode
    {
        get => previewMode;
        set
        {
            if (previewMode == value)
                return;

            previewMode = value;
            Play();
        }
    }

    public AnimationClip Clip
    {
        get => clip;
        set
        {
            if (clip == value)
                return;

            clip = value;
            Play();
        }
    }

    public AnimationClip UpperBodyClip
    {
        get => upperBodyClip;
        set
        {
            if (upperBodyClip == value)
                return;

            upperBodyClip = value;
            Play();
        }
    }

    public AvatarMask UpperBodyMask
    {
        get => upperBodyMask;
        set
        {
            if (upperBodyMask == value)
                return;

            upperBodyMask = value;
            Play();
        }
    }

    public float UpperBodyWeight
    {
        get => upperBodyWeight;
        set
        {
            upperBodyWeight = Mathf.Clamp01(value);
            ApplyUpperBodyLayerSettings();
        }
    }

    public string ActivePairOffsetProfileLabel
    {
        get
        {
            if (!applyPairBoneOffsets)
                return "Disabled";

            PairOffsetProfile profile = ResolveActivePairOffsetProfile();
            return profile != null ? GetPairOffsetProfileName(profile) : "None";
        }
    }

    public PairOffsetProfilesSO PairOffsetProfileAsset
    {
        get => pairOffsetProfileAsset;
        set => pairOffsetProfileAsset = value;
    }

    public bool CanSaveCurrentPairOffsetProfileToAsset =>
        pairOffsetProfileAsset != null &&
        previewMode == PreviewMode.UpperBody &&
        clip != null &&
        upperBodyClip != null &&
        FindPairOffsetProfile(clip, upperBodyClip, true) != null;

    public bool CanLoadCurrentPairOffsetProfileFromAsset =>
        pairOffsetProfileAsset != null &&
        previewMode == PreviewMode.UpperBody &&
        clip != null &&
        upperBodyClip != null &&
        pairOffsetProfileAsset.FindProfile(clip, upperBodyClip, true) != null;

    public int PairOffsetProfileAssetProfileCount => pairOffsetProfileAsset?.PairOffsetProfiles?.Count ?? 0;

    public float Length { get; private set; } = float.NaN;
    public bool IsLooping { get; private set; }
    public bool IsInitialized => graph.IsValid();

    public bool IsPlaying
    {
        get => isPlaying;
        set
        {
            if (value)
                Play();
            else
                Pause();
        }
    }

    public float NormalizedStartTime
    {
        get => normalizedStartTime;
        set => normalizedStartTime = Mathf.Clamp01(value);
    }

    public float Time
    {
        get
        {
            if (playable.IsValid())
                return (float)playable.GetTime();

            return float.IsNaN(Length) ? 0f : normalizedStartTime * Length;
        }
        set
        {
            if (!playable.IsValid())
                return;

            SetPreviewTime(value);
        }
    }

    public float NormalizedTime
    {
        get
        {
            if (Length <= 0f)
                return 0f;

            return Time / Length;
        }
        set
        {
            if (Length <= 0f)
                return;

            Time = value * Length;
        }
    }

    public float Speed
    {
        get => speed;
        set
        {
            speed = value;
            ApplyPlayableSpeedSettings();
        }
    }

    public bool ApplyRootMotion
    {
        get => applyRootMotion;
        set
        {
            if (applyRootMotion == value)
                return;

            applyRootMotion = value;
            ApplyAnimatorRootMotionSetting();
        }
    }

    public bool StopOnDisable
    {
        get => stopOnDisable;
        set => stopOnDisable = value;
    }

    public bool CreatePairOffsetProfileFromCurrentClips()
    {
        if (clip == null || upperBodyClip == null)
            return false;

        if (pairOffsetProfiles == null)
            pairOffsetProfiles = new List<PairOffsetProfile>();

        PairOffsetProfile profile = FindPairOffsetProfile(clip, upperBodyClip, true);
        if (profile == null)
        {
            profile = new PairOffsetProfile
            {
                baseClip = clip,
                upperBodyClip = upperBodyClip,
            };
            pairOffsetProfiles.Add(profile);
        }

        applyPairBoneOffsets = true;
        profile.enabled = true;
        if (string.IsNullOrWhiteSpace(profile.profileName))
            profile.profileName = GetDefaultPairOffsetProfileName(clip, upperBodyClip);

        NormalizePairOffsetProfiles();
        return true;
    }

    public int AddBoneOffsetsToCurrentPairProfile(IList<Transform> bones)
    {
        if (bones == null || bones.Count == 0)
            return 0;

        if (!CreatePairOffsetProfileFromCurrentClips())
            return 0;

        PairOffsetProfile profile = FindPairOffsetProfile(clip, upperBodyClip, true);
        if (profile == null)
            return 0;

        if (profile.boneOffsets == null)
            profile.boneOffsets = new List<BoneRotationOffset>();

        Transform previewRoot = GetPreviewRoot();
        int added = 0;

        for (int i = 0; i < bones.Count; i++)
        {
            Transform bone = bones[i];
            if (bone == null || previewRoot == null || bone == previewRoot || !bone.IsChildOf(previewRoot))
                continue;

            string bonePath = GetRelativePath(previewRoot, bone);
            if (string.IsNullOrEmpty(bonePath) || HasBoneOffset(profile, bone, bonePath))
                continue;

            profile.boneOffsets.Add(new BoneRotationOffset
            {
                bone = bone,
                bonePath = bonePath,
            });
            added++;
        }

        NormalizePairOffsetProfiles();
        return added;
    }

    public void RefreshPairOffsetBonePaths()
    {
        Transform previewRoot = GetPreviewRoot();
        if (previewRoot == null || pairOffsetProfiles == null)
            return;

        for (int profileIndex = 0; profileIndex < pairOffsetProfiles.Count; profileIndex++)
        {
            PairOffsetProfile profile = pairOffsetProfiles[profileIndex];
            if (profile?.boneOffsets == null)
                continue;

            for (int offsetIndex = 0; offsetIndex < profile.boneOffsets.Count; offsetIndex++)
            {
                BoneRotationOffset offset = profile.boneOffsets[offsetIndex];
                if (offset == null)
                    continue;

                if (offset.bone != null && offset.bone != previewRoot && offset.bone.IsChildOf(previewRoot))
                {
                    offset.bonePath = GetRelativePath(previewRoot, offset.bone);
                }
                else if (offset.bone == null && !string.IsNullOrEmpty(offset.bonePath))
                {
                    offset.bone = ResolveRelativeTransform(previewRoot, offset.bonePath);
                }
            }
        }
    }

    public int CaptureCurrentPoseAsActivePairOffsets()
    {
        PairOffsetProfile profile = ResolveActivePairOffsetProfile();
        if (profile == null || profile.boneOffsets == null)
            return 0;

        int captured = 0;
        for (int i = 0; i < profile.boneOffsets.Count; i++)
        {
            BoneRotationOffset offset = profile.boneOffsets[i];
            if (offset == null || !offset.enabled)
                continue;

            Transform bone = ResolvePairOffsetBone(offset);
            if (bone == null || !TryGetLastSampledLocalRotation(bone, out Quaternion sampledLocalRotation))
                continue;

            Quaternion delta = Quaternion.Inverse(sampledLocalRotation) * bone.localRotation;
            Vector3 localEulerOffset = NormalizeEuler(delta.eulerAngles);

            float appliedWeight = Mathf.Clamp01(profile.weight * offset.weight);
            if (appliedWeight > 0.0001f && appliedWeight < 0.9999f)
                localEulerOffset = NormalizeEuler(localEulerOffset / appliedWeight);

            offset.localEulerOffset = localEulerOffset;
            captured++;
        }

        NormalizePairOffsetProfiles();
        return captured;
    }

    public bool SaveCurrentPairOffsetProfileToAsset()
    {
        if (pairOffsetProfileAsset == null || clip == null || upperBodyClip == null)
            return false;

        PairOffsetProfile profile = FindPairOffsetProfile(clip, upperBodyClip, true);
        if (profile == null)
            return false;

        RefreshPairOffsetBonePaths();
        NormalizePairOffsetProfiles();

        PairOffsetProfilesSO.PairOffsetProfile assetProfile = pairOffsetProfileAsset.UpsertProfile(
            profile.enabled,
            GetPairOffsetProfileName(profile),
            profile.basePose,
            profile.upperAction,
            profile.baseClip,
            profile.upperBodyClip,
            profile.weight);

        assetProfile.ClearBoneOffsets();

        if (profile.boneOffsets != null)
        {
            Transform previewRoot = GetPreviewRoot();
            for (int i = 0; i < profile.boneOffsets.Count; i++)
            {
                BoneRotationOffset offset = profile.boneOffsets[i];
                if (offset == null)
                    continue;

                string bonePath = offset.bonePath;
                if (string.IsNullOrEmpty(bonePath) &&
                    previewRoot != null &&
                    offset.bone != null &&
                    offset.bone != previewRoot &&
                    offset.bone.IsChildOf(previewRoot))
                {
                    bonePath = GetRelativePath(previewRoot, offset.bone);
                }

                if (string.IsNullOrEmpty(bonePath))
                    continue;

                assetProfile.AddBoneOffset(offset.enabled, bonePath, offset.localEulerOffset, offset.weight);
            }
        }

        pairOffsetProfileAsset.NormalizeProfiles();
        return true;
    }

    public bool LoadCurrentPairOffsetProfileFromAsset()
    {
        if (pairOffsetProfileAsset == null || clip == null || upperBodyClip == null)
            return false;

        PairOffsetProfilesSO.PairOffsetProfile assetProfile = pairOffsetProfileAsset.FindProfile(clip, upperBodyClip, true);
        if (assetProfile == null)
            return false;

        bool loaded = CopyAssetPairOffsetProfileToPreview(assetProfile);
        if (loaded)
        {
            applyPairBoneOffsets = true;
            NormalizePairOffsetProfiles();
            RefreshPairOffsetBonePaths();
        }

        return loaded;
    }

    public bool LoadPairOffsetProfileFromAsset(int profileIndex)
    {
        PairOffsetProfilesSO.PairOffsetProfile assetProfile = GetPairOffsetProfileFromAsset(profileIndex);
        if (assetProfile == null)
            return false;

        bool loaded = CopyAssetPairOffsetProfileToPreview(assetProfile);
        if (loaded)
        {
            previewMode = PreviewMode.UpperBody;
            clip = assetProfile.BaseClip;
            upperBodyClip = assetProfile.UpperBodyClip;
            applyPairBoneOffsets = true;
            NormalizePairOffsetProfiles();
            RefreshPairOffsetBonePaths();
        }

        return loaded;
    }

    public int LoadAllPairOffsetProfilesFromAsset()
    {
        if (pairOffsetProfileAsset == null || pairOffsetProfileAsset.PairOffsetProfiles == null)
            return 0;

        int loaded = 0;
        IReadOnlyList<PairOffsetProfilesSO.PairOffsetProfile> assetProfiles = pairOffsetProfileAsset.PairOffsetProfiles;
        for (int i = 0; i < assetProfiles.Count; i++)
        {
            if (CopyAssetPairOffsetProfileToPreview(assetProfiles[i]))
                loaded++;
        }

        if (loaded > 0)
        {
            applyPairBoneOffsets = true;
            NormalizePairOffsetProfiles();
            RefreshPairOffsetBonePaths();
        }

        return loaded;
    }

    private PairOffsetProfilesSO.PairOffsetProfile GetPairOffsetProfileFromAsset(int profileIndex)
    {
        if (pairOffsetProfileAsset == null || pairOffsetProfileAsset.PairOffsetProfiles == null)
            return null;

        IReadOnlyList<PairOffsetProfilesSO.PairOffsetProfile> profiles = pairOffsetProfileAsset.PairOffsetProfiles;
        return profileIndex >= 0 && profileIndex < profiles.Count ? profiles[profileIndex] : null;
    }

#if UNITY_EDITOR
    public bool ApplyInEditMode
    {
        get => applyInEditMode;
        set
        {
            if (applyInEditMode == value)
                return;

            applyInEditMode = value;
            if (!Application.isPlaying)
            {
                if (applyInEditMode && enabled)
                    Play();
                else
                    StopPreview(false);
            }
        }
    }
#endif

#if UNITY_EDITOR
    private void Reset()
    {
        animator = GetComponent<Animator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
            animator = GetComponentInParent<Animator>();

        CacheRestorePose();
    }

    private void OnValidate()
    {
        normalizedStartTime = Mathf.Clamp01(normalizedStartTime);
        upperBodyWeight = Mathf.Clamp01(upperBodyWeight);
        NormalizePairOffsetProfiles();
        RefreshPairOffsetBonePaths();
        if (Application.isPlaying)
            return;

        ApplyAnimatorRootMotionSetting();
        ApplyUpperBodyLayerSettings();

        if (applyInEditMode && enabled)
        {
            if (!IsInitialized || IsGraphConfigurationDirty())
            {
                Play();
            }
            else if (playable.IsValid())
            {
                SetTime(normalizedStartTime * Length);
                Evaluate(0f);
            }
        }
        else
        {
            StopPreview(false);
        }
    }
#endif

    public void Play()
    {
        Play(clip);
    }

    public void Play(AnimationClip previewClip)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && !applyInEditMode)
            return;
#endif

        if (previewClip == null)
        {
            Length = 0f;
            IsLooping = false;
            StopPreview(false);
            return;
        }

        clip = previewClip;
        Length = GetPreviewLength(clip);
        IsLooping = GetPreviewLooping(clip);

        if (animator == null)
        {
            StopPreview(false);
            return;
        }

        EnsureRestorePoseCached();

        if (!hasPreviewOrigin)
            CachePreviewOrigin();

        ApplyAnimatorRootMotionSetting();
        RestoreAppliedPairBoneOffsets();

        if (graph.IsValid())
            graph.Destroy();

        CreatePlayableGraph();
        ApplyPlayableSpeedSettings();
        SetTime(normalizedStartTime * Length);

        if (speed != 0f)
        {
            isPlaying = true;
            graph.Play();
        }
        else
        {
            isPlaying = false;
            graph.Stop();
        }

        Evaluate(0f);

#if UNITY_EDITOR
        RegisterEditorUpdate();
#endif
    }

    public void Pause()
    {
        isPlaying = false;

        if (graph.IsValid())
            graph.Stop();
    }

    public void Rewind()
    {
        if (!IsInitialized)
            Play();

        if (playable.IsValid())
        {
            RestorePreviewRootToOrigin();
            SetTime(normalizedStartTime * Length);
            Evaluate(0f);
        }
    }

    public void ResetPreviewOrigin()
    {
        Transform previewRoot = GetPreviewRoot();
        if (previewRoot == null)
            return;

        if (!hasPreviewOrigin)
            CachePreviewOrigin();

        RestorePreviewRootToOrigin();

        Rewind();
    }

    public void Evaluate()
    {
        Evaluate(0f);
    }

    public void RefreshPreviewPose()
    {
        if (!playable.IsValid())
            return;

        SetTime(Time);
        Evaluate(0f);
    }

    public void Evaluate(float deltaTime)
    {
        if (!graph.IsValid())
            return;

        RestoreAppliedPairBoneOffsets();
        ApplyAnimatorRootMotionSetting();
        ApplyUpperBodyLayerSettings();
        graph.Evaluate(deltaTime);
        ApplyActivePairBoneOffsets();
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (applyInEditMode)
                Play();

            RegisterEditorUpdate();
            return;
        }
#endif

        Play();
    }

    private void LateUpdate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            return;
#endif

        if (isPlaying && graph.IsValid())
            Evaluate(UnityEngine.Time.deltaTime);

        StopAtClipEndIfNeeded();
    }

    private void OnDisable()
    {
        if (stopOnDisable)
        {
            Rewind();
            StopPreview(true);
        }
        else
        {
            Pause();
            RestoreAnimatorRootMotionSetting();
        }

#if UNITY_EDITOR
        UnregisterEditorUpdate();
#endif
    }

    private void OnDestroy()
    {
        StopPreview(true);
        RestoreCachedPose();

#if UNITY_EDITOR
        UnregisterEditorUpdate();
#endif
    }

    private void StopPreview(bool clearOrigin)
    {
        isPlaying = false;
        RestoreAppliedPairBoneOffsets();

        if (graph.IsValid())
        {
            graph.Stop();
            graph.Destroy();
        }

        RestoreAnimatorRootMotionSetting();

        if (clearOrigin)
            hasPreviewOrigin = false;
    }

    private void StopAtClipEndIfNeeded()
    {
        if (!isPlaying ||
            IsLooping ||
            !playable.IsValid())
            return;

        float currentTime = (float)playable.GetTime();
        if (speed >= 0f && currentTime >= Length)
        {
            Pause();
            Time = Length;
        }
        else if (speed < 0f && currentTime <= 0f)
        {
            Pause();
            Time = 0f;
        }
    }

    private void SetTime(double value)
    {
        SetPlayableTime(playable, value);

        if (upperBodyPlayable.IsValid())
            SetPlayableTime(upperBodyPlayable, value);
    }

    private void SetPreviewTime(double value)
    {
        if (!playable.IsValid())
            return;

        double targetTime = ClampPreviewTime(value);

        if (ShouldScrubRootMotion())
        {
            ScrubRootMotionToTime(targetTime);
            return;
        }

        SetTime(targetTime);
        Evaluate(0f);
    }

    private double ClampPreviewTime(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return 0d;

        if (Length <= 0f)
            return 0d;

        return Math.Max(0d, Math.Min(value, Length));
    }

    private bool ShouldScrubRootMotion()
    {
#if UNITY_EDITOR
        return applyRootMotion && !Application.isPlaying;
#else
        return false;
#endif
    }

    private void ScrubRootMotionToTime(double targetTime)
    {
        if (!playable.IsValid())
            return;

        if (!hasPreviewOrigin)
            CachePreviewOrigin();

        RestoreAppliedPairBoneOffsets();
        RestorePreviewRootToOrigin();
        ApplyAnimatorRootMotionSetting();

        bool wasPlaying = isPlaying;
        double startTime = ClampPreviewTime(normalizedStartTime * Length);
        double deltaTime = targetTime - startTime;
        double direction = Math.Sign(deltaTime);
        double remaining = Math.Abs(deltaTime);

        graph.Play();
        SetTime(startTime);

        SetPlayableSpeed(0d);
        graph.Evaluate(0f);

        if (remaining > 0.0001d)
        {
            SetPlayableSpeed(direction);

            double stepSize = Math.Max(RootMotionScrubStep, remaining / MaxRootMotionScrubSteps);
            while (remaining > 0.0001d)
            {
                float step = (float)Math.Min(remaining, stepSize);
                graph.Evaluate(step);
                remaining -= step;
            }
        }

        SetTime(targetTime);
        ApplyPlayableSpeedSettings();

        if (wasPlaying)
            graph.Play();
        else
            graph.Stop();

        ApplyActivePairBoneOffsets();

#if UNITY_EDITOR
        lastEditorUpdateTime = EditorApplication.timeSinceStartup;
#endif
    }

    private void EnsureRestorePoseCached()
    {
        Transform previewRoot = GetPreviewRoot();
        if (previewRoot == null)
            return;

        if (hasRestorePose && restorePoseRoot == previewRoot && restorePose != null && restorePose.Length > 0)
            return;

        CacheRestorePose();
    }

    private void CacheRestorePose()
    {
        Transform previewRoot = GetPreviewRoot();
        if (previewRoot == null)
            return;

        Transform[] transforms = previewRoot.GetComponentsInChildren<Transform>(true);
        restorePose = new TransformPose[transforms.Length];

        for (int i = 0; i < transforms.Length; i++)
            restorePose[i] = new TransformPose(transforms[i]);

        restorePoseRoot = previewRoot;
        hasRestorePose = true;
    }

    private void RestoreCachedPose()
    {
        if (!hasRestorePose || restorePose == null)
            return;

        for (int i = 0; i < restorePose.Length; i++)
            restorePose[i].Restore();
    }

    private void CachePreviewOrigin()
    {
        Transform previewRoot = GetPreviewRoot();
        if (previewRoot == null)
            return;

        previewOriginPosition = previewRoot.position;
        previewOriginRotation = previewRoot.rotation;
        previewOriginScale = previewRoot.localScale;
        hasPreviewOrigin = true;
    }

    private void RestorePreviewRootToOrigin()
    {
        if (!hasPreviewOrigin)
            return;

        Transform previewRoot = GetPreviewRoot();
        if (previewRoot == null)
            return;

        previewRoot.SetPositionAndRotation(previewOriginPosition, previewOriginRotation);
        previewRoot.localScale = previewOriginScale;
    }

    internal Transform GetPreviewRoot()
    {
        return animator != null ? animator.transform : transform;
    }

    private void ApplyAnimatorRootMotionSetting()
    {
        if (animator == null)
            return;

        if (!hasCachedAnimatorRootMotion || cachedAnimator != animator)
        {
            RestoreAnimatorRootMotionSetting();
            cachedAnimator = animator;
            cachedAnimatorRootMotion = animator.applyRootMotion;
            hasCachedAnimatorRootMotion = true;
        }

        if (animator.applyRootMotion != applyRootMotion)
            animator.applyRootMotion = applyRootMotion;
    }

    private void RestoreAnimatorRootMotionSetting()
    {
        if (!hasCachedAnimatorRootMotion || cachedAnimator == null)
            return;

        cachedAnimator.applyRootMotion = cachedAnimatorRootMotion;
        cachedAnimator = null;
        hasCachedAnimatorRootMotion = false;
    }

    private void CreatePlayableGraph()
    {
        if (ShouldBuildUpperBodyGraph())
            CreateUpperBodyPlayableGraph();
        else
            playable = AnimationPlayableUtilities.PlayClip(animator, clip, out graph);

        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        MarkGraphConfiguration();
    }

    private bool ShouldBuildUpperBodyGraph()
    {
        return previewMode == PreviewMode.UpperBody && upperBodyClip != null;
    }

    private void CreateUpperBodyPlayableGraph()
    {
        graph = PlayableGraph.Create($"{nameof(SoloRootMotionPreview)} Upper Body");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Animation", animator);
        playable = AnimationClipPlayable.Create(graph, clip);
        upperBodyPlayable = AnimationClipPlayable.Create(graph, upperBodyClip);
        layerMixer = AnimationLayerMixerPlayable.Create(graph, 2);

        graph.Connect(playable, 0, layerMixer, 0);
        graph.Connect(upperBodyPlayable, 0, layerMixer, 1);

        layerMixer.SetInputWeight(0, 1f);
        ApplyUpperBodyLayerSettings();

        output.SetSourcePlayable(layerMixer);
    }

    private float GetPreviewLength(AnimationClip baseClip)
    {
        float length = baseClip != null ? baseClip.length : 0f;

        if (previewMode == PreviewMode.UpperBody && upperBodyClip != null)
            length = Mathf.Max(length, upperBodyClip.length);

        return length;
    }

    private bool GetPreviewLooping(AnimationClip baseClip)
    {
        if (baseClip == null)
            return false;

        if (previewMode != PreviewMode.UpperBody || upperBodyClip == null)
            return baseClip.isLooping;

        return baseClip.isLooping && upperBodyClip.isLooping;
    }

    private bool IsGraphConfigurationDirty()
    {
        return activePreviewMode != previewMode ||
               activeClip != clip ||
               activeUpperBodyClip != upperBodyClip ||
               activeUpperBodyMask != upperBodyMask;
    }

    private void MarkGraphConfiguration()
    {
        activePreviewMode = previewMode;
        activeClip = clip;
        activeUpperBodyClip = upperBodyClip;
        activeUpperBodyMask = upperBodyMask;
    }

    private void ApplyUpperBodyLayerSettings()
    {
        if (!layerMixer.IsValid())
            return;

        layerMixer.SetInputWeight(0, 1f);
        layerMixer.SetInputWeight(1, upperBodyWeight);

        if (upperBodyMask != null)
            layerMixer.SetLayerMaskFromAvatarMask(1, upperBodyMask);
    }

    private bool CopyAssetPairOffsetProfileToPreview(PairOffsetProfilesSO.PairOffsetProfile assetProfile)
    {
        if (assetProfile == null || assetProfile.BaseClip == null || assetProfile.UpperBodyClip == null)
            return false;

        if (pairOffsetProfiles == null)
            pairOffsetProfiles = new List<PairOffsetProfile>();

        PairOffsetProfile profile = FindPairOffsetProfile(assetProfile.BaseClip, assetProfile.UpperBodyClip, true);
        if (profile == null)
        {
            profile = new PairOffsetProfile();
            pairOffsetProfiles.Add(profile);
        }

        profile.enabled = assetProfile.Enabled;
        profile.profileName = string.IsNullOrWhiteSpace(assetProfile.ProfileName)
            ? GetDefaultPairOffsetProfileName(assetProfile.BaseClip, assetProfile.UpperBodyClip)
            : assetProfile.ProfileName;
        profile.basePose = assetProfile.BasePose;
        profile.upperAction = assetProfile.UpperAction;
        profile.baseClip = assetProfile.BaseClip;
        profile.upperBodyClip = assetProfile.UpperBodyClip;
        profile.weight = assetProfile.Weight;
        profile.boneOffsets = new List<BoneRotationOffset>();

        Transform previewRoot = GetPreviewRoot();
        IReadOnlyList<PairOffsetProfilesSO.BoneRotationOffset> assetOffsets = assetProfile.BoneOffsets;
        if (assetOffsets != null)
        {
            for (int i = 0; i < assetOffsets.Count; i++)
            {
                PairOffsetProfilesSO.BoneRotationOffset assetOffset = assetOffsets[i];
                if (assetOffset == null || string.IsNullOrEmpty(assetOffset.BonePath))
                    continue;

                profile.boneOffsets.Add(new BoneRotationOffset
                {
                    enabled = assetOffset.Enabled,
                    bone = previewRoot != null ? ResolveRelativeTransform(previewRoot, assetOffset.BonePath) : null,
                    bonePath = assetOffset.BonePath,
                    localEulerOffset = assetOffset.LocalEulerOffset,
                    weight = assetOffset.Weight,
                });
            }
        }

        return true;
    }

    private void ApplyActivePairBoneOffsets()
    {
        PairOffsetProfile profile = ResolveActivePairOffsetProfile();
        if (profile == null || profile.boneOffsets == null || profile.weight <= 0f)
            return;

        for (int i = 0; i < profile.boneOffsets.Count; i++)
        {
            BoneRotationOffset offset = profile.boneOffsets[i];
            if (offset == null || !offset.enabled || offset.weight <= 0f)
                continue;

            Transform bone = ResolvePairOffsetBone(offset);
            if (bone == null)
                continue;

            float weight = Mathf.Clamp01(profile.weight * offset.weight);
            if (weight <= 0f)
                continue;

            appliedPairBoneRotations.Add(new AppliedBoneRotation(bone));

            Quaternion offsetRotation = Quaternion.Euler(offset.localEulerOffset);
            if (weight < 1f)
                offsetRotation = Quaternion.Slerp(Quaternion.identity, offsetRotation, weight);

            bone.localRotation *= offsetRotation;
        }
    }

    private void RestoreAppliedPairBoneOffsets()
    {
        for (int i = appliedPairBoneRotations.Count - 1; i >= 0; i--)
        {
            AppliedBoneRotation applied = appliedPairBoneRotations[i];
            if (applied.bone != null)
                applied.bone.localRotation = applied.localRotation;
        }

        appliedPairBoneRotations.Clear();
    }

    private bool TryGetLastSampledLocalRotation(Transform bone, out Quaternion sampledLocalRotation)
    {
        for (int i = appliedPairBoneRotations.Count - 1; i >= 0; i--)
        {
            AppliedBoneRotation applied = appliedPairBoneRotations[i];
            if (applied.bone == bone)
            {
                sampledLocalRotation = applied.localRotation;
                return true;
            }
        }

        sampledLocalRotation = default;
        return false;
    }

    private PairOffsetProfile ResolveActivePairOffsetProfile()
    {
        if (!applyPairBoneOffsets ||
            previewMode != PreviewMode.UpperBody ||
            clip == null ||
            upperBodyClip == null ||
            pairOffsetProfiles == null)
        {
            return null;
        }

        return FindPairOffsetProfile(clip, upperBodyClip, false);
    }

    private PairOffsetProfile FindPairOffsetProfile(AnimationClip baseClip, AnimationClip overlayClip, bool includeDisabled)
    {
        if (pairOffsetProfiles == null)
            return null;

        for (int i = 0; i < pairOffsetProfiles.Count; i++)
        {
            PairOffsetProfile profile = pairOffsetProfiles[i];
            if (profile == null)
                continue;

            if (!includeDisabled && !profile.enabled)
                continue;

            if (profile.baseClip == baseClip && profile.upperBodyClip == overlayClip)
                return profile;
        }

        return null;
    }

    private Transform ResolvePairOffsetBone(BoneRotationOffset offset)
    {
        if (offset == null)
            return null;

        if (offset.bone != null)
            return offset.bone;

        Transform previewRoot = GetPreviewRoot();
        return previewRoot != null ? ResolveRelativeTransform(previewRoot, offset.bonePath) : null;
    }

    private void NormalizePairOffsetProfiles()
    {
        if (pairOffsetProfiles == null)
            return;

        for (int profileIndex = 0; profileIndex < pairOffsetProfiles.Count; profileIndex++)
        {
            PairOffsetProfile profile = pairOffsetProfiles[profileIndex];
            if (profile == null)
                continue;

            profile.weight = Mathf.Clamp01(profile.weight);
            if (string.IsNullOrWhiteSpace(profile.profileName))
                profile.profileName = GetDefaultPairOffsetProfileName(profile.baseClip, profile.upperBodyClip);

            if (profile.boneOffsets == null)
                profile.boneOffsets = new List<BoneRotationOffset>();

            for (int offsetIndex = 0; offsetIndex < profile.boneOffsets.Count; offsetIndex++)
            {
                BoneRotationOffset offset = profile.boneOffsets[offsetIndex];
                if (offset == null)
                    continue;

                offset.weight = Mathf.Clamp01(offset.weight);
            }
        }
    }

    private bool HasBoneOffset(PairOffsetProfile profile, Transform bone, string bonePath)
    {
        if (profile?.boneOffsets == null)
            return false;

        for (int i = 0; i < profile.boneOffsets.Count; i++)
        {
            BoneRotationOffset offset = profile.boneOffsets[i];
            if (offset == null)
                continue;

            if (offset.bone == bone)
                return true;

            if (!string.IsNullOrEmpty(bonePath) && string.Equals(offset.bonePath, bonePath, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private string GetPairOffsetProfileName(PairOffsetProfile profile)
    {
        if (profile == null)
            return "None";

        return string.IsNullOrWhiteSpace(profile.profileName)
            ? GetDefaultPairOffsetProfileName(profile.baseClip, profile.upperBodyClip)
            : profile.profileName;
    }

    private static string GetDefaultPairOffsetProfileName(AnimationClip baseClip, AnimationClip overlayClip)
    {
        string baseName = baseClip != null ? baseClip.name : "None";
        string overlayName = overlayClip != null ? overlayClip.name : "None";
        return $"{baseName} + {overlayName}";
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        if (root == null || target == null || target == root || !target.IsChildOf(root))
            return string.Empty;

        string path = target.name;
        Transform current = target.parent;

        while (current != null && current != root)
        {
            path = $"{current.name}/{path}";
            current = current.parent;
        }

        return current == root ? path : string.Empty;
    }

    private static Transform ResolveRelativeTransform(Transform root, string path)
    {
        if (root == null || string.IsNullOrWhiteSpace(path))
            return null;

        if (string.Equals(path, root.name, StringComparison.Ordinal))
            return root;

        string rootPrefix = $"{root.name}/";
        if (path.StartsWith(rootPrefix, StringComparison.Ordinal))
            path = path.Substring(rootPrefix.Length);

        return root.Find(path);
    }

    private static Vector3 NormalizeEuler(Vector3 euler)
    {
        return new Vector3(
            NormalizeEulerAngle(euler.x),
            NormalizeEulerAngle(euler.y),
            NormalizeEulerAngle(euler.z));
    }

    private static float NormalizeEulerAngle(float angle)
    {
        angle = Mathf.Repeat(angle + 180f, 360f) - 180f;
        return Mathf.Abs(angle) < 0.0001f ? 0f : angle;
    }

    private void ApplyPlayableSpeedSettings()
    {
        SetPlayableSpeed(speed);
    }

    private void SetPlayableSpeed(double value)
    {
        if (playable.IsValid())
            playable.SetSpeed(value);

        if (upperBodyPlayable.IsValid())
            upperBodyPlayable.SetSpeed(value);
    }

    private static void SetPlayableTime(AnimationClipPlayable target, double value)
    {
        if (!target.IsValid())
            return;

        target.SetTime(value);
        target.SetTime(value);
    }

    public void GetAnimationClips(List<AnimationClip> clips)
    {
        if (clip != null)
            clips.Add(clip);

        if (upperBodyClip != null && upperBodyClip != clip)
            clips.Add(upperBodyClip);
    }

#if UNITY_EDITOR
    private void RegisterEditorUpdate()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        lastEditorUpdateTime = EditorApplication.timeSinceStartup;
    }

    private void UnregisterEditorUpdate()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (Application.isPlaying ||
            !this ||
            !enabled ||
            !applyInEditMode)
        {
            UnregisterEditorUpdate();
            return;
        }

        if (!isPlaying ||
            !graph.IsValid())
        {
            lastEditorUpdateTime = EditorApplication.timeSinceStartup;
            return;
        }

        double currentTime = EditorApplication.timeSinceStartup;
        float deltaTime = Mathf.Min((float)(currentTime - lastEditorUpdateTime), 0.1f);
        lastEditorUpdateTime = currentTime;

        Evaluate(deltaTime);
        StopAtClipEndIfNeeded();

        SceneView.RepaintAll();
    }

#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(SoloRootMotionPreview))]
internal sealed class SoloRootMotionPreviewEditor : Editor
{
    private SerializedProperty animatorProperty;
    private SerializedProperty previewModeProperty;
    private SerializedProperty clipProperty;
    private SerializedProperty upperBodyClipProperty;
    private SerializedProperty upperBodyMaskProperty;
    private SerializedProperty upperBodyWeightProperty;
    private SerializedProperty applyPairBoneOffsetsProperty;
    private SerializedProperty pairOffsetProfileAssetProperty;
    private SerializedProperty pairOffsetProfilesProperty;
    private SerializedProperty applyInEditModeProperty;
    private SerializedProperty applyRootMotionProperty;
    private SerializedProperty normalizedStartTimeProperty;
    private SerializedProperty speedProperty;
    private SerializedProperty stopOnDisableProperty;
    private bool showPairOffsetAssetProfiles = true;
    private bool showCurrentProfileBones = true;
    private bool showAdvancedLocalProfiles;
    private int selectedAssetProfileIndex = -1;

    private static GUIContent Tip(string text, string tooltip)
    {
        return new GUIContent(text, tooltip);
    }

    private static bool TipButton(string text, string tooltip, params GUILayoutOption[] options)
    {
        return GUILayout.Button(Tip(text, tooltip), options);
    }

    private void OnEnable()
    {
        animatorProperty = serializedObject.FindProperty("animator");
        previewModeProperty = serializedObject.FindProperty("previewMode");
        clipProperty = serializedObject.FindProperty("clip");
        upperBodyClipProperty = serializedObject.FindProperty("upperBodyClip");
        upperBodyMaskProperty = serializedObject.FindProperty("upperBodyMask");
        upperBodyWeightProperty = serializedObject.FindProperty("upperBodyWeight");
        applyPairBoneOffsetsProperty = serializedObject.FindProperty("applyPairBoneOffsets");
        pairOffsetProfileAssetProperty = serializedObject.FindProperty("pairOffsetProfileAsset");
        pairOffsetProfilesProperty = serializedObject.FindProperty("pairOffsetProfiles");
        applyInEditModeProperty = serializedObject.FindProperty("applyInEditMode");
        applyRootMotionProperty = serializedObject.FindProperty("applyRootMotion");
        normalizedStartTimeProperty = serializedObject.FindProperty("normalizedStartTime");
        speedProperty = serializedObject.FindProperty("speed");
        stopOnDisableProperty = serializedObject.FindProperty("stopOnDisable");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var preview = (SoloRootMotionPreview)target;
        bool isUpperBodyMode = previewModeProperty.enumValueIndex == (int)SoloRootMotionPreview.PreviewMode.UpperBody;

        DrawSectionHeader("Preview Setup", "ตั้งค่า Animator และคลิปหลักที่จะใช้ preview");
        EditorGUILayout.PropertyField(animatorProperty, Tip("Animator", "Animator ของตัวละครหรือโมเดลที่ต้องการ preview"));
        EditorGUILayout.PropertyField(previewModeProperty, Tip("Preview Mode", "Single Clip = เล่นคลิปเดียว, Upper Body = เล่น locomotion พร้อมคลิปช่วงบน"));

        GUIContent clipLabel = isUpperBodyMode
            ? Tip("Base Clip", "คลิป locomotion หลัก เช่น เดินหน้า ถอยหลัง หรือ strafe")
            : Tip("Clip", "คลิป animation ที่ต้องการ preview");

        EditorGUILayout.PropertyField(clipProperty, clipLabel);

        if (isUpperBodyMode)
        {
            EditorGUILayout.PropertyField(upperBodyClipProperty, Tip("Upper Body Clip", "คลิปช่วงบนที่จะซ้อนทับบน Base Clip เช่น ยิง เล็ง หรือ reload"));
            EditorGUILayout.PropertyField(upperBodyMaskProperty, Tip("Upper Body Mask", "AvatarMask สำหรับจำกัดให้คลิปช่วงบนกระทบเฉพาะกระดูกที่ต้องการ"));
            EditorGUILayout.PropertyField(upperBodyWeightProperty, Tip("Upper Body Weight", "น้ำหนักของ layer ช่วงบน 0 = ไม่ใช้, 1 = ใช้เต็ม"));

            if (upperBodyClipProperty.objectReferenceValue == null)
                EditorGUILayout.HelpBox("ยังไม่ได้ใส่ Upper Body Clip ดังนั้น preview จะเล่นเฉพาะ Base Clip", MessageType.Info);

            if (upperBodyMaskProperty.objectReferenceValue == null)
                EditorGUILayout.HelpBox("ยังไม่ได้ใส่ Upper Body Mask ดังนั้น overlay จะกระทบทั้งตัว", MessageType.Warning);
        }

        DrawSectionHeader("Playback", "ตั้งค่าวิธีเล่น preview ใน Scene และ Play Mode");
        EditorGUILayout.PropertyField(applyInEditModeProperty, Tip("Apply In Edit Mode", "เปิดให้ preview ทำงานใน Edit Mode โดยไม่ต้องเข้า Play Mode"));
        EditorGUILayout.PropertyField(applyRootMotionProperty, Tip("Apply Root Motion", "ให้ root motion จากคลิปขยับ transform ตอน preview"));
        EditorGUILayout.PropertyField(normalizedStartTimeProperty, Tip("Normalized Start Time", "เวลาเริ่มต้นของคลิปในช่วง 0 ถึง 1"));
        EditorGUILayout.PropertyField(speedProperty, Tip("Speed", "ความเร็วในการเล่น preview"));
        EditorGUILayout.PropertyField(stopOnDisableProperty, Tip("Stop On Disable", "เมื่อปิด component ให้หยุดและ rewind preview กลับจุดเริ่ม"));

        if (isUpperBodyMode)
        {
            DrawSectionHeader("Pair Offset Library", "จัดการ profile offset ระหว่าง component นี้กับ ScriptableObject");
            EditorGUILayout.PropertyField(applyPairBoneOffsetsProperty, Tip("Apply Pair Bone Offsets", "เปิดใช้ rotation offset เฉพาะคู่ Base Clip + Upper Body Clip ปัจจุบัน"));
            EditorGUILayout.PropertyField(pairOffsetProfileAssetProperty, Tip("Pair Offset Profile Asset", "SO ที่ใช้เก็บ profile offset เพื่อเอาไปใช้ต่อกับระบบจริง"));
        }

        bool changed = serializedObject.ApplyModifiedProperties();
        if (changed && preview.enabled)
        {
            if (Application.isPlaying)
                preview.Play();
            else
                preview.RefreshPreviewPose();
        }

        if (isUpperBodyMode)
        {
            DrawPairOffsetTools(preview);
            DrawCurrentWorkingProfile(preview);
            DrawAdvancedPairOffsetData();

            bool pairDataChanged = serializedObject.ApplyModifiedProperties();
            if (pairDataChanged && preview.enabled)
                preview.RefreshPreviewPose();
        }

        DrawSectionHeader("Preview Controls", "ควบคุมการเล่นและ scrub เวลา preview");
        using (new EditorGUI.DisabledScope(preview.Clip == null || preview.Animator == null))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (TipButton("Play", "เริ่มหรือเล่น preview ต่อจากเวลาปัจจุบัน"))
                {
                    CommitInspectorChanges(preview);
                    preview.Play();
                }

                if (TipButton("Pause", "หยุด preview ไว้ที่ pose ปัจจุบันเพื่อจูน offset"))
                {
                    CommitInspectorChanges(preview);
                    preview.Pause();
                }

                if (TipButton("Rewind", "ย้อน preview กลับไปเวลาเริ่มต้น"))
                {
                    CommitInspectorChanges(preview);
                    preview.Rewind();
                }
            }

            if (TipButton("Reset Preview Origin", "ตั้งจุด origin ปัจจุบันใหม่สำหรับ root motion preview"))
            {
                CommitInspectorChanges(preview);
                preview.ResetPreviewOrigin();
            }

            EditorGUI.BeginChangeCheck();
            float normalizedTime = EditorGUILayout.Slider(Tip("Normalized Time", "เลื่อนเวลา preview แบบ normalized 0 ถึง 1"), preview.NormalizedTime, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(preview.GetPreviewRoot(), "Scrub Animation Preview");
                preview.NormalizedTime = normalizedTime;
            }
        }

        DrawSectionHeader("Status", "ข้อมูลสถานะของ preview graph ปัจจุบัน");
        EditorGUILayout.LabelField(Tip("Length", "ความยาว preview เป็นวินาที"), new GUIContent(preview.Length.ToString("0.###")));
        EditorGUILayout.LabelField(Tip("Looping", "บอกว่าคลิป preview loop ได้หรือไม่"), new GUIContent(preview.IsLooping ? "True" : "False"));
        EditorGUILayout.LabelField(Tip("Initialized", "บอกว่า PlayableGraph ถูกสร้างแล้วหรือยัง"), new GUIContent(preview.IsInitialized ? "True" : "False"));
    }

    private void DrawSectionHeader(string label, string tooltip = "")
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(Tip(label, tooltip), EditorStyles.boldLabel);
    }

    private void DrawPairOffsetTools(SoloRootMotionPreview preview)
    {
        EditorGUILayout.LabelField(Tip("Active Pair Offset", "profile offset ที่กำลังถูกใช้กับคู่คลิปปัจจุบัน"), new GUIContent(preview.ActivePairOffsetProfileLabel));
        DrawPairOffsetLibraryStatus(preview);

        string saveButtonLabel = preview.PairOffsetProfileAsset != null
            ? "Save Current Pair"
            : "Save Component Values";
        bool hasCurrentLocalProfile = FindCurrentLocalProfileProperty() != null;

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(preview.Clip == null || preview.UpperBodyClip == null))
            {
                if (TipButton("Create Local Profile", "สร้าง profile ใน component สำหรับ Base Clip + Upper Body Clip ปัจจุบัน"))
                    CreatePairOffsetProfile(preview);
            }

            using (new EditorGUI.DisabledScope(!hasCurrentLocalProfile))
            {
                if (TipButton(saveButtonLabel, "บันทึกค่า profile ปัจจุบัน ถ้า assign SO ไว้จะเขียนลง asset ด้วย"))
                    SavePairOffsetValues(preview);
            }
        }

        using (new EditorGUI.DisabledScope(preview.PairOffsetProfileAsset == null))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!preview.CanLoadCurrentPairOffsetProfileFromAsset))
                {
                    if (TipButton("Load Current Pair", "โหลด profile จาก SO ที่ตรงกับ Base Clip + Upper Body Clip ปัจจุบัน"))
                        LoadCurrentPairOffsetProfileFromAsset(preview);
                }

                if (TipButton("Load All", "โหลดทุก profile จาก SO กลับเข้า component โดยไม่ลบ profile local อื่น"))
                    LoadAllPairOffsetProfilesFromAsset(preview);
            }

            DrawPairOffsetAssetProfiles(preview);
        }
    }

    private void DrawPairOffsetLibraryStatus(SoloRootMotionPreview preview)
    {
        PairOffsetProfilesSO asset = preview.PairOffsetProfileAsset;
        if (asset == null)
        {
            EditorGUILayout.HelpBox("ใส่ Pair Offset Profile Asset ก่อน ถ้าต้องการ save/load profile จาก SO", MessageType.Info);
            return;
        }

        int count = asset.PairOffsetProfiles?.Count ?? 0;
        string currentPairStatus = preview.CanLoadCurrentPairOffsetProfileFromAsset ? "Found in SO" : "Missing in SO";
        EditorGUILayout.LabelField(Tip("Asset Profiles", "จำนวน profile ทั้งหมดที่อยู่ใน SO ที่ assign ไว้"), new GUIContent(count.ToString()));
        EditorGUILayout.LabelField(Tip("Current Pair", "สถานะว่าคู่ Base Clip + Upper Body Clip ปัจจุบันมีอยู่ใน SO หรือยัง"), new GUIContent(currentPairStatus));
    }

    private void DrawPairOffsetAssetProfiles(SoloRootMotionPreview preview)
    {
        PairOffsetProfilesSO asset = preview.PairOffsetProfileAsset;
        if (asset == null)
            return;

        IReadOnlyList<PairOffsetProfilesSO.PairOffsetProfile> profiles = asset.PairOffsetProfiles;
        int count = profiles?.Count ?? 0;
        showPairOffsetAssetProfiles = EditorGUILayout.Foldout(showPairOffsetAssetProfiles, Tip($"SO Profiles ({count})", "รายการ profile ใน SO กด Select เพื่อดูรายละเอียด หรือ Load เพื่อโหลด element นั้น"), true);
        if (!showPairOffsetAssetProfiles)
            return;

        using (new EditorGUI.IndentLevelScope())
        {
            if (count == 0)
            {
                EditorGUILayout.HelpBox("SO ที่ใส่ไว้ยังไม่มี profile", MessageType.Info);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                PairOffsetProfilesSO.PairOffsetProfile profile = profiles[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    string selectLabel = selectedAssetProfileIndex == i ? "Selected" : "Select";
                    if (TipButton(selectLabel, "เลือก element นี้เพื่อดูรายละเอียดด้านล่าง", GUILayout.Width(72f)))
                        selectedAssetProfileIndex = i;

                    EditorGUILayout.LabelField(Tip(GetPairOffsetAssetProfileLabel(i, profile), "ชื่อ profile, คู่คลิป, สถานะ enabled และจำนวน bone offset"), GUILayout.MinWidth(160f));

                    bool canLoad = profile != null && profile.BaseClip != null && profile.UpperBodyClip != null;
                    using (new EditorGUI.DisabledScope(!canLoad))
                    {
                        if (TipButton("Load", "โหลด element นี้เข้า component และสลับ preview ไปใช้คู่คลิปของ element นี้", GUILayout.Width(64f)))
                            LoadPairOffsetProfileFromAsset(preview, i);
                    }
                }
            }

            DrawSelectedPairOffsetAssetProfile(profiles);
        }
    }

    private void DrawSelectedPairOffsetAssetProfile(IReadOnlyList<PairOffsetProfilesSO.PairOffsetProfile> profiles)
    {
        if (profiles == null ||
            selectedAssetProfileIndex < 0 ||
            selectedAssetProfileIndex >= profiles.Count)
        {
            return;
        }

        PairOffsetProfilesSO.PairOffsetProfile profile = profiles[selectedAssetProfileIndex];
        if (profile == null)
            return;

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField(Tip("Selected SO Profile", "รายละเอียดของ element ที่เลือกจาก SO browser"), EditorStyles.boldLabel);
        EditorGUILayout.LabelField(Tip("Name", "ชื่อ profile ใน SO"), new GUIContent(string.IsNullOrWhiteSpace(profile.ProfileName) ? "Unnamed" : profile.ProfileName));
        EditorGUILayout.LabelField(Tip("Base Clip", "คลิป locomotion หลักของ profile นี้"), new GUIContent(profile.BaseClip != null ? profile.BaseClip.name : "Missing"));
        EditorGUILayout.LabelField(Tip("Upper Body Clip", "คลิปช่วงบนของ profile นี้"), new GUIContent(profile.UpperBodyClip != null ? profile.UpperBodyClip.name : "Missing"));
        EditorGUILayout.LabelField(Tip("Bones", "จำนวน bone offset ที่ profile นี้เก็บไว้"), new GUIContent((profile.BoneOffsets?.Count ?? 0).ToString()));
        EditorGUILayout.LabelField(Tip("Status", "สถานะเปิดใช้ profile นี้ใน SO"), new GUIContent(profile.Enabled ? "Enabled" : "Disabled"));
    }

    private string GetPairOffsetAssetProfileLabel(int index, PairOffsetProfilesSO.PairOffsetProfile profile)
    {
        if (profile == null)
            return $"{index}: <Null>";

        string baseName = profile.BaseClip != null ? profile.BaseClip.name : "None";
        string upperName = profile.UpperBodyClip != null ? profile.UpperBodyClip.name : "None";
        string profileName = string.IsNullOrWhiteSpace(profile.ProfileName)
            ? $"{baseName} + {upperName}"
            : profile.ProfileName;
        int boneCount = profile.BoneOffsets?.Count ?? 0;
        string enabledLabel = profile.Enabled ? string.Empty : " (Disabled)";
        return $"{index}: {profileName}{enabledLabel} [{baseName} + {upperName}] Bones: {boneCount}";
    }

    private void DrawCurrentWorkingProfile(SoloRootMotionPreview preview)
    {
        DrawSectionHeader("Current Working Profile", "แก้ profile ของคู่ Base Clip + Upper Body Clip ปัจจุบัน");

        SerializedProperty profile = FindCurrentLocalProfileProperty();
        if (preview.Clip == null || preview.UpperBodyClip == null)
        {
            EditorGUILayout.HelpBox("เลือก Base Clip และ Upper Body Clip ก่อนแก้ pair offset", MessageType.Info);
            return;
        }

        if (profile == null)
        {
            EditorGUILayout.HelpBox("ยังไม่มี local profile สำหรับคู่คลิปปัจจุบัน", MessageType.Info);
            if (TipButton("Create Local Profile", "สร้าง profile local สำหรับคู่คลิปปัจจุบันเพื่อเริ่มจูน offset"))
                CreatePairOffsetProfile(preview);
            return;
        }

        EditorGUILayout.LabelField(Tip("Status", "สถานะว่า profile นี้เป็น local เท่านั้น หรือมีคู่ใน SO แล้ว"), new GUIContent(GetCurrentWorkingProfileStatus(preview)));
        EditorGUILayout.PropertyField(profile.FindPropertyRelative("enabled"), Tip("Enabled", "เปิดหรือปิด profile offset นี้"));
        EditorGUILayout.PropertyField(profile.FindPropertyRelative("profileName"), Tip("Profile Name", "ชื่อ profile สำหรับอ่านใน SO browser และ inspector"));
        EditorGUILayout.PropertyField(profile.FindPropertyRelative("weight"), Tip("Profile Weight", "น้ำหนักรวมของ offset ทั้ง profile"));
        EditorGUILayout.LabelField(Tip("Base Clip", "คลิป locomotion ที่ profile นี้ผูกอยู่"), new GUIContent(preview.Clip != null ? preview.Clip.name : "None"));
        EditorGUILayout.LabelField(Tip("Upper Body Clip", "คลิปช่วงบนที่ profile นี้ผูกอยู่"), new GUIContent(preview.UpperBodyClip != null ? preview.UpperBodyClip.name : "None"));

        using (new EditorGUILayout.HorizontalScope())
        {
            if (TipButton("Capture Pose", "อ่าน pose ปัจจุบันแล้วแปลงกลับไปเป็น Local Euler Offset ของ bone ใน profile"))
                CaptureCurrentPairOffsetPose(preview);

            using (new EditorGUI.DisabledScope(preview.PairOffsetProfileAsset == null))
            {
                if (TipButton("Save To SO", "บันทึก working profile นี้ลง PairOffsetProfilesSO ที่ assign ไว้"))
                    SavePairOffsetValues(preview);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(Selection.transforms.Length == 0))
            {
                if (TipButton("Add Selected Bones", "เพิ่ม Transform ที่เลือกใน Hierarchy เข้า bone offset list ของ profile ปัจจุบัน"))
                    AddSelectedBonesToCurrentProfile(preview);
            }

            if (TipButton("Refresh Paths", "อัปเดต bonePath จาก Transform หรือ resolve Transform กลับจาก bonePath"))
                RefreshPairOffsetBonePaths(preview);

            if (TipButton("Remove Missing", "ลบ bone offset ที่ resolve Transform ไม่เจอใน hierarchy นี้"))
                RemoveMissingBonesFromCurrentProfile(preview);
        }

        DrawCurrentProfileBones(profile, preview);
    }

    private string GetCurrentWorkingProfileStatus(SoloRootMotionPreview preview)
    {
        if (preview.PairOffsetProfileAsset == null)
            return "Local Only";

        return preview.CanLoadCurrentPairOffsetProfileFromAsset ? "Saved Pair Exists In SO" : "Not Saved To SO";
    }

    private SerializedProperty FindCurrentLocalProfileProperty()
    {
        UnityEngine.Object baseClip = clipProperty.objectReferenceValue;
        UnityEngine.Object overlayClip = upperBodyClipProperty.objectReferenceValue;
        if (baseClip == null || overlayClip == null || pairOffsetProfilesProperty == null)
            return null;

        for (int i = 0; i < pairOffsetProfilesProperty.arraySize; i++)
        {
            SerializedProperty profile = pairOffsetProfilesProperty.GetArrayElementAtIndex(i);
            if (profile.FindPropertyRelative("baseClip").objectReferenceValue == baseClip &&
                profile.FindPropertyRelative("upperBodyClip").objectReferenceValue == overlayClip)
            {
                return profile;
            }
        }

        return null;
    }

    private void DrawCurrentProfileBones(SerializedProperty profile, SoloRootMotionPreview preview)
    {
        SerializedProperty boneOffsets = profile.FindPropertyRelative("boneOffsets");
        int count = boneOffsets?.arraySize ?? 0;
        showCurrentProfileBones = EditorGUILayout.Foldout(showCurrentProfileBones, Tip($"Bone Offsets ({count})", "รายการกระดูกที่ถูกเพิ่ม rotation offset สำหรับ profile ปัจจุบัน"), true);
        if (!showCurrentProfileBones || boneOffsets == null)
            return;

        using (new EditorGUI.IndentLevelScope())
        {
            if (count == 0)
            {
                EditorGUILayout.HelpBox("เลือกกระดูก spine/chest ใน Hierarchy แล้วกด Add Selected Bones", MessageType.Info);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                SerializedProperty offset = boneOffsets.GetArrayElementAtIndex(i);
                if (DrawBoneOffsetRow(boneOffsets, offset, i, preview))
                    break;
            }
        }
    }

    private bool DrawBoneOffsetRow(SerializedProperty boneOffsets, SerializedProperty offset, int index, SoloRootMotionPreview preview)
    {
        SerializedProperty enabled = offset.FindPropertyRelative("enabled");
        SerializedProperty bone = offset.FindPropertyRelative("bone");
        SerializedProperty bonePath = offset.FindPropertyRelative("bonePath");
        SerializedProperty localEulerOffset = offset.FindPropertyRelative("localEulerOffset");
        SerializedProperty weight = offset.FindPropertyRelative("weight");

        EditorGUILayout.Space(2f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(enabled, Tip(string.Empty, "เปิดหรือปิด offset ของกระดูกนี้"), GUILayout.Width(18f));
                EditorGUILayout.LabelField(Tip(GetBoneOffsetLabel(index, bone, bonePath), "ชื่อ Transform หรือ bonePath ของกระดูกแถวนี้"), EditorStyles.boldLabel);

                using (new EditorGUI.DisabledScope(bone.objectReferenceValue == null))
                {
                    if (TipButton("Ping", "เลือกและ highlight กระดูกนี้ใน Project/Hierarchy", GUILayout.Width(48f)))
                    {
                        EditorGUIUtility.PingObject(bone.objectReferenceValue);
                        Selection.activeObject = bone.objectReferenceValue;
                    }
                }

                if (TipButton("Remove", "ลบ bone offset แถวนี้ออกจาก profile", GUILayout.Width(68f)))
                {
                    Undo.RecordObject(preview, "Remove Pair Offset Bone");
                    boneOffsets.DeleteArrayElementAtIndex(index);
                    EditorUtility.SetDirty(preview);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(preview);
                    return true;
                }
            }

            EditorGUILayout.PropertyField(bone, Tip("Bone", "Transform กระดูกที่ใช้ preview และช่วย resolve path"));
            EditorGUILayout.PropertyField(bonePath, Tip("Bone Path", "path ของกระดูกเทียบจาก Animator root ใช้เก็บลง SO และใช้ runtime resolve"));
            EditorGUILayout.PropertyField(localEulerOffset, Tip("Local Euler Offset", "มุมหมุน local ที่จะทาเพิ่มหลัง animation sample"));
            EditorGUILayout.PropertyField(weight, Tip("Weight", "น้ำหนัก offset ของกระดูกนี้ก่อนคูณกับ Profile Weight"));

            if (bone.objectReferenceValue == null)
                EditorGUILayout.HelpBox("bonePath นี้ resolve เป็น Transform ใน hierarchy ปัจจุบันไม่ได้", MessageType.Warning);
        }

        return false;
    }

    private string GetBoneOffsetLabel(int index, SerializedProperty bone, SerializedProperty bonePath)
    {
        if (bone.objectReferenceValue != null)
            return $"{index}: {bone.objectReferenceValue.name}";

        return string.IsNullOrWhiteSpace(bonePath.stringValue)
            ? $"{index}: Missing Bone"
            : $"{index}: {bonePath.stringValue}";
    }

    private void DrawAdvancedPairOffsetData()
    {
        DrawSectionHeader("Advanced", "ข้อมูล raw สำหรับ debug หรือแก้ค่าที่ custom UI ไม่ครอบคลุม");
        showAdvancedLocalProfiles = EditorGUILayout.Foldout(showAdvancedLocalProfiles, Tip("Raw Local Pair Profiles", "เปิดดู serialized list ดิบของ profile ใน component"), true);
        if (showAdvancedLocalProfiles)
            EditorGUILayout.PropertyField(pairOffsetProfilesProperty, Tip("Pair Offset Profiles", "ข้อมูล local ทั้งหมดใน component นี้"), true);
    }

    private void CreatePairOffsetProfile(SoloRootMotionPreview preview)
    {
        CommitInspectorChanges(preview);
        Undo.RecordObject(preview, "Create Pair Offset Profile");
        if (preview.CreatePairOffsetProfileFromCurrentClips())
        {
            EditorUtility.SetDirty(preview);
            PrefabUtility.RecordPrefabInstancePropertyModifications(preview);
            serializedObject.Update();
            preview.RefreshPreviewPose();
        }
    }

    private void AddSelectedBonesToCurrentProfile(SoloRootMotionPreview preview)
    {
        CommitInspectorChanges(preview);
        Undo.RecordObject(preview, "Add Pair Offset Bones");
        preview.AddBoneOffsetsToCurrentPairProfile(Selection.transforms);
        EditorUtility.SetDirty(preview);
        PrefabUtility.RecordPrefabInstancePropertyModifications(preview);
        serializedObject.Update();
        preview.RefreshPreviewPose();
    }

    private void RefreshPairOffsetBonePaths(SoloRootMotionPreview preview)
    {
        CommitInspectorChanges(preview);
        Undo.RecordObject(preview, "Refresh Pair Offset Bone Paths");
        preview.RefreshPairOffsetBonePaths();
        EditorUtility.SetDirty(preview);
        PrefabUtility.RecordPrefabInstancePropertyModifications(preview);
        serializedObject.Update();
        preview.RefreshPreviewPose();
    }

    private void CaptureCurrentPairOffsetPose(SoloRootMotionPreview preview)
    {
        CommitInspectorChanges(preview);
        Undo.RecordObject(preview, "Capture Pair Offset Pose");
        preview.CaptureCurrentPoseAsActivePairOffsets();
        EditorUtility.SetDirty(preview);
        PrefabUtility.RecordPrefabInstancePropertyModifications(preview);
        serializedObject.Update();
        preview.RefreshPreviewPose();
    }

    private void RemoveMissingBonesFromCurrentProfile(SoloRootMotionPreview preview)
    {
        RefreshPairOffsetBonePaths(preview);
        serializedObject.Update();

        SerializedProperty profile = FindCurrentLocalProfileProperty();
        SerializedProperty boneOffsets = profile?.FindPropertyRelative("boneOffsets");
        if (boneOffsets == null)
            return;

        Undo.RecordObject(preview, "Remove Missing Pair Offset Bones");
        for (int i = boneOffsets.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty offset = boneOffsets.GetArrayElementAtIndex(i);
            if (offset.FindPropertyRelative("bone").objectReferenceValue == null)
                boneOffsets.DeleteArrayElementAtIndex(i);
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(preview);
        PrefabUtility.RecordPrefabInstancePropertyModifications(preview);
        serializedObject.Update();
        preview.RefreshPreviewPose();
    }

    private void CommitInspectorChanges(SoloRootMotionPreview preview)
    {
        EditorGUIUtility.editingTextField = false;
        GUI.FocusControl(null);
        serializedObject.ApplyModifiedProperties();
        preview.RefreshPairOffsetBonePaths();
    }

    private void LoadPairOffsetProfileFromAsset(SoloRootMotionPreview preview, int profileIndex)
    {
        CommitInspectorChanges(preview);
        Undo.RecordObject(preview, "Load Pair Offset Values From Asset");
        if (!preview.LoadPairOffsetProfileFromAsset(profileIndex))
            return;

        EditorUtility.SetDirty(preview);
        PrefabUtility.RecordPrefabInstancePropertyModifications(preview);
        serializedObject.Update();

        if (preview.enabled)
            preview.Play();
        else
            preview.RefreshPreviewPose();
    }

    private void LoadCurrentPairOffsetProfileFromAsset(SoloRootMotionPreview preview)
    {
        CommitInspectorChanges(preview);
        Undo.RecordObject(preview, "Load Pair Offset Values From Asset");
        if (!preview.LoadCurrentPairOffsetProfileFromAsset())
            return;

        EditorUtility.SetDirty(preview);
        PrefabUtility.RecordPrefabInstancePropertyModifications(preview);
        serializedObject.Update();
        preview.RefreshPreviewPose();
    }

    private void LoadAllPairOffsetProfilesFromAsset(SoloRootMotionPreview preview)
    {
        CommitInspectorChanges(preview);
        Undo.RecordObject(preview, "Load Pair Offset Values From Asset");
        if (preview.LoadAllPairOffsetProfilesFromAsset() <= 0)
            return;

        EditorUtility.SetDirty(preview);
        PrefabUtility.RecordPrefabInstancePropertyModifications(preview);
        serializedObject.Update();
        preview.RefreshPreviewPose();
    }

    private void SavePairOffsetValues(SoloRootMotionPreview preview)
    {
        CommitInspectorChanges(preview);
        Undo.RecordObject(preview, "Save Pair Offset Values");
        PairOffsetProfilesSO targetAsset = preview.PairOffsetProfileAsset;
        if (targetAsset != null)
            Undo.RecordObject(targetAsset, "Save Pair Offset Values To Asset");

        preview.CaptureCurrentPoseAsActivePairOffsets();
        bool savedAsset = preview.SaveCurrentPairOffsetProfileToAsset();
        EditorUtility.SetDirty(preview);
        PrefabUtility.RecordPrefabInstancePropertyModifications(preview);

        if (savedAsset)
            EditorUtility.SetDirty(targetAsset);

        if (preview.gameObject.scene.IsValid() && preview.gameObject.scene.isLoaded)
            EditorSceneManager.SaveScene(preview.gameObject.scene);

        AssetDatabase.SaveAssets();
        serializedObject.Update();
        preview.RefreshPreviewPose();
    }
}
#endif
