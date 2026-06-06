using System;
using Animancer;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

[CreateAssetMenu(menuName = "Game/Characters/Animation Profile", fileName = "CharacterAnimProfile")]
public sealed class CharacterAnimProfileSO : ScriptableObject
{
    public enum ReloadBodyMode
    {
        UpperBody = 0,
        FullBody = 1,
    }

    [Serializable]
    public sealed class DirectionalClipSet2D
    {
        private const float DiagonalThreshold = 0.70710678f;

        public AnimationClip idle;
        public AnimationClip forward;
        public AnimationClip backward;
        public AnimationClip left;
        public AnimationClip right;
        public AnimationClip forwardLeft;
        public AnimationClip forwardRight;
        public AnimationClip backwardLeft;
        public AnimationClip backwardRight;

        public bool HasAnyAssignments =>
            idle != null ||
            forward != null ||
            backward != null ||
            left != null ||
            right != null ||
            forwardLeft != null ||
            forwardRight != null ||
            backwardLeft != null ||
            backwardRight != null;

        public bool HasCardinalSet =>
            idle != null &&
            forward != null &&
            backward != null &&
            left != null &&
            right != null;

        public bool HasFullEightDirectionSet =>
            HasCardinalSet &&
            forwardLeft != null &&
            forwardRight != null &&
            backwardLeft != null &&
            backwardRight != null;

        public bool HasAnyDiagonalAssignments =>
            forwardLeft != null ||
            forwardRight != null ||
            backwardLeft != null ||
            backwardRight != null;

        public string GetStatusLabel()
        {
            if (!HasAnyAssignments)
                return "directional clips missing";

            if (!HasCardinalSet)
                return "directional clips incomplete";

            return HasFullEightDirectionSet
                ? "directional clips 8-direction"
                : "directional clips 4-direction";
        }

        public bool TryGetIssue(out string issue)
        {
            issue = null;

            if (!HasAnyAssignments)
            {
                issue = "Locomotion directional clips are missing. Idle, Forward, Backward, Left, and Right are all required.";
                return true;
            }

            if (!HasCardinalSet)
            {
                issue = "Locomotion directional clips are incomplete. Idle, Forward, Backward, Left, and Right are all required.";
                return true;
            }

            if (HasAnyDiagonalAssignments && !HasFullEightDirectionSet)
            {
                issue = "Locomotion directional clips are missing one or more diagonal clips, so the mixer will use 4-direction blending.";
                return true;
            }

            return false;
        }

        public MixerTransition2D CreateMixer(float fadeDuration, float speed)
        {
            if (!HasCardinalSet)
                return null;

            var mixer = new MixerTransition2D
            {
                Type = MixerTransition2D.MixerType.Directional,
                FadeDuration = SanitizeNonNegative(fadeDuration, DefaultLocomotionFadeDuration),
                Speed = SanitizePlaybackSpeed(speed),
                DefaultParameter = Vector2.zero,
            };

            if (HasFullEightDirectionSet)
            {
                mixer.Animations = new Object[]
                {
                    idle,
                    forward,
                    backward,
                    left,
                    right,
                    forwardLeft,
                    forwardRight,
                    backwardLeft,
                    backwardRight,
                };

                mixer.Thresholds = new[]
                {
                    Vector2.zero,
                    Vector2.up,
                    Vector2.down,
                    Vector2.left,
                    Vector2.right,
                    new Vector2(-DiagonalThreshold, DiagonalThreshold),
                    new Vector2(DiagonalThreshold, DiagonalThreshold),
                    new Vector2(-DiagonalThreshold, -DiagonalThreshold),
                    new Vector2(DiagonalThreshold, -DiagonalThreshold),
                };
            }
            else
            {
                mixer.Animations = new Object[]
                {
                    idle,
                    forward,
                    backward,
                    left,
                    right,
                };

                mixer.Thresholds = new[]
                {
                    Vector2.zero,
                    Vector2.up,
                    Vector2.down,
                    Vector2.left,
                    Vector2.right,
                };
            }

            return mixer;
        }
    }

    private const float DefaultLocomotionFadeDuration = 0.25f;
    private const float DefaultLocomotionPlaybackSpeed = 1f;

    [NonSerialized] private MixerTransition2D resolvedLocomotionMixer;

    [Header("Layers")]
    public AvatarMask upperBodyMask;
    [Min(0f)] public float actionFadeIn = 0.06f;
    [Min(0f)] public float actionFadeOut = 0.08f;

    [Header("Locomotion (Layer 0)")]
    public DirectionalClipSet2D locomotionDirectionalClips = new();
    [Min(0f)] public float locomotionParamLerp = 14f;
    public bool snapTo8Directions = true;
    [Min(0f)] public float locomotionFadeDuration = DefaultLocomotionFadeDuration;
    [Min(0f)] public float locomotionPlaybackSpeed = DefaultLocomotionPlaybackSpeed;


    [Header("StatusEffec (Layer 0)")] 
    public ClipTransition miniStune;
    public ClipTransition stune;
    public ClipTransition root;
    public ClipTransition freez;

    [Header("Knockback (Layer 0)")]
    public ClipTransition knockback;

    [Header("Dash (Layer 0)")]
    public ClipTransition dashF;
    public ClipTransition dashB;
    public ClipTransition dashL;
    public ClipTransition dashR;

    [Header("Dead (Layer 0)")]
    public ClipTransition dead;

    [Header("Shoot (Layer 1)")]
    public ClipTransition shootPulse;
    public ClipTransition shootHoldLoop;
    [Min(0f)] public float holdPulseMinInterval = 0.08f;

    [Header("Pair Offsets")]
    public PairOffsetProfilesSO pairOffsetProfiles;

