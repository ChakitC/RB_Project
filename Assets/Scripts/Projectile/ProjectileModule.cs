using UnityEngine;

public interface IProjectileModuleState { }

public abstract class ProjectileModule : ScriptableObject
{
    public virtual IProjectileModuleState CreateState() => null;

    public virtual void OnSpawn(Projectile p, ProjectileContext ctx, IProjectileModuleState state) { }
    public virtual void Tick(Projectile p, ProjectileContext ctx, IProjectileModuleState state, float dt) { }
    public virtual void OnHit(Projectile p, ProjectileContext ctx, IProjectileModuleState state,
        in ProjectileHitInfo hit, IDamageable target) { }
    public virtual void OnDamageApplied(Projectile p, ProjectileContext ctx, IProjectileModuleState state,
        in ProjectileHitInfo hit, IDamageable target) { }
    public virtual void OnExpire(Projectile p, ProjectileContext ctx, IProjectileModuleState state) { }
}
