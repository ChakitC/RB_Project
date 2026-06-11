using System.Collections;
using UnityEngine;

[RequireComponent(typeof(WeaponAffixRuntimeController))]
[RequireComponent(typeof(WeaponUpgradeRuntimeController))]
public class WeaponSystem : MonoBehaviour
{
    const string DefaultFirePointName = "FirePoint";
    const float StatsHubProbeIntervalSeconds = 0.2f;

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
    public int reserveAmmo = 0;
    public int maxReserveAmmo = 0;
    public bool infiniteReserveAmmo = false;
    public float reloadTime = 0f;
    public float critRate = 0f;
    public float critMultiplier = 1f;
    public bool magazineRelode = false;
    [InspectorName("Stability (%)")]
    public float stability = 0f;
    public bool autoloader;
    public float bulletSpeed = 20f;
    public float staggerPower = 0f;

    public bool isAiming = false;

    public bool IsReloading => isReloading;
    public bool IsFiringHeld => _isFiringHeld;
    public FiringMode CurrentFiringMode => firingMode;
    public bool CanFire => !isReloading && magazine > 0 && GetWeaponTime() >= nextFireTime;
    public bool IsAiming => isAiming;
    public Transform FirePoint => firePoint;
    public float Damage => damage;
    public int CurrentAmmo => magazine;
    public int MagazineSize => MaxMagazine;
    public int CurrentReserveAmmo => HasInfiniteReserveAmmo ? -1 : reserveAmmo;
    public int ReserveAmmoSize => HasInfiniteReserveAmmo ? -1 : MaxReserveAmmo;
    public bool HasInfiniteReserveAmmo =>
        currentWeapon != null &&
        (currentWeapon.infiniteReserveAmmo || (ctx != null && ctx.ForceInfiniteReserveAmmo));
    public bool HasReserveAmmo => HasInfiniteReserveAmmo || reserveAmmo > 0;
    public bool IsMagazineEmpty => magazine <= 0;
    public bool IsOutOfAmmo => magazine <= 0 && !HasReserveAmmo;
    public bool CanReload => !isReloading && currentWeapon != null && magazine < MaxMagazine && HasReserveAmmo;
    public bool IsFreeAmmoActive => ammoState.IsFreeAmmoActive;
    public float FreeAmmoRemaining => ammoState.FreeAmmoRemaining;

    bool _isFiringHeld;
    readonly WeaponAmmoState ammoState = new();
    readonly WeaponProjectileSpawner projectileSpawner = new();

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
    readonly WeaponReloadController reloadController = new();

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
    bool derivedStatsDirty = true;
    GunConfig derivedStatsWeapon;
    WeaponStatSnapshot statSnapshot;
    StatsHub subscribedStatsHub;
    int observedStatsHubRevision = -1;
    float nextStatsHubProbeTime;

    FiringMode firingMode = FiringMode.Auto;
    readonly WeaponFireController fireController = new();
    readonly WeaponRuntimeEffectDispatcher runtimeEffectDispatcher = new();

    void Awake()
    {
        ResolveReferences();

        if (!currentWeapon && ctx) currentWeapon = ctx.currentWeapon;

        RefreshFirePointReference(logIfMissing: true);
    }

    void OnEnable()
    {
        ResolveReferences();
        nextStatsHubProbeTime = 0f;
        MarkDerivedStatsDirty();
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

        SubscribeToStatsHub(statsHub);

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
            BindFirePoint(FindChildByName(ownerRoot, DefaultFirePointName));
        }

        if (firePoint)
        {
            CacheFirePointRestPose();
            return true;
        }

        if (logIfMissing)
            Debug.LogError("WeaponSystem: firePoint is missing.", this);

