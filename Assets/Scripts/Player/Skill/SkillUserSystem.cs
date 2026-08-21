using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DefaultExecutionOrder(-110)]
public class SkillUserSystem : MonoBehaviour, ISkillUser
{
    [FormerlySerializedAs("charactorContext")]
    [FormerlySerializedAs("playerContext")]
    [Header("References")]
    [SerializeField] private CharacteContext characteContext;
    [SerializeField] private StatsHub statsHub;
    [SerializeField] private Transform castOrigin;
    [SerializeField] private Transform aimTransform;
    Transform runtimeAimTransformOverride;

    [Header("Energy (Runtime)")]
    [SerializeField] private float maximumEnergy;
    [SerializeField] private float currentEnergy;

    [Header("Energy Regeneration")]
    [SerializeField, Min(0f)] private float energyRegenPerSecond = 3f;
    [SerializeField, Min(0f)] private float regenDelayAfterSpend = 2f;

    [Header("Hub Sync")]
    [SerializeField] private bool autoRefreshFromHub = true;
    [SerializeField] private bool keepEnergyPercentWhenMaxChanges = true;
    [SerializeField] private float refreshIntervalSeconds = 0.2f;

    float refreshTimer;
    float lastEnergySpendTime = float.NegativeInfinity;
    bool initialized;

    /// <summary>
    /// Energy held for casts that have started but not reached their cast point, keyed by
    /// reservation token. Reserved energy is already gone from <see cref="CurrentEnergy"/>, so two
    /// casts winding up at the same time cannot both spend the same points.
    /// </summary>
    readonly Dictionary<int, float> energyReservations = new Dictionary<int, float>();
    float reservedEnergy;

    public event Action<float, float> EnergyChanged;

    public Transform CastOrigin => castOrigin ? castOrigin : transform;
    public Transform AimTransform => runtimeAimTransformOverride ? runtimeAimTransformOverride : aimTransform ? aimTransform : transform;
    public float currentEnagy
    {
        get
        {
            EnsureInitialized();
            return SpendableEnergy;
        }
    }

    /// <summary>Energy a new cast may spend: the pool minus everything already reserved.</summary>
    public float CurrentEnergy
    {
        get
        {
            EnsureInitialized();
            return SpendableEnergy;
        }
    }

    /// <summary>Raw pool before reservations. Used by regeneration and by the commit path.</summary>
    public float StoredEnergy
    {
        get
        {
            EnsureInitialized();
            return currentEnergy;
        }
    }

    public float ReservedEnergy
    {
        get
        {
            EnsureInitialized();
            return reservedEnergy;
        }
    }

    float SpendableEnergy => Mathf.Max(0f, currentEnergy - reservedEnergy);
    public float MaximumEnergy
    {
        get
        {
            EnsureInitialized();
            return maximumEnergy;
        }
    }
    public StatsHub StatsHub
    {
        get
        {
            EnsureInitialized();
            return statsHub;
        }
    }

    public float FinalCriticalPercent => statsHub ? statsHub.CritRatePercent
        : (characteContext ? characteContext.basecritRate : 0f);

    void Start()
    {
        EnsureInitialized();
    }

    void Update()
    {
        EnsureInitialized();

        RegenerateEnergy();

        if (!autoRefreshFromHub || statsHub == null)
            return;

        refreshTimer += Time.deltaTime;
        if (refreshTimer < refreshIntervalSeconds)
            return;

        refreshTimer = 0f;
        RefreshMaximumEnergy(resetCurrentToMax: false);
    }

    void RegenerateEnergy()
    {
        if (energyRegenPerSecond <= 0f || currentEnergy >= maximumEnergy)
            return;

        if (Time.time < lastEnergySpendTime + regenDelayAfterSpend)
            return;

        float previous = currentEnergy;
        currentEnergy = Mathf.Min(maximumEnergy, currentEnergy + energyRegenPerSecond * Time.deltaTime);
        if (currentEnergy > previous)
            NotifyEnergyChanged();
    }

    float GetMaximumEnergyFromHubOrFallback()
    {
        if (statsHub != null)
            return Mathf.Max(0f, statsHub.GetMaximumEnergy());

        return characteContext != null ? Mathf.Max(0f, characteContext.baseEnagy) : 0f;
    }

    void RefreshMaximumEnergy(bool resetCurrentToMax)
    {
        float oldMaximum = maximumEnergy;
        float newMaximum = GetMaximumEnergyFromHubOrFallback();

        if (Mathf.Approximately(oldMaximum, newMaximum))
        {
            if (resetCurrentToMax)
            {
                currentEnergy = maximumEnergy;
                NotifyEnergyChanged();
            }

            return;
        }

        float percent = oldMaximum > 0f ? currentEnergy / oldMaximum : 1f;
        maximumEnergy = newMaximum;

        if (resetCurrentToMax)
            currentEnergy = maximumEnergy;
        else if (keepEnergyPercentWhenMaxChanges)
            currentEnergy = Mathf.Clamp(percent * maximumEnergy, 0f, maximumEnergy);
        else
            currentEnergy = Mathf.Clamp(currentEnergy, 0f, maximumEnergy);

        NotifyEnergyChanged();
    }

