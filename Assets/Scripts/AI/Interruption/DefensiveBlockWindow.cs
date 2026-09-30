using System;
using UnityEngine;

[Serializable]
public sealed class DefensiveBlockWindow
{
    [Range(0f, 1f)] public float startNormalized;
    [Range(0f, 1f)] public float endNormalized = 0.62f;
    [Tooltip("Optional animation impact frame for Timed Approach / Cooperative. Off preserves the original approach playback.")]
    public bool useImpactMarker;
    [Sirenix.OdinInspector.ShowIf(nameof(useImpactMarker))]
    [Tooltip("Main skill clip time. Must be at or after Close and before the clip ends (and the next Block window).")]
    [Range(0f, 1f)] public float impactNormalized = 0.7f;
    public int[] hitboxSteps = { 0 };
    [Tooltip("Continue cancels only this window's hitboxes; enemy playback continues without knockback. Interrupt ends the skill and knocks the enemy back.")]
    public DefensiveBlockOutcome onSuccess;

    public bool Contains(float time) => time >= startNormalized && time <= endNormalized;
    public bool AllowsStep(int step) => step >= 0 && hitboxSteps != null && Array.IndexOf(hitboxSteps, step) >= 0;
    public bool IsValid => !float.IsNaN(startNormalized) && !float.IsNaN(endNormalized) &&
        startNormalized >= 0f && endNormalized <= 1f && startNormalized <= endNormalized &&
        (!useImpactMarker || (impactNormalized >= endNormalized && impactNormalized < 1f)) &&
        hitboxSteps != null && hitboxSteps.Length > 0 && Array.TrueForAll(hitboxSteps, step => step >= 0) &&
        Enum.IsDefined(typeof(DefensiveBlockOutcome), onSuccess);
    public DefensiveBlockWindow Copy() => new DefensiveBlockWindow
    {
        startNormalized = startNormalized, endNormalized = endNormalized,
        useImpactMarker = useImpactMarker, impactNormalized = impactNormalized,
        hitboxSteps = hitboxSteps != null ? (int[])hitboxSteps.Clone() : null, onSuccess = onSuccess
    };
}
