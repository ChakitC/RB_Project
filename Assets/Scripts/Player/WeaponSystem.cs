using System.Collections;
using UnityEngine;

[RequireComponent(typeof(WeaponAffixRuntimeController))]
public class WeaponSystem : MonoBehaviour
{
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
    public float critMultiplier = 0f;
    public bool magazineRelode = false;
    public float stability = 0f;
    public bool autoloader;
    public float bulletSpeed = 20f;

    public bool isAiming = false;

    public bool IsReloading => isReloading;
    public bool IsFiringHeld => _isFiringHeld;
    public bool CanFire => !isReloading && magazine > 0 && Time.time >= nextFireTime;
    public int CurrentAmmo => magazine;
    public int MagazineSize => MaxMagazine;
    public bool IsMagazineEmpty => magazine <= 0;

    bool _isFiringHeld;

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

    void Awake()
    {
        if (!ctx) ctx = GetComponent<CharacteContext>();
        if (!statsHub) statsHub = GetComponent<StatsHub>();
        if (!statusEffectController) statusEffectController = GetComponent<StatusEffectController>();
        if (!combatEventBus) combatEventBus = GetComponent<CombatEventBus>();
        if (!affixRuntimeController) affixRuntimeController = GetComponent<WeaponAffixRuntimeController>();

        if (!currentWeapon && ctx) currentWeapon = ctx.currentWeapon;

        if (!firePoint) firePoint = transform.Find("FirePoint");
        if (firePoint) firePointOriginalRot = firePoint.localRotation;
        else Debug.LogError("WeaponSystem: firePoint is missing.");
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

        magazine = ResolveStartingMagazine();
        RefreshDerivedStats();
        magazine = Mathf.Clamp(magazine, 0, MaxMagazine);
        SyncWeaponInstanceState();

        affixRuntimeController?.NotifyWeaponEquipped();
        statsHub?.MarkDirty();

        if (currentWeapon != null && currentWeapon != previousWeapon)
            PlayWeaponCue(currentWeapon.equipCue, true);
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
        (currentWeapon ? currentWeapon.critMultiplier : 0f) + (ctx ? ctx.basecritMultiplier : 0f);

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
            if (reloadRoutine != null)
                StopCoroutine(reloadRoutine);

            isReloading = false;
            reloadRoutine = null;
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

        if (firingMode == FiringMode.Auto && isFiring && Time.time >= nextFireTime)
            TryShoot();

        if (!firePoint)
            return;

        if (isFiring && (firingMode == FiringMode.Auto || firingMode == FiringMode.Burst))
        {
            swayTimer += Time.deltaTime * swaySpeed;
            float angle = Mathf.Sin(swayTimer) * maxSwayAngle;
            firePoint.localRotation = firePointOriginalRot * Quaternion.Euler(0f, angle, 0f);
            return;
        }

        swayTimer = Mathf.MoveTowards(swayTimer, 0f, Time.deltaTime * swaySpeed);
        firePoint.localRotation = Quaternion.Lerp(
            firePoint.localRotation,
            firePointOriginalRot,
            Time.deltaTime * returnSpeed);
    }

    public void TryShoot()
    {
        RefreshDerivedStats();

        if (isReloading || Time.time < nextFireTime)
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

        nextFireTime = Time.time + fireRate;

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
        affixRuntimeController?.HandleShotFired();

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

        if (burstCo != null)
        {
            StopCoroutine(burstCo);
            burstCo = null;
        }
    }

    public void TryReload()
    {
        RefreshDerivedStats();

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

    IEnumerator ReloadPerBulletRoutine()
    {
        isReloading = true;

        if (startInsertDelay > 0f)
            yield return new WaitForSeconds(startInsertDelay);

        while (magazine < MaxMagazine)
        {
            if (shootInterruptsReload && (isFiring || isBursting))
                break;

            magazine++;
            SyncWeaponInstanceState();
            ctx.UIManager?.UpdateAmmoText(magazine, MaxMagazine);

            if (perBulletInsertTime > 0f)
                yield return new WaitForSeconds(perBulletInsertTime);
            else
                yield return null;
        }

        if (endInsertDelay > 0f)
            yield return new WaitForSeconds(endInsertDelay);

        isReloading = false;
        reloadRoutine = null;
        StopReloadCue();
        SyncWeaponInstanceState();
        statusEffectController?.NotifyTrigger(EffectTriggerType.OnReload, gameObject);
        affixRuntimeController?.HandleReloadCompleted();
        PublishReloadEvent();
    }

    IEnumerator ReloadFullMagRoutine()
    {
        isReloading = true;
        yield return new WaitForSeconds(reloadTime);
        magazine = MaxMagazine;
        SyncWeaponInstanceState();
        ctx.UIManager?.UpdateAmmoText(magazine, MaxMagazine);
        isReloading = false;
        reloadRoutine = null;
        StopReloadCue();
        statusEffectController?.NotifyTrigger(EffectTriggerType.OnReload, gameObject);
        affixRuntimeController?.HandleReloadCompleted();
        PublishReloadEvent();
    }

    IEnumerator FireBurst()
    {
        if (isReloading)
            yield break;

        int shots = burstCount;

        while (shots > 0)
        {
            if (Time.time < nextFireTime)
            {
                yield return null;
                continue;
            }

            TryShoot();
            shots--;

            if (shots > 0)
                yield return new WaitForSeconds(burstInterval);
        }

        isFiring = false;
        isBursting = false;
        burstCo = null;
    }

    public void OnAim(bool value) => isAiming = value;

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
        var go = Instantiate(projectileToSpawn, firePoint.position, firePoint.rotation);
        var projectile = go.GetComponent<Projectile>();
        var prefabComp = projectileToSpawn.GetComponent<Projectile>();

        if (projectile == null || prefabComp == null)
        {
            Debug.LogWarning("Projectile prefab is missing Projectile component.", projectileToSpawn);
            if (go != null)
                Destroy(go);
            return;
        }

        projectile.gunType = gunType;
        projectile.critRate = critRate;
        projectile.critMult = critMultiplier;

        projectile.Init(config, new ProjectileContext
        {
            owner = transform.root,
            dir = firePoint.forward,
            stats = new ProjectileStats
            {
                damage = projectileDamage,
                speed = projectileSpeed
            },
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
