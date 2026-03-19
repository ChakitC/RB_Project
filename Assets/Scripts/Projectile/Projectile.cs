using UnityEngine;
using System.Collections.Generic;
using VHierarchy.Libs;

 //todo funtion destoy opgject ของ parjectile class กับ model ทำงานทับกันอยู่

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Projectile : MonoBehaviour
{
    [Header("Config")]
    public ProjectileConfig config;

    [Header("VFX Spawn")]
    public GameObject ballVfxPrefab;     // ✅ ตอนยิงจะ spawn เป็นลูกของ projectile
    public GameObject hitVfxPrefab;
    public float vfxScale = 1f;

    [Header("Damage (Unified via DamageCalculator)")]
    public WeaponType gunType;
    public float critRate = 0f;          // ✅ ส่งได้ทั้ง 0..1 หรือ 0..100 (Calculator รองรับ)
    public float critMult = 2f;

    [Header("Area Damage (Skill-style)")]
    public bool useAreaDamage = false;
    public float areaRadius = 0f;
    public LayerMask areaDamageMask = ~0;
    public QueryTriggerInteraction areaQuery = QueryTriggerInteraction.Ignore;

    [Header("Default Despawn Rules")]
    public bool despawnOnHitDamageable = true;
    public bool despawnOnHitWall = true;

    public SkillGemDefinition SourceSkillDef { get; private set; }
    public FinalSkillStats SourceSkillStats { get; private set; }
    
    bool _overrideVelThisFrame;
    Vector3 _overrideVel;
    bool _overridePosThisFrame;
    Vector3 _overridePos;

    // === Pending despawn state ===
    bool _requestedDespawnThisHit;
    bool _requestedExpire;
    public bool RequestedDespawn => _requestedDespawnThisHit || _requestedExpire;
    public void RequestDespawn() => _requestedDespawnThisHit = true;
    public void RequestExpire() => _requestedExpire = true;
    public void PreventDespawnThisHit() => _requestedDespawnThisHit = false;

    // modules
    ProjectileContext _ctx;
    IProjectileModuleState[] _states;

    // lifetime
    float _age;

    // physics + collision ignore
    Rigidbody _rb;
    Collider _col;
    readonly List<Collider> _ignoredCols = new List<Collider>();
    Transform _shooterRoot;

    // spawn distance
    Vector3 _spawnPos;

    // prevent multiple AoE explosions
    bool _areaExploded;

    public Vector3 Direction => _ctx.dir;

    public void SetDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude > 0.0001f)
            _ctx.dir = dir.normalized;
    }

    public void OverridePosition(Vector3 pos)
    {
        _overridePosThisFrame = true;
        _overridePos = pos;
    }

    public void RotateYaw(float degrees)
    {
        _ctx.dir = (Quaternion.AngleAxis(degrees, Vector3.up) * _ctx.dir).normalized;
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();

        _col.isTrigger = true;
        _rb.useGravity = false;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void Init(ProjectileConfig cfg, ProjectileContext ctx, bool preserveSkillSource = false)
    {
        if (!preserveSkillSource)
        {
            SourceSkillDef = null;
            SourceSkillStats = null;
        }

        config = cfg;
        _ctx = ctx;

        _ignoredRootIds.Clear();
        _requestedDespawnThisHit = false;
        _requestedExpire = false;
        _areaExploded = false;

        _ctx.dir = (_ctx.dir.sqrMagnitude > 0.0001f) ? _ctx.dir.normalized : transform.forward;

        if (_ctx.stats.speed <= 0f)
            _ctx.stats.speed = (config != null) ? config.baseSpeed : 20f;

        _age = 0f;
        _spawnPos = transform.position;

        SetupIgnoreCollisionFromOwner();
        SetupModules();
        SpawnBallVfx();
    }
    
    public void InitFromSkillDef(
        ProjectileConfig cfg,
        ISkillUser user,
        SkillGemDefinition def,
        FinalSkillStats skillStats,
        Vector3 dir,
        Projectile prefabProjectileForChildren = null
    )
    {
        // mark source
        SourceSkillDef = def;
        SourceSkillStats = skillStats;

        // VFX จาก def เท่านั้น
        hitVfxPrefab  = def != null ? def.SkillVfxhit : null;
        ballVfxPrefab = def != null ? def.BallVfxPrefab : null;
        vfxScale      = def != null ? def.projectileHitVfxScale : 1f;

        // AoE จาก def + stats
        useAreaDamage = (def != null && def.AreaofEffec) && (skillStats != null && skillStats.areaRadius > 0f);
        areaRadius    = (skillStats != null) ? skillStats.areaRadius : 0f;

        // Crit จาก stats (0..100 ส่งเข้า DamageCalculator ได้เลย)
        critRate = (skillStats != null) ? skillStats.critChance : 0f;
        critMult = (skillStats != null) ? skillStats.critMultiplier : 2f;

        // สกิลไม่อยากโดน falloff -> ใช้ Melee (no falloff ใน Calculator ของคุณ)
        gunType = WeaponType.Melee;

        // direction
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        // owner root เพื่อ ignore collision owner
        Transform ownerRoot = null;
        if (user != null)
        {
            if (user.CastOrigin != null) ownerRoot = user.CastOrigin.root;
            else if (user is Component uc) ownerRoot = uc.transform.root;
        }

        var ctx = new ProjectileContext
        {
            owner = ownerRoot,
            dir   = dir,
            stats = new ProjectileStats
            {
                damage = (skillStats != null) ? skillStats.damage : 0f,
                speed  = (def != null) ? def.projectileSpeed : 20f
            },
            projectilePrefab = prefabProjectileForChildren != null ? prefabProjectileForChildren : this
        };

        Init(cfg != null ? cfg : config, ctx, preserveSkillSource: true);
    }

    void SetupIgnoreCollisionFromOwner()
    {
        RestoreIgnoredCollisions();

        if (_ctx.owner == null) return;

        _shooterRoot = _ctx.owner.root;
        if (_shooterRoot == null) return;

        foreach (var c in _shooterRoot.GetComponentsInChildren<Collider>())
        {
            if (c && _col)
            {
                Physics.IgnoreCollision(_col, c, true);
                _ignoredCols.Add(c);
            }
        }
    }

    void SetupModules()
    {
        if (config == null || config.modules == null)
        {
            _states = null;
            return;
        }

        _states = new IProjectileModuleState[config.modules.Count];

        for (int i = 0; i < config.modules.Count; i++)
            _states[i] = config.modules[i] != null ? config.modules[i].CreateState() : null;

        for (int i = 0; i < config.modules.Count; i++)
        {
            var m = config.modules[i];
            if (m != null) m.OnSpawn(this, _ctx, _states[i]);
        }
    }

    void Update()
    {
        Vector3 face = _ctx.dir;
        face.y = 0f;
        if (face.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(face.normalized);
    }

    void FixedUpdate()
    {
        _overrideVelThisFrame = false;
        _overridePosThisFrame = false;

        float dt = Time.fixedDeltaTime;

        _age += dt;
        float lifeTime = (config != null) ? config.lifeTime : 3f;

        if (_age >= lifeTime)
        {
            _requestedExpire = true;
            Despawn(expired: true);
            return;
        }

        if (config != null && _states != null)
        {
            for (int i = 0; i < config.modules.Count; i++)
                config.modules[i]?.Tick(this, _ctx, _states[i], dt);
        }

        if (_requestedExpire)
        {
            Despawn(expired: true);
            return;
        }

        if (_requestedDespawnThisHit)
        {
            Despawn();
            return;
        }

        if (_overridePosThisFrame)
        {
            _rb.MovePosition(_overridePos);
            SetRbVelocity(Vector3.zero);
            return;
        }

        Vector3 vel = _overrideVelThisFrame ? _overrideVel : (_ctx.dir * _ctx.stats.speed);
        SetRbVelocity(vel);
    }

    void OnTriggerEnter(Collider other)
    {
        
        if (_ignoredRootIds.Contains(other.transform.root.GetInstanceID()))
            return;

        if (_shooterRoot && other.transform.root == _shooterRoot) return;
        if (_ctx.owner != null && other.transform.IsChildOf(_ctx.owner)) return;

        var target = other.GetComponentInParent<IDamageable>();
        
        
        // Debug.Log($"[Projectile] Hit: {other.name} root={other.transform.root.name}");
        // Debug.Log($"[Projectile] target={(target==null ? "NULL" : target.GetType().Name)}");
        
        
        
        
        bool hitWall = other.CompareTag("Wall");

        
        bool willExplodeAoE = (useAreaDamage && areaRadius > 0f && !_areaExploded);

        if (target == null && !hitWall && !willExplodeAoE) return;

        // ===== AoE =====
        if (willExplodeAoE)
        {
            _areaExploded = true;
            RequestDespawn();

            ApplyAreaDamage();
            SpawnHitVfx(transform.position, -_ctx.dir);

            var hitInfo = new ProjectileHitInfo(transform.position, -_ctx.dir, other);
            NotifyHit(hitInfo, target);

            if (_requestedExpire)
                Despawn(expired: true);
            else if (_requestedDespawnThisHit)
                Despawn();

            return;
        }

        // ===== Single / Wall =====
        var hit = new ProjectileHitInfo(transform.position, -_ctx.dir, other);

        if (target != null)
        {
            float finalDamage = CalcFinalDamage(target);
            
            SpawnDamageNumber(transform.position, finalDamage);
            SpawnHitVfx(transform.position, -transform.forward);

            var attackerGO = _ctx.owner != null ? _ctx.owner.gameObject : null;

            if (target is IDamageableWithSource d2)
                d2.TakeDamage(finalDamage, attackerGO);
            else
                target.TakeDamage(finalDamage);

            NotifyDamageApplied(hit, target);
            NotifyOwnerCombatTriggers(target);
        }
        else if (hitWall)
        {
            SpawnHitVfx(transform.position, -transform.forward);
        }

        if ((target != null && despawnOnHitDamageable) ||
            (target == null && hitWall && despawnOnHitWall))
        {
            RequestDespawn();
        }

        NotifyHit(hit, target);

        if (_requestedExpire)
            Despawn(expired: true);
        else if (_requestedDespawnThisHit)
            Despawn();
        
        
    }

    float CalcFinalDamage(IDamageable target)
    {
        float armor = 0f;
        if (target is IHasArmor a) armor = a.Armor;

        return DamageCalculator.CalculateFinalDamage(
            gunType,
            DistanceFromSpawn(),
            _ctx.stats.damage,
            critRate,
            critMult,
            armor
        );
    }

    void ApplyAreaDamage()
    {
        var seen = new HashSet<int>();

        var hits = Physics.OverlapSphere(transform.position, areaRadius, areaDamageMask, areaQuery);
        foreach (var h in hits)
        {
            if (!h) continue;

            var root = h.transform.root;
            int rootId = root.GetInstanceID();

            if (_ignoredRootIds.Contains(rootId)) continue;
            if (_shooterRoot && root == _shooterRoot) continue;
            if (_ctx.owner != null && h.transform.IsChildOf(_ctx.owner)) continue;

            var dmg = h.GetComponentInParent<IDamageable>();
            if (dmg == null) continue;

            int key = (dmg is Component c) ? c.transform.root.GetInstanceID() : dmg.GetHashCode();
            if (!seen.Add(key)) continue;

            float finalDamage = CalcFinalDamage(dmg);

            var attackerGO = _ctx.owner != null ? _ctx.owner.gameObject : null;

            if (dmg is IDamageableWithSource d2)
                d2.TakeDamage(finalDamage, attackerGO);
            else
                dmg.TakeDamage(finalDamage);

            Vector3 hitPoint = h.ClosestPoint(transform.position);
            Vector3 hitNormal = (hitPoint - transform.position);
            if (hitNormal.sqrMagnitude <= 0.0001f)
                hitNormal = -_ctx.dir;
            else
                hitNormal.Normalize();

            var hit = new ProjectileHitInfo(hitPoint, hitNormal, h);
            NotifyDamageApplied(hit, dmg);
            NotifyOwnerCombatTriggers(dmg);
        }
    }

    public void NotifyHit(in ProjectileHitInfo hit, IDamageable target)
    {
        if (config == null || _states == null) return;

        for (int i = 0; i < config.modules.Count; i++)
            config.modules[i]?.OnHit(this, _ctx, _states[i], hit, target);
    }

    public void NotifyDamageApplied(in ProjectileHitInfo hit, IDamageable target)
    {
        if (config == null || _states == null || target == null) return;

        for (int i = 0; i < config.modules.Count; i++)
            config.modules[i]?.OnDamageApplied(this, _ctx, _states[i], hit, target);
    }

    void Despawn(bool expired = false)
    {
        if (expired && config != null && _states != null)
        {
            for (int i = 0; i < config.modules.Count; i++)
                config.modules[i]?.OnExpire(this, _ctx, _states[i]);
        }

        gameObject.Destroy();
    }
    
    void SpawnDamageNumber(Vector3 hitpos, float damage)
    {
        
     VfxSpawner.Instance.SpawnDamageNumber(hitpos, damage);
     
    }
    void SpawnHitVfx(Vector3 hitPos, Vector3 hitNormal)
    {
        if (!hitVfxPrefab) return;
        if (VfxSpawner.Instance == null) return;
        
        VfxSpawner.Instance.SpawnVfx(hitVfxPrefab, hitPos, hitNormal, 1.0f, vfxScale);
    }

    void SpawnBallVfx()
    {
        if (!ballVfxPrefab) return;

        var vfx = Instantiate(ballVfxPrefab, transform.position, Quaternion.identity, transform);
        if (vfxScale != 1f)
            vfx.transform.localScale *= vfxScale;
    }

    float DistanceFromSpawn() => Vector3.Distance(_spawnPos, transform.position);

    void OnDisable() => RestoreIgnoredCollisions();
    void OnDestroy() => RestoreIgnoredCollisions();

    void RestoreIgnoredCollisions()
    {
        if (_col == null) return;

        foreach (var c in _ignoredCols)
            if (c) Physics.IgnoreCollision(_col, c, false);

        _ignoredCols.Clear();
    }

    void SetRbVelocity(Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        _rb.linearVelocity = v;
#else
        _rb.velocity = v;
#endif
    }

    void NotifyOwnerCombatTriggers(IDamageable target)
    {
        if (target == null || _ctx.owner == null)
            return;

        var ownerStatusController = _ctx.owner.GetComponent<StatusEffectController>();
        if (ownerStatusController == null)
            return;

        Component targetComponent = target as Component;
        GameObject targetObject = targetComponent != null ? targetComponent.gameObject : null;
        ownerStatusController.NotifyTrigger(EffectTriggerType.OnHit, targetObject);

        if (!target.IsAlive)
            ownerStatusController.NotifyTrigger(EffectTriggerType.OnKill, targetObject);
    }

    public Projectile SpawnChild(ProjectileConfig childCfg, Vector3 pos, Vector3 dir, float dmgMul, float spdMul)
    {
        var prefab = _ctx.projectilePrefab != null ? _ctx.projectilePrefab : this;

        var p = Instantiate(prefab, pos, Quaternion.LookRotation(dir));

        var childCtx = _ctx;
        childCtx.dir = dir.normalized;
        childCtx.stats.damage *= dmgMul;
        childCtx.stats.speed *= spdMul;

        // copy unified params
        p.gunType = gunType;
        p.critRate = critRate;
        p.critMult = critMult;

        p.hitVfxPrefab = hitVfxPrefab;
        p.ballVfxPrefab = ballVfxPrefab;
        p.vfxScale = vfxScale;

        p.useAreaDamage = useAreaDamage;
        p.areaRadius = areaRadius;
        p.areaDamageMask = areaDamageMask;
        p.areaQuery = areaQuery;

        bool preserveSkillSource = SourceSkillDef != null || SourceSkillStats != null;
        if (preserveSkillSource)
        {
            p.SourceSkillDef = SourceSkillDef;
            p.SourceSkillStats = SourceSkillStats;
        }

        p.Init(childCfg != null ? childCfg : p.config, childCtx, preserveSkillSource);
        return p;
    }

    readonly HashSet<int> _ignoredRootIds = new HashSet<int>();

    public void IgnoreRoot(Transform root)
    {
        if (!root) return;
        _ignoredRootIds.Add(root.root.GetInstanceID());
    }

    public void IgnoreTarget(IDamageable target)
    {
        if (target is Component c) IgnoreRoot(c.transform.root);
    }
}
