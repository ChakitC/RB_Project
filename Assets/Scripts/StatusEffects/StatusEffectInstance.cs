using System;
using UnityEngine;

public sealed class StatusEffectInstance
{
    readonly int[] _triggerCounters;

    public StatusEffectInstance(
        StatusEffectDef definition,
        GameObject source,
        int initialStacks,
        float now,
        string appliedById,
        ulong chainId,
        int depth,
        PassiveEventOrigin origin,
        string originPassiveId,
        string originRuleId)
    {
        Definition = definition;
        Source = source;
        AppliedById = appliedById;
        ChainId = chainId;
        Depth = Mathf.Max(0, depth);
        Origin = origin;
        OriginPassiveId = originPassiveId;
        OriginRuleId = originRuleId;
        CurrentStacks = Mathf.Max(0, initialStacks);
        TimeLeft = definition != null && !definition.IsPermanent ? definition.duration : float.PositiveInfinity;
        NextTickTime = definition != null && definition.tickInterval > 0f
            ? now + definition.tickInterval
            : float.PositiveInfinity;

        _triggerCounters = definition != null && definition.triggerRules != null
            ? new int[definition.triggerRules.Count]
            : Array.Empty<int>();
    }

    public StatusEffectDef Definition { get; }
    public GameObject Source { get; private set; }
    public string AppliedById { get; private set; }
    public ulong ChainId { get; private set; }
    public int Depth { get; private set; }
    public PassiveEventOrigin Origin { get; private set; }
    public string OriginPassiveId { get; private set; }
    public string OriginRuleId { get; private set; }
    public int CurrentStacks { get; private set; }
    public float TimeLeft { get; private set; }
    public float NextTickTime { get; private set; }
    public bool IsPermanent => Definition == null || Definition.IsPermanent;

    public void UpdateSource(GameObject source)
    {
        if (source != null)
            Source = source;
    }

    public void UpdateContext(
        string appliedById,
        ulong chainId,
        int depth,
        PassiveEventOrigin origin,
        string originPassiveId,
        string originRuleId)
    {
        if (!string.IsNullOrWhiteSpace(appliedById))
            AppliedById = appliedById;

        if (chainId != 0)
            ChainId = chainId;

        Depth = Mathf.Max(0, depth);
        Origin = origin;

        if (!string.IsNullOrWhiteSpace(originPassiveId))
            OriginPassiveId = originPassiveId;

        if (!string.IsNullOrWhiteSpace(originRuleId))
            OriginRuleId = originRuleId;
    }

    public void RefreshDuration()
    {
        if (Definition == null || Definition.IsPermanent)
            return;

        TimeLeft = Definition.duration;
    }

    public void AddStacks(int amount, int maxStacks)
    {
        if (amount <= 0)
            return;

        CurrentStacks = Mathf.Clamp(CurrentStacks + amount, 0, maxStacks <= 0 ? int.MaxValue : maxStacks);
    }

    public void TickLifetime(float dt)
    {
        if (IsPermanent)
            return;

        TimeLeft -= dt;
    }

    public bool IsExpired()
    {
        return !IsPermanent && TimeLeft <= 0f;
    }

    public bool ShouldTick(float now)
    {
        return Definition != null &&
               Definition.tickInterval > 0f &&
               now >= NextTickTime;
    }

    public void AdvanceTick(float now)
    {
        if (Definition == null || Definition.tickInterval <= 0f)
        {
            NextTickTime = float.PositiveInfinity;
            return;
        }

        NextTickTime += Definition.tickInterval;
        if (NextTickTime <= now)
            NextTickTime = now + Definition.tickInterval;
    }

    public int IncrementTriggerCounter(int ruleIndex)
    {
        if (ruleIndex < 0 || ruleIndex >= _triggerCounters.Length)
            return 0;

        _triggerCounters[ruleIndex]++;
        return _triggerCounters[ruleIndex];
    }

    public int GetTriggerCounter(int ruleIndex)
    {
        if (ruleIndex < 0 || ruleIndex >= _triggerCounters.Length)
            return 0;

        return _triggerCounters[ruleIndex];
    }

    public void SetTriggerCounter(int ruleIndex, int value)
    {
        if (ruleIndex < 0 || ruleIndex >= _triggerCounters.Length)
            return;

        _triggerCounters[ruleIndex] = Mathf.Max(0, value);
    }
}
