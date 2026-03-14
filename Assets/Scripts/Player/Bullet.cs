using UnityEngine;
using System.Collections.Generic;
using SingularityGroup.HotReload;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Bullet : MonoBehaviour
{
    [Header("VFX Hit")]
    public GameObject hitVfxPrefab;
    public float vfxScale ;
    
    public float speed = 20f;
    public float lifetime = 3f;

    Rigidbody rb;
    Collider myCol;
    Vector3 spawnPos;

    // จากผู้ยิง
    Transform shooterRoot;
    List<Collider> ignoredCols = new List<Collider>();
    WeaponType gunType;
    float baseDamage, critRate, critMult;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        myCol = GetComponent<Collider>();

        myCol.isTrigger = true; // ใช้ OnTriggerEnter
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        Destroy(gameObject, lifetime);
    }

    public void Initialize(Transform shooter, Vector3 shooterPosition, WeaponType gunType, float baseDamage, float critRate, float critMult)
    {
        this.shooterRoot = shooter.root;
        this.spawnPos = shooterPosition;
        this.gunType = gunType;
        this.baseDamage = baseDamage;
        this.critRate = critRate;
        this.critMult = critMult;

        foreach (var col in shooterRoot.GetComponentsInChildren<Collider>())
        {
            if (col && myCol)
            {
                Physics.IgnoreCollision(myCol, col, true);
                ignoredCols.Add(col);
            }
        }
    }

    public void SetDirection(Vector3 dir) => rb.linearVelocity = dir.normalized * speed;

    float DistanceFromSpawn() => Vector3.Distance(spawnPos, transform.position);

    void OnTriggerEnter(Collider other)
    {
        // กันโดนตัวเอง
        if (shooterRoot && other.transform.root == shooterRoot) return;

        var damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            float armor = 0f;
            
            if (other.GetComponentInParent<IHasArmor>() is IHasArmor target)
            {
                armor = target.Armor;
            }
            
            // if (other.GetComponentInParent<HealthSystem>() is HealthSystem playerHealth) armor = playerHealth.armor;
            // if (other.GetComponentInParent<Enemy>() is Enemy enemy) armor = enemy.Armor; OLD 

            float finalDamage = DamageCalculator.CalculateFinalDamage(
                gunType,
                DistanceFromSpawn(),
                baseDamage,
                critRate,
                critMult,
                armor
            );
            
           //VfxSpawner----------------------------------------------------------------------------------------
            Vector3 hitPos = transform.position;
            Vector3 hitNormal = -transform.forward;
            if(VfxSpawner.Instance == null){ Debug.Log("VfxSpawner.Instance Missing");return;}
            VfxSpawner.Instance.SpawnVfx(hitVfxPrefab, hitPos, hitNormal , 1.0f,vfxScale);
            //-------------------------------------------------------------------------------------------------
            
            
            Debug.Log("Weapon =" + gunType);
            damageable.TakeDamage(finalDamage);
            Destroy(gameObject);
        }
        else if (other.CompareTag("Wall"))
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        // คืน IgnoreCollision (กรณีใช้ pooling จะต้องเรียกตอนคืน object)
        foreach (var col in ignoredCols)
            if (col && myCol) Physics.IgnoreCollision(myCol, col, false);
        ignoredCols.Clear();
    }

   
}
