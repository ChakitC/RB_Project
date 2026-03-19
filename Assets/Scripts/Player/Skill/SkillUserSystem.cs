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

    [Header("Energy (Runtime)")]
    [SerializeField] private float maximumEnergy;
    [SerializeField] private float currentEnergy;

    [Header("Hub Sync")]
    [SerializeField] private bool autoRefreshFromHub = true;
    [SerializeField] private bool keepEnergyPercentWhenMaxChanges = true;
    [SerializeField] private float refreshIntervalSeconds = 0.2f;

    float refreshTimer;

    public Transform CastOrigin => castOrigin ? castOrigin : transform;
    public Transform AimTransform => aimTransform ? aimTransform : transform;
    public float currentEnagy => currentEnergy;
    public StatsHub StatsHub => statsHub;

    public float BaseDamage => statsHub ? statsHub.GetSkillBaseDamage()
        : (characteContext ? characteContext.baseDamage : 0f);

    public float FinalCriticalPercent => statsHub ? statsHub.CritRatePercent
        : (characteContext ? characteContext.basecritRate : 0f);

    void Start()
    {
        if (!characteContext) characteContext = GetComponent<CharacteContext>();
        if (!statsHub) statsHub = GetComponent<StatsHub>();
        if (!statsHub) statsHub = GetComponentInParent<StatsHub>();

        maximumEnergy = GetMaximumEnergyFromHubOrFallback();
        currentEnergy = maximumEnergy;

        NotifyEnergyChanged();
    }

    void Update()
    {
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
        if (!float.IsFinite(amount) || amount <= 0f)
            return;

        RefreshMaximumEnergy(resetCurrentToMax: false);

        currentEnergy = Mathf.Max(0f, currentEnergy - amount);
        NotifyEnergyChanged();
    }

    void NotifyEnergyChanged()
    {
        characteContext?.UIManager?.UpdateEnegyText(currentEnergy, maximumEnergy);
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

            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                dir = CastOrigin ? CastOrigin.forward : transform.forward;

            return dir.normalized;
        }
    }
}
