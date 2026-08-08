using System;
using System.Collections.Generic;
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
        string originRuleId,
        float durationOverride = 0f,
        StatusApplicationSpec spec = null)
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
        EffectiveDuration = ResolveEffectiveDuration(definition, durationOverride);
        TimeLeft = !IsPermanent ? EffectiveDuration : float.PositiveInfinity;
        NextTickTime = definition != null && definition.tickInterval > 0f
            ? now + definition.tickInterval
            : float.PositiveInfinity;

        _triggerCounters = definition != null && definition.triggerRules != null
            ? new int[definition.triggerRules.Count]
            : Array.Empty<int>();

        ResolvedModifiers = ResolveModifiers(spec, definition);
        ResolvedTickDamage = ResolveTickDamage(spec, definition);
        StrengthScore = ComputeStrengthScore(ResolvedModifiers);
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
    public float EffectiveDuration { get; private set; }
    public bool IsPermanent => Definition == null || EffectiveDuration <= 0f;

    /// <summary>ค่าบาลานซ์จริงของ instance นี้ — resolve ครั้งเดียวตอนสร้าง (หรือเมื่อ AdoptMagnitude) จาก spec หรือ fallback ไป Definition. เป็น copy เสมอ ไม่ alias asset.</summary>
    public IReadOnlyList<StatusEffectModifier> ResolvedModifiers { get; private set; }
    public float ResolvedTickDamage { get; private set; }
    public float StrengthScore { get; private set; }

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

    public void SetDurationOverride(float durationOverride)
    {
        EffectiveDuration = ResolveEffectiveDuration(Definition, durationOverride);
    }

    public void RefreshDuration()
    {
        if (Definition == null || IsPermanent)
            return;

        TimeLeft = EffectiveDuration;
    }

    /// <summary>เหมือน RefreshDuration แต่ไม่ยอมให้ TimeLeft ที่เหลืออยู่สั้นลง — ใช้กับ StackMode.StrongestOnly เพื่อไม่ให้ application ที่อ่อนกว่าตัด duration ของตัวแรงทิ้ง</summary>
    public void RefreshDurationNoShorten()
    {
        if (Definition == null || IsPermanent)
            return;

        TimeLeft = Mathf.Max(TimeLeft, EffectiveDuration);
    }

    /// <summary>เปลี่ยนค่าบาลานซ์ของ instance นี้เป็นของ spec ที่ระบุ (ใช้ตอน StrongestOnly ตัดสินว่า incoming แรงกว่า หรือ RefreshDuration/AddStackAndRefresh ที่ยอมรับค่าล่าสุดเสมอ)</summary>
    public void AdoptMagnitude(StatusApplicationSpec spec)
    {
        ResolvedModifiers = ResolveModifiers(spec, Definition);
        ResolvedTickDamage = ResolveTickDamage(spec, Definition);
        StrengthScore = ComputeStrengthScore(ResolvedModifiers);
    }

    static float ResolveEffectiveDuration(StatusEffectDef definition, float durationOverride)
    {
        if (durationOverride > 0f)
            return durationOverride;

        return definition != null ? definition.duration : 0f;
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

    // ---- magnitude resolution (static — shared between instance construction and StrongestOnly comparison in the controller) ----

    public static List<StatusEffectModifier> ResolveModifiers(StatusApplicationSpec spec, StatusEffectDef definition)
    {
        List<StatusEffectModifier> source = spec != null && spec.HasModifierOverride
            ? spec.modifiers
            : definition != null ? definition.modifiers : null;

        return CopyModifiers(source);
    }

    public static float ResolveTickDamage(StatusApplicationSpec spec, StatusEffectDef definition)
    {
        if (spec != null && !Mathf.Approximately(spec.tickDamageOverride, 0f))
            return spec.tickDamageOverride;

        return definition != null ? definition.tickDamage : 0f;
    }

    static List<StatusEffectModifier> CopyModifiers(List<StatusEffectModifier> source)
    {
        var copy = new List<StatusEffectModifier>(source?.Count ?? 0);
        if (source == null)
            return copy;

        for (int i = 0; i < source.Count; i++)
        {
            var m = source[i];
            if (m == null)
                continue;

            copy.Add(new StatusEffectModifier { statType = m.statType, operation = m.operation, value = m.value });
        }

        return copy;
    }

    public static float ComputeStrengthScore(IReadOnlyList<StatusEffectModifier> modifiers)
    {
        if (modifiers == null)
            return 0f;

        float score = 0f;
        for (int i = 0; i < modifiers.Count; i++)
            score += ScoreModifier(modifiers[i]);

        return score;
    }

    static float ScoreModifier(StatusEffectModifier m)
    {
        if (m == null)
            return 0f;

        return m.operation switch
        {
            ModifierOp.Multiply => Mathf.Abs(1f - m.value),
            ModifierOp.AddPercent => Mathf.Abs(m.value) * 0.01f,
            _ => Mathf.Abs(m.value),
        };
    }

    /// <summary>เทียบว่า modifier สองชุดมี shape เดียวกันไหม (multiset ของ statType+operation, ไม่สนใจ value) — StrengthScore เทียบกันตรงๆ ได้ก็ต่อเมื่อ shape เหมือนกัน</summary>
    public static bool HasSameModifierShape(IReadOnlyList<StatusEffectModifier> a, IReadOnlyList<StatusEffectModifier> b)
    {
        a ??= Array.Empty<StatusEffectModifier>();
        b ??= Array.Empty<StatusEffectModifier>();

        if (a.Count != b.Count)
            return false;

        var remaining = new List<StatusEffectModifier>(b);
        for (int i = 0; i < a.Count; i++)
        {
            var m = a[i];
            int matchIndex = -1;
            for (int j = 0; j < remaining.Count; j++)
            {
                var candidate = remaining[j];
                if (candidate != null && m != null &&
                    candidate.statType == m.statType && candidate.operation == m.operation)
                {
                    matchIndex = j;
                    break;
                }
            }

            if (matchIndex < 0)
                return false;

            remaining.RemoveAt(matchIndex);
        }

        return true;
    }
}
