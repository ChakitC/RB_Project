using UnityEngine;

public readonly struct StaggerPayload
{
    public StaggerPayload(float amount, float multiplier = 1f, string staggerSourceId = null)
    {
        Amount = amount;
        Multiplier = multiplier;
        StaggerSourceId = staggerSourceId;
    }

    public float Amount { get; }
    public float Multiplier { get; }
    public string StaggerSourceId { get; }

    public bool HasValue => IsFinite(Amount) && Amount > 0f && IsFinite(Multiplier) && Multiplier > 0f;
    public float ResolvedAmount => HasValue ? Amount * Mathf.Max(0f, Multiplier) : 0f;

    static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

public readonly struct DamageResult
{
    public DamageResult(
        IDamageable target,
        GameObject attacker,
        float requestedDamage,
        float appliedDamage,
        bool wasPrevented,
        bool wasAliveBefore,
        bool isAliveAfter)
    {
        Target = target;
        Attacker = attacker;
        RequestedDamage = SanitizeNonNegative(requestedDamage);
        AppliedDamage = SanitizeNonNegative(appliedDamage);
        WasPrevented = wasPrevented;
        WasAliveBefore = wasAliveBefore;
        IsAliveAfter = isAliveAfter;
    }

    public IDamageable Target { get; }
    public GameObject Attacker { get; }
    public float RequestedDamage { get; }
    public float AppliedDamage { get; }
    public bool WasPrevented { get; }
    public bool WasAliveBefore { get; }
    public bool IsAliveAfter { get; }
    public bool Applied => AppliedDamage > 0f;
    public bool Killed => WasAliveBefore && Applied && !IsAliveAfter;

    static float SanitizeNonNegative(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return 0f;

        return Mathf.Max(0f, value);
    }
}

public readonly struct DamageContext
{
    public DamageContext(
        float damage,
        GameObject attacker,
        string damageSourceId,
        string attackId,
        ulong chainId,
        int depth,
        PassiveEventOrigin origin,
        string originPassiveId = null,
        string originRuleId = null,
        KnockbackData knockback = default,
        StaggerPayload stagger = default,
        CharacterHitZone hitZone = CharacterHitZone.None)
    {
        Damage = damage;
        Attacker = attacker;
        DamageSourceId = damageSourceId;
        AttackId = attackId;
        ChainId = chainId;
        Depth = depth;
        Origin = origin;
        OriginPassiveId = originPassiveId;
        OriginRuleId = originRuleId;
        Knockback = knockback;
        Stagger = stagger;
        HitZone = hitZone;
    }

    public float Damage { get; }
    public GameObject Attacker { get; }
    public string DamageSourceId { get; }
    public string AttackId { get; }
    public ulong ChainId { get; }
    public int Depth { get; }
    public PassiveEventOrigin Origin { get; }
    public string OriginPassiveId { get; }
    public string OriginRuleId { get; }
    public KnockbackData Knockback { get; }
    public StaggerPayload Stagger { get; }
    public CharacterHitZone HitZone { get; }
    public bool HasKnockback => Knockback.IsValid;
    public bool HasStagger => Stagger.HasValue;

    public DamageContext WithDamage(float damage)
    {
        return new DamageContext(
            damage,
            Attacker,
            DamageSourceId,
            AttackId,
            ChainId,
            Depth,
            Origin,
            OriginPassiveId,
            OriginRuleId,
            Knockback,
            Stagger,
            HitZone);
    }
}
