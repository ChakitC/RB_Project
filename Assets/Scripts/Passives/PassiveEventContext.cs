using UnityEngine;

public readonly struct PassiveEventContext
{
    public PassiveEventContext(
        PassiveEventType type,
        GameObject actor,
        GameObject source,
        GameObject target,
        string eventSourceId,
        string attackId,
        float value,
        double time,
        ulong chainId,
        int depth,
        PassiveEventOrigin origin,
        string originPassiveId,
        string originRuleId)
    {
        Type = type;
        Actor = actor;
        Source = source;
        Target = target;
        EventSourceId = eventSourceId;
        AttackId = attackId;
        Value = value;
        Time = time;
        ChainId = chainId;
        Depth = depth;
        Origin = origin;
        OriginPassiveId = originPassiveId;
        OriginRuleId = originRuleId;
    }

    public PassiveEventType Type { get; }
    public GameObject Actor { get; }
    public GameObject Source { get; }
    public GameObject Target { get; }
    public string EventSourceId { get; }
    public string AttackId { get; }
    public float Value { get; }
    public double Time { get; }
    public ulong ChainId { get; }
    public int Depth { get; }
    public PassiveEventOrigin Origin { get; }
    public string OriginPassiveId { get; }
    public string OriginRuleId { get; }

    public int TargetInstanceId => Target != null ? Target.GetInstanceID() : 0;
}
