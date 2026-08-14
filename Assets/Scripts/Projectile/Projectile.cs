using System.Runtime.CompilerServices;
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
    public GameObject ballVfxPrefab;
    public GameObject hitVfxPrefab;
    public GameObject spawnFlashPrefab;
    public float vfxScale = 1f;

    [Header("Damage (Unified via DamageCalculator)")]
    public WeaponType gunType;
    public float critRate = 0f;          // 0..100 (%)
    public float critMult = 1f;          // xN

    [Header("Area Damage (Skill-style)")]
    public bool useAreaDamage = false;
    public float areaRadius = 0f;
    public LayerMask areaDamageMask = ~0;
    public QueryTriggerInteraction areaQuery = QueryTriggerInteraction.Ignore;

    [Header("Default Despawn Rules")]
    public bool despawnOnHitDamageable = true;
    public bool despawnOnHitWall = true;

    public SkillGemDefinition SourceSkillDef { get; private set; }
    public ProjectileSkillPayloadDef SourceSkillExecution { get; private set; }
    public FinalSkillStats SourceSkillStats { get; private set; }
    public Projectile SourcePrefab { get; private set; }
    public CombatAttributionSnapshot Attribution => _attribution;

    ProjectilePool _pool;
    GameObject _ballVfxInstance;

    bool _overrideVelThisFrame;
    Vector3 _overrideVel;
    bool _overridePosThisFrame;
    Vector3 _overridePos;

    // === Pending despawn state ===
    bool _requestedDespawnThisHit;
    bool _requestedExpire;
    bool _isDespawning;
    public bool RequestedDespawn => _requestedDespawnThisHit || _requestedExpire;
    public void RequestDespawn() => _requestedDespawnThisHit = true;
    public void RequestExpire() => _requestedExpire = true;
    public void PreventDespawnThisHit() => _requestedDespawnThisHit = false;
    public void DespawnForRoomTransition() => Despawn();

    // modules
    ProjectileContext _ctx;
    IProjectileModuleState[] _states;
    CombatAttributionSnapshot _attribution;

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
            SourceSkillExecution = null;
            SourceSkillStats = null;
        }

        config = cfg;
        _ctx = ctx;
        _attribution = ctx.attribution.HasPhysicalActor || ctx.attribution.HasCredit
            ? ctx.attribution
            : CombatAttributionSnapshot.FromPhysicalActor(
                ctx.sourceActor != null ? ctx.sourceActor.gameObject : null);

        if (_attribution.HasCredit)
        {
            if (_attribution.PhysicalActor != null)
                _ctx.sourceActor = _attribution.PhysicalActor.transform;
            if (_attribution.CreditedEventBus != null)
                _ctx.combatEventBus = _attribution.CreditedEventBus;
            if (_attribution.CreditedStatusOwner != null)
                _ctx.statusEffectController = _attribution.CreditedStatusOwner;
        }

        _ignoredRootIds.Clear();
        _requestedDespawnThisHit = false;
        _requestedExpire = false;
        _isDespawning = false;
        _areaExploded = false;

        _ctx.dir = (_ctx.dir.sqrMagnitude > 0.0001f) ? _ctx.dir.normalized : transform.forward;

        if (_ctx.stats.speed <= 0f)
            _ctx.stats.speed = (config != null) ? config.baseSpeed : 20f;

        _age = 0f;
        _spawnPos = transform.position;

        SetupIgnoreCollisionFromCollisionRoot();

        if (_ctx.sourceActor != null)
            ProjectileLayerUtility.ApplyForSource(gameObject, _ctx.sourceActor);

        SetupModules();
        SpawnBallVfx();
    }
    
    public void InitFromSkillExecution(
        ProjectileConfig cfg,
        ISkillUser user,
        SkillGemDefinition def,
        ProjectileSkillPayloadDef execution,
        FinalSkillStats skillStats,
        Vector3 dir,
        Projectile prefabProjectileForChildren = null
    )
    {
        // mark source
        SourceSkillDef = def;
        SourceSkillExecution = execution;
        SourceSkillStats = skillStats;

        // VFX are owned by the projectile execution payload.
        hitVfxPrefab  = execution != null ? execution.ProjectileHitVfxPrefab : null;
        ballVfxPrefab = execution != null ? execution.ProjectileTrailVfxPrefab : null;
        vfxScale      = execution != null ? execution.ProjectileHitVfxScale : 1f;

        // AoE จาก def + stats
        useAreaDamage = (def != null && def.AreaofEffec) && (skillStats != null && skillStats.areaRadius > 0f);
        areaRadius    = (skillStats != null) ? skillStats.areaRadius : 0f;

        // Crit จาก stats (0..100 / xN)
        critRate = (skillStats != null) ? skillStats.critChance : 0f;
        critMult = (skillStats != null) ? skillStats.critMultiplier : 2f;

        // สกิลไม่อยากโดน falloff -> ใช้ Melee (no falloff ใน Calculator ของคุณ)
        gunType = WeaponType.Melee;

        // direction
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        // Separate the actor used for source/events from the broader root used for collision ignore.
        Transform sourceActor = null;
        Transform collisionIgnoreRoot = null;
        CombatEventBus ownerCombatEventBus = null;
        StatusEffectController ownerStatusController = null;

        if (user != null)
        {
            if (user is Component uc)
            {
                sourceActor = uc.transform;
                collisionIgnoreRoot = uc.transform.root;
                ownerCombatEventBus = uc.GetComponent<CombatEventBus>();
                ownerStatusController = uc.GetComponent<StatusEffectController>();
                _attribution = CombatAttributionSnapshot.FromPhysicalActor(uc.gameObject);
                if (_attribution.HasCredit)
                {
                    sourceActor = _attribution.PhysicalActor != null
                        ? _attribution.PhysicalActor.transform
                        : sourceActor;
                    ownerCombatEventBus = _attribution.CreditedEventBus;
                    ownerStatusController = _attribution.CreditedStatusOwner;
                }
            }

            if (collisionIgnoreRoot == null && user.CastOrigin != null)
                collisionIgnoreRoot = user.CastOrigin.root;

            if (sourceActor == null && user.CastOrigin != null)
                sourceActor = user.CastOrigin;
        }

        var ctx = new ProjectileContext
        {
            sourceActor = sourceActor,
            collisionIgnoreRoot = collisionIgnoreRoot,
            aimTarget = user != null ? user.AimTransform : null,
            combatEventBus = ownerCombatEventBus,
            statusEffectController = ownerStatusController,
            dir   = dir,
            stats = new ProjectileStats
            {
                damage = (skillStats != null) ? skillStats.damage : 0f,
                speed  = (def != null) ? def.projectileSpeed : 20f,
                staggerPower = (skillStats != null) ? skillStats.staggerPower : 0f
            },
            damageSourceId = def != null ? $"skill:{def.name}" : "skill",
            chainId = CombatEventBus.NextChainId(),
            origin = PassiveEventOrigin.External,
            projectilePrefab = prefabProjectileForChildren != null ? prefabProjectileForChildren : this,
            attribution = _attribution
        };

        Init(cfg != null ? cfg : config, ctx, preserveSkillSource: true);
    }

    void SetupIgnoreCollisionFromCollisionRoot()
    {
        RestoreIgnoredCollisions();

        if (_ctx.collisionIgnoreRoot == null) return;

        _shooterRoot = _ctx.collisionIgnoreRoot.root;
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
        if (_isDespawning)
            return;

        Vector3 face = _ctx.dir;
        if (face.sqrMagnitude > 0.0001f)
        {
            Vector3 up = Mathf.Abs(Vector3.Dot(face.normalized, Vector3.up)) > 0.98f
                ? Vector3.forward
                : Vector3.up;
            transform.rotation = Quaternion.LookRotation(face.normalized, up);
        }
    }

    void FixedUpdate()
    {
        if (_isDespawning)
            return;

        _overrideVelThisFrame = false;
        _overridePosThisFrame = false;

        float worldScale = TimeSlowManager.Instance.WorldTimeScale;
        float dt = Time.fixedDeltaTime * worldScale;

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

        if (!_overridePosThisFrame)
        {
            Vector3 sweepVel = _overrideVelThisFrame
                ? _overrideVel * worldScale
                : (_ctx.dir * _ctx.stats.speed * worldScale);
            float sweepDist = sweepVel.magnitude * Time.fixedDeltaTime;

            if (sweepDist > 0.001f)
            {
                Vector3 sweepDir = sweepVel.normalized;
                if (TrySweepWallHit(sweepDir, sweepDist))
                    return;
            }
        }

        if (_overridePosThisFrame)
        {
            _rb.MovePosition(_overridePos);
            SetRbVelocity(Vector3.zero);
            return;
        }

        Vector3 vel = _overrideVelThisFrame ? _overrideVel * worldScale : (_ctx.dir * _ctx.stats.speed * worldScale);
        SetRbVelocity(vel);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_isDespawning || _requestedExpire || _requestedDespawnThisHit)
            return;

        if (MeleeController.IsCombatOnlyHitbox(other))
            return;

        if (IsFriendlyCollider(other))
            return;
        
        if (_ignoredRootIds.Contains(other.transform.root.GetInstanceID()))
            return;

        if (_shooterRoot && other.transform.root == _shooterRoot) return;

        var target = DamageableResolver.ResolveFrom(other);
        if (!TryResolveHitZoneImpact(other, target, out CharacterHitZone hitZone))
            return;
        
        
        // Debug.Log($"[Projectile] Hit: {other.name} root={other.transform.root.name}");
        // Debug.Log($"[Projectile] target={(target==null ? "NULL" : target.GetType().Name)}");
        
        
        
        
        bool hitWall = ProjectileLayerUtility.IsWall(other);
        bool moduleWantsHitNotification = HasModuleWantsHitNotification(other, target);

        
        bool willExplodeAoE = useAreaDamage &&
                              areaRadius > 0f &&
                              !_areaExploded &&
                              !HasModuleSuppressingBuiltinAreaDamage();

        if (target == null && !hitWall && !willExplodeAoE && !moduleWantsHitNotification) return;

        // ===== AoE =====
        if (willExplodeAoE)
        {
            _areaExploded = true;
            RequestDespawn();

            ApplyAreaDamage();
            SpawnHitVfx(transform.position, -_ctx.dir);
            PlayHitCue(transform.position);

            var hitInfo = new ProjectileHitInfo(transform.position, -_ctx.dir, other);
            NotifyHit(hitInfo, target);

            if (_requestedExpire)
                Despawn(expired: true);
            else if (_requestedDespawnThisHit)
                Despawn();

            return;
        }

        bool suppressDamageableImpact = target != null && HasModuleSuppressingBuiltinDamageableHit(target);

        // ===== Single / Wall =====
        var hit = BuildImpactHitInfo(other);

        if (target != null)
        {
            if (suppressDamageableImpact)
            {
                PlayHitCue(transform.position);
            }
            else
            {
                float finalDamage = CalcFinalDamage(target);
                SpawnHitVfx(transform.position, -transform.forward);
                PlayHitCue(transform.position);
                ApplyResolvedDamage(
                    target,
                    finalDamage,
                    hit,
                    BuildConfiguredKnockback(hit, useRadialDirection: false),
                    showDamageNumber: true,
                    hitZone: hitZone);
            }
        }
        else if (hitWall)
        {
            SpawnHitVfx(transform.position, -transform.forward);
            PlayHitCue(transform.position);
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

    bool TryResolveHitZoneImpact(
        Collider hitCollider,
        IDamageable target,
        out CharacterHitZone hitZone)
    {
        hitZone = CharacterHitZone.None;

        if (!_ctx.useHitZones || target == null)
            return true;

        CharacteContext targetContext = hitCollider.GetComponentInParent<CharacteContext>();
        if (targetContext == null && target is Component targetComponent)
            targetContext = targetComponent.GetComponentInParent<CharacteContext>();

        if (targetContext == null)
            return true;

        targetContext.ResolveReferences();
        CharacterColliderRefs colliderRefs = targetContext.ColliderRefs;
        if (colliderRefs == null || !colliderRefs.HasHitZones)
            return true;

        return colliderRefs.TryResolveHitZone(hitCollider, out hitZone);
    }

    bool HasModuleSuppressingBuiltinAreaDamage()
    {
        if (config == null || config.modules == null || _states == null)
            return false;

        for (int i = 0; i < config.modules.Count; i++)
        {
            var module = config.modules[i];
            if (module != null && module.SuppressBuiltinAreaDamage(this, _ctx, _states[i]))
                return true;
        }

        return false;
    }

    bool HasModuleSuppressingBuiltinDamageableHit(IDamageable target)
    {
        if (target == null || config == null || config.modules == null || _states == null)
            return false;

        for (int i = 0; i < config.modules.Count; i++)
        {
            var module = config.modules[i];
            if (module != null && module.SuppressBuiltinDamageableHit(this, _ctx, _states[i], target))
                return true;
        }

        return false;
    }

    bool HasModuleWantsHitNotification(Collider other, IDamageable target)
    {
        if (config == null || config.modules == null || _states == null)
            return false;

        for (int i = 0; i < config.modules.Count; i++)
        {
            var module = config.modules[i];
            if (module != null && module.WantsHitNotification(this, _ctx, _states[i], other, target))
                return true;
        }

        return false;
    }

    bool _lastDamageWasCritical;

    float CalcFinalDamage(IDamageable target)
    {
        float armor = 0f;
        if (target is IHasArmor a) armor = a.Armor;

        DamageCalculationResult calculation = DamageCalculator.CalculateDamage(
            gunType,
            DistanceFromSpawn(),
            _ctx.stats.damage,
            critRate,
            critMult,
            armor);
        _lastDamageWasCritical = calculation.WasCritical;
        return calculation.Damage;
    }

    void ApplyAreaDamage()
    {
        var seen = new HashSet<int>();

        var hits = Physics.OverlapSphere(transform.position, areaRadius, areaDamageMask, areaQuery);
        foreach (var h in hits)
        {
            if (!h) continue;
            if (MeleeController.IsCombatOnlyHitbox(h)) continue;
            if (IsFriendlyCollider(h)) continue;

            var root = h.transform.root;
            int rootId = root.GetInstanceID();

            if (_ignoredRootIds.Contains(rootId)) continue;
            if (_shooterRoot && root == _shooterRoot) continue;

            var dmg = DamageableResolver.ResolveFrom(h);
            if (dmg == null) continue;

            int key = GetDamageableIdentityKey(dmg);
            if (key == 0)
                key = h.GetInstanceID();

            if (!seen.Add(key)) continue;

            float finalDamage = CalcFinalDamage(dmg);

            Vector3 hitPoint = h.ClosestPoint(transform.position);
            Vector3 hitNormal = (hitPoint - transform.position);
            if (hitNormal.sqrMagnitude <= 0.0001f)
                hitNormal = -_ctx.dir;
            else
                hitNormal.Normalize();

            var hit = new ProjectileHitInfo(hitPoint, hitNormal, h);
            ApplyResolvedDamage(dmg, finalDamage, hit, BuildConfiguredKnockback(hit, useRadialDirection: true));
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
        if (_isDespawning)
            return;

        _isDespawning = true;

        if (_col != null)
            _col.enabled = false;

        if (_rb != null)
        {
            _rb.detectCollisions = false;
            SetRbVelocity(Vector3.zero);
        }

        if (expired && config != null && _states != null)
        {
            for (int i = 0; i < config.modules.Count; i++)
                config.modules[i]?.OnExpire(this, _ctx, _states[i]);
        }

        StopBallVfx();

        if (_pool != null)
            _pool.Return(this);
        else
            gameObject.Destroy();
    }
    
    void SpawnDamageNumber(Vector3 hitpos, float damage, IDamageable target)
    {
        
     VfxSpawner.Instance.SpawnDamageNumber(hitpos, damage, target);
     
    }
    void SpawnHitVfx(Vector3 hitPos, Vector3 hitNormal)
    {
        if (!hitVfxPrefab) return;
        if (VfxSpawner.Instance == null) return;
        
        VfxSpawner.Instance.SpawnVfx(hitVfxPrefab, hitPos, hitNormal, 1.0f, vfxScale);
    }

    void SpawnBallVfx()
    {
        StopBallVfx();
        if (!ballVfxPrefab) return;
        if (VfxSpawner.Instance == null) return;
        _ballVfxInstance = VfxSpawner.Instance.SpawnLoopingVfx(
            ballVfxPrefab, transform.position, Quaternion.identity, transform, vfxScale);
    }

    void StopBallVfx()
    {
        if (_ballVfxInstance == null) return;
        if (VfxSpawner.Instance != null)
            VfxSpawner.Instance.StopLoopingVfx(_ballVfxInstance, allowParticlesToFinish: false);
        _ballVfxInstance = null;
    }

    void PlayHitCue(Vector3 hitPosition)
    {
        if (_ctx.hitCue == null)
            return;

        AudioService.Instance.PlayAtPosition(_ctx.hitCue, hitPosition);
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

    public void PrepareForSpawn(ProjectilePool pool, Projectile sourcePrefab, Vector3 pos, Quaternion rot)
    {
        _pool = pool;
        SourcePrefab = sourcePrefab;
        if (sourcePrefab != null)
            ProjectileLayerUtility.InheritLayer(gameObject, sourcePrefab.gameObject);
        transform.SetParent(null, false);
        // Set rb.position/rotation before SetActive so the interpolation buffer starts
        // from the correct location — otherwise PhysX interpolates from the despawn
        // position for one frame, making the spawn point appear to jitter.
        if (_rb != null)
        {
            _rb.position = pos;
            _rb.rotation = rot;
            SetRbVelocity(Vector3.zero);
            _rb.detectCollisions = true;
        }
        transform.SetPositionAndRotation(pos, rot);
        _isDespawning = false;
        if (_col != null) _col.enabled = true;
        gameObject.SetActive(true);
        if (spawnFlashPrefab != null && VfxSpawner.Instance != null)
            VfxSpawner.Instance.SpawnVfx(spawnFlashPrefab, pos, rot);
    }

    public void MarkPooledSource(ProjectilePool pool, Projectile sourcePrefab)
    {
        _pool = pool;
        SourcePrefab = sourcePrefab;
    }

    void SetRbVelocity(Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        _rb.linearVelocity = v;
#else
        _rb.velocity = v;
#endif
    }

    bool TrySweepWallHit(Vector3 sweepDir, float sweepDist)
    {
        int wallMask = ProjectileLayerUtility.GetWallMask();
        if (wallMask == 0) return false;

        float radius = GetSweepRadius();

        if (!Physics.SphereCast(
            _rb.position, radius, sweepDir, out RaycastHit hit,
            sweepDist, wallMask, QueryTriggerInteraction.Ignore))
            return false;

        Transform hitRoot = hit.transform.root;
        if (_shooterRoot && hitRoot == _shooterRoot) return false;
        if (_ignoredRootIds.Contains(hitRoot.GetInstanceID())) return false;

        Vector3 hitPos = hit.point;
        Vector3 hitNormal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal : -sweepDir;

        bool willExplodeAoE = useAreaDamage &&
                              areaRadius > 0f &&
                              !_areaExploded &&
                              !HasModuleSuppressingBuiltinAreaDamage();

        if (willExplodeAoE)
        {
            _areaExploded = true;
            RequestDespawn();
            transform.position = hitPos;
            ApplyAreaDamage();
        }
        else if (despawnOnHitWall)
        {
            RequestDespawn();
        }

        SpawnHitVfx(hitPos, hitNormal);
        PlayHitCue(hitPos);

        var hitInfo = new ProjectileHitInfo(hitPos, hitNormal, hit.collider);
        NotifyHit(hitInfo, null);

        if (_requestedExpire)
            Despawn(expired: true);
        else if (_requestedDespawnThisHit)
            Despawn();

        return true;
    }

    float GetSweepRadius()
    {
        if (_col is SphereCollider sphere)
        {
            Vector3 s = transform.lossyScale;
            return sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }
        if (_col is CapsuleCollider capsule)
        {
            Vector3 s = transform.lossyScale;
            return capsule.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }
        if (_col is BoxCollider box)
        {
            Vector3 half = Vector3.Scale(box.size * 0.5f, transform.lossyScale);
            return Mathf.Max(0.01f, Mathf.Min(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z)));
        }
        Vector3 e = _col.bounds.extents;
        return Mathf.Max(0.01f, Mathf.Min(e.x, e.y, e.z));
    }

    void NotifyOwnerCombatTriggers(IDamageable target, in DamageResult result, CharacterHitZone hitZone)
    {
        if (target == null || !result.WasAliveBefore || !result.Applied)
            return;

        var ownerStatusController = ResolveOwnerStatusEffectController();
        Component targetComponent = target as Component;
        GameObject targetObject = targetComponent != null ? targetComponent.gameObject : null;
        ownerStatusController?.NotifyTrigger(EffectTriggerType.OnHit, targetObject);

        var ownerEventBus = ResolveOwnerCombatEventBus();
        var shot = _ctx.combatMetadata;
        var metadata = new CombatEventMetadata(
            result.RequestedDamage, result.ResolvedDamage, result.AppliedDamage,
            result.HealthBeforeHit, result.MaxHealth, _lastDamageWasCritical, hitZone,
            result.StaggerApplied, result.EnteredChainReady, shot.WeaponInstanceId,
            shot.AmmoBefore, shot.AmmoAfter, shot.MaxMagazine, shot.AmmoConsumed,
            shot.IsLastRound, shot.SourceKind, shot.WeaponAffixId);
        if (ownerEventBus != null)
        {
            var hitContext = CreateOwnerEventContext(ownerEventBus, PassiveEventType.Hit, targetObject, result.AppliedDamage, metadata);
            ownerEventBus.Publish(hitContext);
        }

        if (result.Killed)
        {
            ownerStatusController?.NotifyTrigger(EffectTriggerType.OnKill, targetObject);

            if (ownerEventBus != null)
            {
                var killContext = CreateOwnerEventContext(ownerEventBus, PassiveEventType.Kill, targetObject, result.AppliedDamage, metadata);
                ownerEventBus.Publish(killContext);
            }
        }
    }

    public Projectile SpawnChild(ProjectileConfig childCfg, Vector3 pos, Vector3 dir, float dmgMul, float spdMul)
    {
        var prefab = _ctx.projectilePrefab != null ? _ctx.projectilePrefab : this;

        var spawnPool = ProjectilePool.Instance;
        var p = spawnPool != null
            ? spawnPool.Get(prefab, pos, Quaternion.LookRotation(dir))
            : Instantiate(prefab, pos, Quaternion.LookRotation(dir));
        if (p == null) return null;
        ProjectileLayerUtility.InheritLayer(p.gameObject, gameObject);

        var childCtx = _ctx;
        childCtx.dir = dir.normalized;
        childCtx.stats.damage *= dmgMul;
        childCtx.stats.staggerPower *= dmgMul;
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
            p.SourceSkillExecution = SourceSkillExecution;
            p.SourceSkillStats = SourceSkillStats;
        }

        p.Init(childCfg != null ? childCfg : p.config, childCtx, preserveSkillSource);
        return p;
    }

    readonly HashSet<int> _ignoredRootIds = new HashSet<int>();

    // Deduplicate AoE hits per resolved damageable instead of transform.root so grouped actors
    // under a shared squad root can still each receive the explosion once.
    public static int GetDamageableIdentityKey(IDamageable damageable)
    {
        if (damageable is Component component)
            return component.GetInstanceID();

        return damageable != null ? RuntimeHelpers.GetHashCode(damageable) : 0;
    }

    public void IgnoreRoot(Transform root)
    {
        if (!root) return;
        _ignoredRootIds.Add(root.root.GetInstanceID());
    }

    public void IgnoreTarget(IDamageable target)
    {
        if (target is Component c) IgnoreRoot(c.transform.root);
    }

    bool IsFriendlyCollider(Collider other)
    {
        if (other == null || _ctx.sourceActor == null)
            return false;

        CharacteContext sourceContext = _ctx.sourceActor.GetComponentInParent<CharacteContext>();
        CharacteContext targetContext = other.GetComponentInParent<CharacteContext>();
        if (sourceContext == null || targetContext == null)
            return false;

        bool sourceFriendly =
            sourceContext.TargetIdentity == AITargetIdentity.Player ||
            sourceContext.TargetIdentity == AITargetIdentity.Companion;
        bool targetFriendly =
            targetContext.TargetIdentity == AITargetIdentity.Player ||
            targetContext.TargetIdentity == AITargetIdentity.Companion;

        if (sourceFriendly && targetFriendly)
            return true;

        return sourceContext.TargetIdentity == AITargetIdentity.Enemy &&
               targetContext.TargetIdentity == AITargetIdentity.Enemy;
    }

    KnockbackData BuildConfiguredKnockback(in ProjectileHitInfo hit, bool useRadialDirection)
    {
        if (config == null || !config.applyKnockbackOnHit)
            return default(KnockbackData);

        KnockbackSettings settings = config.ToKnockbackSettings();
        KnockbackBuildContext context = BuildConfiguredKnockbackContext(hit, useRadialDirection);

        return KnockbackFactory.TryBuild(in settings, in context, out KnockbackData knockback)
            ? knockback
            : default;
    }

    KnockbackBuildContext BuildConfiguredKnockbackContext(in ProjectileHitInfo hit, bool useRadialDirection)
    {
        Vector3 fallbackDirection = ResolveConfiguredKnockbackFallbackDirection();
        Vector3 impactPoint = hit.ResolvePoint(transform.position);

        if (useRadialDirection)
        {
            return new KnockbackBuildContext(
                transform.position,
                impactPoint,
                fallbackDirection);
        }

        return new KnockbackBuildContext(
            impactPoint,
            impactPoint,
            fallbackDirection,
            explicitDirection: ResolveConfiguredImpactDirection(hit, fallbackDirection));
    }

    Vector3 ResolveConfiguredImpactDirection(in ProjectileHitInfo hit, Vector3 fallbackDirection)
    {
        Vector3 impactDirection = -hit.normal;
        if (impactDirection.sqrMagnitude > 0.0001f)
            return impactDirection;

        if (_ctx.dir.sqrMagnitude > 0.0001f)
            return _ctx.dir;

        return fallbackDirection;
    }

    ProjectileHitInfo BuildImpactHitInfo(Collider other)
    {
        Vector3 impactNormal = -_ctx.dir;
        if (TryResolveImpactPoint(other, out Vector3 point))
            return new ProjectileHitInfo(point, impactNormal, other);

        return ProjectileHitInfo.WithoutPoint(impactNormal, other);
    }

    bool TryResolveImpactPoint(Collider other, out Vector3 point)
    {
        if (other != null)
        {
            point = other.ClosestPoint(transform.position);
            return true;
        }

        point = default;
        return false;
    }

    Vector3 ResolveConfiguredKnockbackFallbackDirection()
    {
        if (_ctx.dir.sqrMagnitude > 0.0001f)
            return _ctx.dir;

        if (transform.forward.sqrMagnitude > 0.0001f)
            return transform.forward;

        return Vector3.forward;
    }

    DamageResult ApplyDamageToTarget(
        IDamageable target,
        float finalDamage,
        GameObject attackerGO,
        KnockbackData knockback,
        CharacterHitZone hitZone)
    {
        var build = new WeaponDamageBuildContext
        {
            WeaponInstanceId = _ctx.combatMetadata.WeaponInstanceId,
            Target = (target as Component) != null ? ((Component)target).gameObject : null,
            AttackId = _ctx.attackId,
            Damage = finalDamage,
            StaggerPower = _ctx.stats.staggerPower
        };
        _ctx.preDamageRuntime?.ModifyDamage(ref build);
        StaggerPayload configuredStagger = BuildStaggerPayload();
        var damageContext = new DamageContext(
            build.Damage,
            attackerGO,
            _ctx.damageSourceId,
            _ctx.attackId,
            _ctx.chainId == 0 ? CombatEventBus.NextChainId() : _ctx.chainId,
            _ctx.depth + 1,
            _ctx.origin,
            _ctx.originPassiveId,
            _ctx.originRuleId,
            knockback,
            new StaggerPayload(build.StaggerPower, configuredStagger.Multiplier, _ctx.damageSourceId),
            hitZone,
            _attribution);

        return target.TakeDamage(in damageContext);
    }

    StaggerPayload BuildStaggerPayload()
    {
        if (config != null)
            return config.ToStaggerPayload(_ctx.stats.staggerPower, _ctx.damageSourceId);

        return new StaggerPayload(_ctx.stats.staggerPower, 1f, _ctx.damageSourceId);
    }

    public void ApplyResolvedDamage(IDamageable target, float finalDamage, in ProjectileHitInfo hit, bool showDamageNumber = false)
    {
        ApplyResolvedDamage(
            target,
            finalDamage,
            hit,
            BuildConfiguredKnockback(hit, useRadialDirection: false),
            showDamageNumber,
            CharacterHitZone.None);
    }

    public void ApplyResolvedDamage(
        IDamageable target,
        float finalDamage,
        in ProjectileHitInfo hit,
        KnockbackData knockback,
        bool showDamageNumber = false)
    {
        ApplyResolvedDamage(
            target,
            finalDamage,
            hit,
            knockback,
            showDamageNumber,
            CharacterHitZone.None);
    }

    void ApplyResolvedDamage(
        IDamageable target,
        float finalDamage,
        in ProjectileHitInfo hit,
        KnockbackData knockback,
        bool showDamageNumber,
        CharacterHitZone hitZone)
    {
        if (target == null || finalDamage <= 0f)
            return;

        bool wasAliveBeforeDamage = target.IsAlive;
        if (!wasAliveBeforeDamage)
            return;

        var attackerGO = ResolveSourceObject();
        DamageResult result = ApplyDamageToTarget(target, finalDamage, attackerGO, knockback, hitZone);
        if (!result.Applied)
            return;

        if (showDamageNumber)
            SpawnDamageNumber(hit.ResolvePoint(transform.position), result.AppliedDamage, target);

        NotifyDamageApplied(hit, target);
        NotifyOwnerCombatTriggers(target, result, hitZone);
        if (_ctx.affixImpactPayload.IsValid)
        {
            WeaponAffixAreaDamage.Apply(
                _ctx.sourceActor != null ? _ctx.sourceActor.GetComponentInParent<CharacteContext>() : null,
                ResolveOwnerCombatEventBus(),
                hit.ResolvePoint(transform.position),
                _ctx.affixImpactPayload.Radius,
                _ctx.affixImpactPayload.Damage,
                _ctx.combatMetadata.WeaponInstanceId,
                _ctx.affixImpactPayload.AffixId,
                _ctx.attackId,
                _ctx.chainId,
                _ctx.depth);
            _ctx.affixImpactPayload = default;
        }
    }

    PassiveEventContext CreateOwnerEventContext(
        CombatEventBus ownerEventBus,
        PassiveEventType type,
        GameObject targetObject,
        float value,
        in CombatEventMetadata metadata)
    {
        GameObject sourceObject = ResolveSourceObject();

        if (_ctx.chainId != 0)
        {
            var parent = new PassiveEventContext(
                PassiveEventType.None,
                sourceObject,
                sourceObject,
                targetObject,
                _ctx.damageSourceId,
                _ctx.attackId,
                value,
                Time.timeAsDouble,
                _ctx.chainId,
                _ctx.depth,
                _ctx.origin,
                _ctx.originPassiveId,
                _ctx.originRuleId,
                metadata);

            return ownerEventBus.CreateChildContext(
                parent,
                type,
                sourceObject,
                targetObject,
                _ctx.damageSourceId,
                _ctx.attackId,
                value,
                _ctx.origin,
                _ctx.originPassiveId,
                _ctx.originRuleId,
                metadata,
                _attribution.CreditedActor);
        }

        return ownerEventBus.CreateExternalContext(
            type,
            sourceObject,
            targetObject,
            _ctx.damageSourceId,
            _ctx.attackId,
            value,
            _ctx.origin,
            _ctx.originPassiveId,
            _ctx.originRuleId,
            metadata,
            _attribution.CreditedActor);
    }

    GameObject ResolveSourceObject()
    {
        return _ctx.sourceActor != null ? _ctx.sourceActor.gameObject : gameObject;
    }

    CombatEventBus ResolveOwnerCombatEventBus()
    {
        if (_ctx.combatEventBus != null)
            return _ctx.combatEventBus;

        if (_attribution.CreditedEventBus != null)
            return _attribution.CreditedEventBus;

        return _ctx.sourceActor != null ? _ctx.sourceActor.GetComponent<CombatEventBus>() : null;
    }

    StatusEffectController ResolveOwnerStatusEffectController()
    {
        if (_ctx.statusEffectController != null)
            return _ctx.statusEffectController;

        if (_attribution.CreditedStatusOwner != null)
            return _attribution.CreditedStatusOwner;

        return _ctx.sourceActor != null ? _ctx.sourceActor.GetComponent<StatusEffectController>() : null;
    }
}
