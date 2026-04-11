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
    public ImpactReactionKind knockbackReaction = ImpactReactionKind.MiniStun;
    public bool knockbackInterruptsActions = true;

    // เรียงลำดับมีผล (เช่น Pierce ก่อน Split ถ้า Split ทำตอนนัดสุดท้าย)
    public List<ProjectileModule> modules = new();
}
