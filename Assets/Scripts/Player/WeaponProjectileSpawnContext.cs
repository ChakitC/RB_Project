using UnityEngine;

public readonly struct WeaponProjectileSpawnContext
{
    public readonly ProjectileConfig Config;
    public readonly GameObject ProjectilePrefab;
    public readonly Transform FirePoint;
    public readonly Transform SourceActor;
    public readonly Transform CollisionIgnoreRoot;
    public readonly CharacteContext OwnerContext;
    public readonly CombatEventBus CombatEventBus;
    public readonly StatusEffectController StatusEffectController;
    public readonly WeaponType GunType;
    public readonly float CritRate;
    public readonly float CritMultiplier;
    public readonly float Damage;
    public readonly float Speed;
    public readonly Vector3 Direction;
    public readonly float StaggerPower;
    public readonly AudioCue HitCue;
    public readonly string DamageSourceId;
    public readonly string AttackId;
    public readonly PassiveEventContext PassiveContext;
    public readonly CombatEventMetadata CombatMetadata;
    public readonly IWeaponAffixPreDamageRuntime PreDamageRuntime;
    public readonly WeaponAffixImpactPayload ImpactPayload;

    public WeaponProjectileSpawnContext(
        ProjectileConfig config,
        GameObject projectilePrefab,
        Transform firePoint,
        Transform sourceActor,
        Transform collisionIgnoreRoot,
        CharacteContext ownerContext,
        CombatEventBus combatEventBus,
        StatusEffectController statusEffectController,
        WeaponType gunType,
        float critRate,
        float critMultiplier,
        float damage,
        float speed,
        Vector3 direction,
        float staggerPower,
        AudioCue hitCue,
        string damageSourceId,
        string attackId,
        PassiveEventContext passiveContext,
        CombatEventMetadata combatMetadata = default,
        IWeaponAffixPreDamageRuntime preDamageRuntime = null,
        WeaponAffixImpactPayload impactPayload = default)
    {
        Config = config;
        ProjectilePrefab = projectilePrefab;
        FirePoint = firePoint;
        SourceActor = sourceActor;
        CollisionIgnoreRoot = collisionIgnoreRoot;
        OwnerContext = ownerContext;
        CombatEventBus = combatEventBus;
        StatusEffectController = statusEffectController;
        GunType = gunType;
        CritRate = critRate;
        CritMultiplier = critMultiplier;
        Damage = damage;
        Speed = speed;
        Direction = direction;
        StaggerPower = staggerPower;
        HitCue = hitCue;
        DamageSourceId = damageSourceId;
        AttackId = attackId;
        PassiveContext = passiveContext;
        CombatMetadata = combatMetadata;
        PreDamageRuntime = preDamageRuntime;
        ImpactPayload = impactPayload;
    }
}
