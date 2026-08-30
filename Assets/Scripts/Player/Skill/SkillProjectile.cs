using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class SkillProjectile : MonoBehaviour, IBarrierBlockableProjectile
{
    GameObject hitVfxPrefab;
    GameObject BallVfxPrefab;
    float vfxScale = 1f;

    [Header("Movement")]
    public float speed = 15f;
    public float lifetime = 4f;
    public bool useAreaDamage = true;

    Rigidbody rb;
    Collider myCol;

    ISkillUser caster;
    Transform casterRoot;
    FinalSkillStats stats;

    Vector3 spawnPos;
    Vector3 moveDirection = Vector3.forward;
    float age;
    readonly List<Collider> ignoredCols = new List<Collider>();

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        myCol = GetComponent<Collider>();

        myCol.isTrigger = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

    }

    public void Initialize(
        ISkillUser caster,
        FinalSkillStats stats,
        bool areaOfEffect,
        Vector3 direction,
        float speed,
        GameObject hitVfxPrefab,
        GameObject ballVfxPrefab,
        float vfxScale
    )
    {
        this.caster        = caster;
        this.stats         = stats;
        this.useAreaDamage = areaOfEffect;

        this.hitVfxPrefab  = hitVfxPrefab;
        this.BallVfxPrefab = ballVfxPrefab;
        this.vfxScale      = vfxScale;

        this.speed         = speed > 0 ? speed : this.speed;
        age = 0f;

        if (caster is Component casterComp)
        {
            casterRoot = casterComp.transform.root;
            spawnPos   = casterComp.transform.position;

            foreach (var col in casterRoot.GetComponentsInChildren<Collider>())
            {
                if (col && myCol)
                {
                    Physics.IgnoreCollision(myCol, col, true);
                    ignoredCols.Add(col);
                }
            }
        }
        else
        {
            spawnPos = transform.position;
        }

        SpawnBallVfx();
        SetDirection(direction);
    }

    public void SetDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;

        moveDirection = dir.normalized;
        ApplyVelocity();
    }

    void FixedUpdate()
    {
        float worldScale = TimeSlowManager.Instance.WorldTimeScale;
        age += Time.fixedDeltaTime * worldScale;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        ApplyVelocity();
    }

    void ApplyVelocity()
    {
        rb.linearVelocity = moveDirection * speed * TimeSlowManager.Instance.WorldTimeScale;
    }

    // ===== Barrier =====

    GameObject IBarrierBlockableProjectile.BarrierSourceActor =>
        caster is Component casterComponent ? casterComponent.gameObject : null;

    Vector3 IBarrierBlockableProjectile.BarrierSpawnPosition => spawnPos;

    Vector3 IBarrierBlockableProjectile.BarrierTravelDirection => moveDirection;

    float IBarrierBlockableProjectile.GetBarrierImpactDamage()
    {
        if (stats == null)
            return 0f;

        // Crit applies; the barrier has no armor.
        float damage = stats.damage;
        float critChance01 = Mathf.Clamp01(stats.critChance / 100f);
        if (critChance01 > 0f && Random.value < critChance01)
            damage *= Mathf.Max(1f, stats.critMultiplier);

        return damage;
    }

    void IBarrierBlockableProjectile.OnBlockedByBarrier(in BarrierBlockContext context)
    {
        // Consumed by the barrier: no area damage, no single-target damage, no hit VFX.
        Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        // Checked before area damage, single-target damage, and walls.
        if (ProjectileBarrierGate.TryBlock(this, other))
            return;

        if (casterRoot && other.transform.root == casterRoot) return;

        bool didHit = false;

        if (stats != null && stats.areaRadius > 0f && useAreaDamage)
        {
            DoAreaDamage();
            didHit = true;
        }
        else
        {
            var damageable = DamageableResolver.ResolveFrom(other);
            if (damageable != null)
            {
                ApplyDamage(damageable, other);
                didHit = true;
            }
        }

        if (ProjectileLayerUtility.IsWall(other))
            didHit = true;

        if (didHit)
        {
            SpawnHitVfx();
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Applies one hit. <paramref name="hitCollider"/> is the direct collision that produced it, or
    /// null for the area path — a Special Shoot Point is only ever reachable through a direct hit.
    /// </summary>
    void ApplyDamage(IDamageable damageable, Collider hitCollider)
    {
        GameObject attacker = caster is Component casterComponent ? casterComponent.gameObject : null;
        const string damageSourceId = "skill:legacy_projectile";

        if (stats == null)
        {
            damageable.TakeDamage(0f, attacker, damageSourceId);
            return;
        }

        float damage = stats.damage;

        // Armor ลดดาเมจ
        if (damageable is IHasArmor armorHolder)
        {
            float armor = Mathf.Max(armorHolder.Armor, 0f);
            damage *= 100f / (100f + armor);
        }

        // ✅ critChance = 0..100 (%)
        float critChance01 = Mathf.Clamp01(stats.critChance / 100f);
        if (critChance01 > 0f && Random.value < critChance01)
        {
            // ✅ critMultiplier = 2.0 = 200%
            damage *= Mathf.Max(1f, stats.critMultiplier);
        }

        // A player Active Skill projectile that lands directly on a live Special Shoot Point runs
        // through the same one-result contract as a weapon shot: one TakeDamage, whose applied
        // damage is then fed to the point, with the anchor's hit zone honoured.
        using (SpecialShootPointHitScope pointScope = SpecialShootPointHitScope.Begin(
            hitCollider,
            damageable,
            attacker))
        {
            DamageResult result = damageable.TakeDamage(
                damage,
                attacker,
                damageSourceId,
                stagger: new StaggerPayload(stats.staggerPower, 1f, damageSourceId),
                hitZone: pointScope.HitZone);

            pointScope.ApplyPointDamage(result);
        }
    }

    void DoAreaDamage()
    {
        if (stats == null) return;

        var hits = Physics.OverlapSphere(transform.position, stats.areaRadius);
        foreach (var h in hits)
        {
            if (casterRoot && h.transform.root == casterRoot) continue;

            var dmg = DamageableResolver.ResolveFrom(h);
            if (dmg == null) continue;

            // Null collider: area damage must never reach a Special Shoot Point.
            ApplyDamage(dmg, null);
        }
    }

    void SpawnHitVfx()
    {
        if (hitVfxPrefab == null) return;
        if (VfxSpawner.Instance == null) return;

        Vector3 hitPos    = transform.position;
        Vector3 hitNormal = transform.forward;

        VfxSpawner.Instance.SpawnVfx(hitVfxPrefab, hitPos, hitNormal, 1.0f, vfxScale);
    }

    void SpawnBallVfx()
    {
        if (BallVfxPrefab == null) return;

        var vfx = Instantiate(
            BallVfxPrefab,
            transform.position,
            Quaternion.identity,
            transform
        );

        // ถ้าอยากให้สเกลตาม vfxScale จริง ๆ
        if (vfxScale != 1f)
            vfx.transform.localScale *= vfxScale;

        if (vfx.GetComponent<WorldTimeScaledVfx>() == null)
            vfx.AddComponent<WorldTimeScaledVfx>();
    }

    void OnDestroy()
    {
        foreach (var col in ignoredCols)
            if (col && myCol) Physics.IgnoreCollision(myCol, col, false);

        ignoredCols.Clear();
    }
}