    public void SpendEnagy(float amount)
    {
        EnsureInitialized();

        if (!float.IsFinite(amount) || amount <= 0f)
            return;

        RefreshMaximumEnergy(resetCurrentToMax: false);

        currentEnergy = Mathf.Max(0f, currentEnergy - amount);
        lastEnergySpendTime = Time.time;
        NotifyEnergyChanged();
    }

    /// <summary>
    /// Holds <paramref name="amount"/> for a cast that has started but not yet reached its cast
    /// point. Idempotent: reserving again with the same token keeps the original hold and reports
    /// success rather than doubling it.
    /// </summary>
    public bool TryReserveEnergy(int token, float amount)
    {
        EnsureInitialized();

        if (energyReservations.ContainsKey(token))
            return true;

        if (!float.IsFinite(amount) || amount <= 0f)
        {
            // A free cast still takes a token so commit/release stay symmetric for every caller.
            energyReservations[token] = 0f;
            return true;
        }

        RefreshMaximumEnergy(resetCurrentToMax: false);

        if (SpendableEnergy < amount)
            return false;

        energyReservations[token] = amount;
        reservedEnergy += amount;
        NotifyEnergyChanged();
        return true;
    }

    /// <summary>
    /// Spends a held amount for real. Idempotent: committing an unknown or already-settled token
    /// does nothing.
    /// </summary>
    public bool CommitEnergyReservation(int token)
    {
        EnsureInitialized();

        if (!energyReservations.TryGetValue(token, out float amount))
            return false;

        energyReservations.Remove(token);
        reservedEnergy = Mathf.Max(0f, reservedEnergy - amount);

        if (amount > 0f)
        {
            currentEnergy = Mathf.Max(0f, currentEnergy - amount);
            lastEnergySpendTime = Time.time;
        }

        NotifyEnergyChanged();
        return true;
    }

    /// <summary>
    /// Gives a held amount back. Idempotent: releasing an unknown or already-settled token does
    /// nothing.
    /// </summary>
    public bool ReleaseEnergyReservation(int token)
    {
        EnsureInitialized();

        if (!energyReservations.TryGetValue(token, out float amount))
            return false;

        energyReservations.Remove(token);
        reservedEnergy = Mathf.Max(0f, reservedEnergy - amount);
        NotifyEnergyChanged();
        return true;
    }

    public bool CanRestoreEnergy(float amount)
    {
        EnsureInitialized();

        if (!float.IsFinite(amount) || amount <= 0f)
            return false;

        RefreshMaximumEnergy(resetCurrentToMax: false);
        return currentEnergy < maximumEnergy;
    }

    public bool RestoreEnergy(float amount)
    {
        EnsureInitialized();

        if (!CanRestoreEnergy(amount))
            return false;

        float previous = currentEnergy;
        currentEnergy = Mathf.Clamp(currentEnergy + amount, 0f, maximumEnergy);
        if (currentEnergy <= previous)
            return false;

        NotifyEnergyChanged();
        return true;
    }

    void NotifyEnergyChanged()
    {
        EnergyChanged?.Invoke(SpendableEnergy, maximumEnergy);
    }

    void EnsureInitialized()
    {
        if (initialized)
            return;

        ResolveReferences();

        maximumEnergy = GetMaximumEnergyFromHubOrFallback();
        currentEnergy = maximumEnergy;
        initialized = true;

        NotifyEnergyChanged();
    }

    void ResolveReferences()
    {
        if (!characteContext)
        {
            TryGetComponent(out characteContext);
            if (!characteContext)
                characteContext = GetComponentInParent<CharacteContext>();
        }

        characteContext?.ResolveReferences();

        if (characteContext != null && characteContext.EnegySystem != this)
            characteContext.EnegySystem = this;

        if (!statsHub && characteContext != null)
            statsHub = characteContext.StatsHub;
        if (!statsHub)
            TryGetComponent(out statsHub);
        if (!statsHub && characteContext != null)
            statsHub = characteContext.GetComponentInChildren<StatsHub>(true);
    }

    public void SetRuntimeAimTransformOverride(Transform overrideTransform)
    {
        runtimeAimTransformOverride = overrideTransform;
    }

    public void ClearRuntimeAimTransformOverride(Transform overrideTransform = null)
    {
        if (overrideTransform == null || runtimeAimTransformOverride == overrideTransform)
            runtimeAimTransformOverride = null;
    }

    public Vector3 AimDirection
    {
        get
        {
            Vector3 dir;

            if (CastOrigin != null && AimTransform != null)
                dir = AimTransform.position - CastOrigin.position;
            else
                dir = transform.forward;

            if (dir.sqrMagnitude < 0.0001f)
                dir = CastOrigin ? CastOrigin.forward : transform.forward;

            return dir.normalized;
        }
    }
}
