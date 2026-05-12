using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(WeaponAffixRuntimeController))]
[RequireComponent(typeof(WeaponUpgradeRuntimeController))]
public class WeaponSystem : MonoBehaviour
{
    const string DefaultFirePointName = "FirePoint";

    [Header("Refs")]
    public CharacteContext ctx;
    [SerializeField] private StatsHub statsHub;
    [SerializeField] private StatusEffectController statusEffectController;
    [SerializeField] private CombatEventBus combatEventBus;
    [SerializeField] private WeaponAffixRuntimeController affixRuntimeController;

    public ProjectileConfig projectileConfig;
    public GameObject projectilePrefab;

    [SerializeField] private GunConfig currentWeapon;
    [SerializeField] private WeaponInstanceData currentWeaponInstance;

    public GunConfig CurrentWeapon => currentWeapon;
    public WeaponInstanceData CurrentWeaponInstance => currentWeaponInstance;

    [Header("WeaponStats (debug/inspector)")]
    public Transform firePoint;
    public WeaponType gunType;
    public float damage = 0f;
    public float fireRate = 0f;
    public int magazine = 0;
    public int maxMagazine = 0;
    public float reloadTime = 0f;
    public float critRate = 0f;
    public float critMultiplier = 1f;
    public bool magazineRelode = false;
    public float stability = 0f;
    public bool autoloader;
    public float bulletSpeed = 20f;
    public float staggerPower = 0f;

    public bool isAiming = false;

    public bool IsReloading => isReloading;
    public bool IsFiringHeld => _isFiringHeld;
    public bool CanFire => !isReloading && magazine > 0 && GetWeaponTime() >= nextFireTime;
    public int CurrentAmmo => magazine;
    public int MagazineSize => MaxMagazine;
    public bool IsMagazineEmpty => magazine <= 0;

    bool _isFiringHeld;
    readonly Dictionary<GameObject, Projectile> _projectilePrefabCache = new();

    float baseSwaySpeed = 0f;
    float baseMaxSwayAngle = 0f;
    float baseReturnSpeed = 0f;
    float swaySpeed;
    float maxSwayAngle;
    float returnSpeed;

    float swayTimer = 0f;
    Quaternion firePointOriginalRot;

    bool reloadPerBullet = true;
    float startInsertDelay = 0f;
    float perBulletInsertTime = 0f;
    float endInsertDelay = 0f;
    bool shootInterruptsReload = true;
    Coroutine reloadRoutine;
    AudioHandle reloadAudioHandle;

    [Header("Burst Settings")]
    public int burstCount = 0;
    public float burstInterval = 0f;

    Coroutine burstCo;

    public bool IsBursting => isBursting;
    public bool IsFiringActivity =>
        (firingMode == FiringMode.Auto && isFiring) ||
        (firingMode == FiringMode.Burst && isBursting);

    public bool isFiring;

    float nextFireTime;
    bool isReloading;
    bool isBursting;

    FiringMode firingMode = FiringMode.Auto;
    readonly List<IWeaponRuntimeEffectHandler> runtimeEffectHandlers = new();

    void Awake()
    {
        ResolveReferences();

        if (!currentWeapon && ctx) currentWeapon = ctx.currentWeapon;

        RefreshFirePointReference(logIfMissing: true);
    }

