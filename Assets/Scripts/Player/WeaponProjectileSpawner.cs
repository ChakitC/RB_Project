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

        var projectile = Object.Instantiate(prefabComp, context.FirePoint.position, context.FirePoint.rotation);
        ProjectileLayerUtility.ApplyForContext(projectile.gameObject, context.OwnerContext);

        projectile.gunType = context.GunType;
        projectile.critRate = context.CritRate;
        projectile.critMult = context.CritMultiplier;

        projectile.Init(context.Config, new ProjectileContext
        {
            sourceActor = context.SourceActor,
            collisionIgnoreRoot = context.CollisionIgnoreRoot,
            combatEventBus = context.CombatEventBus,
            statusEffectController = context.StatusEffectController,
            dir = context.FirePoint.forward,
            stats = new ProjectileStats
            {
                damage = context.Damage,
                speed = context.Speed,
                staggerPower = context.StaggerPower
            },
            hitCue = context.HitCue,
            sourceId = context.SourceId,
            attackId = context.AttackId,
            chainId = context.PassiveContext.ChainId,
            depth = context.PassiveContext.Depth,
            origin = context.CombatEventBus != null ? context.PassiveContext.Origin : PassiveEventOrigin.External,
            originPassiveId = context.PassiveContext.OriginPassiveId,
            originRuleId = context.PassiveContext.OriginRuleId,
            projectilePrefab = prefabComp
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
