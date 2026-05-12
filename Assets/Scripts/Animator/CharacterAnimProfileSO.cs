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
                return "serialized mixer";

            if (!HasCardinalSet)
                return "directional override incomplete";

            return HasFullEightDirectionSet
                ? "generated 8-direction"
                : "generated 4-direction";
        }

        public bool TryGetIssue(out string issue)
        {
            issue = null;

            if (!HasAnyAssignments)
                return false;

            if (!HasCardinalSet)
            {
                issue = "Locomotion directional override is incomplete. Idle, Forward, Backward, Left, and Right are all required.";
                return true;
            }

            if (HasAnyDiagonalAssignments && !HasFullEightDirectionSet)
            {
                issue = "Locomotion directional override is missing one or more diagonal clips, so it will fall back to 4-direction blending.";
                return true;
            }

            return false;
        }

        public MixerTransition2D CreateMixer(MixerTransition2D template)
        {
            if (!HasCardinalSet)
                return null;

            var mixer = new MixerTransition2D
            {
                Type = MixerTransition2D.MixerType.Directional,
                FadeDuration = template != null ? template.FadeDuration : 0.25f,
                Speed = template != null ? template.Speed : 1f,
                DefaultParameter = Vector2.zero,
            };

            if (template != null)
            {
                mixer.ParameterNameX = template.ParameterNameX;
                mixer.ParameterNameY = template.ParameterNameY;
                mixer.Speeds = template.Speeds;
                mixer.SynchronizeChildren = template.SynchronizeChildren;
            }

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

    [NonSerialized] private MixerTransition2D resolvedLocomotionMixer;

    [Header("Layers")]
    public AvatarMask upperBodyMask;
    [Min(0f)] public float actionFadeIn = 0.06f;
    [Min(0f)] public float actionFadeOut = 0.08f;

    [Header("Locomotion (Layer 0)")]
    public MixerTransition2D locomotionMixer;
    [Min(0f)] public float locomotionParamLerp = 14f;
    public bool snapTo8Directions = true;

    [Header("Locomotion Override (Optional Explicit Directional Clips)")]
    public DirectionalClipSet2D locomotionDirectionalClips = new();


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
        if (resolvedLocomotionMixer == null)
            resolvedLocomotionMixer = locomotionDirectionalClips.CreateMixer(locomotionMixer) ?? locomotionMixer;

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
        => locomotionDirectionalClips.GetStatusLabel();

    public bool TryGetLocomotionConfigurationIssue(out string issue)
        => locomotionDirectionalClips.TryGetIssue(out issue);

    private void OnEnable()
    {
        resolvedLocomotionMixer = null;
    }

    private void OnValidate()
    {
        resolvedLocomotionMixer = null;
    }
}
