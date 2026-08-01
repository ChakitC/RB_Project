using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-110)]
public class HealthSystem : MonoBehaviour, IDamageable, IHasArmor, IInteractable, IHoldInteractable
{
    [Header("References")]
    public CharacteContext CTX;
    [SerializeField] private StatsHub statsHub;
    [SerializeField] private StatusEffectController statusEffectController;
    [SerializeField] private CombatEventBus combatEventBus;
    [SerializeField] private HitZoneDamageProfileSO hitZoneDamageProfile;

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
    readonly HashSet<int> _invincibilityTokens = new();
    int _nextInvincibilityToken = 1;

    Coroutine dieRoutine;
    float IHasArmor.Armor => GetFinalArmor();

    Slider healthBarSlider;

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
    public event Action<float, GameObject> HeadshotTaken;
    public event Action<float, float, float> Healed;
    public event Action<float, float> HealthChanged;

    public bool IsAlive => currentHealth > 0f;
    public bool IsDown => currentHealth <= 0f && DownTime > 0f;
    public bool IsDead => currentHealth <= 0f && DownTime <= 0f;
    public bool IsInvincible => invincible || _invincibilityTokens.Count > 0;

    void Start()
    {
        ResolveReferences();

        downTimeDefault = DownTime;
        if (!CTX)
        {
            Debug.LogError("HealthSystem: missing CharacteContext", this);
            return;
        }

        InitializeFromHub(resetCurrentToMax: true);
        RaiseHealthChanged();
    }

    void ResolveReferences()
    {
        if (!CTX)
        {
            TryGetComponent(out CTX);
            if (!CTX)
                CTX = GetComponentInParent<CharacteContext>();
        }

        CTX?.ResolveReferences();

        if (CTX != null && CTX.HealthSystem != this)
            CTX.HealthSystem = this;

        if (!statsHub && CTX != null)
            statsHub = CTX.StatsHub;
        if (!statsHub)
            TryGetComponent(out statsHub);
        if (!statsHub && CTX != null)
            statsHub = CTX.GetComponentInChildren<StatsHub>(true);

        if (!statusEffectController && CTX != null)
            statusEffectController = CTX.GetComponentInChildren<StatusEffectController>(true);
        if (!statusEffectController)
            TryGetComponent(out statusEffectController);

        if (!combatEventBus && CTX != null)
            combatEventBus = CTX.CombatEventBus;
        if (!combatEventBus)
            TryGetComponent(out combatEventBus);
        if (!combatEventBus && CTX != null)
            combatEventBus = CTX.GetComponentInChildren<CombatEventBus>(true);
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

    public void SetHealthBarSlider(Slider slider)
    {
        healthBarSlider = slider;
        ApplyHealthBarValues();
    }

    public void ClearHealthBarSlider(Slider slider)
    {
        if (healthBarSlider == slider)
            healthBarSlider = null;
    }

    void ApplyHealthBarValues()
    {
        if (!healthBarSlider)
            return;

        healthBarSlider.maxValue = maximumHealth;
        healthBarSlider.value = currentHealth;
    }

    void RaiseHealthChanged()
    {
        ApplyHealthBarValues();
        HealthChanged?.Invoke(currentHealth, maximumHealth);
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

        RaiseHealthChanged();
    }

    float GetFinalArmor()
    {
        if (statsHub)
            return Mathf.Max(0f, statsHub.GetArmor());

        return Mathf.Max(0f, cachedArmor);
    }

    public void SetInvincible(bool value) => invincible = value;

    public int AcquireInvincibilityToken()
    {
        int token = _nextInvincibilityToken++;
        _invincibilityTokens.Add(token);
        return token;
    }

    public void ReleaseInvincibilityToken(int token)
    {
        if (token <= 0)
            return;

        _invincibilityTokens.Remove(token);
    }

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

        float healedAmount = currentHealth - previous;
        RaiseHealthChanged();
        Healed?.Invoke(healedAmount, currentHealth, maximumHealth);
        return true;
    }

    public virtual DamageResult TakeDamage(in DamageContext damageContext)
    {
        DamageContext resolvedContext = ResolveHitZoneDamage(damageContext);
        return ApplyDamageInternal(
            Mathf.Max(0f, resolvedContext.Damage),
            resolvedContext.Attacker,
            true,
            resolvedContext);
    }

    protected DamageResult ApplyDamage(float damage, GameObject attacker)
    {
        return ApplyDamageInternal(Mathf.Max(0f, damage), attacker, false, default);
    }

    protected DamageResult ApplyDamage(in DamageContext damageContext)
    {
        return ApplyDamageInternal(Mathf.Max(0f, damageContext.Damage), damageContext.Attacker, true, damageContext);
    }

