using UnityEngine;

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
        KnockbackData knockback = default)
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
    public bool HasKnockback => Knockback.IsValid;
}
