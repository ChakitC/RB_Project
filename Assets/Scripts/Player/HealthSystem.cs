using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-110)]
public class HealthSystem : MonoBehaviour, IDamageable, IHasArmor , IInteractable, IHoldInteractable
{
    
    [Header("References")]
    public CharacteContext CTX;
    [SerializeField] private StatsHub statsHub;
    
    [Header("Runtime")]
    [SerializeField] private float reviveTime = 2.0f;
    
    

    [Header("Runtime")]
    public float maximumHealth;
    public float currentHealth;
    public float DownTime = 15f;
    private Coroutine downRoutine;
    private float downTimeDefault;
    
    [SerializeField] private float cachedArmor; // ไว้โชว์/แคช แต่ค่าใช้งานจริงจะอ่านจาก hub
    private bool invincible;
    
    private Coroutine dieRoutine;
    float IHasArmor.Armor => GetFinalArmor();

    [Header("Health Bar")]
    public GameObject healthBarPrefab;
    private Slider healthBarSlider;
    public float healthBarHight = 2f;

    [Header("Hub Sync")]
    [Tooltip("ถ้า MaxHP/Armor เปลี่ยนระหว่างเล่น (เช่น บัฟ/ดีบัฟ) ให้ซิงก์ตาม Hub")]
    [SerializeField] private bool autoRefreshFromHub = true;

    [Tooltip("ถ้า MaxHP เปลี่ยน ให้คง %HP เดิม เช่น 50/100 -> 75/150")]
    [SerializeField] private bool keepHealthPercentWhenMaxChanges = true;

    [SerializeField] private float refreshIntervalSeconds = 0.2f;
    private float refreshTimer;

    public event Action CharacterDead;
    public event Action CharacterDown;
    public event Action CharacterRevive;
     public event Action ReturnbaseUI;
    
    
     public bool IsAlive => currentHealth > 0f;
     public bool IsDown => currentHealth <= 0f && DownTime > 0f;
     public bool IsDead => currentHealth <= 0f && DownTime <= 0f;
    
    void Start()
    {
        if (!CTX) CTX = GetComponent<CharacteContext>();
        if (!statsHub) statsHub = GetComponent<StatsHub>();
        downTimeDefault = DownTime;
        if (!CTX)
        {
            Debug.LogError("HealthSystem: ไม่มี CharactorConText บน GameObject นี้เลย");
            return;
        }

        // init จาก hub
        InitializeFromHub(resetCurrentToMax: true);

        Debug.Log($"HealthSystem Init: HP={currentHealth}/{maximumHealth}, Armor={GetFinalArmor()}");
        CreateHealthBarIfNeeded();
        ApplyHealthBarValues();
        CTX.UIManager?.UpdateHPText(currentHealth, maximumHealth);
    }

    void Update()
    {
        
        if (!autoRefreshFromHub || !statsHub) return;

        refreshTimer += Time.deltaTime;
        if (refreshTimer >= refreshIntervalSeconds)
        {
            refreshTimer = 0f;
            RefreshFromHub();
        }
    }
    
    /// <summary>
    /// ///// InterfaceImprement ////////////////////
    /// </summary>
    
    public int Priority => 100;
    public float HoldDuration => reviveTime;
    public string GetPrompt(Interactor i) => CanInteract(i) ? "Hold [F] to Revive" : "Can't revive";
    public bool CanInteract(Interactor i)
    {
        if (i == null) return false;
        if (CTX == null) return false;
        if (CTX.stateHub == null) return false;
        if (CTX.stateHub.LifeSM == null) return false;

        // เป้าหมายต้อง Down ก่อน
        if (CTX.stateHub.LifeSM.CurrentId != LifeStateId.Down)
            return false;

        // ห้ามชุบตัวเอง
        if (i.OwnerContext == CTX)
            return false;

        return true;
    }
    public void Interact(Interactor i)
    {
        Debug.Log($"Interactor '{i.name}' interacted with '{name}'");
        Revive();
    }
    public void BeginHold(Interactor i) { /* เปิด UI/progress ได้ */           Debug.Log("BeginHold");                }
    public void CancelHold(Interactor i) { /* ปิด UI/progress ได้ */             Debug.Log("CancelHold");                  }
    public void CompleteHold(Interactor i) { /* จะให้เล่นเสียง/FX ก็ใส่ตรงนี้ */        Debug.Log("CompleteHold");                          }
    
    
    /////////////////////////////////////////////////////////
    

    void CreateHealthBarIfNeeded()
    {
        if (!healthBarPrefab) return;

        var healthBarInstance = Instantiate(
            healthBarPrefab,
            transform.position + Vector3.up * healthBarHight,
            Quaternion.identity,
            transform
        );

        healthBarSlider = healthBarInstance.GetComponentInChildren<Slider>();
    }

    void ApplyHealthBarValues()
    {
        if (!healthBarSlider) return;

        healthBarSlider.maxValue = maximumHealth;
        healthBarSlider.value = currentHealth;
    }

