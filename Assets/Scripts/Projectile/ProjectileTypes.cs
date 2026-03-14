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
    public Vector3 dir;                  // normalized
    public ProjectileStats stats;

    // ใช้สำหรับแตกย่อย spawn ลูก
    public Projectile  projectilePrefab;
}