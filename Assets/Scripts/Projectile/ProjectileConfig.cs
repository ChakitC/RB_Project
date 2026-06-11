using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Projectile Config")]
public class ProjectileConfig : ScriptableObject
{
    public float baseSpeed = 30f;
    public float lifeTime = 3f;

    [Header("Impact")]
    public bool applyKnockbackOnHit = false;
    [Min(0f)] public float knockbackDistance = 1.25f;
    [Min(0f)] public float knockbackDuration = 0.12f;
    [Tooltip("Normalized knockback travel over time. X = time, Y = travel progress. Leave null for linear.")]
    public AnimationCurve knockbackProgressCurve;
    public ImpactReactionKind knockbackReaction = ImpactReactionKind.MiniStun;
    public bool knockbackInterruptsActions = true;

    [Header("Stagger")]
    public bool overrideStaggerPower = false;
    [Min(0f)] public float staggerPower = 0f;
    [Min(0f)] public float staggerPowerMultiplier = 1f;
    [Min(0f)] public float bonusStaggerPower = 0f;

    // เรียงลำดับมีผล (เช่น Pierce ก่อน Split ถ้า Split ทำตอนนัดสุดท้าย)
    public List<ProjectileModule> modules = new();

    public KnockbackSettings ToKnockbackSettings()
    {
        return new KnockbackSettings(
            knockbackDistance,
            knockbackDuration,
            knockbackProgressCurve,
            knockbackReaction,
            knockbackInterruptsActions);
    }

    public StaggerPayload ToStaggerPayload(float baseStaggerPower, string staggerSourceId)
    {
        float basePower = overrideStaggerPower ? staggerPower : baseStaggerPower;
        float resolvedPower = basePower * Mathf.Max(0f, staggerPowerMultiplier) + Mathf.Max(0f, bonusStaggerPower);
        return new StaggerPayload(resolvedPower, 1f, staggerSourceId);
    }
}
