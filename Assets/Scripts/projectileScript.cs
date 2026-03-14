using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class BulletTest : MonoBehaviour
{
    [Header("Flight")] public float speed = 20f;
    public float lifetime = 3f;

    private Rigidbody rb;
    private Vector3 initialPosition;


    WeaponType gunType;
    private float baseDamage;
    private float critRate;
    private float critDamage;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        Destroy(gameObject, lifetime);

    }

    public void Initialize(
        Vector3 shooterPosition,
        WeaponType gunType,
        float baseDamage,
        float critRate,
        float critDamage)
    {
        initialPosition = shooterPosition;
        this.gunType = gunType;
        this.baseDamage = baseDamage;
        this.critRate = critRate;
        this.critDamage = critDamage;
    }

    public void SetDirection(Vector3 newDirection)
    {
        if (!rb)
        {
            Debug.LogError("Rigidbody component not found on Bullet.");
            return;
        }

        rb.linearVelocity = newDirection.normalized * speed;
    }

    private float GetActualDistance()
    {
        return Vector3.Distance(initialPosition, transform.position);
    }

    void OnTriggerEnter(Collider other)
    {
        float distance = GetActualDistance();

        if (other.TryGetComponent<IDamageable>(out var damageable))
        {
            float armor = 0f;
            if (other.TryGetComponent<Enemy>(out var enemy))
                armor = enemy.armor;

            float finalDamage = DamageCalculator.CalculateFinalDamage(
                gunType,
                distance,
                baseDamage,
                critRate,
                critDamage,
                armor
            );

            damageable.TakeDamage(finalDamage);
            Destroy(gameObject);
        }

        if (other.CompareTag("Wall"))
        {
            Destroy(gameObject);
        }
    }
}
