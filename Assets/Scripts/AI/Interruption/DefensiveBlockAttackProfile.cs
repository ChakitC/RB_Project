using UnityEngine;

[CreateAssetMenu(menuName = "RB/Combat/Defensive Block Attack")]
public sealed class DefensiveBlockAttackProfile : ScriptableObject
{
    [Min(0f)] public float commandRange = 8f;
    public LayerMask worldLayers = 1;
    [Range(0f, 1f)] public float windowStartNormalized;
    [Range(0f, 1f)] public float windowEndNormalized = 0.62f;
    public int[] hitboxSteps = { 0, 1 };
    [Header("Threat prediction (before the hitbox is active)")]
    [Min(0.01f)] public float threatHalfWidth = 1.5f;
    [Min(0f)] public float threatForwardReach = 2.3f;
    [Min(0.01f)] public float estimatedChargeSpeed = 8f;
    [Min(0f)] public float knockbackDistance = 2f;
    [Min(0.01f)] public float knockbackSeconds = 0.4f;
    public bool AllowsStep(int step) => step >= 0 && hitboxSteps != null && System.Array.IndexOf(hitboxSteps, step) >= 0;
    public bool IsConfigured => commandRange > 0f && windowStartNormalized >= 0f &&
        windowEndNormalized <= 1f && windowEndNormalized >= windowStartNormalized &&
        hitboxSteps != null && hitboxSteps.Length > 0 && System.Array.TrueForAll(hitboxSteps, step => step >= 0) &&
        knockbackDistance >= 0f && knockbackSeconds > 0f &&
        threatHalfWidth > 0f && threatForwardReach >= 0f && estimatedChargeSpeed > 0f;
}
