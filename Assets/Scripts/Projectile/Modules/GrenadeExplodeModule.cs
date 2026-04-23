using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Projectile Modules/Grenade Explode")]
public class GrenadeExplodeModule : ProjectileModule
{
    [Header("Trigger")]
    public bool explodeOnDamageableHit = true;
    public bool explodeOnWallHit = true;

    [Tooltip("ระเบิดเองเมื่อครบเวลา (<=0 = ปิด)")]
    public float fuseTime = 0f;

    [Tooltip("ถ้า true จะระเบิดตอน OnExpire (เช่นหมดอายุ) ด้วย")]
    public bool explodeOnExpire = true;

    [Header("Explosion")]
    public float radius = 3.5f;

    [Tooltip("ถ้า < 0 จะใช้ ctx.stats.damage เป็น baseDamage")]
    public float explosionBaseDamage = -1f;

    [Tooltip("0=ขอบวง, 1=กลางวง")]
    public AnimationCurve damageFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Tooltip("รวมเป้าหมายที่โดนชนด้วยไหม (ถ้า Projectile ทำ impact damage อยู่แล้ว และไม่อยากซ้ำ ให้ปิด)")]
    public bool includeHitTarget = true;

    [Tooltip("ไม่ทำดาเมจ owner (คนยิง)")]
    public bool ignoreOwner = true;
    public bool suppressDirectHitDamage = true;

    public LayerMask damageMask = ~0;
    public QueryTriggerInteraction queryTriggers = QueryTriggerInteraction.Collide;

    [Header("Crit/Armor")]
    public bool allowCrit = true;

    [Header("Knockback (optional)")]
    public bool applyKnockback = false;
    [Tooltip("Legacy scalar from the old rigidbody impulse path. If knockbackDistance <= 0, this is converted to distance.")]
    public float knockbackForce = 6f;
    [Min(0f)] public float knockbackDistance = 0f;
    [Min(0f)] public float knockbackDuration = 0.12f;
    [Tooltip("Normalized knockback travel over time. X = time, Y = travel progress. Leave null for linear.")]
    public AnimationCurve knockbackProgressCurve;
    public ImpactReactionKind knockbackReaction = ImpactReactionKind.MiniStun;
    public bool knockbackInterruptsActions = true;

    [Header("VFX (optional)")]
    public GameObject explosionVfxPrefab;
    public float vfxScale = 1f;

    public KnockbackSettings BuildResolvedKnockbackSettings(float resolvedKnockbackDistance)
    {
        return new KnockbackSettings(
            resolvedKnockbackDistance,
            knockbackDuration,
            knockbackProgressCurve,
            knockbackReaction,
            knockbackInterruptsActions);
    }

    static Vector3 ResolveExplosionKnockbackDirection(Collider collider, IDamageable damageable, Vector3 center, Vector3 closest, Vector3 fallbackDirection)
    {
        if (TryGetGroundedDirection(closest - center, out Vector3 direction))
            return direction;

        if (collider != null && TryGetGroundedDirection(collider.bounds.center - center, out direction))
            return direction;

        if (damageable is Component damageableComponent &&
            TryGetGroundedDirection(damageableComponent.transform.position - center, out direction))
        {
            return direction;
        }

        return fallbackDirection;
    }

    static bool TryGetGroundedDirection(Vector3 candidate, out Vector3 direction)
    {
        direction = candidate;

        if (!IsFinite(direction))
        {
            direction = Vector3.zero;
            return false;
        }

        direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        return direction.sqrMagnitude > 0.0001f;
    }

    static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    class State : IProjectileModuleState
    {
        public float t;
        public bool exploded;
        public HashSet<int> hitOnce = new();
    }

    public override IProjectileModuleState CreateState() => new State();

    public override bool SuppressBuiltinAreaDamage(Projectile p, ProjectileContext ctx, IProjectileModuleState state) => true;
    public override bool SuppressBuiltinDamageableHit(Projectile p, ProjectileContext ctx, IProjectileModuleState state, IDamageable target)
        => suppressDirectHitDamage && explodeOnDamageableHit && target != null;

    public override void OnSpawn(Projectile p, ProjectileContext ctx, IProjectileModuleState st)
    {
        var s = (State)st;
        s.t = 0f;
        s.exploded = false;
        s.hitOnce.Clear();
    }

    public override void Tick(Projectile p, ProjectileContext ctx, IProjectileModuleState st, float dt)
    {
        var s = (State)st;
        if (s.exploded) return;

        if (fuseTime > 0f)
        {
            s.t += dt;
            if (s.t >= fuseTime)
            {
                Explode(p, ctx, p.transform.position, null, st);
                p.RequestDespawn();
                s.exploded = true;
            }
        }
    }

    public override void OnHit(Projectile p, ProjectileContext ctx, IProjectileModuleState st,
        in ProjectileHitInfo hit, IDamageable target)
    {
        var s = (State)st;
        if (s.exploded) return;

        // กัน explode ซ้ำจาก trigger ค้าง (เช่นชนหลาย collider)
        int cid = hit.collider ? hit.collider.transform.root.GetInstanceID() : 0;
        if (cid != 0 && s.hitOnce.Contains(cid)) return;
        if (cid != 0) s.hitOnce.Add(cid);

        if (target != null)
        {
            if (!explodeOnDamageableHit) return;
            Explode(p, ctx, hit.ResolvePoint(p.transform.position), hit.collider, st);
            p.RequestDespawn();
            s.exploded = true;
            return;
        }

        // target == null -> wall (Projectile ของคุณส่ง OnHit มาเฉพาะ damageable/wall อยู่แล้ว)
        if (hit.collider != null && hit.collider.CompareTag("Wall") && explodeOnWallHit)
        {
            Explode(p, ctx, hit.ResolvePoint(p.transform.position), hit.collider, st);
            p.RequestDespawn();
            s.exploded = true;
        }
    }

    public override void OnExpire(Projectile p, ProjectileContext ctx, IProjectileModuleState st)
    {
        var s = (State)st;
        if (s.exploded) return;

        if (explodeOnExpire)
        {
            Explode(p, ctx, p.transform.position, null, st);
            s.exploded = true;
        }
    }

  void Explode(Projectile p, ProjectileContext ctx, Vector3 center, Collider hitCol, IProjectileModuleState st)
{
    // ---- from skill? ----
    var def = p.SourceSkillDef;
    var execution = p.SourceSkillExecution;
    var sStats = p.SourceSkillStats;
    bool fromSkill = (def != null);

    // radius: ถ้าเป็นสกิล ใช้ FinalSkillStats.areaRadius (ผ่าน support แล้ว)
    float usedRadius = (fromSkill && sStats != null && sStats.areaRadius > 0f)
        ? sStats.areaRadius
        : Mathf.Max(0.01f, radius);

    // base damage: ถ้าตั้ง explosionBaseDamage ให้ใช้มัน (เหมือนเดิม)
    float baseDmg = (explosionBaseDamage >= 0f) ? explosionBaseDamage : ctx.stats.damage;

    // falloff: ใช้ของโมดูล (ไม่ดึงจาก def)
    var usedFalloff = damageFalloff;

    // Prefer the skill override when present, otherwise fall back to the projectile/module explosion VFX.
    GameObject usedVfxPrefab = fromSkill && execution != null && execution.ProjectileHitVfxPrefab != null
        ? execution.ProjectileHitVfxPrefab
        : (p.hitVfxPrefab != null ? p.hitVfxPrefab : explosionVfxPrefab);

    float usedVfxScale = fromSkill && execution != null && execution.ProjectileHitVfxPrefab != null
        ? execution.ProjectileHitVfxScale
        : (p.vfxScale > 0f ? p.vfxScale : vfxScale);

    // ---- VFX ----
    if (usedVfxPrefab != null)
    {
        if (VfxSpawner.Instance != null)
            VfxSpawner.Instance.SpawnVfx(usedVfxPrefab, center, Vector3.up, 1.0f, usedVfxScale);
        else
            Object.Instantiate(usedVfxPrefab, center, Quaternion.identity);
    }

    var cols = Physics.OverlapSphere(center, usedRadius, damageMask, queryTriggers);

    var damaged = new HashSet<int>();

    int ownerId = (ignoreOwner && ctx.collisionIgnoreRoot != null) ? ctx.collisionIgnoreRoot.root.GetInstanceID() : 0;
    IDamageable hitTarget = hitCol != null ? hitCol.GetComponentInParent<IDamageable>() : null;
    int hitTargetId = Projectile.GetDamageableIdentityKey(hitTarget);

    foreach (var c in cols)
    {
        if (!c) continue;

        int rid = c.transform.root.GetInstanceID();
        if (ownerId != 0 && rid == ownerId) continue;

        var dmgable = c.GetComponentInParent<IDamageable>();
        if (dmgable == null) continue;

        int targetId = Projectile.GetDamageableIdentityKey(dmgable);
        if (targetId == 0)
            targetId = c.GetInstanceID();

        if (!includeHitTarget && hitTargetId != 0 && targetId == hitTargetId) continue;
        if (damaged.Contains(targetId)) continue;

        damaged.Add(targetId);

        Vector3 closest = c.ClosestPoint(center);
        float dist = Vector3.Distance(center, closest);
        // The curve is authored as 0=edge, 1=center, so invert normalized distance before sampling.
        float normalizedDistance = Mathf.Clamp01(dist / Mathf.Max(0.01f, usedRadius));
        float factor = Mathf.Clamp01(usedFalloff.Evaluate(1f - normalizedDistance));
        float scaled = baseDmg * factor;

        float armor = 0f;
        if (c.GetComponentInParent<IHasArmor>() is IHasArmor hasArmor)
            armor = hasArmor.Armor;

        float critR = allowCrit ? p.critRate : 0f;
        float critM = allowCrit ? p.critMult : 1f;

        float final = DamageCalculator.CalculateFinalDamage(
            p.gunType,
            0f,        // ระเบิดไม่ควรโดน range falloff
            scaled,
            critR,
            critM,
            armor
        );

        Vector3 hitNormal = closest - center;
        if (hitNormal.sqrMagnitude <= 0.0001f)
            hitNormal = Vector3.up;
        else
            hitNormal.Normalize();

        float resolvedKnockbackDistance = knockbackDistance > 0f
            ? knockbackDistance
            : Mathf.Max(0f, knockbackForce * 0.2f);

        KnockbackData knockback = default(KnockbackData);
        if (applyKnockback)
        {
            KnockbackSettings settings = BuildResolvedKnockbackSettings(resolvedKnockbackDistance);
            Vector3 knockbackDirection = ResolveExplosionKnockbackDirection(c, dmgable, center, closest, hitNormal);
            KnockbackBuildContext knockbackContext = new KnockbackBuildContext(
                center,
                closest,
                hitNormal,
                explicitDirection: knockbackDirection);

            if (KnockbackFactory.TryBuild(in settings, in knockbackContext, out KnockbackData builtKnockback))
                knockback = builtKnockback;
        }

        var hit = new ProjectileHitInfo(closest, hitNormal, c);
        p.ApplyResolvedDamage(dmgable, final, hit, knockback, showDamageNumber: true);
    }
}
}
