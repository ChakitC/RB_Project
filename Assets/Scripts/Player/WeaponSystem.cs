using System;
using UnityEngine;
using System.Collections;

public class WeaponSystem : MonoBehaviour
{
    [Header("Refs")]
    public CharacteContext ctx;
    [SerializeField] private StatsHub statsHub;

    public ProjectileConfig projectileConfig;
    public GameObject projectilePrefab;

    [SerializeField] private GunConfig currentWeapon;
    public GunConfig CurrentWeapon => currentWeapon;

    [Header("WeaponStats (debug/inspector)")]
    public Transform firePoint;
    public WeaponType gunType;
    public float damage = 0f;          // <- จะอัปเดตจาก hub (ค่า final)
    public float fireRate = 0f;        // <- “เวลาระหว่างนัด” (ตามของเดิม)
    public int magazine = 0;           // runtime state
    public int maxMagazine = 0;        // <- final (อัปเดตจาก hub ถ้ามีบัฟ)
    public float reloadTime = 0f;      // มาจาก config (คงที่เป็นหลัก)
    public float critRate = 0f;        // <- final
    public float critMultiplier = 0f;  // <- final
    public bool magazineRelode = false;
    public float stability = 0f;       // <- final
    public bool autoloader;
    public float bulletSpeed = 20f;    // <- final ถ้าคุณอยากให้บัฟได้

    public bool isAiming = false;
    
    // ========== Public property =================
    public bool IsReloading => isReloading;
    public bool IsFiringHeld => _isFiringHeld;
    public bool CanFire => !isReloading && magazine > 0 && Time.time >= nextFireTime;
    public int CurrentAmmo => magazine;
    public int MagazineSize => MaxMagazine;
    public bool IsMagazineEmpty => magazine <= 0;

   // ===============================================
   
    private bool _isFiringHeld;
    
    // Sway
    float baseSwaySpeed = 0f;
    float baseMaxSwayAngle = 0f;
    float baseReturnSpeed = 0f;
    float swaySpeed;
    float maxSwayAngle;
    float returnSpeed;

    float swayTimer = 0f;
    Quaternion firePointOriginalRot;

    // Reload
    bool reloadPerBullet = true;
    float startInsertDelay = 0;
    float perBulletInsertTime = 0;
    float endInsertDelay = 0;
    bool shootInterruptsReload = true;
    Coroutine reloadRoutine;
    

    [Header("Burst Settings")]
    public int burstCount = 0;
    public float burstInterval = 0f; 
    
    Coroutine burstCo;
    
    //FSM
    
    public bool IsBursting => isBursting;
    public bool IsFiringActivity =>
        (firingMode == FiringMode.Auto && isFiring) ||
        (firingMode == FiringMode.Burst && isBursting);
    
    public bool isFiring;

    float nextFireTime;
    private bool isReloading;
    
    bool isBursting;

    FiringMode firingMode = FiringMode.Auto;
    

    void Awake()
    {
        if (!ctx) ctx = GetComponent<CharacteContext>();
        if (!statsHub) statsHub = GetComponent<StatsHub>();
        
        if (!currentWeapon && ctx) currentWeapon = ctx.currentWeapon;

        if (!firePoint) firePoint = transform.Find("FirePoint");
        if (firePoint) firePointOriginalRot = firePoint.localRotation;
        else Debug.LogError("WeaponSystem: firePoint is missing.");
    }

    void Start()
    {
     
        Equip(currentWeapon);
        
        ctx.UIManager?.UpdateAmmoText(magazine, MaxMagazine);
        
        if (projectilePrefab == null) { projectilePrefab = ctx.currentWeapon.BulletPrefab; }
    }

    public void Equip(GunConfig weapon)
    {
        currentWeapon = weapon;
        if (!currentWeapon) return;

        // ค่าคงที่/พฤติกรรมจากปืน
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

        // runtime state
        magazine = currentWeapon.magazine;

        // ดึงค่า final ครั้งแรก (เผื่อ UI / sway)
        RefreshDerivedStats();
        magazine = Mathf.Clamp(magazine, 0, MaxMagazine);
    }

    void OnDisable()
    {
        StopAllCoroutines();
        isFiring = false;
        isBursting = false;
        isReloading = false;
        _isFiringHeld = false;
        reloadRoutine = null;
    }

    // ---------- Final stats getters ----------
    
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
        if (!currentWeapon) return;

        // อัปเดตไว้ให้ดูใน inspector + ใช้จริง
        damage = FinalDamage;
        fireRate = FireInterval;
        critRate = FinalCritRate;
        critMultiplier = FinalCritMult;
        stability = FinalStability;
        bulletSpeed = FinalBulletSpeed;
        maxMagazine = MaxMagazine;

