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

    public Transform CastOrigin => castOrigin ? castOrigin : transform;
    public Transform AimTransform => aimTransform ? aimTransform : transform;

    public float currentEnagy => currentEnergy;
    
    public StatsHub StatsHub => statsHub;

    // (Optional) ไว้ใช้ที่อื่นได้ แต่ไม่จำเป็นต่อ SkillInstance แล้ว
    public float BaseDamage => statsHub ? statsHub.GetSkillBaseDamage()
        : (characteContext ? characteContext.baseDamage : 0f);

    public float FinalCriticalPercent => statsHub ? statsHub.CritRatePercent
        : (characteContext ? characteContext.basecritRate : 0f);

    void Start()
    {
        if (!characteContext) characteContext = GetComponent<CharacteContext>();

        // แนะนำ: เผื่อ StatsHub อยู่ที่ parent/root
        if (!statsHub) statsHub = GetComponent<StatsHub>();
        if (!statsHub) statsHub = GetComponentInParent<StatsHub>();

        maximumEnergy = GetMaximumEnergyFromHubOrFallback();
        currentEnergy = maximumEnergy;

        characteContext?.UIManager?.UpdateEnegyText(currentEnergy, maximumEnergy);
    }

    float GetMaximumEnergyFromHubOrFallback()
    {
        if (statsHub != null) return Mathf.Max(0f, statsHub.GetMaximumEnergy());
        return characteContext != null ? Mathf.Max(0f, characteContext.baseEnagy) : 0f;
    }

    public void SpendEnagy(float amount)
    {
        if (!float.IsFinite(amount) || amount <= 0f) return;

        // ถ้า MaxEnergy เปลี่ยนระหว่างเล่น ให้ซิงก์ก่อนใช้
        float newMax = GetMaximumEnergyFromHubOrFallback();
        if (!Mathf.Approximately(newMax, maximumEnergy))
        {
            float percent = (maximumEnergy > 0f) ? (currentEnergy / maximumEnergy) : 1f;
            maximumEnergy = newMax;
            currentEnergy = Mathf.Clamp(percent * maximumEnergy, 0f, maximumEnergy);
        }

        currentEnergy = Mathf.Max(0f, currentEnergy - amount);
        characteContext?.UIManager?.UpdateEnegyText(currentEnergy, maximumEnergy);
    }
    


    public Vector3 AimDirection
    {
        get
        {
            Vector3 dir;

            if (CastOrigin != null && AimTransform != null)
            {
                dir = AimTransform.position - CastOrigin.position;
            }
            else
            {
                dir = transform.forward;
            }

            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                dir = CastOrigin ? CastOrigin.forward : transform.forward;

            return dir.normalized;
        }
    }
}
