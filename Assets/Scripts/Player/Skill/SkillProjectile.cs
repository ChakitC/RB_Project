using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class SkillProjectile : MonoBehaviour
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

    void OnTriggerEnter(Collider other)
    {
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
                ApplyDamage(damageable);
                didHit = true;
            }
        }

        if (other.CompareTag("Wall"))
            didHit = true;

        if (didHit)
        {
            SpawnHitVfx();
            Destroy(gameObject);
        }
    }

    void ApplyDamage(IDamageable damageable)
    {
        GameObject attacker = caster is Component casterComponent ? casterComponent.gameObject : null;
        const string sourceId = "skill:legacy_projectile";

        if (stats == null)
        {
            damageable.TakeDamage(0f, attacker, sourceId);
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

        damageable.TakeDamage(
            damage,
            attacker,
            sourceId,
            stagger: new StaggerPayload(stats.staggerPower, 1f, sourceId));
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

            ApplyDamage(dmg);
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
