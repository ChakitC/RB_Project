using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-110)]
public class HealthSystem : MonoBehaviour, IDamageable, IDamageableWithContext, IHasArmor, IInteractable, IHoldInteractable
{
    [Header("References")]
    public CharacteContext CTX;
    [SerializeField] private StatsHub statsHub;
    [SerializeField] private StatusEffectController statusEffectController;
    [SerializeField] private CombatEventBus combatEventBus;

    [Header("Runtime")]
    [SerializeField] private float reviveTime = 2.0f;

    [Header("Runtime")]
    public float maximumHealth;
    public float currentHealth;
    public float DownTime = 15f;
    Coroutine downRoutine;
    float downTimeDefault;

    [SerializeField] private float cachedArmor;
    bool invincible;

    Coroutine dieRoutine;
    float IHasArmor.Armor => GetFinalArmor();

    [Header("Health Bar")]
    public GameObject healthBarPrefab;
    Slider healthBarSlider;
    public float healthBarHight = 2f;

    [Header("Hub Sync")]
    [SerializeField] private bool autoRefreshFromHub = true;
    [SerializeField] private bool keepHealthPercentWhenMaxChanges = true;
    [SerializeField] private float refreshIntervalSeconds = 0.2f;
    float refreshTimer;

    public event Action CharacterDead;
    public event Action CharacterDown;
    public event Action CharacterRevive;
    public event Action ReturnbaseUI;
    public event Action<float, GameObject> DamageTaken;

    public bool IsAlive => currentHealth > 0f;
    public bool IsDown => currentHealth <= 0f && DownTime > 0f;
    public bool IsDead => currentHealth <= 0f && DownTime <= 0f;

    void Start()
    {
        if (!CTX) CTX = GetComponent<CharacteContext>();
        if (!statsHub) statsHub = GetComponent<StatsHub>();
        if (!statusEffectController) statusEffectController = GetComponent<StatusEffectController>();
        if (!combatEventBus) combatEventBus = GetComponent<CombatEventBus>();

        downTimeDefault = DownTime;
        if (!CTX)
        {
            Debug.LogError("HealthSystem: missing CharacteContext", this);
            return;
        }

        InitializeFromHub(resetCurrentToMax: true);
        CreateHealthBarIfNeeded();
        ApplyHealthBarValues();
        CTX.UIManager?.UpdateHPText(currentHealth, maximumHealth);
    }

    void Update()
    {
        if (!autoRefreshFromHub || !statsHub)
            return;

        refreshTimer += Time.deltaTime;
        if (refreshTimer >= refreshIntervalSeconds)
        {
            refreshTimer = 0f;
            RefreshFromHub();
        }
    }

    public int Priority => 100;
    public float HoldDuration => reviveTime;
    public string GetPrompt(Interactor i) => CanInteract(i) ? "Hold [F] to Revive" : "Can't revive";

    public bool CanInteract(Interactor i)
    {
        if (i == null || CTX == null || CTX.stateHub == null || CTX.stateHub.LifeSM == null)
            return false;

        if (CTX.stateHub.LifeSM.CurrentId != LifeStateId.Down)
            return false;

        return i.OwnerContext != CTX;
    }

    public void Interact(Interactor i)
    {
        Revive();
    }

    public void BeginHold(Interactor i) { }
    public void CancelHold(Interactor i) { }
    public void CompleteHold(Interactor i) { }

    void CreateHealthBarIfNeeded()
    {
        if (!healthBarPrefab)
            return;

        var healthBarInstance = Instantiate(
            healthBarPrefab,
            transform.position + Vector3.up * healthBarHight,
            Quaternion.identity,
            transform);

        healthBarSlider = healthBarInstance.GetComponentInChildren<Slider>();
    }

    void ApplyHealthBarValues()
    {
        if (!healthBarSlider)
            return;

        healthBarSlider.maxValue = maximumHealth;
        healthBarSlider.value = currentHealth;
    }

    void InitializeFromHub(bool resetCurrentToMax)
    {
        float newMaximumHealth = statsHub ? statsHub.GetMaximumHealth() : CTX.basemaxHealth;
        float newArmor = statsHub ? statsHub.GetArmor() : CTX.basearmor;

        maximumHealth = Mathf.Max(1f, newMaximumHealth);
        cachedArmor = Mathf.Max(0f, newArmor);

        if (resetCurrentToMax)
            currentHealth = maximumHealth;
        else
            currentHealth = Mathf.Clamp(currentHealth, 0f, maximumHealth);
    }

    void RefreshFromHub()
    {
        float oldMaximumHealth = maximumHealth;
        float oldArmor = cachedArmor;

        float newMaximumHealth = Mathf.Max(1f, statsHub.GetMaximumHealth());
        float newArmor = Mathf.Max(0f, statsHub.GetArmor());

        bool maximumHealthChanged = !Mathf.Approximately(oldMaximumHealth, newMaximumHealth);
        bool armorChanged = !Mathf.Approximately(oldArmor, newArmor);

        if (!maximumHealthChanged && !armorChanged)
            return;

        if (maximumHealthChanged)
        {
            float healthPercent = oldMaximumHealth > 0f ? currentHealth / oldMaximumHealth : 1f;
            maximumHealth = newMaximumHealth;

            if (keepHealthPercentWhenMaxChanges)
                currentHealth = Mathf.Clamp(healthPercent * maximumHealth, 0f, maximumHealth);
            else
                currentHealth = Mathf.Clamp(currentHealth, 0f, maximumHealth);
        }

        if (armorChanged)
            cachedArmor = newArmor;

        ApplyHealthBarValues();
        CTX.UIManager?.UpdateHPText(currentHealth, maximumHealth);
    }

