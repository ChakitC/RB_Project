using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Projectile Modules/Split On Hit")]
public class SplitOnHitModule : ProjectileModule
{
    /// <summary>
    /// Upper bound on children per split. Authoring above this is a mistake, not a design: a
    /// three-generation split at 64 children per hop is a quarter of a million projectiles.
    /// </summary>
    public const int MaxChildCount = 32;

    public ProjectileConfig childConfig;

    [Range(1, MaxChildCount)]
    public int childCount = 3;
    public float spreadAngleDeg = 25f;

    [Tooltip("How many split hops are allowed from the originally fired shot. 1 means the shot " +
             "splits once and its children never split again. Capped by Projectile.AbsoluteMaxSplitGeneration.")]
    [Range(0, Projectile.AbsoluteMaxSplitGeneration)]
    public int maxSplitGenerations = 1;

    public float childDamageMultiplier = 0.6f;
    public float childSpeedMultiplier = 1.0f;

    public bool onlyWhenFinalHit = true;
    public bool destroyParent = true;

    class State : IProjectileModuleState { public bool done; }
    public override IProjectileModuleState CreateState() => new State();

    public int ResolvedChildCount => Mathf.Clamp(childCount, 1, MaxChildCount);

    public int ResolvedMaxSplitGenerations =>
        Mathf.Clamp(maxSplitGenerations, 0, Projectile.AbsoluteMaxSplitGeneration);

    void OnValidate()
    {
        childCount = Mathf.Clamp(childCount, 1, MaxChildCount);
        maxSplitGenerations = Mathf.Clamp(maxSplitGenerations, 0, Projectile.AbsoluteMaxSplitGeneration);
    }

    public override void OnHit(Projectile p, ProjectileContext ctx, IProjectileModuleState st,
        in ProjectileHitInfo hit, IDamageable target)
    {
        var s = (State)st;
        if (s.done) return;

        Transform ignoreRoot = null;
        if (target is Component tc) ignoreRoot = tc.transform.root;

        if (childConfig == null || ctx.projectilePrefab == null) return;
        if (onlyWhenFinalHit && !p.RequestedDespawn) return;

        // Generation budget, not chain depth. A childConfig that points back at a config carrying
        // this module would otherwise split forever.
        if (!p.CanSpawnSplitChild(ResolvedMaxSplitGenerations))
        {
            s.done = true;
            if (destroyParent) p.RequestDespawn();
            return;
        }

        s.done = true;

        Vector3 baseDir = ctx.dir;
        int n = ResolvedChildCount;

        float start = -spreadAngleDeg * 0.5f;
        float step = (n == 1) ? 0f : spreadAngleDeg / (n - 1);

        Vector3 spawnPos = hit.ResolvePoint(p.transform.position) + baseDir * 0.1f;

        for (int i = 0; i < n; i++)
        {
            float ang = start + step * i;
            Vector3 d = Quaternion.AngleAxis(ang, Vector3.up) * baseDir;

            // The authored budget travels with the child so a permissive childConfig deeper in the
            // chain cannot hand itself more generations than this module allowed.
            var child = p.SpawnChild(
                childConfig, spawnPos, d, childDamageMultiplier, childSpeedMultiplier,
                ResolvedMaxSplitGenerations);
            if (child == null) continue;

            if (ignoreRoot != null)
                child.IgnoreRoot(ignoreRoot);
        }

        if (destroyParent) p.RequestDespawn();
    }
}
