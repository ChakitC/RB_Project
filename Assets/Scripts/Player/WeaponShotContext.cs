using UnityEngine;

public readonly struct WeaponShotContext
{
    public readonly ProjectileConfig ProjectileConfig;
    public readonly GameObject ProjectilePrefab;
    public readonly Transform FirePoint;
    public readonly float Damage;
    public readonly float Speed;
    public readonly string WeaponSourceId;
    public readonly string AttackId;
    public readonly PassiveEventContext PassiveContext;

    public WeaponShotContext(
        ProjectileConfig projectileConfig,
        GameObject projectilePrefab,
        Transform firePoint,
        float damage,
        float speed,
        string weaponSourceId,
        string attackId,
        PassiveEventContext passiveContext)
    {
        ProjectileConfig = projectileConfig;
        ProjectilePrefab = projectilePrefab;
        FirePoint = firePoint;
        Damage = damage;
        Speed = speed;
        WeaponSourceId = weaponSourceId;
        AttackId = attackId;
        PassiveContext = passiveContext;
    }
}
