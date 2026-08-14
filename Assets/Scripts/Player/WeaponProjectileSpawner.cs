using System.Collections.Generic;
using UnityEngine;

public sealed class WeaponProjectileSpawner
{
    readonly Dictionary<GameObject, Projectile> projectilePrefabCache = new();

    public void Spawn(WeaponProjectileSpawnContext context)
    {
        if (!TryResolveProjectilePrefab(context.ProjectilePrefab, out var prefabComp))
            return;

        if (!context.FirePoint)
            return;

        var pool = ProjectilePool.Instance;
        if (pool == null) return;
        Vector3 direction = context.Direction.sqrMagnitude > 0.0001f
            ? context.Direction.normalized
            : context.FirePoint.forward;
        Quaternion rotation = Quaternion.LookRotation(direction, context.FirePoint.up);
        var projectile = pool.Get(prefabComp, context.FirePoint.position, rotation);
        if (projectile == null) return;
        ProjectileLayerUtility.ApplyForContext(projectile.gameObject, context.OwnerContext);

        projectile.gunType = context.GunType;
        projectile.critRate = context.CritRate;
        projectile.critMult = context.CritMultiplier;

        CombatAttributionSnapshot attribution = CombatAttributionSnapshot.FromPhysicalActor(
            context.SourceActor != null ? context.SourceActor.gameObject : null);
        Transform sourceActor = context.SourceActor;
        CombatEventBus combatEventBus = context.CombatEventBus;
        StatusEffectController statusEffectController = context.StatusEffectController;
        if (attribution.HasCredit)
        {
            if (attribution.PhysicalActor != null)
                sourceActor = attribution.PhysicalActor.transform;
            combatEventBus = attribution.CreditedEventBus ?? combatEventBus;
            statusEffectController = attribution.CreditedStatusOwner ?? statusEffectController;
        }

        projectile.Init(context.Config, new ProjectileContext
        {
            sourceActor = sourceActor,
            collisionIgnoreRoot = context.CollisionIgnoreRoot,
            combatEventBus = combatEventBus,
            statusEffectController = statusEffectController,
            dir = direction,
            stats = new ProjectileStats
            {
                damage = context.Damage,
                speed = context.Speed,
                staggerPower = context.StaggerPower
            },
            hitCue = context.HitCue,
            damageSourceId = context.DamageSourceId,
            attackId = context.AttackId,
            chainId = context.PassiveContext.ChainId,
            depth = context.PassiveContext.Depth,
            origin = context.CombatEventBus != null ? context.PassiveContext.Origin : PassiveEventOrigin.External,
            originPassiveId = context.PassiveContext.OriginPassiveId,
            originRuleId = context.PassiveContext.OriginRuleId,
            projectilePrefab = prefabComp,
            useHitZones = true,
            combatMetadata = context.CombatMetadata,
            attribution = attribution,
            preDamageRuntime = context.PreDamageRuntime,
            affixImpactPayload = context.ImpactPayload
        });
    }

    public bool TryResolveProjectilePrefab(GameObject prefab, out Projectile projectileComponent)
    {
        projectileComponent = null;

        if (!prefab)
            return false;

        if (projectilePrefabCache.TryGetValue(prefab, out projectileComponent))
            return projectileComponent != null;

        projectileComponent = prefab.GetComponent<Projectile>();
        projectilePrefabCache[prefab] = projectileComponent;

        if (projectileComponent == null)
            Debug.LogWarning("Projectile prefab is missing Projectile component.", prefab);

        return projectileComponent != null;
    }
}