    void ResolveReferences()
    {
        if (!ctx)
            ctx = GetComponent<CharacteContext>();

        if (!ctx)
            ctx = GetComponentInParent<CharacteContext>();

        ctx?.ResolveReferences();

        Transform ownerRoot = ctx ? ctx.transform : transform.root;

        if (!statsHub)
            statsHub = ctx != null ? ctx.StatsHub : null;

        if (!statsHub)
            statsHub = GetComponent<StatsHub>();

        if (!statsHub && ctx)
            statsHub = ctx.StatsHub;

        if (!statsHub && ownerRoot)
            statsHub = ownerRoot.GetComponentInChildren<StatsHub>(true);

        if (!statusEffectController)
            statusEffectController = GetComponent<StatusEffectController>();

        if (!statusEffectController && ownerRoot)
            statusEffectController = ownerRoot.GetComponentInChildren<StatusEffectController>(true);

        if (!combatEventBus)
            combatEventBus = GetComponent<CombatEventBus>();

        if (!combatEventBus && ctx)
            combatEventBus = ctx.CombatEventBus;

        if (!combatEventBus && ownerRoot)
            combatEventBus = ownerRoot.GetComponentInChildren<CombatEventBus>(true);

        if (!affixRuntimeController)
            affixRuntimeController = GetComponent<WeaponAffixRuntimeController>();

        if (Application.isPlaying && GetComponentInChildren<WeaponUpgradeRuntimeController>(true) == null)
            gameObject.AddComponent<WeaponUpgradeRuntimeController>();

        if (ctx != null && ctx.WeaponSystem != this)
            ctx.WeaponSystem = this;

        RebuildRuntimeEffectHandlers();
    }

    public bool RefreshFirePointReference(bool logIfMissing = false)
    {
        ResolveReferences();

        if (!firePoint)
        {
            Transform ownerRoot = ctx ? ctx.transform : transform;
            firePoint = FindChildByName(ownerRoot, DefaultFirePointName);
        }

        if (firePoint)
        {
            firePointOriginalRot = firePoint.localRotation;
            swayTimer = 0f;
            return true;
        }

        if (logIfMissing)
            Debug.LogError("WeaponSystem: firePoint is missing.", this);

        return false;
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (!root)
            return null;

        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildByName(root.GetChild(i), targetName);
            if (found)
                return found;
        }

