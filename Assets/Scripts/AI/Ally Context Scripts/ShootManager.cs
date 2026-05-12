using UnityEngine;

/// <summary>
/// ระบบกลางสำหรับยิงกระสุน ทุกคนในเกมเรียกมาที่นี่ตัวเดียว
/// </summary>
public class ShootManager : MonoBehaviour
{
    public static ShootManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ShootManager] มี Instance ซ้ำในซีน ลบทิ้งตัวหลัง", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
      
        // DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// ยิงกระสุน 1 นัด
    /// </summary>
    public void Shoot(
        Transform shooter,
        Vector3 origin,
        Vector3 direction,
        GameObject bulletPrefab,
        WeaponType weaponType,
        float damage,
        float critChance,
        float critMultiplier
    )
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("[ShootManager] bulletPrefab ว่าง", this);
            return;
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            Debug.LogWarning("[ShootManager] direction เป็นศูนย์", this);
            return;
        }

        direction.Normalize();
        Quaternion rot = Quaternion.LookRotation(direction);

        GameObject bulletObj = Object.Instantiate(bulletPrefab, origin, rot);
        ProjectileLayerUtility.ApplyForSource(bulletObj, shooter);

        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet != null)
        {
            // ตรงนี้ปรับให้ตรงกับ Bullet.Initialize ของโปรเจกต์จริง ๆ
            bullet.Initialize(
                shooter,
                origin,
                weaponType,
                damage,
                critChance,
                critMultiplier
            );

            bullet.SetDirection(direction);
        }
        else
        {
            Debug.LogWarning("[ShootManager] Bullet prefab ไม่มีคอมโพเนนต์ Bullet", bulletObj);
        }
    }
}
