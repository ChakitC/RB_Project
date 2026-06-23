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
    Vector3 moveDirection = Vector3.forward;
    float age;

    // จากผู้ยิง
    Transform shooterRoot;
    List<Collider> ignoredCols = new List<Collider>();
    WeaponType gunType;
    float baseDamage, critRate, critMult, staggerPower;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        myCol = GetComponent<Collider>();

        myCol.isTrigger = true; // ใช้ OnTriggerEnter
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

    }

    public void Initialize(
        Transform shooter,
        Vector3 shooterPosition,
        WeaponType gunType,
        float baseDamage,
        float critRate,
        float critMult,
        float staggerPower = 0f)
    {
        this.shooterRoot = shooter.root;
        this.spawnPos = shooterPosition;
        this.gunType = gunType;
        this.baseDamage = baseDamage;
        this.critRate = critRate;
        this.critMult = critMult;
        this.staggerPower = staggerPower;
        age = 0f;

        foreach (var col in shooterRoot.GetComponentsInChildren<Collider>())
        {
            if (col && myCol)
            {
                Physics.IgnoreCollision(myCol, col, true);
                ignoredCols.Add(col);
            }
        }
    }

    public void SetDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;

        moveDirection = dir.normalized;
        ApplyVelocity();
    }

    void FixedUpdate()
    {
        float worldScale = TimeSlowManager.Instance.WorldTimeScale;
        age += Time.fixedDeltaTime * worldScale;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        ApplyVelocity();
    }

    void ApplyVelocity()
    {
        rb.linearVelocity = moveDirection * speed * TimeSlowManager.Instance.WorldTimeScale;
    }

    float DistanceFromSpawn() => Vector3.Distance(spawnPos, transform.position);

    void OnTriggerEnter(Collider other)
    {
        if (MeleeController.IsCombatOnlyHitbox(other))
            return;
        // กันโดนตัวเอง
        if (shooterRoot && other.transform.root == shooterRoot) return;

        var damageable = DamageableResolver.ResolveFrom(other);
        if (damageable != null)
        {
            float armor = 0f;
            
            if (damageable is IHasArmor target)
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
            damageable.TakeDamage(
                finalDamage,
                shooterRoot != null ? shooterRoot.gameObject : null,
                damageSourceId: $"bullet:{gunType}",
                stagger: new StaggerPayload(staggerPower, 1f, $"bullet:{gunType}"));
            Destroy(gameObject);
        }
        else if (ProjectileLayerUtility.IsWall(other))
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
