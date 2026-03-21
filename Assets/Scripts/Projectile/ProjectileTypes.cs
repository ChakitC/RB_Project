using UnityEngine;

public readonly struct ProjectileHitInfo
{
    public readonly Vector3 point;
    public readonly Vector3 normal;
    public readonly Collider collider;

    public ProjectileHitInfo(Vector3 point, Vector3 normal, Collider collider)
    {
        this.point = point;
        this.normal = normal;
        this.collider = collider;
    }
}

[System.Serializable]
public struct ProjectileStats
{
    public float damage;
    public float speed;
}

public struct ProjectileContext
{
    public Transform owner;
    public Vector3 dir;
    public ProjectileStats stats;
    public AudioCue hitCue;
    public string sourceId;
    public string attackId;
    public ulong chainId;
    public int depth;
    public PassiveEventOrigin origin;
    public string originPassiveId;
    public string originRuleId;
    public Projectile projectilePrefab;
}
