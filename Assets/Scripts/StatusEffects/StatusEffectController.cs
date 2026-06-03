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
    }

    void OnDisable()
    {
        NotifyStatModifiersChanged();
        if (stateHub != null)
            stateHub.SetStatusEffectControlState(ControlBlockFlags.None, false);
        RefreshDebugSnapshot();
    }

    void Update()
    {
        Tick(Time.deltaTime);

        if (debugInInspector)
            RefreshDebugSnapshot();
    }

    public StatusEffectInstance ApplyEffect(StatusEffectDef definition, GameObject source = null, int initialStacks = 1)
    {
        string sourceId = definition != null && !string.IsNullOrWhiteSpace(definition.effectId)
            ? definition.effectId
            : null;

        return ApplyEffect(
            definition,
            source,
            initialStacks,
            sourceId,
            0,
            0,
            PassiveEventOrigin.External);
    }

    public StatusEffectInstance ApplyEffect(
        StatusEffectDef definition,
        GameObject source,
        int initialStacks,
        string sourceId,
        ulong chainId,
        int depth,
        PassiveEventOrigin origin,
        string originPassiveId = null,
        string originRuleId = null)
    {
        if (definition == null)
            return null;

        float now = Time.time;
        int clampedStacks = Mathf.Max(0, initialStacks);

        if (definition.stackMode == StackMode.IndependentInstances)
        {
            var instance = CreateInstance(definition, source, clampedStacks, now, sourceId, chainId, depth, origin, originPassiveId, originRuleId);
            _activeEffects.Add(instance);
            NotifyEffectsChanged(StatusEffectEventType.AppliedNew, instance, 0, instance.CurrentStacks);
            return instance;
        }

        var existing = FindActiveEffect(definition);
        if (existing == null)
        {
            existing = CreateInstance(definition, source, clampedStacks, now, sourceId, chainId, depth, origin, originPassiveId, originRuleId);
            _activeEffects.Add(existing);
            NotifyEffectsChanged(StatusEffectEventType.AppliedNew, existing, 0, existing.CurrentStacks);
            return existing;
        }

        existing.UpdateSource(source);
        existing.UpdateContext(sourceId, chainId, depth, origin, originPassiveId, originRuleId);
        int oldStacks = existing.CurrentStacks;

        switch (definition.stackMode)
        {
            case StackMode.RefreshDuration:
                existing.RefreshDuration();
                break;

            case StackMode.AddStackAndRefresh:
                existing.AddStacks(clampedStacks, definition.ClampedMaxStacks);
                existing.RefreshDuration();
                break;

            case StackMode.StrongestOnly:
                existing.RefreshDuration();
                break;
        }

        StatusEffectEventType eventType = existing.CurrentStacks != oldStacks
            ? StatusEffectEventType.StackChanged
            : StatusEffectEventType.Refreshed;

        NotifyEffectsChanged(eventType, existing, oldStacks, existing.CurrentStacks);
        return existing;
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

    public StatusEffectInstance FindActiveEffect(StatusEffectDef definition)
    {
        if (definition == null)
            return null;

        for (int i = 0; i < _activeEffects.Count; i++)
        {
            if (MatchesDefinition(_activeEffects[i], definition))
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
        if (definition == null || Mathf.Approximately(definition.tickDamage, 0f))
            return;

        int stacks = Mathf.Max(0, instance.CurrentStacks);
        if (stacks <= 0)
            return;

        float damage = definition.tickDamage * stacks;
        if (damage <= 0f)
            return;

        var targetDamageable = GetComponentInParent<IDamageable>();
        if (targetDamageable == null || !targetDamageable.IsAlive)
            return;

        var damageContext = new DamageContext(
            damage,
            instance.Source,
            instance.SourceId,
            definition.effectId,
            instance.ChainId == 0 ? CombatEventBus.NextChainId() : instance.ChainId,
            instance.Depth + 1,
            PassiveEventOrigin.StatusEffect,
            instance.OriginPassiveId,
            instance.OriginRuleId);

        targetDamageable.TakeDamage(in damageContext);
        PublishStatusEffectEvent(StatusEffectEventType.Ticked, instance, stacks, stacks);
    }

    StatusEffectInstance CreateInstance(
        StatusEffectDef definition,
        GameObject source,
        int initialStacks,
        float now,
        string sourceId,
        ulong chainId,
        int depth,
        PassiveEventOrigin origin,
        string originPassiveId,
        string originRuleId)
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
            sourceId,
            chainId,
            depth,
            origin,
            originPassiveId,
            originRuleId);
    }

    int ResolveRuleMaxStacks(StatusEffectDef definition, StatusEffectTriggerRule rule)
    {
        int effectMaxStacks = definition.ClampedMaxStacks;
        if (rule == null || rule.maxStacks <= 0)
            return effectMaxStacks;

        return Mathf.Min(effectMaxStacks, rule.maxStacks);
    }

    bool MatchesDefinition(StatusEffectInstance instance, StatusEffectDef definition)
    {
        if (instance == null || definition == null || instance.Definition == null)
            return false;

        if (instance.Definition == definition)
            return true;

        if (string.IsNullOrWhiteSpace(definition.effectId) ||
            string.IsNullOrWhiteSpace(instance.Definition.effectId))
            return false;

        return string.Equals(instance.Definition.effectId, definition.effectId, StringComparison.Ordinal);
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
            if (definition == null || instance.CurrentStacks <= 0 || definition.modifiers == null)
                continue;

            string sourceId = !string.IsNullOrWhiteSpace(instance.SourceId)
                ? instance.SourceId
                : definition.effectId;

            for (int j = 0; j < definition.modifiers.Count; j++)
            {
                var modifier = definition.modifiers[j];
                if (modifier == null)
                    continue;

                buffer.Add(new RuntimeStatModifier(
                    modifier.statType,
                    modifier.operation,
                    modifier.value * instance.CurrentStacks,
                    sourceId));
            }
        }
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

        string durationText = definition.IsPermanent
            ? "perm"
            : $"{Mathf.Max(0f, instance.TimeLeft):0.0}s";

        return $"{label} x{instance.CurrentStacks} ({durationText})";
    }
}
