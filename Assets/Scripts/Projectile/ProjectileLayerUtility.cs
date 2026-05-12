using UnityEngine;

public static class ProjectileLayerUtility
{
    const string PlayerBulletLayerName = "PlayerBullet";
    const string EnemyBulletLayerName = "EnemyBullet";
    const int UnresolvedLayer = -2;

    static int playerBulletLayer = UnresolvedLayer;
    static int enemyBulletLayer = UnresolvedLayer;
    static bool warnedMissingPlayerLayer;
    static bool warnedMissingEnemyLayer;

    public static void ApplyForContext(GameObject projectileObject, CharacteContext ctx)
    {
        if (projectileObject == null || ctx == null)
            return;

        if (TryResolveLayer(ctx.TargetIdentity, out int layer))
            ApplyLayerRecursively(projectileObject, layer);
    }

    public static void ApplyForSkillUser(GameObject projectileObject, ISkillUser user)
    {
        ApplyForContext(projectileObject, ResolveContext(user));
    }

    public static void ApplyForSource(GameObject projectileObject, Transform source)
    {
        ApplyForContext(projectileObject, ResolveContext(source));
    }

    public static void InheritLayer(GameObject projectileObject, GameObject sourceObject)
    {
        if (projectileObject == null || sourceObject == null)
            return;

        ApplyLayerRecursively(projectileObject, sourceObject.layer);
    }

    public static CharacteContext ResolveContext(ISkillUser user)
    {
        if (user == null)
            return null;

        if (user is Component component)
        {
            CharacteContext ctx = ResolveContext(component.transform);
            if (ctx != null)
                return ctx;

            ctx = component.GetComponentInChildren<CharacteContext>(true);
            if (ctx != null)
                return ctx;
        }

        if (user.StatsHub != null)
        {
            CharacteContext ctx = ResolveContext(user.StatsHub.transform);
            if (ctx != null)
                return ctx;
        }

        return ResolveContext(user.CastOrigin);
    }

    static CharacteContext ResolveContext(Transform source)
    {
        if (source == null)
            return null;

        CharacteContext ctx = source.GetComponent<CharacteContext>();
        if (ctx != null)
            return ctx;

        return source.GetComponentInParent<CharacteContext>();
    }

    static bool TryResolveLayer(AITargetIdentity identity, out int layer)
    {
        switch (identity)
        {
            case AITargetIdentity.Player:
            case AITargetIdentity.Companion:
                return TryGetLayer(PlayerBulletLayerName, ref playerBulletLayer, ref warnedMissingPlayerLayer, out layer);

            case AITargetIdentity.Enemy:
                return TryGetLayer(EnemyBulletLayerName, ref enemyBulletLayer, ref warnedMissingEnemyLayer, out layer);

            default:
                layer = -1;
                return false;
        }
    }

    static bool TryGetLayer(string layerName, ref int cachedLayer, ref bool warnedMissingLayer, out int layer)
    {
        if (cachedLayer == UnresolvedLayer)
            cachedLayer = LayerMask.NameToLayer(layerName);

        layer = cachedLayer;
        if (layer >= 0)
            return true;

        if (!warnedMissingLayer)
        {
            Debug.LogWarning($"Projectile layer '{layerName}' is not defined in Project Settings.");
            warnedMissingLayer = true;
        }

        return false;
    }

    static void ApplyLayerRecursively(GameObject root, int layer)
    {
        if (root == null || layer < 0 || layer > 31)
            return;

        ApplyLayerRecursively(root.transform, layer);
    }

    static void ApplyLayerRecursively(Transform node, int layer)
    {
        node.gameObject.layer = layer;

        for (int i = 0; i < node.childCount; i++)
            ApplyLayerRecursively(node.GetChild(i), layer);
    }
}
