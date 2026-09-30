using UnityEngine;

[System.Serializable]
public sealed class CooperativeBlockSettings
{
    [Min(.1f)] public float duration = 3f;
    [Range(0f, 1f)] public float initialMeter = .5f;
    [Range(.001f, 1f)] public float gainPerPress = .1f;
    [Min(0f)] public float drainPerSecond = .2f;
    [Range(.05f, 1f)] public float worldScale = .15f;
    [Tooltip("Optional enemy struggle loop for this skill. Empty holds the attack's impact pose. Use an in-place, non-Legacy clip.")]
    public AnimationClip enemyStruggleClip;
    [Min(0f)] public float enemyStruggleFadeSeconds = .06f;
    public bool IsValid => Finite(duration) && duration > 0f && Finite(initialMeter) && initialMeter > 0f && initialMeter < 1f &&
        Finite(gainPerPress) && gainPerPress > 0f && gainPerPress <= 1f && Finite(drainPerSecond) && drainPerSecond >= 0f &&
        Finite(worldScale) && worldScale >= .05f && worldScale <= 1f &&
        Finite(enemyStruggleFadeSeconds) && enemyStruggleFadeSeconds >= 0f &&
        (enemyStruggleClip == null || (!enemyStruggleClip.legacy && enemyStruggleClip.length > 0f));
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public CooperativeBlockSettings Copy() => (CooperativeBlockSettings)MemberwiseClone();
}