        return null;
    }

    void Start()
    {
        Equip(currentWeapon, currentWeaponInstance);
        ctx.UIManager?.UpdateAmmoText(magazine, MaxMagazine);
    }

    public void Equip(GunConfig weapon)
    {
        Equip(weapon, currentWeaponInstance);
    }

    public void Equip(GunConfig weapon, WeaponInstanceData instance)
    {
        var previousWeapon = currentWeapon;
        currentWeapon = weapon;
        currentWeaponInstance = instance;

        if (ctx != null)
            ctx.currentWeapon = currentWeapon;

        if (!currentWeapon)
            return;

        gunType = currentWeapon.WeaponType;
        firingMode = currentWeapon.firingModes;

        reloadTime = currentWeapon.reloadTime;
        magazineRelode = currentWeapon.magazineReload;
        autoloader = currentWeapon.autoloader;

        reloadPerBullet = currentWeapon.reloadPerBullet;
        startInsertDelay = currentWeapon.startInsertDelay;
        perBulletInsertTime = currentWeapon.perBulletInsertTime;
        endInsertDelay = currentWeapon.endInsertDelay;
        shootInterruptsReload = currentWeapon.shootInterruptsReload;

        baseSwaySpeed = currentWeapon.baseSwaySpeed;
        baseMaxSwayAngle = currentWeapon.baseMaxSwayAngle;
        baseReturnSpeed = currentWeapon.baseReturnSpeed;

        burstCount = currentWeapon.burstCount;
        burstInterval = currentWeapon.burstInterval;

        projectilePrefab = currentWeapon.BulletPrefab;
        TryResolveProjectilePrefab(projectilePrefab, out _);

        magazine = ResolveStartingMagazine();
        RefreshDerivedStats();
        magazine = Mathf.Clamp(magazine, 0, MaxMagazine);
        SyncWeaponInstanceState();

        NotifyRuntimeEffectHandlersWeaponEquipped();
        statsHub?.MarkDirty();
        RefreshWeaponVisual();

        if (currentWeapon != null && currentWeapon != previousWeapon)
            PlayWeaponCue(currentWeapon.equipCue, true);
    }

    void RefreshWeaponVisual()
    {
        ResolveReferences();

        CharacterVisualController visual = ctx != null ? ctx.Visual : null;

        if (!visual && ctx != null)
        {
            visual = ctx.GetComponentInChildren<CharacterVisualController>(true);
            if (visual != null)
                ctx.Visual = visual;
        }

        if (!visual)
            visual = GetComponent<CharacterVisualController>();

        if (!visual)
            visual = GetComponentInParent<CharacterVisualController>();

        if (!visual)
        {
            Transform ownerRoot = ctx ? ctx.transform : transform.root;
            if (ownerRoot)
                visual = ownerRoot.GetComponentInChildren<CharacterVisualController>(true);
        }

        if (ctx != null && visual != null && ctx.Visual != visual)
            ctx.Visual = visual;

        visual?.BuildModelFromWeaponDef();
    }

    void OnDisable()
    {
        StopAllCoroutines();
        isFiring = false;
        isBursting = false;
        isReloading = false;
        _isFiringHeld = false;
        reloadRoutine = null;
        StopReloadCue();
        SyncWeaponInstanceState();
    }

    int MaxMagazine =>
        statsHub ? statsHub.GetMaxMagazine(currentWeapon) :
        (currentWeapon ? currentWeapon.maxMagazine : 0);

    float FireInterval =>
        statsHub ? statsHub.GetFireInterval(currentWeapon) :
        (currentWeapon ? currentWeapon.fireRate : 0f);

    float FinalDamage =>
        statsHub ? statsHub.GetDamage(currentWeapon) :
        (currentWeapon ? currentWeapon.damage : 0f) + (ctx ? ctx.baseDamage : 0f);

    float FinalCritRate =>
        statsHub ? statsHub.GetCritRatePercent(currentWeapon) :
        (currentWeapon ? currentWeapon.critRate : 0f) + (ctx ? ctx.basecritRate : 0f);

    float FinalCritMult =>
        statsHub ? statsHub.GetCritMultiplier(currentWeapon) :
        (currentWeapon ? Mathf.Max(1f, currentWeapon.critMultiplier) : 1f) +
        (ctx ? Mathf.Max(1f, ctx.basecritMultiplier) - 1f : 0f);

    float FinalStability =>
        statsHub ? statsHub.GetStability(currentWeapon) :
        (currentWeapon ? currentWeapon.stability : 0f);

    float FinalBulletSpeed =>
        statsHub ? statsHub.GetBulletSpeed(currentWeapon) :
        (currentWeapon ? currentWeapon.BulletSpeed : 0f);

    float FinalReloadTime =>
        statsHub ? statsHub.GetReloadTime(currentWeapon) :
        (currentWeapon ? currentWeapon.reloadTime : 0f);

    public float GetReloadAnimDuration()
    {
        if (reloadPerBullet)
        {
            int missing = Mathf.Max(0, MaxMagazine - magazine);
            return startInsertDelay + (missing * perBulletInsertTime) + endInsertDelay;
        }

        return reloadTime;
    }

    void RefreshDerivedStats()
    {
        if (!currentWeapon)
            return;

        damage = FinalDamage;
        fireRate = FireInterval;
        critRate = FinalCritRate;
        critMultiplier = FinalCritMult;
        stability = FinalStability;
        bulletSpeed = FinalBulletSpeed;
        staggerPower = currentWeapon ? Mathf.Max(0f, currentWeapon.staggerPower) : 0f;
        reloadTime = FinalReloadTime;
        maxMagazine = MaxMagazine;

        float k = 0.1f;
        float stabilityFactor = 1f / (1f + stability * k);

        swaySpeed = baseSwaySpeed * stabilityFactor;
        maxSwayAngle = baseMaxSwayAngle * stabilityFactor;
        returnSpeed = baseReturnSpeed * (1f + stability * k);
    }

    public void SetFiring(bool value)
    {
        _isFiringHeld = value;

        if (!value)
        {
            isFiring = false;
            if (isBursting)
                isBursting = false;
            return;
        }

        if (!ctx.stateHub.CanShoot())
            return;

        if (isReloading && shootInterruptsReload)
        {
            CancelReload();
        }

        switch (firingMode)
        {
            case FiringMode.Burst:
                if (isReloading || isBursting)
                    return;

                isBursting = true;
                isFiring = true;
                burstCo = StartCoroutine(FireBurst());
                break;

            case FiringMode.Semi:
                TryShoot();
                isFiring = false;
                break;

            case FiringMode.Auto:
                isFiring = true;
                break;
        }
    }

    void Update()
    {
        RefreshDerivedStats();

        if (firingMode == FiringMode.Auto && isFiring && GetWeaponTime() >= nextFireTime)
            TryShoot();

        if (!firePoint)
            return;

        if (isFiring && (firingMode == FiringMode.Auto || firingMode == FiringMode.Burst))
        {
            swayTimer += GetWeaponDeltaTime() * swaySpeed;
            float angle = Mathf.Sin(swayTimer) * maxSwayAngle;
            firePoint.localRotation = firePointOriginalRot * Quaternion.Euler(0f, angle, 0f);
            return;
        }

        swayTimer = Mathf.MoveTowards(swayTimer, 0f, GetWeaponDeltaTime() * swaySpeed);
        firePoint.localRotation = Quaternion.Lerp(
            firePoint.localRotation,
            firePointOriginalRot,
            GetWeaponDeltaTime() * returnSpeed);
    }

    public void TryShoot()
    {
        RefreshDerivedStats();

        if (isReloading || GetWeaponTime() < nextFireTime)
            return;

        if (magazine <= 0)
        {
            if (autoloader)
                TryReload();
            else
                PlayWeaponCue(currentWeapon != null ? currentWeapon.emptyCue : null, false);
            return;
        }

        if (!projectilePrefab || !firePoint)
            return;

        nextFireTime = GetWeaponTime() + fireRate;

        magazine--;
        SyncWeaponInstanceState();
        ctx.UIManager?.UpdateAmmoText(magazine, MaxMagazine);

        string weaponSourceId = GetWeaponSourceId();
        string attackId = combatEventBus != null ? combatEventBus.CreateAttackId(weaponSourceId) : null;
        PassiveEventContext shotContext = CreateShotContext(weaponSourceId, attackId);

        SpawnProjectile(
            projectileConfig,
            projectilePrefab,
            damage,
            bulletSpeed,
            weaponSourceId,
            attackId,
            shotContext);

        PlayWeaponCue(currentWeapon != null ? currentWeapon.fireCue : null, false);

        ctx.stateHub.ReportShotFired();
        if (combatEventBus != null)
            combatEventBus.Publish(shotContext);
        statusEffectController?.NotifyTrigger(EffectTriggerType.OnShotFired, gameObject);
        NotifyRuntimeEffectHandlersShotFired();

        if (magazine <= 0 && autoloader)
            TryReload();
    }

    public bool SpawnAffixProjectile(
        ProjectileConfig configOverride,
        GameObject prefabOverride,
        float damageMultiplier = 1f,
        float speedMultiplier = 1f)
    {
        if (!currentWeapon || !firePoint)
            return false;

        var projectileToSpawn = prefabOverride ? prefabOverride : projectilePrefab;
        var configToUse = configOverride ? configOverride : projectileConfig;

        if (!projectileToSpawn || !configToUse)
            return false;

        string weaponSourceId = GetWeaponSourceId();
        string attackId = combatEventBus != null ? combatEventBus.CreateAttackId($"{weaponSourceId}:affix") : null;

        SpawnProjectile(
            configToUse,
            projectileToSpawn,
            damage * Mathf.Max(0f, damageMultiplier),
            bulletSpeed * Mathf.Max(0f, speedMultiplier),
            weaponSourceId,
            attackId,
            default);

        return true;
    }

    public void CancelReload()
    {
        if (!isReloading)
            return;

        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }

        isReloading = false;
        isBursting = false;
        StopReloadCue();
        ctx?.AnimBrain?.StopReloadAction();

        if (burstCo != null)
        {
            StopCoroutine(burstCo);
            burstCo = null;
        }
    }

    bool IsReloadBlockedByActorState()
    {
        var stateHub = ctx != null ? ctx.stateHub : null;
        if (stateHub != null)
        {
            if (!stateHub.IsAlive || stateHub.Isdown)
                return true;

            if (stateHub.MoveSM != null && stateHub.MoveSM.CurrentId == MoveStateId.Knockback)
                return true;
        }

        var knockbackMotor = ctx != null ? ctx.KnockbackMotor : null;
        if (!knockbackMotor && ctx != null)
            knockbackMotor = ctx.GetComponentInChildren<CharacterKnockbackMotor>(true);
        if (!knockbackMotor)
            knockbackMotor = GetComponentInParent<CharacterKnockbackMotor>();
        if (!knockbackMotor)
            knockbackMotor = GetComponentInChildren<CharacterKnockbackMotor>(true);

        return knockbackMotor != null && knockbackMotor.IsActive;
    }

    void AbortReloadRoutine()
    {
        isReloading = false;
        reloadRoutine = null;
        isBursting = false;
        StopReloadCue();
        SyncWeaponInstanceState();
    }

    public void TryReload()
    {
        RefreshDerivedStats();

        if (IsReloadBlockedByActorState())
            return;

        isFiring = false;
        isBursting = false;

        if (isReloading || magazine >= MaxMagazine)
            return;

        if (reloadRoutine != null)
            StopCoroutine(reloadRoutine);

        if (reloadPerBullet)
        {
            reloadRoutine = StartCoroutine(ReloadPerBulletRoutine());
            PlayReloadCue();
        }
        else if (magazineRelode)
        {
            reloadRoutine = StartCoroutine(ReloadFullMagRoutine());
            PlayReloadCue();
        }
        else
        {
            Debug.LogWarning("[WeaponSystem] TryReload called but no reload mode is enabled.", this);
        }
    }

    public bool CanRestoreMagazine(int amount, bool fillToMax = false)
    {
        RefreshDerivedStats();

        if (!currentWeapon || MaxMagazine <= 0)
            return false;

        if (fillToMax)
            return magazine < MaxMagazine;

        return amount > 0 && magazine < MaxMagazine;
    }

    public bool RestoreMagazine(int amount, bool fillToMax = false)
    {
        if (!CanRestoreMagazine(amount, fillToMax))
            return false;

        if (isReloading)
            CancelReload();

        int previous = magazine;
        int amountToRestore = fillToMax ? MaxMagazine - magazine : Mathf.Max(0, amount);
        magazine = Mathf.Clamp(magazine + amountToRestore, 0, MaxMagazine);

        if (magazine <= previous)
            return false;

        SyncWeaponInstanceState();
        ctx.UIManager?.UpdateAmmoText(magazine, MaxMagazine);
        return true;
    }

    IEnumerator ReloadPerBulletRoutine()
    {
        isReloading = true;

        if (IsReloadBlockedByActorState())
        {
            AbortReloadRoutine();
            yield break;
        }

        if (startInsertDelay > 0f)
            yield return WaitForWeaponSeconds(startInsertDelay);

        if (IsReloadBlockedByActorState())
        {
            AbortReloadRoutine();
            yield break;
        }

        while (magazine < MaxMagazine)
        {
            if (IsReloadBlockedByActorState())
            {
                AbortReloadRoutine();
                yield break;
            }

            if (shootInterruptsReload && (isFiring || isBursting))
                break;

            magazine++;
            SyncWeaponInstanceState();
            ctx.UIManager?.UpdateAmmoText(magazine, MaxMagazine);

            if (perBulletInsertTime > 0f)
                yield return WaitForWeaponSeconds(perBulletInsertTime);
            else
                yield return null;
        }

        if (endInsertDelay > 0f)
            yield return WaitForWeaponSeconds(endInsertDelay);

        if (IsReloadBlockedByActorState())
        {
            AbortReloadRoutine();
            yield break;
        }

        isReloading = false;
        reloadRoutine = null;
        StopReloadCue();
        SyncWeaponInstanceState();
        statusEffectController?.NotifyTrigger(EffectTriggerType.OnReload, gameObject);
        NotifyRuntimeEffectHandlersReloadCompleted();
        PublishReloadEvent();
    }

    IEnumerator ReloadFullMagRoutine()
    {
        isReloading = true;
        yield return WaitForWeaponSeconds(reloadTime);

        if (IsReloadBlockedByActorState())
        {
            AbortReloadRoutine();
            yield break;
        }

        magazine = MaxMagazine;
        SyncWeaponInstanceState();
        ctx.UIManager?.UpdateAmmoText(magazine, MaxMagazine);
        isReloading = false;
        reloadRoutine = null;
        StopReloadCue();
        statusEffectController?.NotifyTrigger(EffectTriggerType.OnReload, gameObject);
        NotifyRuntimeEffectHandlersReloadCompleted();
        PublishReloadEvent();
    }

    IEnumerator FireBurst()
    {
        if (isReloading)
            yield break;

        int shots = burstCount;

        while (shots > 0)
        {
            if (GetWeaponTime() < nextFireTime)
            {
                yield return null;
                continue;
            }

            TryShoot();
            shots--;

            if (shots > 0)
                yield return WaitForWeaponSeconds(burstInterval);
        }

        isFiring = false;
        isBursting = false;
        burstCo = null;
    }

    public void OnAim(bool value) => isAiming = value;

    bool UsesWorldSlow()
    {
        return ctx == null || ctx.UsesWorldSlow;
    }

    float GetWeaponTime()
    {
        return UsesWorldSlow() ? TimeSlowManager.Instance.WorldTime : Time.time;
    }

    float GetWeaponDeltaTime()
    {
        return UsesWorldSlow() ? TimeSlowManager.Instance.WorldDeltaTime : Time.deltaTime;
    }

    IEnumerator WaitForWeaponSeconds(float seconds)
    {
        float remaining = Mathf.Max(0f, seconds);
        while (remaining > 0f)
        {
            remaining -= GetWeaponDeltaTime();
            yield return null;
        }
    }

    void PublishReloadEvent()
    {
        if (combatEventBus == null)
            return;

        var reloadContext = combatEventBus.CreateExternalContext(
            PassiveEventType.Reload,
            gameObject,
            null,
            GetWeaponSourceId(),
            null,
            magazine);

        combatEventBus.Publish(reloadContext);
    }

    PassiveEventContext CreateShotContext(string weaponSourceId, string attackId)
    {
        if (combatEventBus == null)
            return default;

        return combatEventBus.CreateExternalContext(
            PassiveEventType.ShotFired,
            gameObject,
            null,
            weaponSourceId,
            attackId,
            damage);
    }

    void SpawnProjectile(
        ProjectileConfig config,
        GameObject projectileToSpawn,
        float projectileDamage,
        float projectileSpeed,
        string weaponSourceId,
        string attackId,
        PassiveEventContext shotContext)
    {
        if (!TryResolveProjectilePrefab(projectileToSpawn, out var prefabComp))
            return;

        var projectile = Instantiate(prefabComp, firePoint.position, firePoint.rotation);
        ProjectileLayerUtility.ApplyForContext(projectile.gameObject, ctx);

        projectile.gunType = gunType;
        projectile.critRate = critRate;
        projectile.critMult = critMultiplier;

        projectile.Init(config, new ProjectileContext
        {
            sourceActor = transform,
            collisionIgnoreRoot = transform.root,
            combatEventBus = combatEventBus,
            statusEffectController = statusEffectController,
            dir = firePoint.forward,
            stats = new ProjectileStats
            {
                damage = projectileDamage,
                speed = projectileSpeed,
                staggerPower = staggerPower
            },
            hitCue = currentWeapon != null ? currentWeapon.hitCue : null,
            sourceId = weaponSourceId,
            attackId = attackId,
            chainId = shotContext.ChainId,
            depth = shotContext.Depth,
            origin = combatEventBus != null ? shotContext.Origin : PassiveEventOrigin.External,
            originPassiveId = shotContext.OriginPassiveId,
            originRuleId = shotContext.OriginRuleId,
            projectilePrefab = prefabComp
        });
    }

    bool TryResolveProjectilePrefab(GameObject prefab, out Projectile projectileComponent)
    {
        projectileComponent = null;

        if (!prefab)
            return false;

        if (_projectilePrefabCache.TryGetValue(prefab, out projectileComponent))
            return projectileComponent != null;

        projectileComponent = prefab.GetComponent<Projectile>();
        _projectilePrefabCache[prefab] = projectileComponent;

        if (projectileComponent == null)
            Debug.LogWarning("Projectile prefab is missing Projectile component.", prefab);

        return projectileComponent != null;
    }

    public void NotifyWeaponInstanceChanged()
    {
        ResolveReferences();

        statsHub?.MarkDirty();
        RefreshDerivedStats();
        magazine = Mathf.Clamp(magazine, 0, MaxMagazine);
        SyncWeaponInstanceState();
        ctx?.UIManager?.UpdateAmmoText(magazine, MaxMagazine);
        NotifyRuntimeEffectHandlersWeaponEquipped();
    }

    void RebuildRuntimeEffectHandlers()
    {
        runtimeEffectHandlers.Clear();

        AddRuntimeEffectHandler(affixRuntimeController);

        Transform ownerRoot = ctx ? ctx.transform : transform.root;
        if (!ownerRoot)
            ownerRoot = transform;

        var behaviours = ownerRoot.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IWeaponRuntimeEffectHandler handler)
                AddRuntimeEffectHandler(handler);
        }
    }

    void AddRuntimeEffectHandler(IWeaponRuntimeEffectHandler handler)
    {
        if (handler == null || runtimeEffectHandlers.Contains(handler))
            return;

        runtimeEffectHandlers.Add(handler);
    }

    void NotifyRuntimeEffectHandlersWeaponEquipped()
    {
        RebuildRuntimeEffectHandlers();

        for (int i = 0; i < runtimeEffectHandlers.Count; i++)
            runtimeEffectHandlers[i]?.NotifyWeaponEquipped();
    }

    void NotifyRuntimeEffectHandlersShotFired()
    {
        if (runtimeEffectHandlers.Count == 0)
            RebuildRuntimeEffectHandlers();

        for (int i = 0; i < runtimeEffectHandlers.Count; i++)
            runtimeEffectHandlers[i]?.HandleShotFired();
    }

    void NotifyRuntimeEffectHandlersReloadCompleted()
    {
        if (runtimeEffectHandlers.Count == 0)
            RebuildRuntimeEffectHandlers();

        for (int i = 0; i < runtimeEffectHandlers.Count; i++)
            runtimeEffectHandlers[i]?.HandleReloadCompleted();
    }

    int ResolveStartingMagazine()
    {
        if (currentWeaponInstance != null)
        {
            int defaultMagazine = WeaponInstanceFactory.ResolveDefaultMagazine(currentWeapon);
            int desiredMagazine = currentWeaponInstance.currentMagazine;
            if (desiredMagazine < 0)
                desiredMagazine = defaultMagazine;

            return Mathf.Clamp(desiredMagazine, 0, Mathf.Max(currentWeapon.maxMagazine, defaultMagazine));
        }

        return WeaponInstanceFactory.ResolveDefaultMagazine(currentWeapon);
    }

    void SyncWeaponInstanceState()
    {
        if (currentWeaponInstance == null)
            return;

        currentWeaponInstance.currentMagazine = Mathf.Max(0, magazine);
    }

    string GetWeaponSourceId()
    {
        if (currentWeaponInstance != null && !string.IsNullOrWhiteSpace(currentWeaponInstance.instanceId))
            return $"weapon:{currentWeaponInstance.instanceId}";

        string weaponName = currentWeapon ? currentWeapon.name : "weapon";
        return $"weapon:{weaponName}";
    }

    void PlayWeaponCue(AudioCue cue, bool followOwner)
    {
        if (cue == null)
            return;

        if (followOwner)
        {
            Transform followTarget = firePoint ? firePoint : transform;
            AudioService.Instance.PlayAttached(cue, followTarget, Vector3.zero);
            return;
        }

        Vector3 position = firePoint ? firePoint.position : transform.position;
        AudioService.Instance.PlayAtPosition(cue, position);
    }

    void PlayReloadCue()
    {
        StopReloadCue();

        if (currentWeapon == null || currentWeapon.reloadCue == null)
            return;

        Transform followTarget = firePoint ? firePoint : transform;
        reloadAudioHandle = AudioService.Instance.PlayAttached(currentWeapon.reloadCue, followTarget, Vector3.zero);
    }

    void StopReloadCue()
    {
        reloadAudioHandle.Stop();
        reloadAudioHandle = default;
    }
}