    [Header("Reload (Layer 1 or Full Body)")]
    public ClipTransition reload;
    [Tooltip("UpperBody blends reload over locomotion using the action mask. FullBody plays reload on the locomotion layer and temporarily owns the whole character.")]
    public ReloadBodyMode reloadBodyMode = ReloadBodyMode.UpperBody;

    [Header("Melee Combo")]
    public MeleeComboSO meleeCombo;
    public bool meleeCanInterruptReload = true;
    public MeleeComboSO lightCombo;
    public MeleeComboSO heavyCombo;

    [Header("Downed (Layer 0)")] 
    public ClipTransition crawling;
    public MixerTransition2D crawlMixer;
    [Min(0f)] public float crawlParamLerp = 10f;
    [Range(0f, 1f)] public float crawlSpeedMultiplier01 = 0.35f;

    [Header("Utility Warp Out (Layer 0)")]
    [FormerlySerializedAs("utilityWarpInClip")]
    public ClipTransition utilityWarpOutClip;
    [FormerlySerializedAs("utilityWarpInCastPointNormalized")]
    [Range(0f, 1f)] public float utilityWarpOutCastPointNormalized = 0.35f;

    [Header("Utility Warp In (Layer 0)")]
    public ClipTransition utilityWarpInClip;
    [Range(0f, 1f)] public float utilityWarpInCastPointNormalized = 0.35f;

    [Header("Skill (Layer 0, Legacy Fallback)")]
    public ClipTransition skillClip;

    public MixerTransition2D ResolveLocomotionMixer()
    {
        EnsureLocomotionDirectionalClips();

        if (resolvedLocomotionMixer == null)
        {
            resolvedLocomotionMixer = locomotionDirectionalClips.CreateMixer(
                locomotionFadeDuration,
                locomotionPlaybackSpeed);
        }

        return resolvedLocomotionMixer;
    }

    public bool HasValidResolvedLocomotionMixer
    {
        get
        {
            MixerTransition2D mixer = ResolveLocomotionMixer();
            return mixer != null && mixer.IsValid;
        }
    }

    public string GetLocomotionConfigurationLabel()
    {
        EnsureLocomotionDirectionalClips();
        return locomotionDirectionalClips.GetStatusLabel();
    }

    public bool TryGetLocomotionConfigurationIssue(out string issue)
    {
        EnsureLocomotionDirectionalClips();
        return locomotionDirectionalClips.TryGetIssue(out issue);
    }

    public PairOffsetProfilesSO.PairOffsetProfile FindPairOffsetProfile(
        PairOffsetBasePose basePose,
        PairOffsetUpperAction upperAction,
        bool includeDisabled)
    {
        if (pairOffsetProfiles == null)
            return null;

        PairOffsetProfilesSO.PairOffsetProfile profile =
            pairOffsetProfiles.FindProfile(basePose, upperAction, includeDisabled);
        if (profile != null)
            return profile;

        AnimationClip baseClip = GetPairOffsetBaseClip(basePose);
        AnimationClip upperClip = GetPairOffsetUpperClip(upperAction);
        return baseClip != null && upperClip != null
            ? pairOffsetProfiles.FindProfile(baseClip, upperClip, includeDisabled)
            : null;
    }

    public AnimationClip GetPairOffsetBaseClip(PairOffsetBasePose pose)
    {
        EnsureLocomotionDirectionalClips();

        return pose switch
        {
            PairOffsetBasePose.Idle => locomotionDirectionalClips.idle,
            PairOffsetBasePose.Forward => locomotionDirectionalClips.forward,
            PairOffsetBasePose.Backward => locomotionDirectionalClips.backward,
            PairOffsetBasePose.Left => locomotionDirectionalClips.left,
            PairOffsetBasePose.Right => locomotionDirectionalClips.right,
            PairOffsetBasePose.ForwardLeft => locomotionDirectionalClips.forwardLeft,
            PairOffsetBasePose.ForwardRight => locomotionDirectionalClips.forwardRight,
            PairOffsetBasePose.BackwardLeft => locomotionDirectionalClips.backwardLeft,
            PairOffsetBasePose.BackwardRight => locomotionDirectionalClips.backwardRight,
            _ => null,
        };
    }

    public AnimationClip GetPairOffsetUpperClip(PairOffsetUpperAction action)
    {
        return action switch
        {
            PairOffsetUpperAction.ShootPulse => shootPulse != null ? shootPulse.Clip : null,
            PairOffsetUpperAction.ShootHold => shootHoldLoop != null ? shootHoldLoop.Clip : null,
            PairOffsetUpperAction.Reload => reload != null ? reload.Clip : null,
            _ => null,
        };
    }

    private void OnEnable()
    {
        EnsureLocomotionDirectionalClips();
        resolvedLocomotionMixer = null;
    }

    private void OnValidate()
    {
        locomotionFadeDuration = SanitizeNonNegative(locomotionFadeDuration, DefaultLocomotionFadeDuration);
        locomotionPlaybackSpeed = SanitizePlaybackSpeed(locomotionPlaybackSpeed);
        EnsureLocomotionDirectionalClips();
        resolvedLocomotionMixer = null;
    }

    private void EnsureLocomotionDirectionalClips()
    {
        locomotionDirectionalClips ??= new DirectionalClipSet2D();
    }

    private static float SanitizeNonNegative(float value, float fallback)
    {
        if (float.IsNaN(value) || value < 0f)
            return fallback;

        return value;
    }

    private static float SanitizePlaybackSpeed(float value)
    {
        if (float.IsNaN(value) || value < 0f || Mathf.Approximately(value, 0f))
            return DefaultLocomotionPlaybackSpeed;

        return value;
    }
}