    void InitializeFromHub(bool resetCurrentToMax)
    {
        // อ่านค่าจาก Hub ก่อน ถ้าไม่มี Hub ให้ fallback
        float newMaximumHealth = statsHub ? statsHub.GetMaximumHealth() : CTX.basemaxHealth;
        float newArmor = statsHub ? statsHub.GetArmor() : CTX.basearmor;

        maximumHealth = Mathf.Max(1f, newMaximumHealth);
        cachedArmor = Mathf.Max(0f, newArmor);

        if (resetCurrentToMax)
        {
            currentHealth = maximumHealth;
        }
        else
        {
            currentHealth = Mathf.Clamp(currentHealth, 0f, maximumHealth);
        }
    }

    void RefreshFromHub()
    {
        float oldMaximumHealth = maximumHealth;
        float oldArmor = cachedArmor;

        float newMaximumHealth = statsHub.GetMaximumHealth();
        float newArmor = statsHub.GetArmor();

        newMaximumHealth = Mathf.Max(1f, newMaximumHealth);
        newArmor = Mathf.Max(0f, newArmor);

        bool maximumHealthChanged = !Mathf.Approximately(oldMaximumHealth, newMaximumHealth);
        bool armorChanged = !Mathf.Approximately(oldArmor, newArmor);

        if (!maximumHealthChanged && !armorChanged) return;

        // MaxHP เปลี่ยน: เลือกว่าจะคง % เดิมไหม
        if (maximumHealthChanged)
        {
            float healthPercent = (oldMaximumHealth > 0f) ? (currentHealth / oldMaximumHealth) : 1f;
            maximumHealth = newMaximumHealth;

            if (keepHealthPercentWhenMaxChanges)
                currentHealth = Mathf.Clamp(healthPercent * maximumHealth, 0f, maximumHealth);
            else
                currentHealth = Mathf.Clamp(currentHealth, 0f, maximumHealth);
        }

        if (armorChanged)
        {
            cachedArmor = newArmor;
        }

        ApplyHealthBarValues();
        CTX.UIManager?.UpdateHPText(currentHealth, maximumHealth);
    }

    float GetFinalArmor()
    {
        // ค่าใช้งานจริงอ่านจาก Hub (ถ้ามี) เพื่อให้บัฟ/ดีบัฟสะท้อนทันที
        if (statsHub) return Mathf.Max(0f, statsHub.GetArmor());
        return Mathf.Max(0f, cachedArmor);
    }

    public void SetInvincible(bool value) => invincible = value;

    public void TakeDamage(float damage)
    {
        Debug.Log($"{name} TakeDamage({damage}) hpBefore={currentHealth}");

        if (invincible) return;
        if (CTX == null || CTX.stateHub == null || CTX.stateHub.LifeSM == null) return;

        // กันไม่ให้โดนซ้ำตอน Down/Dead
        if (CTX.stateHub.LifeSM.CurrentId == LifeStateId.Down ||
            CTX.stateHub.LifeSM.CurrentId == LifeStateId.Dead)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - Mathf.Max(0f, damage));

        CTX.UIManager?.UpdateHPText(currentHealth, maximumHealth);

        if (healthBarSlider != null)
            healthBarSlider.value = currentHealth;

        Debug.Log($"{name} hpAfter={currentHealth}");

        if (currentHealth <= 0f)
        {
            Debug.Log($"{name} HP <= 0 -> Down()");
            Down();
        }
    }

    public void Revive()
    {
        
        Debug.Log($"Revive called on '{name}'");
        
        if (!CTX.stateHub.Isdown) return;

        if (downRoutine != null)
        {
            StopCoroutine(downRoutine);
            downRoutine = null;
        }

        if (dieRoutine != null)
        {
            StopCoroutine(dieRoutine);
            dieRoutine = null;
        }

        DownTime = downTimeDefault;
        currentHealth = Mathf.Clamp(maximumHealth * 0.25f, 1f, maximumHealth);

        if (CTX.cc) CTX.cc.enabled = true;

        ApplyHealthBarValues();
        CTX.UIManager?.UpdateHPText(currentHealth, maximumHealth);

        CTX.stateHub.LifeSM.TryChange(LifeStateId.Alive);
        CharacterRevive?.Invoke();
    }
    void Down()
    {
        DownTime = downTimeDefault;

        CTX.stateHub.LifeSM.TryChange(LifeStateId.Down);
        CharacterDown?.Invoke();

        if (downRoutine != null) StopCoroutine(downRoutine);
        downRoutine = StartCoroutine(DownCoroutine());
        Debug.Log($"{name} has been Down!");
    }

    public virtual void Die()
    {
        if (dieRoutine != null) return;

        CTX.stateHub.LifeSM.TryChange(LifeStateId.Dead);
        CharacterDead?.Invoke();

        if (CTX.cc) CTX.cc.enabled = false;

        dieRoutine = StartCoroutine(DieCoroutine());
    }

    IEnumerator DieCoroutine()
    {
        yield return new WaitForSeconds(5.0f);
        ReturnbaseUI?.Invoke();
        Destroy(gameObject);
    }
    
    IEnumerator DownCoroutine()
    {
        while (DownTime > 0f)
        {
            DownTime = Mathf.Max(0f, DownTime - Time.deltaTime);
            yield return null; 
        }
        Die();
    }



}
