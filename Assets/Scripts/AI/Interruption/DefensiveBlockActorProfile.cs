using UnityEngine;

[CreateAssetMenu(menuName = "RB/Combat/Defensive Block Actor")]
public sealed class DefensiveBlockActorProfile : ScriptableObject
{
    [Header("Animation")]
    public BlockAnimationProfile animation;
    [Header("Placement")]
    public LayerMask worldLayers = 1;
    [Min(0.1f)] public float standAhead = 2.5f;
    [Min(0.1f)] public float minimumStandAhead = 0.8f;
    [Min(0.1f)] public float approachClearance = 4.2f;
    [Header("Guard")]
    [Min(0f)] public float guardCenterHeight = 1.2f;
    [Min(0.1f)] public float guardHalfWidth = 0.65f;
    [Min(0.1f)] public float guardHalfHeight = 1.2f;
    [Min(0f)] public float guardHalfDepth = 0.05f;
    [Min(0f)] public float guardForwardOffset = 1.4f;
    [Min(0.1f)] public float timeoutSeconds = 2f;
    [Header("Recoil")]
    [Min(0f)] public float slideDistance = 1.5f;
    [Min(0.01f)] public float slideSeconds = 0.35f;
    [Header("Warp presentation")]
    [Min(0f)] public float warpFadeOutSeconds = 0.04f;
    [Min(0f)] public float warpFadeInSeconds = 0.08f;
    [Header("Impact presentation")]
    [Tooltip("Time from accepted Block to impact, using the attacker's actor clock. Zero uses physical interception.")]
    [Min(0f)] public float timedApproachSeconds = 0.5f;
    public GameObject impactVfx;
    [Min(0.01f)] public float impactVfxLifetime = 2f;
    [Tooltip("Played once when Block succeeds, for both companion and self guard.")]
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
    public bool IsConfigured => animation != null && animation.beginClip != null && animation.impactClip != null &&
        minimumStandAhead > 0f && standAhead >= minimumStandAhead && guardHalfWidth > 0f && guardHalfHeight > 0f &&
        timeoutSeconds > 0f && slideDistance >= 0f && slideSeconds > 0f && guardCenterHeight >= 0f &&
        timedApproachSeconds >= 0f && impactVfxLifetime > 0f && hitLagDuration >= 0f && hitLagTimeScale >= 0.01f && hitLagTimeScale <= 1f;
}
