using System;
using System.Threading;
using UnityEngine;

[DefaultExecutionOrder(-118)]
public sealed class CombatEventBus : MonoBehaviour
{
    [SerializeField] private CharacteContext ctx;

    static long _nextChainId;
    static long _nextAttackId;

    public event Action<PassiveEventContext> EventPublished;

    void Awake()
    {
        ResolveReferences();
    }

    void ResolveReferences()
    {
        if (!ctx)
        {
            TryGetComponent(out ctx);
            if (!ctx)
                ctx = GetComponentInParent<CharacteContext>();
        }

        ctx?.ResolveReferences();

        if (ctx != null)
            ctx.CombatEventBus = this;
    }

    public static ulong NextChainId()
    {
        return unchecked((ulong)Interlocked.Increment(ref _nextChainId));
    }

    public string CreateAttackId(string prefix = null)
    {
        long id = Interlocked.Increment(ref _nextAttackId);
        if (string.IsNullOrWhiteSpace(prefix))
            return $"atk:{id}";

        return $"{prefix}:{id}";
    }

    public PassiveEventContext CreateExternalContext(
        PassiveEventType type,
        GameObject source = null,
        GameObject target = null,
        string eventSourceId = null,
        string attackId = null,
        float value = 0f,
        PassiveEventOrigin origin = PassiveEventOrigin.External,
        string originPassiveId = null,
        string originRuleId = null,
        CombatEventMetadata metadata = default)
    {
        return new PassiveEventContext(
            type,
            ctx ? ctx.gameObject : gameObject,
            source ? source : gameObject,
            target,
            eventSourceId,
            attackId,
            value,
            Time.timeAsDouble,
            NextChainId(),
            0,
            origin,
            originPassiveId,
            originRuleId,
            metadata);
    }

    public PassiveEventContext CreateChildContext(
        in PassiveEventContext parent,
        PassiveEventType type,
        GameObject source = null,
        GameObject target = null,
        string eventSourceId = null,
        string attackId = null,
        float value = 0f,
        PassiveEventOrigin origin = PassiveEventOrigin.Passive,
        string originPassiveId = null,
        string originRuleId = null,
        CombatEventMetadata metadata = default)
    {
        return new PassiveEventContext(
            type,
            ctx ? ctx.gameObject : gameObject,
            source ? source : parent.Source,
            target,
            string.IsNullOrWhiteSpace(eventSourceId) ? parent.EventSourceId : eventSourceId,
            string.IsNullOrWhiteSpace(attackId) ? parent.AttackId : attackId,
            value,
            Time.timeAsDouble,
            parent.ChainId == 0 ? NextChainId() : parent.ChainId,
            Mathf.Max(0, parent.Depth) + 1,
            origin,
            originPassiveId,
            originRuleId,
            metadata);
    }

    public void Publish(in PassiveEventContext context)
    {
        EventPublished?.Invoke(context);
    }
}
