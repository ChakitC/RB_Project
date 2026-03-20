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
        string originRuleId = null)
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
}
