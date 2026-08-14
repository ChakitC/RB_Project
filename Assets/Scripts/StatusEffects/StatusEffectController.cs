using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-115)]
public sealed class StatusEffectController : MonoBehaviour, IStatModifierProvider
{
    [Header("Refs")]
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private StatsHub statsHub;
    [SerializeField] private StateHub stateHub;
    [SerializeField] private HealthSystem healthSystem;

    [Header("Visual")]
    [SerializeField] private bool autoCreateVfxPresenter = true;
    [SerializeField] private Transform vfxPresenterHost;
    [SerializeField] private string vfxPresenterHostName = "CharacterVisual_System";

    [Header("Debug")]
    [SerializeField] private bool debugInInspector = true;
    [SerializeField] private int dbgActiveEffectCount;
    [TextArea(2, 8)]
    [SerializeField] private string dbgBuffs;
    [TextArea(2, 8)]
    [SerializeField] private string dbgDebuffs;
    [TextArea(2, 8)]
    [SerializeField] private string dbgNeutralEffects;

    readonly List<StatusEffectInstance> _activeEffects = new();

    public IReadOnlyList<StatusEffectInstance> ActiveEffects => _activeEffects;

    public event Action StatModifiersChanged;
    public event Action EffectsChanged;
    public event Action<StatusEffectEvent> EffectLifecycleChanged;

    void Awake()
    {
        ResolveReferences();

        if (autoCreateVfxPresenter)
            EnsureVfxPresenter();
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

        if (!statsHub && ctx != null)
            statsHub = ctx.StatsHub;
        if (!statsHub)
            TryGetComponent(out statsHub);
        if (!statsHub && ctx != null)
            statsHub = ctx.GetComponentInChildren<StatsHub>(true);

        if (!stateHub && ctx != null)
            stateHub = ctx.stateHub;
        if (!stateHub)
            TryGetComponent(out stateHub);
        if (!stateHub && ctx != null)
            stateHub = ctx.GetComponentInChildren<StateHub>(true);

        if (!healthSystem && ctx != null)
            healthSystem = ctx.HealthSystem;
        if (!healthSystem)
            TryGetComponent(out healthSystem);
        if (!healthSystem && ctx != null)
            healthSystem = ctx.GetComponentInChildren<HealthSystem>(true);
    }

    void EnsureVfxPresenter()
    {
        if (TryGetComponent<StatusEffectVfxPresenter>(out var localPresenter))
        {
            localPresenter.Bind(this);
            return;
        }

        Transform host = ResolveVfxPresenterHost();

        if (host != transform && host.TryGetComponent(out StatusEffectVfxPresenter hostPresenter))
        {
            hostPresenter.Bind(this);
            return;
        }

        if (host == transform)
        {
            StatusEffectVfxPresenter childPresenter = GetComponentInChildren<StatusEffectVfxPresenter>(true);
            if (childPresenter)
            {
                childPresenter.Bind(this);
                return;
            }
        }

        var presenter = host.gameObject.AddComponent<StatusEffectVfxPresenter>();
        presenter.Bind(this);
    }

    Transform ResolveVfxPresenterHost()
    {
        if (vfxPresenterHost)
            return vfxPresenterHost;

        Transform ownerRoot = ctx ? ctx.transform : null;
        if (!ownerRoot)
        {
            var ownerContext = GetComponentInParent<CharacteContext>();
            if (ownerContext)
                ownerRoot = ownerContext.transform;
        }

        Transform namedHost = FindChildByName(ownerRoot, vfxPresenterHostName);
        if (namedHost)
            return namedHost;

        namedHost = FindChildByName(transform.parent, vfxPresenterHostName);
        if (namedHost)
            return namedHost;

        return transform;
    }

