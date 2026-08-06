using UnityEngine;

public readonly struct WeaponShotContext
{
    public readonly ProjectileConfig ProjectileConfig;
    public readonly GameObject ProjectilePrefab;
    public readonly Transform FirePoint;
    public readonly float Damage;
    public readonly float Speed;
    public readonly float CritRate;
    public readonly float CritMultiplier;
    public readonly float StaggerPower;
    public readonly Vector3 Direction;
    public readonly string WeaponSourceId;
    public readonly string AttackId;
    public readonly PassiveEventContext PassiveContext;
    public readonly string WeaponInstanceId;
    public readonly int AmmoBefore;
    public readonly int AmmoAfter;
    public readonly int MaxMagazine;
    public readonly bool AmmoConsumed;
    public readonly bool IsLastRound;
    public readonly WeaponAffixImpactPayload ImpactPayload;

    public WeaponShotContext(
        ProjectileConfig projectileConfig,
        GameObject projectilePrefab,
        Transform firePoint,
        float damage,
        float speed,
        Vector3 direction,
        string weaponSourceId,
        string attackId,
        PassiveEventContext passiveContext,
        float critRate = 0f,
        float critMultiplier = 1f,
        float staggerPower = 0f,
        string weaponInstanceId = null,
        int ammoBefore = 0,
        int ammoAfter = 0,
        int maxMagazine = 0,
        bool ammoConsumed = false,
        bool isLastRound = false,
        WeaponAffixImpactPayload impactPayload = default)
    {
        ProjectileConfig = projectileConfig;
        ProjectilePrefab = projectilePrefab;
        FirePoint = firePoint;
        Damage = damage;
        Speed = speed;
        Direction = direction;
        WeaponSourceId = weaponSourceId;
        AttackId = attackId;
        PassiveContext = passiveContext;
        CritRate = critRate;
        CritMultiplier = critMultiplier;
        StaggerPower = staggerPower;
        WeaponInstanceId = weaponInstanceId;
        AmmoBefore = ammoBefore;
        AmmoAfter = ammoAfter;
        MaxMagazine = maxMagazine;
        AmmoConsumed = ammoConsumed;
        IsLastRound = isLastRound;
        ImpactPayload = impactPayload;
    }
}
