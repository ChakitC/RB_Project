using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class Enemy : MonoBehaviour, IDamageable, IHasArmor
{
    [Header("Stats")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] public float armor = 10f;

    float IHasArmor.Armor => armor;
    private float currentHealth;
    public bool IsAlive => currentHealth > 0f;
    public EnemyContext ect;
    public float dropChance;
    
    
    [Header("Health Bar")]
    public GameObject healthBarPrefab;   
    private Slider healthBarSlider;

    void Start()
    {
        currentHealth = maxHealth;

        if (healthBarPrefab != null)
        {
            var healthBarInstance = Instantiate(
                healthBarPrefab,
                transform.position + Vector3.up * 3f,
                Quaternion.identity,
                transform
            );

            healthBarSlider = healthBarInstance.GetComponentInChildren<Slider>();
            if (healthBarSlider != null)
            {
                healthBarSlider.maxValue = maxHealth;
                healthBarSlider.value = currentHealth;
            }
        }
        
        var col = GetComponent<Collider>();
        col.isTrigger = false; 
    }
    public void TakeDamage(float finalDamage)
    {
        if (!IsAlive) return;

        currentHealth = Mathf.Max(0f, currentHealth - Mathf.Max(0f, finalDamage));
        Debug.Log($"Hit Enemy: {finalDamage} | HP: {currentHealth}/{maxHealth}");

        if (healthBarSlider != null)
            healthBarSlider.value = currentHealth;

        if (!IsAlive)
            Die();
    }

    private void Die()
    {
        
        ect.animator.SetBool("IsDead" , true); 
        ect.Collider.enabled = false;
        ect.dropper.DropItem();

        StartCoroutine(DieCoroutine());
    }
 
    private IEnumerator DieCoroutine()
    {
        Debug.Log($"{gameObject.name} has been defeated!");
        yield return new WaitForSeconds(5f);
        Destroy(gameObject);
    }
}
