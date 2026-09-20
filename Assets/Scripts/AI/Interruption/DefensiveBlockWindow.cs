using System;
using UnityEngine;

[Serializable]
public sealed class DefensiveBlockWindow
{
    [Range(0f, 1f)] public float startNormalized;
    [Range(0f, 1f)] public float endNormalized = 0.62f;
    public int[] hitboxSteps = { 0 };
    [Tooltip("Continue cancels only this window's hitboxes; enemy playback continues without knockback. Interrupt ends the skill and knocks the enemy back.")]
    public DefensiveBlockOutcome onSuccess;

    public bool Contains(float time) => time >= startNormalized && time <= endNormalized;
    public bool AllowsStep(int step) => step >= 0 && hitboxSteps != null && Array.IndexOf(hitboxSteps, step) >= 0;
    public bool IsValid => !float.IsNaN(startNormalized) && !float.IsNaN(endNormalized) &&
        startNormalized >= 0f && endNormalized <= 1f && startNormalized <= endNormalized &&
        hitboxSteps != null && hitboxSteps.Length > 0 && Array.TrueForAll(hitboxSteps, step => step >= 0) &&
        Enum.IsDefined(typeof(DefensiveBlockOutcome), onSuccess);
    public DefensiveBlockWindow Copy() => new DefensiveBlockWindow
    {
        startNormalized = startNormalized, endNormalized = endNormalized,
        hitboxSteps = hitboxSteps != null ? (int[])hitboxSteps.Clone() : null, onSuccess = onSuccess
    };
}
