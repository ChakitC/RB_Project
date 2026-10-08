using UnityEngine;

[CreateAssetMenu(menuName = "RB/Combat/Defensive Block Actor")]
public sealed class DefensiveBlockActorProfile : ScriptableObject
{
    // Retained only to migrate old assets. Runtime animation comes from the character's animProfile.
    [HideInInspector]
    public BlockAnimationProfile animation;
    [Tooltip("Allow this actor to participate in Player + Ally Cooperative Block.")]
    public bool cooperativeEnabled;
    [Header("Placement")]
    public LayerMask worldLayers = 1;
    [Min(0.1f)] public float standAhead = 2.5f;
    [Tooltip("Companion guard offset to the right of the Player-to-attacker heading. Tries the opposite side if needed. Zero keeps centered placement.")]
    [Min(0f)] public float standSideways;
    [Min(0.1f)] public float minimumStandAhead = 0.8f;
    [Min(0.1f)] public float approachClearance = 4.2f;
    [Header("Guard")]
    [Min(0f)] public float guardCenterHeight = 1.2f;
    [Min(0.1f)] public float guardHalfWidth = 0.65f;
    [Min(0.1f)] public float guardHalfHeight = 1.2f;
    [Min(0f)] public float guardHalfDepth = 0.05f;
    [Min(0f)] public float guardForwardOffset = 1.4f;
    [Min(0.1f)] public float timeoutSeconds = 2f;
    [Header("Warp presentation")]
    [Min(0f)] public float warpFadeOutSeconds = 0.04f;
    [Min(0f)] public float warpFadeInSeconds = 0.08f;
    [Header("Impact presentation")]
    public GameObject impactVfx;
    [Min(0.01f)] public float impactVfxLifetime = 2f;
    [Tooltip("Played when Block succeeds, and once at the initial contact of Cooperative Block.")]
    public AudioCue impactCue;
    [Header("Ready signal")]
    [Tooltip("Played when the actionable Block flare first becomes visible for the selected attack.")]
    public AudioCue readyCue;
    [Header("Global HitLag")]
    [Tooltip("Zero disables Block HitLag. Duration is measured in real seconds.")]
    [Min(0f)] public float hitLagDuration = 0.06f;
    [Range(0.01f, 1f)] public float hitLagTimeScale = 0.1f;
    [Tooltip("Blend strength: 1 uses HitLag Time Scale, 0 uses normal speed. Empty uses the global default curve.")]
    public AnimationCurve hitLagShape;
    [Header("Camera")]
    public bool cameraEnabled = true;
    public Vector3 cameraLocalPosition = new Vector3(2.65f, 1.55f, -1.91f);
    public Vector3 cameraLocalEulerAngles = new Vector3(-0.35f, -35.39f, 10.49f);
    [Range(20f, 100f)] public float cameraFieldOfView = 60f;
    [Min(0.01f)] public float cameraBlendInSeconds = 0.16f;
    [Min(0f)] public float cameraHoldSeconds = 0.12f;
    [Min(0.01f)] public float cameraBlendOutSeconds = 0.4f;
    [Header("Cooperative Struggle Camera")]
    [Tooltip("Relative to the defenders' midpoint and guard heading. Leaves the right side clear for Block UI.")]
    public Vector3 cooperativeCameraLocalPosition = new Vector3(1.9f, 2.2f, -3.4f);
    public Vector3 cooperativeCameraLocalEulerAngles = new Vector3(10f, -12f, 8f);
    [Range(20f, 100f)] public float cooperativeCameraFieldOfView = 55f;
    [Min(0.01f)] public float cooperativeCameraBlendSeconds = 0.2f;
    [Header("Normal Block Camera Shake")]
    [Tooltip("Impact rotation in degrees on successful Contact or Timed Approach Block. Zero disables shake.")]
    [Min(0f)] public float normalImpactShakeDegrees = 1.2f;
    [Tooltip("Impact decay duration in real seconds, excluding pause.")]
    [Min(0f)] public float normalImpactShakeSeconds = .15f;
    [Min(0f)] public float normalImpactShakeFrequency = 20f;
    [Header("Cooperative Camera Shake")]
    [Tooltip("Impact rotation in degrees when the enemy reaches the pair. Zero disables the impact shake.")]
    [Min(0f)] public float cooperativeImpactShakeDegrees = 1.8f;
    [Tooltip("Impact decay duration in real seconds, excluding pause.")]
    [Min(0f)] public float cooperativeImpactShakeSeconds = .22f;
    [Min(0f)] public float cooperativeImpactShakeFrequency = 22f;
    [Tooltip("Gentle rotation in degrees while struggling. Zero disables continuous shake.")]
    [Min(0f)] public float cooperativeStruggleShakeDegrees = .12f;
    [Min(0f)] public float cooperativeStruggleShakeFrequency = 9f;
    [Header("Cooperative Success Camera Shake")]
    [Tooltip("One camera kick when the pair wins. Zero disables the success shake.")]
    [Min(0f)] public float cooperativeSuccessShakeDegrees = 1.8f;
    [Tooltip("Success kick decay in real seconds, excluding pause.")]
    [Min(0f)] public float cooperativeSuccessShakeSeconds = .2f;
    [Min(0f)] public float cooperativeSuccessShakeFrequency = 20f;
    public bool IsConfigured =>
        minimumStandAhead > 0f && standAhead >= minimumStandAhead && guardHalfWidth > 0f && guardHalfHeight > 0f &&
        timeoutSeconds > 0f && guardCenterHeight >= 0f &&
        impactVfxLifetime > 0f && hitLagDuration >= 0f && hitLagTimeScale >= 0.01f && hitLagTimeScale <= 1f;
}