        // sway ควรผูกกับ stability (ถ้า stability โดนบัฟ/ดีบัฟได้)
        float k = 0.1f;
        float stabilityFactor = 1f / (1f + stability * k);

        swaySpeed = baseSwaySpeed * stabilityFactor;
        maxSwayAngle = baseMaxSwayAngle * stabilityFactor;
        returnSpeed = baseReturnSpeed * (1f + stability * k);
    }

    public void SetFiring(bool value)
    {   
        _isFiringHeld =  value;
        
        // การหยุดยิง ต้องทำได้เสมอ
        if (!value)
        {
            isFiring = false;

            if (isBursting)
                isBursting = false;

            return;
        }

        if (!ctx.stateHub.CanShoot()) return;

        if (isReloading && shootInterruptsReload)
        {
            if (reloadRoutine != null) StopCoroutine(reloadRoutine);
            isReloading = false;
            reloadRoutine = null;
        }

        switch (firingMode)
        {
            case FiringMode.Burst:
                if (isReloading) return;
                if (isBursting) return;

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
        // ถ้ามีบัฟ/ดีบัฟเปลี่ยนระหว่างเล่น ให้รีเฟรชค่าทุกเฟรม (ชัวร์สุด)
        // ถ้าจะ optimize ทีหลัง ค่อยเปลี่ยนเป็น "dirty flag" จาก hub
        
        RefreshDerivedStats();

        if (firingMode == FiringMode.Auto && isFiring)
        {
            if (Time.time >= nextFireTime)
                TryShoot();
        }

        if (firePoint)
        {
            if (isFiring && (firingMode == FiringMode.Auto || firingMode == FiringMode.Burst))
            {
                swayTimer += Time.deltaTime * swaySpeed;
                float angle = Mathf.Sin(swayTimer) * maxSwayAngle;
                firePoint.localRotation = firePointOriginalRot * Quaternion.Euler(0f, angle, 0f);
            }
            else
            {
                swayTimer = Mathf.MoveTowards(swayTimer, 0f, Time.deltaTime * swaySpeed);
                firePoint.localRotation = Quaternion.Lerp(
                    firePoint.localRotation,
                    firePointOriginalRot,
                    Time.deltaTime * returnSpeed
                );
            }
        }
    }

    public void TryShoot()
    {
        
        RefreshDerivedStats(); // กันกรณีเรียกจาก Semi/Burst เฟรมเดียวกัน

        if (isReloading) return;
        if (Time.time < nextFireTime) return;

        if (magazine <= 0)
        {
            if (autoloader) TryReload();
            return;
        }

        if (!projectilePrefab || !firePoint) return;

        nextFireTime = Time.time + fireRate;

        magazine--;
        ctx.UIManager?.UpdateAmmoText(magazine, MaxMagazine);

        var go = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        var p = go.GetComponent<Projectile>();

        p.gunType = gunType;
        p.critRate = critRate;
        p.critMult = critMultiplier;

        Projectile prefabComp = projectilePrefab.GetComponent<Projectile>();

        p.Init(projectileConfig, new ProjectileContext
        {
            owner = transform.root,
            dir = firePoint.forward,
            stats = new ProjectileStats { damage = damage, speed = bulletSpeed },
            projectilePrefab = prefabComp
        });
        
        ctx.stateHub.ReportShotFired();
        
        if (magazine <= 0 && autoloader)
        {
            TryReload();
        }
        
    }
    public void CancelReload()
    {
        if (!isReloading) return;

        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }

        isReloading = false;

        // กันสถานะค้าง
        isBursting = false;

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

        if (isReloading || magazine >= MaxMagazine) return;

        if (reloadRoutine != null) StopCoroutine(reloadRoutine);

        if (reloadPerBullet)
        {
          
            reloadRoutine = StartCoroutine(ReloadPerBulletRoutine());
        }
        else if (magazineRelode)
        {
           
            reloadRoutine = StartCoroutine(ReloadFullMagRoutine());
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
    }

    IEnumerator ReloadFullMagRoutine()
    {
        isReloading = true;
        yield return new WaitForSeconds(reloadTime);
        magazine = MaxMagazine;
        ctx.UIManager?.UpdateAmmoText(magazine, MaxMagazine);
        isReloading = false;
        reloadRoutine = null;
    }

    IEnumerator FireBurst()
    {
        if (isReloading) yield break;

        // isBursting ถูกตั้ง true แล้วจาก SetFiring()

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
}