        return false;
    }

    public bool BindFirePoint(Transform value)
    {
        firePoint = value;

        if (!firePoint)
            return false;

        CacheFirePointRestPose();
        return true;
    }

    void CacheFirePointRestPose()
    {
        firePointOriginalRot = firePoint.localRotation;
        swayTimer = 0f;
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
        UpdateAmmoUI();
    }

    public void Equip(GunConfig weapon)
    {
        Equip(weapon, currentWeaponInstance);
    }

    public void Equip(GunConfig weapon, WeaponInstanceData instance)
    {
        var previousWeapon = currentWeapon;

        if (weapon != null && instance == null && TryGetEquipmentInstanceForWeapon(weapon, out WeaponInstanceData equipmentInstance))
            instance = equipmentInstance;

        SyncWeaponInstanceState();
        StopWeaponActivity(clearHeld: true, stopReloadAnim: true, syncInstance: false);

        currentWeapon = weapon;
        currentWeaponInstance = instance;
        MarkDerivedStatsDirty();

        if (ctx != null)
            ctx.currentWeapon = currentWeapon;

        if (!currentWeapon)
        {
            ClearWeaponState();
            return;
        }

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

        NotifyRuntimeEffectHandlersWeaponEquipped();
        statsHub?.MarkDirty();
        RefreshDerivedStats();
        ammoState.Equip(currentWeapon, currentWeaponInstance, MaxMagazine, MaxReserveAmmo, HasInfiniteReserveAmmo);
        SyncAmmoMirrorsFromState();
        SyncWeaponInstanceState();
        UpdateAmmoUI();

        RefreshWeaponVisual();

        if (currentWeapon != null && currentWeapon != previousWeapon)
            PlayWeaponCue(currentWeapon.equipCue, true);
    }

    bool TryGetEquipmentInstanceForWeapon(GunConfig weapon, out WeaponInstanceData instance)
    {
        instance = null;

        if (ctx == null || ctx.Equipment == null)
            ResolveReferences();

        CharacterEquipment equipment = ctx != null ? ctx.Equipment : null;
        if (equipment == null || equipment.CurrentWeaponInstance == null)
            return false;

        if (!IsInstanceForWeapon(equipment.CurrentWeaponInstance, weapon))
            return false;

        instance = equipment.CurrentWeaponInstance;
        return true;
    }

    static bool IsInstanceForWeapon(WeaponInstanceData instance, GunConfig weapon)
    {
        if (instance == null || weapon == null)
            return false;

        string weaponId = WeaponInstanceFactory.ResolveBaseWeaponId(weapon);
        return string.Equals(instance.baseWeaponId, weaponId, System.StringComparison.Ordinal);
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
        burstCo = null;
        reloadRoutine = null;
        StopReloadCue();
        SyncWeaponInstanceState();
        UnsubscribeFromStatsHub();
    }

    void OnDestroy()
    {
        UnsubscribeFromStatsHub();
    }

    int MaxMagazine =>
        statsHub ? statsHub.GetMaxMagazine(currentWeapon) :
        (currentWeapon ? currentWeapon.maxMagazine : 0);

    int MaxReserveAmmo =>
        HasInfiniteReserveAmmo
            ? 0
            : statsHub
                ? statsHub.GetMaxReserveAmmo(currentWeapon)
                : WeaponInstanceFactory.ResolveMaxReserveAmmo(currentWeapon, MaxMagazine);

    public float GetReloadAnimDuration()
    {
        return reloadController.GetAnimationDuration(
            reloadPerBullet,
            MaxMagazine,
            magazine,
            HasInfiniteReserveAmmo,
            reserveAmmo,
            startInsertDelay,
            perBulletInsertTime,
            endInsertDelay,
            reloadTime);
    }

    void RefreshDerivedStats()
    {
        if (!currentWeapon)
        {
            ClearDerivedStats();
            statSnapshot = default;
            derivedStatsDirty = false;
            derivedStatsWeapon = null;
            observedStatsHubRevision = ResolveStatsHubRevision();
            return;
        }

        statSnapshot = WeaponStatSnapshotBuilder.Build(currentWeapon, statsHub, ctx);

        damage = statSnapshot.Damage;
        fireRate = statSnapshot.FireInterval;
        critRate = statSnapshot.CritRate;
        critMultiplier = statSnapshot.CritMultiplier;
        stability = statSnapshot.Stability;
        bulletSpeed = statSnapshot.BulletSpeed;
        staggerPower = statSnapshot.StaggerPower;
        reloadTime = statSnapshot.ReloadTime;
        maxMagazine = statSnapshot.MaxMagazine;
        infiniteReserveAmmo = statSnapshot.InfiniteReserveAmmo;
        maxReserveAmmo = statSnapshot.MaxReserveAmmo;

        SyncAmmoStateFromMirrors();
        ammoState.RefreshLimits(maxMagazine, maxReserveAmmo, infiniteReserveAmmo, clampMagazine: false, resetInfiniteReserve: false);
        SyncAmmoMirrorsFromState();

        float stability01 = Mathf.Clamp01(stability * 0.01f);
        float swayMultiplier = 1f - stability01;

        swaySpeed = baseSwaySpeed * swayMultiplier;
        maxSwayAngle = baseMaxSwayAngle * swayMultiplier;
        returnSpeed = baseReturnSpeed * (1f + stability01);
        derivedStatsDirty = false;
        derivedStatsWeapon = currentWeapon;
        observedStatsHubRevision = ResolveStatsHubRevision();
    }

    void RefreshDerivedStatsIfDirty(bool forceStatsHubProbe = false)
    {
        int statsHubRevision = ResolveStatsHubRevision();
        bool shouldRefresh =
            derivedStatsDirty ||
            derivedStatsWeapon != currentWeapon ||
            observedStatsHubRevision != statsHubRevision;

        if (!shouldRefresh && ShouldProbeStatsHubInputs(forceStatsHubProbe))
        {
            if (statsHub.RefreshCacheIfInputsChanged(logSilentChange: true))
                statsHubRevision = ResolveStatsHubRevision();

            shouldRefresh = observedStatsHubRevision != statsHubRevision;
        }

        if (!shouldRefresh)
            return;

        RefreshDerivedStats();
    }

    bool ShouldProbeStatsHubInputs(bool forceProbe)
    {
        if (statsHub == null)
            return false;

        if (forceProbe)
        {
            ScheduleNextStatsHubProbe();
            return true;
        }

        if (!Application.isPlaying)
            return true;

        float now = Time.unscaledTime;
        if (now < nextStatsHubProbeTime)
            return false;

        nextStatsHubProbeTime = now + StatsHubProbeIntervalSeconds;
        return true;
    }

    void ScheduleNextStatsHubProbe()
    {
        if (Application.isPlaying)
            nextStatsHubProbeTime = Time.unscaledTime + StatsHubProbeIntervalSeconds;
    }

    int ResolveStatsHubRevision()
    {
        return statsHub != null ? statsHub.Revision : -1;
    }

    void MarkDerivedStatsDirty()
    {
        derivedStatsDirty = true;
    }

    void SubscribeToStatsHub(StatsHub nextStatsHub)
    {
        if (subscribedStatsHub == nextStatsHub)
            return;

        UnsubscribeFromStatsHub();
        subscribedStatsHub = nextStatsHub;
        observedStatsHubRevision = -1;

        if (subscribedStatsHub != null)
            subscribedStatsHub.StatsDirty += MarkDerivedStatsDirty;
    }

    void UnsubscribeFromStatsHub()
    {
        if (subscribedStatsHub == null)
            return;

        subscribedStatsHub.StatsDirty -= MarkDerivedStatsDirty;
        subscribedStatsHub = null;
        observedStatsHubRevision = -1;
    }

    void ClearDerivedStats()
    {
        ammoState.ClearAmmo();

        gunType = default;
        damage = 0f;
        fireRate = 0f;
        magazine = 0;
        maxMagazine = 0;
        reserveAmmo = 0;
        maxReserveAmmo = 0;
        infiniteReserveAmmo = false;
        reloadTime = 0f;
        critRate = 0f;
        critMultiplier = 1f;
        magazineRelode = false;
        stability = 0f;
        autoloader = false;
        bulletSpeed = 20f;
        staggerPower = 0f;
        burstCount = 0;
        burstInterval = 0f;

        reloadPerBullet = true;
        startInsertDelay = 0f;
        perBulletInsertTime = 0f;
        endInsertDelay = 0f;
        shootInterruptsReload = true;

        baseSwaySpeed = 0f;
        baseMaxSwayAngle = 0f;
        baseReturnSpeed = 0f;
        swaySpeed = 0f;
        maxSwayAngle = 0f;
        returnSpeed = 0f;
        swayTimer = 0f;
    }

    public void SetFiring(bool value)
    {
        _isFiringHeld = value;

        if (!value)
        {
            StopFiringState(clearHeld: false);
            return;
        }

        bool hasWeapon = currentWeapon != null;
        if (!fireController.CanStartFiring(hasWeapon, hasWeapon && CanShootNow()))
            return;

        if (fireController.ShouldCancelReload(isReloading, shootInterruptsReload))
            CancelReload();

        switch (fireController.ResolveStartAction(firingMode, isReloading, isBursting, burstCo != null))
        {
            case WeaponFireStartAction.Burst:
                isBursting = true;
                isFiring = true;
                burstCo = StartCoroutine(FireBurst());
                break;

            case WeaponFireStartAction.Semi:
                TryShoot();
                isFiring = false;
                break;

            case WeaponFireStartAction.Auto:
                isFiring = true;
                break;
        }
    }

    void Update()
    {
        RefreshDerivedStatsIfDirty();

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

    public bool TryShoot()
    {
        RefreshDerivedStatsIfDirty(forceStatsHubProbe: true);

        if (!CanBeginShotAttempt())
            return false;

        if (TryHandleEmptyMagazineShot())
            return false;

        if (!HasProjectileLaunchRefs())
            return false;

        nextFireTime = GetWeaponTime() + fireRate;
        ConsumeAmmoForShotIfNeeded();

        WeaponShotContext shot = CreateWeaponShotContext();
        SpawnProjectile(shot);

        PlayWeaponCue(currentWeapon != null ? currentWeapon.fireCue : null, false);
        DispatchShotNotifications(shot);
        TryStartAutoloadReloadAfterShot();

        return true;
    }

    bool CanBeginShotAttempt()
    {
        return currentWeapon && !isReloading && GetWeaponTime() >= nextFireTime && CanShootNow();
    }

    bool TryHandleEmptyMagazineShot()
    {
        if (magazine > 0)
            return false;

        if (autoloader && HasReserveAmmo)
            TryReload();
        else
            PlayWeaponCue(currentWeapon != null ? currentWeapon.emptyCue : null, false);

        return true;
    }

    bool HasProjectileLaunchRefs()
    {
        return projectilePrefab && firePoint;
    }

    void ConsumeAmmoForShotIfNeeded()
    {
        if (IsFreeAmmoActive)
            return;

        SyncAmmoStateFromMirrors();
        ammoState.ConsumeMagazineAmmo();
        SyncAmmoMirrorsFromState();
        SyncWeaponInstanceState();
        UpdateAmmoUI();
    }

    WeaponShotContext CreateWeaponShotContext()
    {
        string weaponSourceId = GetWeaponSourceId();
        string attackId = combatEventBus != null ? combatEventBus.CreateAttackId(weaponSourceId) : null;
        PassiveEventContext passiveContext = CreateShotContext(weaponSourceId, attackId);

        return new WeaponShotContext(
            projectileConfig,
            projectilePrefab,
            firePoint,
            damage,
            bulletSpeed,
            weaponSourceId,
            attackId,
            passiveContext);
    }

    void DispatchShotNotifications(WeaponShotContext shot)
    {
        ctx?.stateHub?.ReportShotFired();
        if (combatEventBus != null)
            combatEventBus.Publish(shot.PassiveContext);
        statusEffectController?.NotifyTrigger(EffectTriggerType.OnShotFired, gameObject);
        NotifyRuntimeEffectHandlersShotFired();
    }

    void TryStartAutoloadReloadAfterShot()
    {
        if (magazine <= 0 && autoloader && HasReserveAmmo)
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
        if (!isReloading && reloadRoutine == null)
            return;

        StopReloadState(stopRoutine: true, stopAnim: true, syncInstance: true);
        StopBurstRoutine();
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
        StopReloadState(stopRoutine: false, stopAnim: false, syncInstance: false);
        StopBurstRoutine();
        SyncWeaponInstanceState();
    }

    public void TryReload()
    {
        RefreshDerivedStatsIfDirty(forceStatsHubProbe: true);

        if (IsReloadBlockedByActorState())
            return;

        StopFiringState(clearHeld: false);

        if (isReloading || magazine >= MaxMagazine)
            return;

        if (!HasReserveAmmo)
        {
            PlayWeaponCue(currentWeapon != null ? currentWeapon.emptyCue : null, false);
            return;
        }

        if (reloadRoutine != null)
            StopCoroutine(reloadRoutine);

        switch (reloadController.ResolveReloadMode(reloadPerBullet, magazineRelode))
        {
            case WeaponReloadMode.PerBullet:
                reloadRoutine = StartCoroutine(ReloadPerBulletRoutine());
                PlayReloadCue();
                break;

            case WeaponReloadMode.FullMagazine:
                reloadRoutine = StartCoroutine(ReloadFullMagRoutine());
                PlayReloadCue();
                break;

            default:
                Debug.LogWarning("[WeaponSystem] TryReload called but no reload mode is enabled.", this);
                break;
        }
    }

    public bool CanRestoreMagazine(int amount, bool fillToMax = false)
    {
        RefreshDerivedStatsIfDirty(forceStatsHubProbe: true);
        SyncAmmoStateFromMirrors();

        if (!currentWeapon)
            return false;

        return ammoState.CanRestoreMagazine(amount, fillToMax);
    }

    public bool CanRestoreReserveAmmo(int amount, bool fillToMax = false)
    {
        RefreshDerivedStatsIfDirty(forceStatsHubProbe: true);
        SyncAmmoStateFromMirrors();

        if (!currentWeapon)
            return false;

        return ammoState.CanRestoreReserveAmmo(amount, fillToMax);
    }

    public bool RestoreReserveAmmo(int amount, bool fillToMax = false)
    {
        if (!CanRestoreReserveAmmo(amount, fillToMax))
            return false;

        if (!ammoState.RestoreReserveAmmo(amount, fillToMax))
            return false;

        SyncAmmoMirrorsFromState();
        SyncWeaponInstanceState();
        UpdateAmmoUI();
        return true;
    }

    public bool RestoreMagazine(int amount, bool fillToMax = false)
    {
        if (!CanRestoreMagazine(amount, fillToMax))
            return false;

        if (isReloading)
            CancelReload();

        SyncAmmoStateFromMirrors();
        if (!ammoState.RestoreMagazine(amount, fillToMax))
            return false;

        SyncAmmoMirrorsFromState();
        SyncWeaponInstanceState();
        UpdateAmmoUI();
        return true;
    }

    public void GrantFreeAmmo(float duration)
    {
        ammoState.GrantFreeAmmo(duration);
    }

    IEnumerator ReloadPerBulletRoutine()
    {
        isReloading = true;
        bool reloadedAny = false;

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

            int restored = ConsumeReserveAmmo(1);
            if (restored <= 0)
                break;

            AddMagazineAmmo(restored);
            reloadedAny = true;
            SyncWeaponInstanceState();
            UpdateAmmoUI();

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
        if (reloadedAny)
        {
            statusEffectController?.NotifyTrigger(EffectTriggerType.OnReload, gameObject);
            NotifyRuntimeEffectHandlersReloadCompleted();
            PublishReloadEvent();
        }
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

        int missing = Mathf.Max(0, MaxMagazine - magazine);
        int restored = ConsumeReserveAmmo(missing);
        if (restored <= 0)
        {
            isReloading = false;
            reloadRoutine = null;
            StopReloadCue();
            SyncWeaponInstanceState();
            UpdateAmmoUI();
            yield break;
        }

        AddMagazineAmmo(restored);
        SyncWeaponInstanceState();
        UpdateAmmoUI();
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
        {
            burstCo = null;
            isBursting = false;
            isFiring = false;
            yield break;
        }

        int shots = burstCount;

        while (shots > 0 && isBursting && isFiring && isActiveAndEnabled)
        {
            if (GetWeaponTime() < nextFireTime)
            {
                yield return null;
                continue;
            }

            if (TryShoot())
            {
                shots--;
            }
            else
            {
                if (isReloading || magazine <= 0 || !projectilePrefab || !firePoint || !CanShootNow())
                    break;

                yield return null;
                continue;
            }

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
        var timeSlow = TimeSlowManager.Instance;
        return UsesWorldSlow() && timeSlow != null ? timeSlow.WorldTime : Time.time;
    }

    float GetWeaponDeltaTime()
    {
        var timeSlow = TimeSlowManager.Instance;
        return UsesWorldSlow() && timeSlow != null ? timeSlow.WorldDeltaTime : Time.deltaTime;
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
        WeaponShotContext shot)
    {
        SpawnProjectile(
            shot.ProjectileConfig,
            shot.ProjectilePrefab,
            shot.Damage,
            shot.Speed,
            shot.WeaponSourceId,
            shot.AttackId,
            shot.PassiveContext);
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
        projectileSpawner.Spawn(new WeaponProjectileSpawnContext(
            config,
            projectileToSpawn,
            firePoint,
            transform,
            transform.root,
            ctx,
            combatEventBus,
            statusEffectController,
            gunType,
            critRate,
            critMultiplier,
            projectileDamage,
            projectileSpeed,
            staggerPower,
            currentWeapon != null ? currentWeapon.hitCue : null,
            weaponSourceId,
            attackId,
            shotContext));
    }

    bool TryResolveProjectilePrefab(GameObject prefab, out Projectile projectileComponent)
    {
        return projectileSpawner.TryResolveProjectilePrefab(prefab, out projectileComponent);
    }

    public void NotifyWeaponInstanceChanged()
    {
        ResolveReferences();

        NotifyRuntimeEffectHandlersWeaponEquipped();
        statsHub?.MarkDirty();
        MarkDerivedStatsDirty();
        RefreshDerivedStats();
        SyncAmmoStateFromMirrors();
        ammoState.RefreshLimits(MaxMagazine, MaxReserveAmmo, HasInfiniteReserveAmmo, clampMagazine: true, resetInfiniteReserve: true);
        SyncAmmoMirrorsFromState();
        ClampReserveAmmoToMax();
        SyncWeaponInstanceState();
        UpdateAmmoUI();
    }

    bool CanShootNow()
    {
        var stateHub = ctx != null ? ctx.stateHub : null;
        return stateHub != null && stateHub.CanShoot();
    }

    void ClearWeaponState()
    {
        projectilePrefab = null;
        nextFireTime = 0f;
        ammoState.ClearFreeAmmo();
        firingMode = FiringMode.Auto;

        ClearDerivedStats();
        statSnapshot = default;
        derivedStatsDirty = false;
        derivedStatsWeapon = null;

        if (firePoint)
            firePoint.localRotation = firePointOriginalRot;

        NotifyRuntimeEffectHandlersWeaponEquipped();
        statsHub?.MarkDirty();
        UpdateAmmoUI();
        RefreshWeaponVisual();
    }

    void StopWeaponActivity(bool clearHeld, bool stopReloadAnim, bool syncInstance)
    {
        StopFiringState(clearHeld);
        StopReloadState(stopRoutine: true, stopAnim: stopReloadAnim, syncInstance: syncInstance);
    }

    void StopFiringState(bool clearHeld)
    {
        if (clearHeld)
            _isFiringHeld = false;

        isFiring = false;
        StopBurstRoutine();
    }

    void StopBurstRoutine()
    {
        if (burstCo != null)
        {
            StopCoroutine(burstCo);
            burstCo = null;
        }

        isBursting = false;
    }

    void StopReloadState(bool stopRoutine, bool stopAnim, bool syncInstance)
    {
        if (stopRoutine && reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
        }

        reloadRoutine = null;
        isReloading = false;
        StopReloadCue();

        if (stopAnim)
            ctx?.AnimBrain?.StopReloadAction();

        if (syncInstance)
            SyncWeaponInstanceState();
    }

    void RebuildRuntimeEffectHandlers()
    {
        runtimeEffectDispatcher.Rebuild(ctx, transform, affixRuntimeController);
    }

    void NotifyRuntimeEffectHandlersWeaponEquipped()
    {
        runtimeEffectDispatcher.NotifyWeaponEquipped(ctx, transform, affixRuntimeController);
    }

    void NotifyRuntimeEffectHandlersShotFired()
    {
        runtimeEffectDispatcher.NotifyShotFired(ctx, transform, affixRuntimeController);
    }

    void NotifyRuntimeEffectHandlersReloadCompleted()
    {
        runtimeEffectDispatcher.NotifyReloadCompleted(ctx, transform, affixRuntimeController);
    }

    void SyncAmmoStateFromMirrors()
    {
        ammoState.SetCurrent(magazine, reserveAmmo);
        ammoState.RefreshLimits(maxMagazine, maxReserveAmmo, infiniteReserveAmmo, clampMagazine: false, resetInfiniteReserve: false);
    }

    void SyncAmmoMirrorsFromState()
    {
        magazine = ammoState.Magazine;
        reserveAmmo = ammoState.ReserveAmmo;
        maxMagazine = ammoState.MaxMagazine;
        maxReserveAmmo = ammoState.MaxReserveAmmo;
        infiniteReserveAmmo = ammoState.HasInfiniteReserveAmmo;
    }

    void SyncWeaponInstanceState()
    {
        if (currentWeaponInstance == null)
            return;

        SyncAmmoStateFromMirrors();
        ammoState.SyncInstance(currentWeaponInstance);
    }

    int ConsumeReserveAmmo(int amount)
    {
        SyncAmmoStateFromMirrors();
        int consumed = ammoState.ConsumeReserveAmmo(amount);
        SyncAmmoMirrorsFromState();
        return consumed;
    }

    void AddMagazineAmmo(int amount)
    {
        SyncAmmoStateFromMirrors();
        ammoState.AddMagazineAmmo(amount);
        SyncAmmoMirrorsFromState();
    }

    void ClampReserveAmmoToMax()
    {
        SyncAmmoStateFromMirrors();
        ammoState.RefreshLimits(MaxMagazine, MaxReserveAmmo, HasInfiniteReserveAmmo, clampMagazine: false, resetInfiniteReserve: true);
        SyncAmmoMirrorsFromState();
    }

    void UpdateAmmoUI()
    {
        ctx?.UIManager?.UpdateAmmoText(
            magazine,
            MaxMagazine,
            HasInfiniteReserveAmmo ? -1 : reserveAmmo,
            HasInfiniteReserveAmmo);
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
