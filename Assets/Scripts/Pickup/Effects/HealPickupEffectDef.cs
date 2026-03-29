using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

public enum HealPickupAmountMode
{
    FlatAmount,
    PercentOfHealth,
}

public enum HealPickupPercentSource
{
    MaximumHealth,
    CurrentHealth,
}

[CreateAssetMenu(fileName = "Heal Pickup Effect", menuName = "Game/Pickup Effect/Heal")]
public class HealPickupEffectDef : PickupEffectDef
{
    bool IsFlatAmountMode => amountMode == HealPickupAmountMode.FlatAmount;
    bool IsPercentMode => amountMode == HealPickupAmountMode.PercentOfHealth;

    [SerializeField] private HealPickupAmountMode amountMode = HealPickupAmountMode.FlatAmount;
    [SerializeField, ShowIf(nameof(IsFlatAmountMode)), Min(0f)] private float amount = 25f;
    [SerializeField, ShowIf(nameof(IsPercentMode))] private HealPickupPercentSource percentSource = HealPickupPercentSource.MaximumHealth;
    [FormerlySerializedAs("maximumHealthPercent")]
    [SerializeField, ShowIf(nameof(IsPercentMode)), Min(0f)] private float healthPercent = 25f;

    public override bool CanApply(GameObject target, in PickupContext context)
    {
        var health = FindTargetComponent<HealthSystem>(target);
        if (health == null)
            return false;

        float healAmount = GetHealAmount(health);
        return health.CanHeal(healAmount);
    }

    public override bool Apply(GameObject target, in PickupContext context)
    {
        var health = FindTargetComponent<HealthSystem>(target);
        if (health == null)
            return false;

        float healAmount = GetHealAmount(health);
        return health.Heal(healAmount);
    }

    float GetHealAmount(HealthSystem health)
    {
        if (health == null)
            return 0f;

        switch (amountMode)
        {
            case HealPickupAmountMode.PercentOfHealth:
                float baseHealth = percentSource == HealPickupPercentSource.CurrentHealth
                    ? health.currentHealth
                    : health.maximumHealth;
                return Mathf.Max(0f, baseHealth * (healthPercent * 0.01f));

            case HealPickupAmountMode.FlatAmount:
            default:
                return Mathf.Max(0f, amount);
        }
    }
}
