using System;
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

    [Header("Hub Sync")]
    [SerializeField] private bool autoRefreshFromHub = true;
    [SerializeField] private bool keepEnergyPercentWhenMaxChanges = true;
    [SerializeField] private float refreshIntervalSeconds = 0.2f;

    float refreshTimer;
    bool initialized;

    public event Action<float, float> EnergyChanged;

    public Transform CastOrigin => castOrigin ? castOrigin : transform;
    public Transform AimTransform => runtimeAimTransformOverride ? runtimeAimTransformOverride : aimTransform ? aimTransform : transform;
    public float currentEnagy
    {
        get
        {
            EnsureInitialized();
            return currentEnergy;
        }
    }
    public float CurrentEnergy
    {
        get
        {
            EnsureInitialized();
            return currentEnergy;
        }
    }
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

    public float BaseDamage => statsHub ? statsHub.GetSkillBaseDamage()
        : (characteContext ? characteContext.baseDamage : 0f);

    public float FinalCriticalPercent => statsHub ? statsHub.CritRatePercent
        : (characteContext ? characteContext.basecritRate : 0f);

    void Start()
    {
        EnsureInitialized();
    }

    void Update()
    {
        EnsureInitialized();

        if (!autoRefreshFromHub || statsHub == null)
            return;

        refreshTimer += Time.deltaTime;
        if (refreshTimer < refreshIntervalSeconds)
            return;

        refreshTimer = 0f;
        RefreshMaximumEnergy(resetCurrentToMax: false);
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
        NotifyEnergyChanged();
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
        EnergyChanged?.Invoke(currentEnergy, maximumEnergy);
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