    float GetFinalArmor()
    {
        if (statsHub)
            return Mathf.Max(0f, statsHub.GetArmor());

        return Mathf.Max(0f, cachedArmor);
    }

    public void SetInvincible(bool value) => invincible = value;

    public bool CanHeal(float amount)
    {
        if (!float.IsFinite(amount) || amount <= 0f)
            return false;

        if (CTX == null || CTX.stateHub == null || CTX.stateHub.LifeSM == null)
            return false;

        if (CTX.stateHub.LifeSM.CurrentId != LifeStateId.Alive)
            return false;

        return currentHealth < maximumHealth;
    }

    public bool Heal(float amount)
    {
        if (!CanHeal(amount))
            return false;

        float previous = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maximumHealth);
        if (currentHealth <= previous)
            return false;

        ApplyHealthBarValues();
        CTX.UIManager?.UpdateHPText(currentHealth, maximumHealth);
        return true;
    }

    public virtual void TakeDamage(float damage)
    {
        ApplyDamageInternal(Mathf.Max(0f, damage), null, false, default);
    }

    public virtual void TakeDamage(in DamageContext damageContext)
    {
        ApplyDamageInternal(Mathf.Max(0f, damageContext.Damage), damageContext.Attacker, true, damageContext);
    }

    protected void ApplyDamage(float damage, GameObject attacker)
    {
        ApplyDamageInternal(Mathf.Max(0f, damage), attacker, false, default);
    }

    protected void ApplyDamage(in DamageContext damageContext)
    {
        ApplyDamageInternal(Mathf.Max(0f, damageContext.Damage), damageContext.Attacker, true, damageContext);
    }

    void ApplyDamageInternal(float damage, GameObject attacker, bool hasContext, in DamageContext damageContext)
    {
        if (damage <= 0f)
            return;

        if (CTX == null || CTX.stateHub == null || CTX.stateHub.LifeSM == null)
            return;

        if (CTX.stateHub.LifeSM.CurrentId == LifeStateId.Down ||
            CTX.stateHub.LifeSM.CurrentId == LifeStateId.Dead)
            return;

        if (invincible)
        {
            var preventedContext = PublishDamageEvent(PassiveEventType.DamagePrevented, damage, attacker, hasContext, damageContext);
            CTX.DashSystem?.TryRegisterPerfectDodge(in preventedContext);
            return;
        }

        float healthBefore = currentHealth;
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        float appliedDamage = Mathf.Max(0f, healthBefore - currentHealth);

        statusEffectController?.NotifyTrigger(EffectTriggerType.OnTakeDamage, attacker);
        PublishDamageEvent(PassiveEventType.TakeDamage, appliedDamage, attacker, hasContext, damageContext);
        DamageTaken?.Invoke(appliedDamage, attacker);

        CTX.UIManager?.UpdateHPText(currentHealth, maximumHealth);
        if (healthBarSlider != null)
            healthBarSlider.value = currentHealth;

        if (currentHealth <= 0f)
            Down();
    }

    PassiveEventContext PublishDamageEvent(
        PassiveEventType eventType,
        float value,
        GameObject attacker,
        bool hasContext,
        in DamageContext damageContext)
    {
        if (combatEventBus == null || eventType == PassiveEventType.None)
            return default;

        GameObject actorObject = gameObject;

        if (hasContext)
        {
            if (damageContext.ChainId != 0)
            {
                var parent = new PassiveEventContext(
                    PassiveEventType.None,
                    actorObject,
                    attacker,
                    actorObject,
                    damageContext.SourceId,
                    damageContext.AttackId,
                    value,
                    Time.timeAsDouble,
                    damageContext.ChainId,
                    damageContext.Depth,
                    damageContext.Origin,
                    damageContext.OriginPassiveId,
                    damageContext.OriginRuleId);

                var child = combatEventBus.CreateChildContext(
                    parent,
                    eventType,
                    attacker,
                    actorObject,
                    damageContext.SourceId,
                    damageContext.AttackId,
                    value,
                    damageContext.Origin,
                    damageContext.OriginPassiveId,
                    damageContext.OriginRuleId);

                combatEventBus.Publish(child);
                return child;
            }

            var external = combatEventBus.CreateExternalContext(
                eventType,
                attacker,
                actorObject,
                damageContext.SourceId,
                damageContext.AttackId,
                value,
                damageContext.Origin,
                damageContext.OriginPassiveId,
                damageContext.OriginRuleId);

            combatEventBus.Publish(external);
            return external;
        }

        string fallbackSourceId = attacker != null ? $"attacker:{attacker.name}" : "damage";
        var context = combatEventBus.CreateExternalContext(
            eventType,
            attacker,
            actorObject,
            fallbackSourceId,
            null,
            value,
            PassiveEventOrigin.External);

        combatEventBus.Publish(context);
        return context;
    }

    public void Revive()
    {
        if (!CTX.stateHub.Isdown)
            return;

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

        if (CTX.cc)
            CTX.cc.enabled = true;

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

        if (downRoutine != null)
            StopCoroutine(downRoutine);

        downRoutine = StartCoroutine(DownCoroutine());
    }

    public virtual void Die()
    {
        if (dieRoutine != null)
            return;

        CTX.stateHub.LifeSM.TryChange(LifeStateId.Dead);
        CharacterDead?.Invoke();

        if (CTX.cc)
            CTX.cc.enabled = false;

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