    protected DamageContext ResolveHitZoneDamage(in DamageContext damageContext)
    {
        if (damageContext.HitZone == CharacterHitZone.None || hitZoneDamageProfile == null)
            return damageContext;

        float multiplier = hitZoneDamageProfile.GetMultiplier(damageContext.HitZone);
        if (Mathf.Approximately(multiplier, 1f))
            return damageContext;

        return damageContext.WithDamage(damageContext.Damage * multiplier);
    }

    DamageResult ApplyDamageInternal(float damage, GameObject attacker, bool hasContext, in DamageContext damageContext)
    {
        bool wasAliveBefore = IsAlive;

        if (damage <= 0f)
            return BuildDamageResult(attacker, damage, 0f, false, wasAliveBefore);

        if (CTX == null || CTX.stateHub == null || CTX.stateHub.LifeSM == null)
            return BuildDamageResult(attacker, damage, 0f, false, wasAliveBefore);

        if (CTX.stateHub.LifeSM.CurrentId == LifeStateId.Down ||
            CTX.stateHub.LifeSM.CurrentId == LifeStateId.Dead)
            return BuildDamageResult(attacker, damage, 0f, false, wasAliveBefore);

        if (IsInvincible)
        {
            var preventedContext = PublishDamageEvent(PassiveEventType.DamagePrevented, damage, attacker, hasContext, damageContext);
            CTX.DashSystem?.TryRegisterPerfectDodge(in preventedContext);
            return BuildDamageResult(attacker, damage, 0f, true, wasAliveBefore);
        }

        if (hasContext && damageContext.HasKnockback)
            ResolveKnockbackMotor()?.ApplyKnockback(damageContext.Knockback);

        float healthBefore = currentHealth;
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        float appliedDamage = Mathf.Max(0f, healthBefore - currentHealth);

        statusEffectController?.NotifyTrigger(EffectTriggerType.OnTakeDamage, attacker);
        PublishDamageEvent(PassiveEventType.TakeDamage, appliedDamage, attacker, hasContext, damageContext);
        DamageTaken?.Invoke(appliedDamage, attacker);
        if (hasContext && damageContext.HitZone == CharacterHitZone.Head && appliedDamage > 0f)
            HeadshotTaken?.Invoke(appliedDamage, attacker);

        RaiseHealthChanged();

        if (currentHealth <= 0f)
            Down();

        return BuildDamageResult(attacker, damage, appliedDamage, false, wasAliveBefore);
    }

    DamageResult BuildDamageResult(GameObject attacker, float requestedDamage, float appliedDamage, bool wasPrevented, bool wasAliveBefore)
    {
        return new DamageResult(this, attacker, requestedDamage, appliedDamage, wasPrevented, wasAliveBefore, IsAlive);
    }

    CharacterKnockbackMotor ResolveKnockbackMotor()
    {
        if (CTX == null)
        {
            CTX = GetComponent<CharacteContext>();
            if (CTX == null)
                CTX = GetComponentInParent<CharacteContext>();
        }

        if (CTX == null)
            return null;

        if (CTX.KnockbackMotor == null)
            CTX.KnockbackMotor = CTX.GetComponent<CharacterKnockbackMotor>();

        if (CTX.KnockbackMotor == null)
            CTX.KnockbackMotor = CTX.GetComponentInChildren<CharacterKnockbackMotor>(true);

        return CTX.KnockbackMotor;
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
                    damageContext.DamageSourceId,
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
                    damageContext.DamageSourceId,
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
                damageContext.DamageSourceId,
                damageContext.AttackId,
                value,
                damageContext.Origin,
                damageContext.OriginPassiveId,
                damageContext.OriginRuleId);

            combatEventBus.Publish(external);
            return external;
        }

        string fallbackEventSourceId = attacker != null ? $"attacker:{attacker.name}" : "damage";
        var context = combatEventBus.CreateExternalContext(
            eventType,
            attacker,
            actorObject,
            fallbackEventSourceId,
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

        RaiseHealthChanged();

        CTX.stateHub.LifeSM.TryChange(LifeStateId.Alive);
        CharacterRevive?.Invoke();
    }

    void Down()
    {
        DownTime = downTimeDefault;

        var weaponSystem = CTX != null ? CTX.WeaponSystem : null;
        if (!weaponSystem && CTX)
            weaponSystem = CTX.GetComponentInChildren<WeaponSystem>(true);
        if (weaponSystem != null && weaponSystem.IsReloading)
            weaponSystem.CancelReload();

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

        GameObject destroyTarget = GetDestroyTarget();
        if (destroyTarget != null)
            Destroy(destroyTarget);
    }

    protected virtual GameObject GetDestroyTarget()
    {
        return gameObject;
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
