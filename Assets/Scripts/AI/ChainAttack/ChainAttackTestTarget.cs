using System;
using Sirenix.OdinInspector;
using UnityEngine;

[AddComponentMenu("Game/Debug/Chain Attack Test Target")]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody), typeof(StaggerMeter))]
public sealed class ChainAttackTestTarget : AITargetInfo, IDamageable
{
    [Header("Test Health")]
    [SerializeField, Min(1f)] private float maxHealth = 1000f;
    [SerializeField] private float currentHealth = 1000f;
    [SerializeField] private bool resetOnEnable = true;
    [SerializeField] private bool preventDeathDuringChainReady = true;

    [Header("References")]
    [SerializeField] private StaggerMeter staggerMeter;

    [Header("Overhead Bars")]
    [SerializeField] private GameObject overheadBarsPrefab;
    [SerializeField] private Vector3 overheadBarsOffset = new Vector3(0f, 3f, 0f);

    GameObject overheadBarsInstance;
    CharacterOverheadBarsView overheadBarsView;

    public event Action<float, float> HealthChanged;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => Mathf.Max(1f, maxHealth);

    void Awake()
    {
        ResolveReferences();
        ConfigureRigidbody();
        currentHealth = Mathf.Clamp(currentHealth, 0f, MaxHealth);
        SetAlive(currentHealth > 0f);
    }

    void OnEnable()
    {
        if (resetOnEnable)
            ResetTarget();

        if (Application.isPlaying)
            CreateOverheadBarsIfNeeded();
    }

    void OnDestroy()
    {
        overheadBarsView?.Unbind();
    }

    void Reset()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = maxHealth;
        ResolveReferences();
        ConfigureRigidbody();
    }

    void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    public DamageResult TakeDamage(in DamageContext damageContext)
    {
        bool wasAliveBefore = IsAlive;
        float requestedDamage = float.IsNaN(damageContext.Damage) || float.IsInfinity(damageContext.Damage)
            ? 0f
            : Mathf.Max(0f, damageContext.Damage);

        if (!wasAliveBefore || requestedDamage <= 0f)
        {
            return new DamageResult(
                this,
                damageContext.Attacker,
                requestedDamage,
                0f,
                false,
                wasAliveBefore,
                IsAlive);
        }

        ResolveReferences();

        float resolvedDamage = requestedDamage;
        if (preventDeathDuringChainReady &&
            staggerMeter != null &&
            (staggerMeter.IsChainReady || staggerMeter.IsChainExecutionActive))
        {
            resolvedDamage = Mathf.Min(resolvedDamage, Mathf.Max(0f, currentHealth - 1f));
        }

        float healthBefore = currentHealth;
        currentHealth = Mathf.Max(0f, currentHealth - resolvedDamage);
        float appliedDamage = Mathf.Max(0f, healthBefore - currentHealth);
        SetAlive(currentHealth > 0f);
        NotifyHealthChanged();

        var result = new DamageResult(
            this,
            damageContext.Attacker,
            requestedDamage,
            appliedDamage,
            false,
            wasAliveBefore,
            IsAlive);

        if (staggerMeter != null && result.Applied && result.IsAliveAfter && damageContext.HasStagger)
            staggerMeter.ApplyStagger(damageContext.Stagger, damageContext.Attacker);

        return result;
    }

    [Button("Reset Target")]
    [ContextMenu("Reset Target")]
    public void ResetTarget()
    {
        ResolveReferences();
        ResetStaggerState();

        currentHealth = MaxHealth;
        SetAlive(true);
        SetTargetable(true);
        NotifyHealthChanged();
    }

    [Button("Force ChainReady")]
    [ContextMenu("Force ChainReady")]
    public void ForceChainReady()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ChainAttackTestTarget] Force ChainReady is available only in Play Mode.", this);
            return;
        }

        ResetTarget();

        if (staggerMeter == null ||
            !staggerMeter.ApplyStagger(staggerMeter.MaxStagger, gameObject))
        {
            Debug.LogWarning("[ChainAttackTestTarget] Failed to enter ChainReady.", this);
        }
    }

    void ResolveReferences()
    {
        if (staggerMeter == null)
            staggerMeter = GetComponent<StaggerMeter>();
    }

    void CreateOverheadBarsIfNeeded()
    {
        if (overheadBarsInstance != null || overheadBarsPrefab == null)
            return;

        overheadBarsInstance = Instantiate(overheadBarsPrefab, transform, false);
        overheadBarsInstance.transform.localPosition = overheadBarsOffset;
        overheadBarsInstance.transform.localRotation = Quaternion.identity;

        if (overheadBarsInstance.GetComponent<Billboard>() == null)
            overheadBarsInstance.AddComponent<Billboard>();

        overheadBarsView = overheadBarsInstance.GetComponent<CharacterOverheadBarsView>();
        overheadBarsView?.Bind(this, staggerMeter);
    }

    void NotifyHealthChanged()
    {
        HealthChanged?.Invoke(currentHealth, MaxHealth);
    }

    void ConfigureRigidbody()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
            return;

        body.isKinematic = true;
        body.useGravity = false;
    }

    void ResetStaggerState()
    {
        if (staggerMeter == null)
            return;

        staggerMeter.ResetRuntimeState();
    }
}
