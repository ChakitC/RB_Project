using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class StatusEffectInstance
{
    readonly int[] _triggerCounters;

    /// <summary>
    /// ลำดับ application ทั่วทั้งเกม — ใช้ตัดสิน "ตัวล่าสุดชนะ" แบบ deterministic (เช่น multi-Taunt)
    /// แทนการอาศัยลำดับใน list ซึ่งเปลี่ยนได้เมื่อ instance ถูก evict/หมดอายุ.
    /// </summary>
    static ulong _nextApplicationSequence = 1;

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
        StatusApplicationSpec spec = null,
        float fallbackDuration = 0f)
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
        ApplicationSequence = _nextApplicationSequence++;
        EffectiveDuration = ResolveDuration(spec, definition, fallbackDuration);
        TimeLeft = !IsPermanent ? EffectiveDuration : float.PositiveInfinity;

        _triggerCounters = definition != null && definition.triggerRules != null
            ? new int[definition.triggerRules.Count]
            : Array.Empty<int>();

        ResolvedModifiers = ResolveModifiers(spec, definition);
        ResolvedTickDamage = ResolveTickDamage(spec, definition);
        ResolvedTickInterval = ResolveTickInterval(spec, definition);
        StrengthScore = ComputeStrengthScore(ResolvedModifiers);
        Attribution = CombatAttributionSnapshot.FromPhysicalActor(source);
        ScheduleFirstTick(now);
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

    /// <summary>
    /// เวลาที่ควร tick ครั้งถัดไป — อยู่ใน owner-local status clock ของ <c>StatusEffectController</c>
    /// (ไม่ใช่ <c>Time.time</c>). ทุก <c>now</c> ที่คลาสนี้รับมาต้องมาจาก clock ตัวเดียวกันนั้น.
    /// </summary>
    public float NextTickTime { get; private set; }
    public float EffectiveDuration { get; private set; }
    public bool IsPermanent => Definition == null || EffectiveDuration <= 0f;

    /// <summary>เลขลำดับ application ที่เพิ่มขึ้นเรื่อยๆ — instance ที่ค่ามากกว่าคือตัวที่ถูก apply/refresh ทีหลัง.</summary>
    public ulong ApplicationSequence { get; private set; }

    /// <summary>ค่าบาลานซ์จริงของ instance นี้ — resolve ครั้งเดียวตอนสร้าง (หรือเมื่อ AdoptMagnitude) จาก spec หรือ fallback ไป Definition. เป็น copy เสมอ ไม่ alias asset.</summary>
    public IReadOnlyList<StatusEffectModifier> ResolvedModifiers { get; private set; }
    public float ResolvedTickDamage { get; private set; }
    public float ResolvedTickInterval { get; private set; }
    public float StrengthScore { get; private set; }
    public CombatAttributionSnapshot Attribution { get; private set; }

    /// <summary>เรียกเมื่อ instance เดิมถูก apply ซ้ำ (refresh/stack) — ทำให้ถือว่าเป็น application ล่าสุด.</summary>
    public void MarkReapplied()
    {
        ApplicationSequence = _nextApplicationSequence++;
    }

    public void UpdateSource(GameObject source)
    {
        if (source != null)
        {
            Source = source;
            CombatAttributionSnapshot snapshot = CombatAttributionSnapshot.FromPhysicalActor(source);
            if (snapshot.HasPhysicalActor || snapshot.HasCredit)
                Attribution = snapshot;
        }
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

    /// <summary>ตั้ง EffectiveDuration ใหม่จาก spec ของ application ล่าสุด (ยังไม่แตะ TimeLeft — ให้ RefreshDuration* เป็นคนทำ).</summary>
    public void AdoptDuration(StatusApplicationSpec spec, float fallbackDuration)
    {
        EffectiveDuration = ResolveDuration(spec, Definition, fallbackDuration);
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
    public void AdoptMagnitude(StatusApplicationSpec spec, float now)
    {
        ResolvedModifiers = ResolveModifiers(spec, Definition);
        ResolvedTickDamage = ResolveTickDamage(spec, Definition);
        StrengthScore = ComputeStrengthScore(ResolvedModifiers);

        float nextInterval = ResolveTickInterval(spec, Definition);
        if (!Mathf.Approximately(nextInterval, ResolvedTickInterval))
        {
            ResolvedTickInterval = nextInterval;
            ScheduleFirstTick(now);
        }
    }

    void ScheduleFirstTick(float now)
    {
        NextTickTime = ResolvedTickInterval > 0f
            ? now + ResolvedTickInterval
            : float.PositiveInfinity;
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
               ResolvedTickInterval > 0f &&
               now >= NextTickTime;
    }

    public void AdvanceTick(float now)
    {
        if (Definition == null || ResolvedTickInterval <= 0f)
        {
            NextTickTime = float.PositiveInfinity;
            return;
        }

        NextTickTime += ResolvedTickInterval;
        if (NextTickTime <= now)
            NextTickTime = now + ResolvedTickInterval;
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
        if (spec != null && spec.HasTickDamageOverride)
            return spec.tickDamageOverride;

        return definition != null ? definition.tickDamage : 0f;
    }

    public static float ResolveTickInterval(StatusApplicationSpec spec, StatusEffectDef definition)
    {
        if (spec != null && spec.HasTickIntervalOverride)
            return Mathf.Max(0f, spec.tickIntervalOverride);

        return definition != null ? Mathf.Max(0f, definition.tickInterval) : 0f;
    }

    /// <summary>
    /// ลำดับความสำคัญของ duration: application override (รวมค่า 0 = permanent) > fallback ของ apply site
    /// (เช่น FinalSkillStats.effectDuration หรือ taunt duration) > StatusEffectDef.duration.
    /// </summary>
    public static float ResolveDuration(StatusApplicationSpec spec, StatusEffectDef definition, float fallbackDuration)
    {
        if (spec != null && spec.HasDurationOverride)
            return Mathf.Max(0f, spec.durationOverride);

        if (fallbackDuration > 0f)
            return fallbackDuration;

        return definition != null ? definition.duration : 0f;
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
