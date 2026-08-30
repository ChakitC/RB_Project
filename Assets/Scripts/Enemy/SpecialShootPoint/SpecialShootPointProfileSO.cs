using UnityEngine;

/// <summary>
/// Shared Special Shoot Point rules and presentation. Contains no per-enemy bone references, so
/// bosses and elites can swap profiles without touching the runtime code or the anchor authoring
/// on their prefab.
/// </summary>
[CreateAssetMenu(
    fileName = "SpecialShootPointProfile",
    menuName = "RB/Enemy/Special Shoot Point Profile")]
public class SpecialShootPointProfileSO : ScriptableObject
{
    [Header("Timing (gameplay time)")]
    [Tooltip("Points are visible but not hittable. Shots pass through to the ordinary body colliders.")]
    [Min(0f)] public float telegraphDuration = 0.6f;

    [Tooltip("How long the player has to destroy every active point.")]
    [Min(0.05f)] public float activeDuration = 4f;

    [Tooltip("Starts when an accepted challenge resolves: success, timeout, or cancellation.")]
    [Min(0f)] public float cooldown = 8f;

    [Tooltip("Remaining active time at which every surviving point flashes faster.")]
    [Min(0f)] public float lastSecondWarningThreshold = 1f;

    [Header("Point Count")]
    [Tooltip("Used when the Behavior Tree does not override the count.")]
    [Min(1)] public int defaultPointCount = 2;

    [Tooltip("Hard ceiling. A Behavior Tree override is clamped to this and to the valid anchor count.")]
    [Min(1)] public int maxPointCount = 4;

    [Header("Point HP")]
    [Tooltip("Percentage of the enemy's current Max HP each point is worth. Every point in a round shares this HP.")]
    [Min(0f)] public float pointHealthPercentOfMaxHp = 3f;

    [Tooltip("Lower clamp on the resolved per-point HP.")]
    [Min(1f)] public float pointHealthMin = 10f;

    [Tooltip("Upper clamp on the resolved per-point HP.")]
    [Min(1f)] public float pointHealthMax = 5000f;

    [Header("Reward")]
    [Tooltip("Percentage of the target's Max Stagger granted the moment the final point breaks.")]
    [Min(0f)] public float staggerRewardPercentOfMaxStagger = 25f;

    [Tooltip("Gameplay lock held after a successful round when the Mini Stun clip is missing or invalid.")]
    [Min(0.05f)] public float missingClipFallbackSeconds = 0.6f;

    [Header("Runtime Point")]
    [Tooltip("Pooled runtime point prefab. Must carry a SpecialShootPointInstance.")]
    public SpecialShootPointInstance runtimePointPrefab;

    [Tooltip("Layer the point collider lives on while the round is Active. Defaults to 'Hit'.")]
    public int pointColliderLayer = 3;

    [Header("Presentation")]
    [Tooltip("Played once per point when it breaks. Success feedback.")]
    public GameObject breakVfxPrefab;

    [Tooltip("Played once per surviving point when the round times out. Must read differently from a break.")]
    public GameObject timeoutVfxPrefab;

    [Tooltip("Seconds a broken or extinguished point takes to fade out before it returns to the pool.")]
    [Min(0f)] public float resolveFadeSeconds = 0.25f;

    [Tooltip("Played on every accepted point hit.")]
    public AudioClip pointHitSfx;

    [Tooltip("Played when a point breaks.")]
    public AudioClip pointBreakSfx;

    /// <summary>Per-point HP for a round, from the owner's Max HP, clamped by the profile.</summary>
    public float ResolvePointHealth(float ownerMaxHealth)
    {
        float raw = Mathf.Max(0f, ownerMaxHealth) * Mathf.Max(0f, pointHealthPercentOfMaxHp) * 0.01f;
        float min = Mathf.Max(1f, pointHealthMin);
        float max = Mathf.Max(min, pointHealthMax);
        return Mathf.Clamp(raw, min, max);
    }

    /// <summary>Stagger granted for a completed round, from the target's Max Stagger.</summary>
    public float ResolveStaggerReward(float maxStagger)
    {
        return Mathf.Max(0f, maxStagger) * Mathf.Max(0f, staggerRewardPercentOfMaxStagger) * 0.01f;
    }

    /// <summary>Clamps a requested count to the profile ceiling and the caller's valid anchor count.</summary>
    public int ResolvePointCount(int requestedCount, int validAnchorCount)
    {
        int ceiling = Mathf.Min(Mathf.Max(1, maxPointCount), Mathf.Max(0, validAnchorCount));
        int requested = requestedCount > 0 ? requestedCount : Mathf.Max(1, defaultPointCount);
        return Mathf.Clamp(requested, 0, ceiling);
    }

    void OnValidate()
    {
        maxPointCount = Mathf.Max(1, maxPointCount);
        defaultPointCount = Mathf.Clamp(defaultPointCount, 1, maxPointCount);
        pointHealthMax = Mathf.Max(pointHealthMin, pointHealthMax);
        lastSecondWarningThreshold = Mathf.Clamp(lastSecondWarningThreshold, 0f, activeDuration);
        pointColliderLayer = Mathf.Clamp(pointColliderLayer, 0, 31);
    }
}
