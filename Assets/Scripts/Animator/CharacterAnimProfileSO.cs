using Animancer;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Game/Characters/Animation Profile", fileName = "CharacterAnimProfile")]
public sealed class CharacterAnimProfileSO : ScriptableObject
{
    [Header("Layers")]
    public AvatarMask upperBodyMask;
    [Min(0f)] public float actionFadeIn = 0.06f;
    [Min(0f)] public float actionFadeOut = 0.08f;

    [Header("Locomotion (Layer 0)")]
    public MixerTransition2D locomotionMixer;
    [Min(0f)] public float locomotionParamLerp = 14f;
    public bool snapTo8Directions = true;


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

    [Header("Reload (Layer 1)")]
    public ClipTransition reload;

    [Header("Melee Combo")]
    public MeleeComboSO meleeCombo;
    public bool meleeCanInterruptReload = true;
    public MeleeComboSO lightCombo;
    public MeleeComboSO heavyCombo;

    [Header("Downed (Layer 0)")]
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
}
