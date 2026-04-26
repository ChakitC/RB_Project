using UnityEngine;

public readonly struct StaggerPayload
{
    public StaggerPayload(float amount, float multiplier = 1f, string sourceId = null)
    {
        Amount = amount;
        Multiplier = multiplier;
        SourceId = sourceId;
    }

    public float Amount { get; }
    public float Multiplier { get; }
    public string SourceId { get; }

    public bool HasValue => IsFinite(Amount) && Amount > 0f && IsFinite(Multiplier) && Multiplier > 0f;
    public float ResolvedAmount => HasValue ? Amount * Mathf.Max(0f, Multiplier) : 0f;

    static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

public readonly struct DamageContext
{
    public DamageContext(
        float damage,
        GameObject attacker,
        string sourceId,
        string attackId,
        ulong chainId,
        int depth,
        PassiveEventOrigin origin,
        string originPassiveId = null,
        string originRuleId = null,
        KnockbackData knockback = default,
        StaggerPayload stagger = default)
    {
        Damage = damage;
        Attacker = attacker;
        SourceId = sourceId;
        AttackId = attackId;
        ChainId = chainId;
        Depth = depth;
        Origin = origin;
        OriginPassiveId = originPassiveId;
        OriginRuleId = originRuleId;
        Knockback = knockback;
        Stagger = stagger;
    }

    public float Damage { get; }
    public GameObject Attacker { get; }
    public string SourceId { get; }
    public string AttackId { get; }
    public ulong ChainId { get; }
    public int Depth { get; }
    public PassiveEventOrigin Origin { get; }
    public string OriginPassiveId { get; }
    public string OriginRuleId { get; }
    public KnockbackData Knockback { get; }
    public StaggerPayload Stagger { get; }
    public bool HasKnockback => Knockback.IsValid;
    public bool HasStagger => Stagger.HasValue;

    public DamageContext WithDamage(float damage)
    {
        return new DamageContext(
            damage,
            Attacker,
            SourceId,
            AttackId,
            ChainId,
            Depth,
            Origin,
            OriginPassiveId,
            OriginRuleId,
            Knockback,
            Stagger);
    }
}
