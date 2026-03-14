using System;
using UnityEngine;


public class StaminaSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacteContext characterContext;
    [SerializeField] private StatsHub statsHub;

    [Header("Runtime")]
    [SerializeField] private float maximumStamina = 30f;
    [SerializeField] private float currentStamina = 0f;

    [Header("Regen")]
    [SerializeField] private float regenerationRate = 15f;
    [SerializeField] private float regenerationDelay = 1f;

    [Header("Local Modifiers (optional)")]
    public float bonusFlat = 0f;   // +20
    public float bonusMult = 1f;   // x1.2

    [Header("Hub Sync")]
    [SerializeField] private bool autoRefreshFromHub = true;
    [SerializeField] private bool keepStaminaPercentWhenMaxChanges = true;
    [SerializeField] private float refreshIntervalSeconds = 0.2f;

    float unblockRegenerationAtTime;
    float refreshTimer;

    public float Max => maximumStamina;
    public float Current => currentStamina;

    public event Action<float, float> OnStaminaChanged; // (current,max)

    void Awake()
    {
        if (!characterContext) characterContext = GetComponent<CharacteContext>();
        if (!statsHub) statsHub = GetComponent<StatsHub>();
    }

    void Start()
    {
        RecalculateMaximumStamina(resetCurrentToMax: true);
        Notify();
    }

    void Update()
    {
        if (autoRefreshFromHub && statsHub != null)
        {
            refreshTimer += Time.deltaTime;
            if (refreshTimer >= refreshIntervalSeconds)
            {
                refreshTimer = 0f;
                RecalculateMaximumStamina(resetCurrentToMax: false);
            }
        }

        if (Time.time < unblockRegenerationAtTime) return;
        if (currentStamina >= maximumStamina) return;

        SetCurrent(currentStamina + regenerationRate * Time.deltaTime);
    }

    public void RecalculateMaximumStamina(bool resetCurrentToMax = false)
    {
        float oldMaximum = maximumStamina;

        // base จาก Hub (อนาคตค่อยรวม buff/debuff ใน Hub)
        float baseMaximumFromHub =
            (statsHub != null)
                ? statsHub.GetMaximumStamina()
                : (characterContext != null ? characterContext.baseStamina : 0f);

        // local modifier (ยังใช้ได้)
        float mult = Mathf.Max(0.01f, bonusMult);
        float newMaximum = Mathf.Max(1f, (baseMaximumFromHub + bonusFlat) * mult);

        if (Mathf.Approximately(newMaximum, oldMaximum))
        {
            if (resetCurrentToMax)
            {
                currentStamina = maximumStamina;
                Notify();
            }
            return;
        }

        // Max เปลี่ยน: เลือกว่าจะคง % เดิมไหม
        float percent = (oldMaximum > 0f) ? (currentStamina / oldMaximum) : 1f;

        maximumStamina = newMaximum;

        if (resetCurrentToMax)
        {
            currentStamina = maximumStamina;
        }
        else if (keepStaminaPercentWhenMaxChanges)
        {
            currentStamina = Mathf.Clamp(percent * maximumStamina, 0f, maximumStamina);
        }
        else
        {
            currentStamina = Mathf.Clamp(currentStamina, 0f, maximumStamina);
        }

        Notify();
    }

    public bool Spend(float cost)
    {
        if (!float.IsFinite(cost) || cost <= 0f) return true;
        if (currentStamina + 0.0001f < cost) return false;

        SetCurrent(currentStamina - cost);
        unblockRegenerationAtTime = Time.time + regenerationDelay;
        return true;
    }

    void SetCurrent(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, maximumStamina);
        if (Mathf.Abs(clamped - currentStamina) < 0.0005f) return;

        currentStamina = clamped;
        Notify();
    }

    void Notify() => OnStaminaChanged?.Invoke(currentStamina, maximumStamina);
}
