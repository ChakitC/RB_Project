using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Stagger Profile", fileName = "StaggerProfile")]
public sealed class StaggerProfileSO : ScriptableObject
{
    [Header("Meter")]
    [Min(1f)] public float maxStagger = 100f;
    [Min(0f)] public float staggerGainMultiplier = 1f;

    [Header("Break State")]
    [Min(0.01f)] public float staggerDuration = 1.5f;
    [Min(1f)] public float damageTakenMultiplierWhileStaggered = 1.25f;
    [Min(0f)] public float postStaggerImmunity = 1f;

    [Header("Decay")]
    [Min(0f)] public float decayDelay = 1.5f;
    [Min(0f)] public float decayPerSecond = 20f;
}