    static Transform FindChildByName(Transform root, string targetName)
    {
        if (!root || string.IsNullOrWhiteSpace(targetName))
            return null;

        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), targetName);
            if (found)
                return found;
        }

        return null;
    }

    void OnEnable()
    {
        ResolveReferences();
        NotifyStatModifiersChanged();
        SyncControlState();
        RefreshDebugSnapshot();

        if (healthSystem != null)
            healthSystem.CharacterDead += HandleCharacterDead;
    }

    void OnDisable()
    {
        if (healthSystem != null)
            healthSystem.CharacterDead -= HandleCharacterDead;

        NotifyStatModifiersChanged();
        if (stateHub != null)
            stateHub.SetStatusEffectControlState(ControlBlockFlags.None, false);
        RefreshDebugSnapshot();
    }

    void HandleCharacterDead()
    {
        ClearAllEffects();
    }

    void Update()
    {
        Tick(Time.deltaTime);

        if (debugInInspector)
            RefreshDebugSnapshot();
    }

    /// <summary>
    /// Overload สำหรับระบบที่ไม่ใช่ authored application (ไม่มี StatusApplicationSpec ให้ถือ) — ใช้ค่าบาลานซ์
    /// ของ StatusEffectDef ทั้งหมด. Skill/Passive/Pickup effect/Projectile ต้องใช้ Spec overload ด้านล่างแทน.
    /// </summary>
    public StatusEffectInstance ApplyEffect(StatusEffectDef definition, GameObject source = null, int initialStacks = 1)
    {
        string appliedById = source != null ? $"actor:{source.GetInstanceID()}" : "system";

        return ApplyEffectCore(
            definition,
            null,
            source,
            initialStacks,
            appliedById,
            0,
            0,
            PassiveEventOrigin.External,
            null,
            null,
            0f);
    }

    /// <summary>
    /// Apply site ที่ authored ค่าบาลานซ์เอง (StatusApplicationSpec) แทนที่จะใช้ค่าจาก StatusEffectDef ตรงๆ.
    /// <paramref name="fallbackDuration"/> คือ duration ระดับ apply site (เช่น FinalSkillStats.effectDuration
    /// หรือ taunt duration) ที่ใช้เมื่อ spec ไม่ได้ override duration ไว้เอง — ส่งมาเป็นพารามิเตอร์ตรงๆ
    /// ไม่ต้อง merge ลง spec ที่ serialize อยู่ใน asset.
    /// </summary>
    public StatusEffectInstance ApplyEffect(StatusApplicationSpec spec, GameObject source = null, float fallbackDuration = 0f)
    {
        if (spec == null || spec.effect == null)
            return null;

        string appliedById = source != null ? $"actor:{source.GetInstanceID()}" : "system";

        return ApplyEffect(spec, source, appliedById, 0, 0, PassiveEventOrigin.External, null, null, fallbackDuration);
    }

    public StatusEffectInstance ApplyEffect(
        StatusApplicationSpec spec,
        GameObject source,
        string appliedById,
        ulong chainId,
        int depth,
        PassiveEventOrigin origin,
        string originPassiveId = null,
        string originRuleId = null,
        float fallbackDuration = 0f)
    {
        if (spec == null || spec.effect == null)
            return null;

        return ApplyEffectCore(
            spec.effect,
            spec,
            source,
            Mathf.Max(1, spec.stacks),
            appliedById,
            chainId,
            depth,
            origin,
            originPassiveId,
            originRuleId,
            fallbackDuration);
    }

    StatusEffectInstance ApplyEffectCore(
        StatusEffectDef definition,
        StatusApplicationSpec spec,
        GameObject source,
        int initialStacks,
        string appliedById,
        ulong chainId,
        int depth,
        PassiveEventOrigin origin,
        string originPassiveId,
        string originRuleId,
        float fallbackDuration)
    {
        if (definition == null)
            return null;

        float now = Time.time;
        int clampedStacks = Mathf.Max(0, initialStacks);
        string sourceKey = definition.separatePerSource ? appliedById : null;

        if (definition.stackMode == StackMode.IndependentInstances)
        {
            var instance = CreateInstance(definition, spec, source, clampedStacks, now, appliedById, chainId, depth, origin, originPassiveId, originRuleId, fallbackDuration);
            _activeEffects.Add(instance);

            List<StatusEffectEvent> newLifecycleEvents = null;
            AddStatusEffectEvent(ref newLifecycleEvents, StatusEffectEventType.AppliedNew, instance, 0, instance.CurrentStacks);
            EnforceInstanceCap(definition, ref newLifecycleEvents);
            NotifyEffectsChanged(newLifecycleEvents);
            return instance;
        }

        var existing = FindActiveEffect(definition, sourceKey);
        if (existing == null)
        {
            existing = CreateInstance(definition, spec, source, clampedStacks, now, appliedById, chainId, depth, origin, originPassiveId, originRuleId, fallbackDuration);
            _activeEffects.Add(existing);

            List<StatusEffectEvent> newLifecycleEvents = null;
            AddStatusEffectEvent(ref newLifecycleEvents, StatusEffectEventType.AppliedNew, existing, 0, existing.CurrentStacks);
            EnforceInstanceCap(definition, ref newLifecycleEvents);
            NotifyEffectsChanged(newLifecycleEvents);
            return existing;
        }

        existing.UpdateSource(source);
        existing.UpdateContext(appliedById, chainId, depth, origin, originPassiveId, originRuleId);
        // instance เดิมที่ถูก apply ซ้ำนับเป็น application ล่าสุด — ระบบที่ใช้ latest-wins (เช่น multi-Taunt)
        // ต้องเห็นว่ามันใหม่กว่า instance ที่ไม่ได้ถูกแตะ
        existing.MarkReapplied();
        int oldStacks = existing.CurrentStacks;

        switch (definition.stackMode)
        {
            case StackMode.RefreshDuration:
                existing.AdoptDuration(spec, fallbackDuration);
                existing.RefreshDuration();
                if (spec != null)
                    existing.AdoptMagnitude(spec, now);
                break;

            case StackMode.AddStackAndRefresh:
                existing.AdoptDuration(spec, fallbackDuration);
                existing.AddStacks(clampedStacks, definition.ClampedMaxStacks);
                existing.RefreshDuration();
                if (spec != null)
                    existing.AdoptMagnitude(spec, now);
                break;

            case StackMode.StrongestOnly:
                existing.AdoptDuration(spec, fallbackDuration);
                existing.RefreshDurationNoShorten();
                if (spec != null)
                    ApplyStrongestOnlyMagnitude(existing, spec, now);
                break;
        }

        StatusEffectEventType eventType = existing.CurrentStacks != oldStacks
            ? StatusEffectEventType.StackChanged
            : StatusEffectEventType.Refreshed;

        NotifyEffectsChanged(eventType, existing, oldStacks, existing.CurrentStacks);
        return existing;
    }

    /// <summary>
    /// StackMode.StrongestOnly: ถ้า incoming shape ต่างจาก existing (คนละ stat/op) เทียบ score กันไม่ได้จริง — ตัวหลังชนะ.
    /// ถ้า shape เดียวกัน เทียบ StrengthScore แล้วให้ตัวแรงกว่าชนะ.
    /// </summary>
    static void ApplyStrongestOnlyMagnitude(StatusEffectInstance existing, StatusApplicationSpec spec, float now)
    {
        List<StatusEffectModifier> incomingModifiers = StatusEffectInstance.ResolveModifiers(spec, existing.Definition);
        bool sameShape = StatusEffectInstance.HasSameModifierShape(existing.ResolvedModifiers, incomingModifiers);

        if (!sameShape)
        {
            existing.AdoptMagnitude(spec, now);
            return;
        }

        float incomingScore = StatusEffectInstance.ComputeStrengthScore(incomingModifiers);
        if (incomingScore > existing.StrengthScore)
            existing.AdoptMagnitude(spec, now);
    }

    const int MaxInstancesPerEffect = 8;

    /// <summary>กันโตไม่มีเพดานเมื่อหลายแหล่ง/IndependentInstances สร้าง instance ใหม่เรื่อยๆ — evict ตัวที่ TimeLeft น้อยสุดเมื่อเกิน cap</summary>
    void EnforceInstanceCap(StatusEffectDef definition, ref List<StatusEffectEvent> lifecycleEvents)
    {
        if (definition == null)
            return;

        int count = 0;
        int weakestIndex = -1;
        float weakestTimeLeft = float.PositiveInfinity;

        for (int i = 0; i < _activeEffects.Count; i++)
        {
            if (!MatchesDefinition(_activeEffects[i], definition))
                continue;

            count++;
            if (_activeEffects[i].TimeLeft < weakestTimeLeft)
            {
                weakestTimeLeft = _activeEffects[i].TimeLeft;
                weakestIndex = i;
            }
        }

        if (count <= MaxInstancesPerEffect || weakestIndex < 0)
            return;

        var evicted = _activeEffects[weakestIndex];
        AddStatusEffectEvent(ref lifecycleEvents, StatusEffectEventType.Removed, evicted, evicted.CurrentStacks, 0);
        _activeEffects.RemoveAt(weakestIndex);
    }

    public void RemoveEffect(StatusEffectDef definition)
    {
        if (definition == null)
            return;

        List<StatusEffectEvent> lifecycleEvents = null;
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            if (!MatchesDefinition(_activeEffects[i], definition))
                continue;

            var removedInstance = _activeEffects[i];
            AddStatusEffectEvent(ref lifecycleEvents, StatusEffectEventType.Removed, removedInstance, removedInstance.CurrentStacks, 0);
            _activeEffects.RemoveAt(i);
        }

        if (lifecycleEvents != null)
            NotifyEffectsChanged(lifecycleEvents);
    }

    public void RemoveEffect(string effectId)
    {
        if (string.IsNullOrWhiteSpace(effectId))
            return;

        List<StatusEffectEvent> lifecycleEvents = null;
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            var definition = _activeEffects[i]?.Definition;
            if (definition == null || !string.Equals(definition.effectId, effectId, StringComparison.Ordinal))
                continue;

            var removedInstance = _activeEffects[i];
            AddStatusEffectEvent(ref lifecycleEvents, StatusEffectEventType.Removed, removedInstance, removedInstance.CurrentStacks, 0);
            _activeEffects.RemoveAt(i);
        }

        if (lifecycleEvents != null)
            NotifyEffectsChanged(lifecycleEvents);
    }

    public void ClearAllEffects()
    {
        if (_activeEffects.Count == 0)
            return;

        List<StatusEffectEvent> lifecycleEvents = null;
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            var removedInstance = _activeEffects[i];
            AddStatusEffectEvent(ref lifecycleEvents, StatusEffectEventType.Removed, removedInstance, removedInstance.CurrentStacks, 0);
            _activeEffects.RemoveAt(i);
        }

        if (lifecycleEvents != null)
            NotifyEffectsChanged(lifecycleEvents);
    }

    public void NotifyTrigger(EffectTriggerType triggerType, GameObject source = null)
    {
        if (triggerType == EffectTriggerType.None || _activeEffects.Count == 0)
            return;

        bool changed = false;
        List<StatusEffectEvent> lifecycleEvents = null;

        for (int i = 0; i < _activeEffects.Count; i++)
        {
            var instance = _activeEffects[i];
            var definition = instance?.Definition;
            if (definition == null || definition.triggerRules == null || definition.triggerRules.Count == 0)
                continue;

            for (int ruleIndex = 0; ruleIndex < definition.triggerRules.Count; ruleIndex++)
            {
                var rule = definition.triggerRules[ruleIndex];
                if (rule == null || rule.triggerType != triggerType)
                    continue;

                int counter = instance.IncrementTriggerCounter(ruleIndex);
                int requiredCount = Mathf.Max(1, rule.requiredCount);

                while (counter >= requiredCount)
                {
                    int oldStacks = instance.CurrentStacks;
                    bool ruleChanged = false;

                    if (definition.stackMode == StackMode.AddStackAndRefresh)
                    {
                        int maxStacks = ResolveRuleMaxStacks(definition, rule);
                        instance.AddStacks(rule.grantedStacks, maxStacks);
                        instance.RefreshDuration();
                        instance.UpdateSource(source);
                        ruleChanged = true;
                        changed = true;
                    }
                    else if (definition.stackMode == StackMode.RefreshDuration ||
                             definition.stackMode == StackMode.StrongestOnly)
                    {
                        instance.RefreshDuration();
                        instance.UpdateSource(source);
                        ruleChanged = true;
                        changed = true;
                    }

                    if (ruleChanged)
                    {
                        StatusEffectEventType eventType = instance.CurrentStacks != oldStacks
                            ? StatusEffectEventType.StackChanged
                            : StatusEffectEventType.Refreshed;

                        AddStatusEffectEvent(ref lifecycleEvents, eventType, instance, oldStacks, instance.CurrentStacks);
                    }

                    counter = rule.resetCounterAfterGrant
                        ? 0
                        : counter - requiredCount;

                    instance.SetTriggerCounter(ruleIndex, counter);

                    if (rule.resetCounterAfterGrant)
                        break;
                }
            }
        }

        if (changed)
            NotifyEffectsChanged(lifecycleEvents);
    }

    public bool HasEffect(StatusEffectDef definition)
    {
        return FindActiveEffect(definition) != null;
    }

    public StatusEffectInstance FindActiveEffect(StatusEffectDef definition, string sourceKey = null)
    {
        if (definition == null)
            return null;

        for (int i = 0; i < _activeEffects.Count; i++)
        {
            if (MatchesDefinition(_activeEffects[i], definition, sourceKey))
                return _activeEffects[i];
        }

        return null;
    }

    void Tick(float dt)
    {
        if (_activeEffects.Count == 0)
            return;

        bool changed = false;
        List<StatusEffectEvent> lifecycleEvents = null;
        float now = Time.time;

        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            var instance = _activeEffects[i];
            var definition = instance?.Definition;
            if (definition == null)
            {
                _activeEffects.RemoveAt(i);
                changed = true;
                continue;
            }

            while (instance.ShouldTick(now))
            {
                ApplyTick(instance);
                instance.AdvanceTick(now);
            }

            instance.TickLifetime(dt);

            if (!instance.IsExpired())
                continue;

            AddStatusEffectEvent(ref lifecycleEvents, StatusEffectEventType.Removed, instance, instance.CurrentStacks, 0);
            _activeEffects.RemoveAt(i);
            changed = true;
        }

        if (changed)
            NotifyEffectsChanged(lifecycleEvents);
    }

    void ApplyTick(StatusEffectInstance instance)
    {
        var definition = instance?.Definition;
        if (definition == null || Mathf.Approximately(instance.ResolvedTickDamage, 0f))
            return;

        int stacks = Mathf.Max(0, instance.CurrentStacks);
        if (stacks <= 0)
            return;

        float damage = instance.ResolvedTickDamage * stacks;
        if (Mathf.Approximately(damage, 0f))
            return;

        // ทั้งฝั่ง damage และ heal ต้องหยุด tick เมื่อเป้าหมายล้ม/ตาย ไม่งั้น VFX ของ tick
        // จะเล่นค้างบนตัวที่ล้มอยู่ทั้งที่เลือดไม่ขยับ (Heal ถูกบล็อกที่ CanHeal อยู่แล้ว)
        IDamageable targetDamageable = healthSystem;
        if (targetDamageable == null && ctx != null)
            targetDamageable = ctx.HealthSystem;
        if (targetDamageable == null)
            targetDamageable = GetComponentInParent<IDamageable>();

        if (targetDamageable == null || !targetDamageable.IsAlive)
            return;

        if (damage < 0f)
        {
            ApplyTickHeal(-damage);
            PublishStatusEffectEvent(StatusEffectEventType.Ticked, instance, stacks, stacks);
            return;
        }

        GameObject physicalSource = instance.Attribution.PhysicalActor;
        var damageContext = new DamageContext(
            damage,
            physicalSource,
            BuildStatusEffectIdentity(definition),
            definition.effectId,
            instance.ChainId == 0 ? CombatEventBus.NextChainId() : instance.ChainId,
            instance.Depth + 1,
            PassiveEventOrigin.StatusEffect,
            instance.OriginPassiveId,
            instance.OriginRuleId,
            attribution: instance.Attribution);

        DamageResult result = targetDamageable.TakeDamage(in damageContext);
        if (result.Applied)
            PublishStatusDamageCombatEvents(instance, definition, targetDamageable, result, damageContext);
        PublishStatusEffectEvent(StatusEffectEventType.Ticked, instance, stacks, stacks);
    }

    void ApplyTickHeal(float amount)
    {
        if (amount <= 0f)
            return;

        HealthSystem health = ctx != null ? ctx.HealthSystem : GetComponentInParent<HealthSystem>();
        health?.Heal(amount);
    }

    void PublishStatusDamageCombatEvents(
        StatusEffectInstance instance,
        StatusEffectDef definition,
        IDamageable target,
        in DamageResult result,
        in DamageContext damageContext)
    {
        CombatEventBus sourceBus = instance.Attribution.CreditedEventBus;
        if (sourceBus == null && instance.Source != null)
            sourceBus = instance.Source.GetComponentInParent<CombatEventBus>();
        if (sourceBus == null)
            return;

        Component targetComponent = target as Component;
        GameObject targetObject = targetComponent != null ? targetComponent.gameObject : gameObject;
        GameObject sourceObject = instance.Attribution.PhysicalActor;
        GameObject creditedActor = instance.Attribution.CreditedActor;
        var metadata = new CombatEventMetadata(
            result.RequestedDamage,
            result.ResolvedDamage,
            result.AppliedDamage,
            result.HealthBeforeHit,
            result.MaxHealth,
            sourceKind: CombatSourceKind.Status);
        PublishStatusDamageEvent(sourceBus, PassiveEventType.Hit, sourceObject, creditedActor, targetObject,
            definition, result.AppliedDamage, damageContext, metadata);
        if (result.Killed)
            PublishStatusDamageEvent(sourceBus, PassiveEventType.Kill, sourceObject, creditedActor, targetObject,
                definition, result.AppliedDamage, damageContext, metadata);
    }

    static void PublishStatusDamageEvent(
        CombatEventBus bus,
        PassiveEventType type,
        GameObject source,
        GameObject actor,
        GameObject target,
        StatusEffectDef definition,
        float value,
        in DamageContext damageContext,
        in CombatEventMetadata metadata)
    {
        var context = new PassiveEventContext(
            type,
            actor != null ? actor : source,
            source,
            target,
            BuildStatusEffectIdentity(definition),
            damageContext.AttackId,
            value,
            Time.timeAsDouble,
            damageContext.ChainId,
            damageContext.Depth,
            PassiveEventOrigin.StatusEffect,
            damageContext.OriginPassiveId,
            damageContext.OriginRuleId,
            metadata);
        bus.Publish(context);
    }

    StatusEffectInstance CreateInstance(
        StatusEffectDef definition,
        StatusApplicationSpec spec,
        GameObject source,
        int initialStacks,
        float now,
        string appliedById,
        ulong chainId,
        int depth,
        PassiveEventOrigin origin,
        string originPassiveId,
        string originRuleId,
        float fallbackDuration)
    {
        int startingStacks = definition.stackMode == StackMode.RefreshDuration ||
                             definition.stackMode == StackMode.StrongestOnly
            ? Mathf.Clamp(initialStacks <= 0 ? 1 : initialStacks, 0, definition.ClampedMaxStacks)
            : Mathf.Clamp(initialStacks, 0, definition.ClampedMaxStacks);

        return new StatusEffectInstance(
            definition,
            source,
            startingStacks,
            now,
            appliedById,
            chainId,
            depth,
            origin,
            originPassiveId,
            originRuleId,
            spec,
            fallbackDuration);
    }

    /// <summary>ถอด instance ทุกตัวของ effect ที่ติด tag นี้ (เช่น ล้าง taunt ตอน sensor reset).</summary>
    public void RemoveEffectsWithTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return;

        List<StatusEffectEvent> lifecycleEvents = null;
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            if (!StatusEffectTags.Has(_activeEffects[i]?.Definition, tag))
                continue;

            var removedInstance = _activeEffects[i];
            AddStatusEffectEvent(ref lifecycleEvents, StatusEffectEventType.Removed, removedInstance, removedInstance.CurrentStacks, 0);
            _activeEffects.RemoveAt(i);
        }

        if (lifecycleEvents != null)
            NotifyEffectsChanged(lifecycleEvents);
    }

    int ResolveRuleMaxStacks(StatusEffectDef definition, StatusEffectTriggerRule rule)
    {
        int effectMaxStacks = definition.ClampedMaxStacks;
        if (rule == null || rule.maxStacks <= 0)
            return effectMaxStacks;

        return Mathf.Min(effectMaxStacks, rule.maxStacks);
    }

    bool MatchesDefinition(StatusEffectInstance instance, StatusEffectDef definition, string sourceKey = null)
    {
        if (instance == null || definition == null || instance.Definition == null)
            return false;

        bool defMatches = instance.Definition == definition;
        if (!defMatches)
        {
            if (string.IsNullOrWhiteSpace(definition.effectId) ||
                string.IsNullOrWhiteSpace(instance.Definition.effectId))
                return false;

            defMatches = string.Equals(instance.Definition.effectId, definition.effectId, StringComparison.Ordinal);
        }

        if (!defMatches)
            return false;

        if (string.IsNullOrEmpty(sourceKey))
            return true;

        return string.Equals(instance.AppliedById, sourceKey, StringComparison.Ordinal);
    }

    void NotifyEffectsChanged()
    {
        NotifyEffectsChangedCore();
        EffectsChanged?.Invoke();
    }

    void NotifyEffectsChanged(StatusEffectEventType eventType, StatusEffectInstance instance, int oldStacks, int newStacks)
    {
        NotifyEffectsChangedCore();
        PublishStatusEffectEvent(eventType, instance, oldStacks, newStacks);
        EffectsChanged?.Invoke();
    }

    void NotifyEffectsChanged(List<StatusEffectEvent> lifecycleEvents)
    {
        NotifyEffectsChangedCore();
        PublishStatusEffectEvents(lifecycleEvents);
        EffectsChanged?.Invoke();
    }

    void NotifyEffectsChangedCore()
    {
        NotifyStatModifiersChanged();
        SyncControlState();
        RefreshDebugSnapshot();
    }

    void NotifyStatModifiersChanged()
    {
        var handler = StatModifiersChanged;
        if (handler != null)
            handler.Invoke();
        else
            statsHub?.MarkDirty();
    }

    void AddStatusEffectEvent(
        ref List<StatusEffectEvent> buffer,
        StatusEffectEventType eventType,
        StatusEffectInstance instance,
        int oldStacks,
        int newStacks)
    {
        if (instance == null || instance.Definition == null)
            return;

        buffer ??= new List<StatusEffectEvent>();
        buffer.Add(CreateStatusEffectEvent(eventType, instance, oldStacks, newStacks));
    }

    StatusEffectEvent CreateStatusEffectEvent(
        StatusEffectEventType eventType,
        StatusEffectInstance instance,
        int oldStacks,
        int newStacks)
    {
        return new StatusEffectEvent(
            this,
            eventType,
            instance,
            instance?.Definition,
            instance?.Source,
            oldStacks,
            newStacks);
    }

    void PublishStatusEffectEvent(StatusEffectEventType eventType, StatusEffectInstance instance, int oldStacks, int newStacks)
    {
        if (instance == null || instance.Definition == null)
            return;

        EffectLifecycleChanged?.Invoke(CreateStatusEffectEvent(eventType, instance, oldStacks, newStacks));
    }

    void PublishStatusEffectEvents(List<StatusEffectEvent> lifecycleEvents)
    {
        if (lifecycleEvents == null || lifecycleEvents.Count == 0)
            return;

        for (int i = 0; i < lifecycleEvents.Count; i++)
            EffectLifecycleChanged?.Invoke(lifecycleEvents[i]);
    }

    public void AppendStatModifiers(List<RuntimeStatModifier> buffer)
    {
        if (buffer == null || _activeEffects.Count == 0)
            return;

        for (int i = 0; i < _activeEffects.Count; i++)
        {
            var instance = _activeEffects[i];
            var definition = instance?.Definition;
            if (definition == null || instance.CurrentStacks <= 0)
                continue;

            var modifiers = instance.ResolvedModifiers;
            if (modifiers == null || modifiers.Count == 0)
                continue;

            string modifierKey = BuildStatusEffectIdentity(definition);

            for (int j = 0; j < modifiers.Count; j++)
            {
                var modifier = modifiers[j];
                if (modifier == null)
                    continue;

                float stackedValue = modifier.operation == ModifierOp.Multiply
                    ? Mathf.Pow(modifier.value, instance.CurrentStacks)
                    : modifier.value * instance.CurrentStacks;

                buffer.Add(new RuntimeStatModifier(
                    modifier.statType,
                    modifier.operation,
                    stackedValue,
                    modifierKey));
            }
        }
    }

    static string BuildStatusEffectIdentity(StatusEffectDef definition)
    {
        string effectId = definition != null && !string.IsNullOrWhiteSpace(definition.effectId)
            ? definition.effectId
            : definition != null ? definition.name : "unknown";

        return $"status:{effectId}";
    }

    void SyncControlState()
    {
        if (!stateHub)
            return;

        ControlBlockFlags controlBlocks = ControlBlockFlags.None;
        bool stunned = false;

        for (int i = 0; i < _activeEffects.Count; i++)
        {
            var instance = _activeEffects[i];
            var definition = instance?.Definition;
            if (definition == null || instance.CurrentStacks <= 0)
                continue;

            controlBlocks |= definition.controlBlocks;
            stunned |= definition.pushStunnedState;
        }

        stateHub.SetStatusEffectControlState(controlBlocks, stunned);
    }

    void RefreshDebugSnapshot()
    {
        if (!debugInInspector)
            return;

        dbgActiveEffectCount = _activeEffects.Count;
        dbgBuffs = BuildDebugList(StatusEffectCategory.Buff);
        dbgDebuffs = BuildDebugList(StatusEffectCategory.Debuff);
        dbgNeutralEffects = BuildDebugList(StatusEffectCategory.Neutral);
    }

    string BuildDebugList(StatusEffectCategory category)
    {
        if (_activeEffects.Count == 0)
            return "<none>";

        List<string> lines = null;

        for (int i = 0; i < _activeEffects.Count; i++)
        {
            var instance = _activeEffects[i];
            var definition = instance?.Definition;
            if (definition == null || definition.category != category)
                continue;

            lines ??= new List<string>();
            lines.Add(FormatDebugLine(instance));
        }

        if (lines == null || lines.Count == 0)
            return "<none>";

        return string.Join("\n", lines);
    }

    string FormatDebugLine(StatusEffectInstance instance)
    {
        var definition = instance.Definition;
        string label = !string.IsNullOrWhiteSpace(definition.effectId)
            ? definition.effectId
            : definition.name;

        string durationText = instance.IsPermanent
            ? "perm"
            : $"{Mathf.Max(0f, instance.TimeLeft):0.0}s";

        return $"{label} x{instance.CurrentStacks} ({durationText})";
    }
}
