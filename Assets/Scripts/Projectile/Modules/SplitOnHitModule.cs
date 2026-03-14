using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Projectile Modules/Split On Hit")]
public class SplitOnHitModule : ProjectileModule
{
    public ProjectileConfig childConfig;

    public int childCount = 3;
    public float spreadAngleDeg = 25f;

    public float childDamageMultiplier = 0.6f;
    public float childSpeedMultiplier = 1.0f;

    public bool onlyWhenFinalHit = true;
    public bool destroyParent = true;

    class State : IProjectileModuleState { public bool done; }
    public override IProjectileModuleState CreateState() => new State();

    public override void OnHit(Projectile p, ProjectileContext ctx, IProjectileModuleState st,
        in ProjectileHitInfo hit, IDamageable target)
    {
        var s = (State)st;
        if (s.done) return;
        
        Transform ignoreRoot = null;
        if (target is Component tc) ignoreRoot = tc.transform.root;

        if (childConfig == null || ctx.projectilePrefab == null) return;
        if (onlyWhenFinalHit && !p.RequestedDespawn) return;

        s.done = true;

        Vector3 baseDir = ctx.dir;
        int n = Mathf.Max(1, childCount);

        float start = -spreadAngleDeg * 0.5f;
        float step = (n == 1) ? 0f : spreadAngleDeg / (n - 1);
        
        Vector3 spawnPos = hit.point + baseDir * 0.1f;

        for (int i = 0; i < n; i++)
        {
            float ang = start + step * i;
            Vector3 d = Quaternion.AngleAxis(ang, Vector3.up) * baseDir;

            var child = p.SpawnChild(childConfig, spawnPos, d, childDamageMultiplier, childSpeedMultiplier);

            if (ignoreRoot != null)
                child.IgnoreRoot(ignoreRoot);
        }

        if (destroyParent) p.RequestDespawn();
    }
}