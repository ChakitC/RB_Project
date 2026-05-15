public readonly struct WeaponStatSnapshot
{
    public readonly GunConfig Weapon;
    public readonly WeaponType GunType;
    public readonly FiringMode FiringMode;
    public readonly float Damage;
    public readonly float FireInterval;
    public readonly int MaxMagazine;
    public readonly int MaxReserveAmmo;
    public readonly bool InfiniteReserveAmmo;
    public readonly float ReloadTime;
    public readonly float CritRate;
    public readonly float CritMultiplier;
    public readonly float Stability;
    public readonly float BulletSpeed;
    public readonly float StaggerPower;
    public readonly bool MagazineReload;
    public readonly bool Autoloader;
    public readonly bool ReloadPerBullet;
    public readonly float StartInsertDelay;
    public readonly float PerBulletInsertTime;
    public readonly float EndInsertDelay;
    public readonly bool ShootInterruptsReload;
    public readonly float BaseSwaySpeed;
    public readonly float BaseMaxSwayAngle;
    public readonly float BaseReturnSpeed;
    public readonly int BurstCount;
    public readonly float BurstInterval;

    public bool HasWeapon => Weapon != null;

    public WeaponStatSnapshot(
        GunConfig weapon,
        WeaponType gunType,
        FiringMode firingMode,
        float damage,
        float fireInterval,
        int maxMagazine,
        int maxReserveAmmo,
        bool infiniteReserveAmmo,
        float reloadTime,
        float critRate,
        float critMultiplier,
        float stability,
        float bulletSpeed,
        float staggerPower,
        bool magazineReload,
        bool autoloader,
        bool reloadPerBullet,
        float startInsertDelay,
        float perBulletInsertTime,
        float endInsertDelay,
        bool shootInterruptsReload,
        float baseSwaySpeed,
        float baseMaxSwayAngle,
        float baseReturnSpeed,
        int burstCount,
        float burstInterval)
    {
        Weapon = weapon;
        GunType = gunType;
        FiringMode = firingMode;
        Damage = damage;
        FireInterval = fireInterval;
        MaxMagazine = maxMagazine;
        MaxReserveAmmo = maxReserveAmmo;
        InfiniteReserveAmmo = infiniteReserveAmmo;
        ReloadTime = reloadTime;
        CritRate = critRate;
        CritMultiplier = critMultiplier;
        Stability = stability;
        BulletSpeed = bulletSpeed;
        StaggerPower = staggerPower;
        MagazineReload = magazineReload;
        Autoloader = autoloader;
        ReloadPerBullet = reloadPerBullet;
        StartInsertDelay = startInsertDelay;
        PerBulletInsertTime = perBulletInsertTime;
        EndInsertDelay = endInsertDelay;
        ShootInterruptsReload = shootInterruptsReload;
        BaseSwaySpeed = baseSwaySpeed;
        BaseMaxSwayAngle = baseMaxSwayAngle;
        BaseReturnSpeed = baseReturnSpeed;
        BurstCount = burstCount;
        BurstInterval = burstInterval;
    }
}
