using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-112)]
public sealed class PassiveController : MonoBehaviour, IStatModifierProvider
{
    [Header("Refs")]
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private StatsHub statsHub;
    [SerializeField] private CombatEventBus combatEventBus;
    [SerializeField] private StatusEffectController statusEffectController;

    [Header("Loadout")]
    [SerializeField] private List<PassiveDefinition> runtimePassives = new();
    [SerializeField] private List<PassiveDefinition> extraPassives = new();

    [Header("Safety")]
    [SerializeField, Min(1)] private int maxEventDepth = 8;
    [SerializeField, Min(0f)] private float chainStateTtlSeconds = 10f;

    readonly List<RuntimeStatModifier> _alwaysOnModifiers = new();
    readonly List<ActivePassiveModifier> _activeModifiers = new();
    readonly List<TriggeredPassiveRuntime> _triggeredPassives = new();
    readonly List<EquippedPassive> _customPassives = new();
    readonly List<IPassiveDefinitionProvider> _passiveProviders = new();
    readonly List<PassiveDefinition> _providerPassiveBuffer = new();
    readonly Dictionary<ulong, HashSet<RuleExecutionKey>> _executionsByChain = new();
    readonly Dictionary<ulong, double> _chainLastSeenTime = new();
    readonly List<ulong> _chainCleanupBuffer = new();
    readonly List<PassiveEventSourceRequest> _eventSourceRequests = new();
    readonly List<PassiveEventSourceRequest> _eventSourceKindRequests = new();
    readonly HashSet<PassiveEventSourceKind> _requiredEventSourceKinds = new();
    readonly Dictionary<PassiveEventSourceKind, PassiveEventSource> _activeEventSources = new();
    readonly HashSet<PassiveEventSource> _autoAddedEventSources = new();
    readonly List<PassiveEventSourceKind> _eventSourceKindCleanupBuffer = new();

    public event Action StatModifiersChanged;

    long _nextIndependentModifierId;
    bool _subscribed;
    CharacterSkillManager _subscribedSkillManager;

    void Awake()
    {
        ResolveReferences();

        if (ctx != null)
            ctx.PassiveController = this;

        statsHub?.RebuildModifierProviders();
    }

    void OnEnable()
    {
        ResolveReferences();
        RefreshPassiveLoadout();
        SubscribeToEvents();
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

        if (ctx != null && ctx.PassiveController != this)
            ctx.PassiveController = this;

        if (!statsHub && ctx != null)
            statsHub = ctx.StatsHub;
        if (!statsHub)
            TryGetComponent(out statsHub);
        if (!statsHub && ctx != null)
            statsHub = ctx.GetComponentInChildren<StatsHub>(true);

        if (!combatEventBus && ctx != null)
            combatEventBus = ctx.CombatEventBus;
        if (!combatEventBus)
            TryGetComponent(out combatEventBus);
        if (!combatEventBus && ctx != null)
            combatEventBus = ctx.GetComponentInChildren<CombatEventBus>(true);

        if (!statusEffectController && ctx != null)
            statusEffectController = ctx.GetComponentInChildren<StatusEffectController>(true);
        if (!statusEffectController)
            TryGetComponent(out statusEffectController);

        SubscribeToSkillManager(ctx != null ? ctx.SkillManager : null);
    }

    void SubscribeToSkillManager(CharacterSkillManager skillManager)
    {
        if (ReferenceEquals(_subscribedSkillManager, skillManager))
            return;

        if (_subscribedSkillManager != null)
            _subscribedSkillManager.PassiveLoadoutChanged -= HandlePassiveLoadoutChanged;

        _subscribedSkillManager = skillManager;

        if (_subscribedSkillManager != null)
            _subscribedSkillManager.PassiveLoadoutChanged += HandlePassiveLoadoutChanged;
    }

    void HandlePassiveLoadoutChanged()
    {
        if (isActiveAndEnabled)
            RefreshPassiveLoadout();
    }

    void OnDisable()
    {
        UnsubscribeFromEvents();
        ReleaseAllOptionalEventSources();
        UnloadCustomPassives();
        _activeModifiers.Clear();
        _executionsByChain.Clear();
        _chainLastSeenTime.Clear();
        NotifyStatModifiersChanged();
    }

    void OnDestroy()
    {
        SubscribeToSkillManager(null);
    }

    void Update()
    {
        double now = Time.timeAsDouble;

        if (CleanupExpiredModifiers(now))
            NotifyStatModifiersChanged();

        CleanupOldChainState(now);
    }

    void NotifyStatModifiersChanged()
    {
        var handler = StatModifiersChanged;
        if (handler != null)
            handler.Invoke();
        else
            statsHub?.MarkDirty();
    }

    public void RefreshPassiveLoadout()
    {
        List<ActivePassiveModifier> preservedModifiers = new(_activeModifiers);
        Dictionary<RuleStateKey, TriggeredRuleState> preservedRuleStates = CaptureTriggeredRuleStates();
        HashSet<CustomPassiveDef> previousCustomDefinitions = CaptureCustomDefinitions();

        _alwaysOnModifiers.Clear();
        _activeModifiers.Clear();
        _triggeredPassives.Clear();
        _customPassives.Clear();

        if (!TryAddPassivesFromSkillManager())
            AddPassivesFromList(ctx != null && ctx.baseStats != null ? ctx.baseStats.passives : null);

        AddPassivesFromList(runtimePassives);
        AddPassivesFromProviders();
        AddPassivesFromList(extraPassives);

        RestoreTriggeredRuleStates(preservedRuleStates);
        _activeModifiers.AddRange(preservedModifiers);
        NotifyCustomPassiveLifecycleDelta(previousCustomDefinitions);

        RefreshOptionalEventSources();
        statsHub?.RebuildModifierProviders();
        NotifyStatModifiersChanged();
    }

    bool TryAddPassivesFromSkillManager()
    {
        CharacterSkillManager skillManager = ctx != null ? ctx.SkillManager : null;
        if (skillManager == null || !skillManager.HasConfiguredPassiveSlots)
            return false;

        List<EquippedPassive> equippedPassives = new();
        skillManager.AppendConfiguredPassiveDefinitions(equippedPassives);
        if (equippedPassives.Count == 0)
            return false;

        AddPassivesFromList(equippedPassives);
        return true;
    }

    HashSet<CustomPassiveDef> CaptureCustomDefinitions()
    {
        var set = new HashSet<CustomPassiveDef>();
        for (int i = 0; i < _customPassives.Count; i++)
        {
            if (_customPassives[i].Definition is CustomPassiveDef definition)
                set.Add(definition);
        }

        return set;
    }

    void NotifyCustomPassiveLifecycleDelta(HashSet<CustomPassiveDef> previousDefinitions)
    {
        var currentDefinitions = new HashSet<CustomPassiveDef>();

        for (int i = 0; i < _customPassives.Count; i++)
        {
            EquippedPassive equipped = _customPassives[i];
            if (equipped.Definition is not CustomPassiveDef definition || definition.behaviors == null)
                continue;

            currentDefinitions.Add(definition);

            if (previousDefinitions.Contains(definition))
                continue;

            for (int j = 0; j < definition.behaviors.Count; j++)
                definition.behaviors[j]?.OnEquipped(this, definition, equipped.Upgrades);
        }

        foreach (CustomPassiveDef definition in previousDefinitions)
        {
            if (definition == null || definition.behaviors == null || currentDefinitions.Contains(definition))
                continue;

            for (int j = 0; j < definition.behaviors.Count; j++)
                definition.behaviors[j]?.OnUnequipped(this, definition);
        }
    }

    Dictionary<RuleStateKey, TriggeredRuleState> CaptureTriggeredRuleStates()
    {
        var map = new Dictionary<RuleStateKey, TriggeredRuleState>();

        for (int i = 0; i < _triggeredPassives.Count; i++)
        {
            TriggeredPassiveRuntime runtime = _triggeredPassives[i];
            if (runtime?.Definition == null || runtime.RuleStates == null)
                continue;

            string passiveId = runtime.Definition.RuntimeId;
            for (int j = 0; j < runtime.RuleStates.Count; j++)
            {
                TriggeredRuleState state = runtime.RuleStates[j];
                if (state?.Rule == null)
                    continue;

                map[new RuleStateKey(passiveId, state.Rule.RuntimeRuleId)] = state;
            }
        }

        return map;
    }

    void RestoreTriggeredRuleStates(Dictionary<RuleStateKey, TriggeredRuleState> preserved)
    {
        if (preserved == null || preserved.Count == 0)
            return;

        for (int i = 0; i < _triggeredPassives.Count; i++)
        {
            TriggeredPassiveRuntime runtime = _triggeredPassives[i];
            if (runtime?.Definition == null || runtime.RuleStates == null)
                continue;

            string passiveId = runtime.Definition.RuntimeId;
            for (int j = 0; j < runtime.RuleStates.Count; j++)
            {
                TriggeredRuleState state = runtime.RuleStates[j];
                if (state?.Rule == null)
                    continue;

                if (preserved.TryGetValue(new RuleStateKey(passiveId, state.Rule.RuntimeRuleId), out TriggeredRuleState previous))
                    state.CopyStateFrom(previous);
            }
        }
    }

    public void SetRuntimePassives(IReadOnlyList<PassiveDefinition> passives)
    {
        runtimePassives.Clear();

        if (passives != null)
        {
            for (int i = 0; i < passives.Count; i++)
            {
                var passive = passives[i];
                if (passive == null)
                    continue;

                runtimePassives.Add(passive);
            }
        }

        if (isActiveAndEnabled)
            RefreshPassiveLoadout();
    }

    public void AppendStatModifiers(List<RuntimeStatModifier> buffer)
    {
        if (buffer == null)
            return;

        for (int i = 0; i < _alwaysOnModifiers.Count; i++)
            buffer.Add(_alwaysOnModifiers[i]);

        double now = Time.timeAsDouble;
        for (int i = 0; i < _activeModifiers.Count; i++)
        {
            var modifier = _activeModifiers[i];
            if (modifier == null || modifier.IsExpired(now))
                continue;

            int stackCount = Mathf.Max(1, modifier.Stacks);
            for (int j = 0; j < modifier.Modifiers.Count; j++)
            {
                var statModifier = modifier.Modifiers[j];
                if (statModifier == null)
                    continue;

                buffer.Add(new RuntimeStatModifier(
                    statModifier.statType,
                    statModifier.operation,
                    statModifier.value * stackCount,
                    modifier.ModifierKey));
            }
        }
    }

    public bool TryApplyRuntimeModifier(PassiveActionDefinition action, string modifierKey, double now)
    {
        if (action == null || action.modifiers == null || action.modifiers.Count == 0)
            return false;

        CleanupExpiredModifiers(now);

        int maxStacks = action.maxStacks <= 0 ? int.MaxValue : action.maxStacks;
        int grantedStacks = Mathf.Clamp(action.grantedStacks, 1, maxStacks);
        string resolvedModifierKey = string.IsNullOrWhiteSpace(modifierKey)
            ? $"passive:{GetInstanceID()}:modifier"
            : modifierKey;

        switch (action.stackPolicy)
        {
            case PassiveModifierStackPolicy.Independent:
                _activeModifiers.Add(CreateActiveModifier(action, $"{resolvedModifierKey}:inst:{++_nextIndependentModifierId}", grantedStacks, maxStacks, now));
                return true;

            case PassiveModifierStackPolicy.IgnoreWhileActive:
            {
                var existing = FindActiveModifier(resolvedModifierKey, now);
                if (existing != null)
                    return false;

                _activeModifiers.Add(CreateActiveModifier(action, resolvedModifierKey, grantedStacks, maxStacks, now));
                return true;
            }

            case PassiveModifierStackPolicy.RefreshDuration:
            {
                var existing = FindActiveModifier(resolvedModifierKey, now);
                if (existing == null)
                {
                    _activeModifiers.Add(CreateActiveModifier(action, resolvedModifierKey, grantedStacks, maxStacks, now));
                    return true;
                }

                existing.Stacks = Mathf.Clamp(Mathf.Max(existing.Stacks, grantedStacks), 1, maxStacks);
                existing.MaxStacks = maxStacks;
                existing.ExpiresAt = ResolveExpiry(action.durationSeconds, now);
                existing.Modifiers = action.modifiers;
                return true;
            }

            case PassiveModifierStackPolicy.AddStacks:
            {
                var existing = FindActiveModifier(resolvedModifierKey, now);
                if (existing == null)
                {
                    _activeModifiers.Add(CreateActiveModifier(action, resolvedModifierKey, grantedStacks, maxStacks, now));
                    return true;
                }

                existing.Stacks = Mathf.Clamp(existing.Stacks + grantedStacks, 1, maxStacks);
                existing.MaxStacks = maxStacks;
                existing.ExpiresAt = ResolveExpiry(action.durationSeconds, now);
                existing.Modifiers = action.modifiers;
                return true;
            }

            case PassiveModifierStackPolicy.Replace:
            default:
            {
                var existing = FindActiveModifier(resolvedModifierKey, now);
                if (existing == null)
                {
                    _activeModifiers.Add(CreateActiveModifier(action, resolvedModifierKey, grantedStacks, maxStacks, now));
                    return true;
                }

                existing.Stacks = grantedStacks;
                existing.MaxStacks = maxStacks;
                existing.ExpiresAt = ResolveExpiry(action.durationSeconds, now);
                existing.Modifiers = action.modifiers;
                return true;
            }
        }
    }

    public bool PublishChildEvent(
        in PassiveEventContext parent,
        PassiveEventType type,
        GameObject target = null,
        string eventSourceId = null,
        float value = 0f,
        string originPassiveId = null,
        string originRuleId = null)
    {
        if (combatEventBus == null || type == PassiveEventType.None)
            return false;

        var child = combatEventBus.CreateChildContext(
            parent,
            type,
            gameObject,
            target,
            eventSourceId,
            parent.AttackId,
            value,
            PassiveEventOrigin.Passive,
            originPassiveId,
            originRuleId);

        if (child.Depth > maxEventDepth)
            return false;

        combatEventBus.Publish(child);
        return true;
    }

    void SubscribeToEvents()
    {
        if (_subscribed || combatEventBus == null)
            return;

        combatEventBus.EventPublished += HandlePassiveEvent;
        _subscribed = true;
    }

    void UnsubscribeFromEvents()
    {
        if (!_subscribed || combatEventBus == null)
            return;

        combatEventBus.EventPublished -= HandlePassiveEvent;
        _subscribed = false;
    }

    void HandlePassiveEvent(PassiveEventContext context)
    {
        if (context.Type == PassiveEventType.None || context.Depth > maxEventDepth)
            return;

        TouchChain(context.ChainId, context.Time);

        for (int i = 0; i < _triggeredPassives.Count; i++)
            HandleTriggeredPassive(_triggeredPassives[i], context);

        for (int i = 0; i < _customPassives.Count; i++)
        {
            EquippedPassive equipped = _customPassives[i];
            if (equipped.Definition is not CustomPassiveDef definition || definition.behaviors == null)
                continue;

            for (int j = 0; j < definition.behaviors.Count; j++)
                definition.behaviors[j]?.OnPassiveEvent(this, definition, context, equipped.Upgrades);
        }
    }

    void HandleTriggeredPassive(TriggeredPassiveRuntime runtime, PassiveEventContext context)
    {
        if (runtime?.Definition == null || runtime.RuleStates == null)
            return;

        string passiveId = runtime.Definition.RuntimeId;

        for (int i = 0; i < runtime.RuleStates.Count; i++)
        {
            var state = runtime.RuleStates[i];
            var rule = state.Rule;
            if (rule == null || rule.trigger != context.Type)
                continue;

            if (!PassiveUpgradeGate.IsRuleEnabled(rule, runtime.Upgrades))
                continue;

            if (!MatchesOriginFilter(rule.originFilter, context.Origin))
                continue;

            if (!MatchesEventSource(rule, context))
                continue;

            if (rule.requireTarget && context.Target == null)
                continue;

            if (rule.requireAttackId && string.IsNullOrWhiteSpace(context.AttackId))
                continue;

            state.RecordEvent(context.Time);
            if (!state.IsReady(context.Time))
                continue;

            if (!RegisterRuleExecution(context, passiveId, rule))
                continue;

            ExecuteRuleActions(runtime.Definition, rule, context);
            state.Consume(context.Time);
        }
    }

    void ExecuteRuleActions(TriggeredPassiveDef definition, TriggeredPassiveRule rule, PassiveEventContext context)
    {
        if (definition == null || rule == null || rule.actions == null)
            return;

        string passiveId = definition.RuntimeId;
        string ruleId = rule.RuntimeRuleId;

        for (int i = 0; i < rule.actions.Count; i++)
        {
            var action = rule.actions[i];
            if (action == null)
                continue;

            GameObject targetObject = ResolveTargetObject(action.targetSelector, context);

            switch (action.actionType)
            {
                case PassiveActionType.GrantModifier:
                    ApplyModifierAction(action, targetObject, ResolveModifierKey(passiveId, ruleId, action), context.Time);
                    break;

                case PassiveActionType.ApplyStatusEffect:
                    ApplyStatusEffectAction(action, targetObject, ResolveAppliedById(passiveId, ruleId, action), context, passiveId, ruleId);
                    break;

                case PassiveActionType.EmitEvent:
                    PublishChildEvent(context, action.emittedEventType, targetObject, ResolveEmittedEventSourceId(passiveId, ruleId, action), action.emittedValue, passiveId, ruleId);
                    break;
            }
        }
    }

    void ApplyModifierAction(PassiveActionDefinition action, GameObject targetObject, string modifierKey, double now)
    {
        if (action == null || targetObject == null)
            return;

        var targetController = targetObject.GetComponentInParent<PassiveController>();
        if (targetController == null)
            return;

        if (targetController.TryApplyRuntimeModifier(action, modifierKey, now))
            targetController.NotifyStatModifiersChanged();
    }

    void ApplyStatusEffectAction(
        PassiveActionDefinition action,
        GameObject targetObject,
        string appliedById,
        PassiveEventContext context,
        string passiveId,
        string ruleId)
    {
        if (action == null || action.statusEffect == null || targetObject == null)
            return;

        var targetStatusController = targetObject.GetComponentInParent<StatusEffectController>();
        if (targetStatusController == null)
            return;

        targetStatusController.ApplyEffect(
            action.statusEffect,
            gameObject,
            action.statusInitialStacks,
            appliedById,
            context.ChainId,
            context.Depth + 1,
            PassiveEventOrigin.Passive,
            passiveId,
            ruleId);
    }

    void RefreshOptionalEventSources()
    {
        _eventSourceRequests.Clear();
        _requiredEventSourceKinds.Clear();

        CollectRequiredEventSourceRequests(_eventSourceRequests);

        for (int i = 0; i < _eventSourceRequests.Count; i++)
        {
            var request = _eventSourceRequests[i];
            if (request.IsValid)
                _requiredEventSourceKinds.Add(request.Kind);
        }

        foreach (PassiveEventSourceKind kind in _requiredEventSourceKinds)
        {
            PassiveEventSource source = EnsureEventSource(kind);
            if (source == null)
                continue;

            _eventSourceKindRequests.Clear();
            for (int i = 0; i < _eventSourceRequests.Count; i++)
            {
                var request = _eventSourceRequests[i];
                if (request.Kind == kind && request.IsValid)
                    _eventSourceKindRequests.Add(request);
            }

            source.ApplyRequests(ctx, combatEventBus, _eventSourceKindRequests);
        }

        _eventSourceKindRequests.Clear();
        ReleaseUnusedOptionalEventSources();
    }

    void CollectRequiredEventSourceRequests(List<PassiveEventSourceRequest> buffer)
    {
        if (buffer == null)
            return;

        for (int i = 0; i < _triggeredPassives.Count; i++)
        {
            TriggeredPassiveRuntime runtime = _triggeredPassives[i];
            if (runtime?.Definition == null || runtime.Definition.rules == null)
                continue;

            for (int j = 0; j < runtime.Definition.rules.Count; j++)
            {
                TriggeredPassiveRule rule = runtime.Definition.rules[j];
                if (rule == null || rule.eventSourceKind == PassiveEventSourceKind.None)
                    continue;

                if (!PassiveUpgradeGate.IsRuleEnabled(rule, runtime.Upgrades))
                    continue;

                var request = new PassiveEventSourceRequest(
                    rule.eventSourceKind,
                    rule.trigger,
                    rule.RuntimeEventSourceId,
                    rule.eventSourceFloatValue,
                    rule.eventSourceIntValue);

                if (request.IsValid)
                    buffer.Add(request);
            }
        }
    }

    PassiveEventSource EnsureEventSource(PassiveEventSourceKind kind)
    {
        if (kind == PassiveEventSourceKind.None)
            return null;

        if (_activeEventSources.TryGetValue(kind, out PassiveEventSource activeSource) && activeSource != null)
            return activeSource;

        PassiveEventSource source = FindEventSource(kind);
        if (source == null)
        {
            Type sourceType = PassiveEventSourceRegistry.GetSourceType(kind);
            if (sourceType == null || !typeof(PassiveEventSource).IsAssignableFrom(sourceType))
                return null;

            GameObject host = ctx != null ? ctx.gameObject : gameObject;
            source = host.AddComponent(sourceType) as PassiveEventSource;
            if (source != null)
                _autoAddedEventSources.Add(source);
        }

        if (source != null)
            _activeEventSources[kind] = source;

        return source;
    }

    PassiveEventSource FindEventSource(PassiveEventSourceKind kind)
    {
        PassiveEventSource[] sources = ctx != null
            ? ctx.GetComponentsInChildren<PassiveEventSource>(true)
            : GetComponentsInChildren<PassiveEventSource>(true);

        for (int i = 0; i < sources.Length; i++)
        {
            PassiveEventSource source = sources[i];
            if (source != null && source.Kind == kind)
                return source;
        }

        return null;
    }

    void ReleaseUnusedOptionalEventSources()
    {
        _eventSourceKindCleanupBuffer.Clear();

        foreach (var pair in _activeEventSources)
        {
            if (_requiredEventSourceKinds.Contains(pair.Key))
                continue;

            PassiveEventSource source = pair.Value;
            ReleaseOptionalEventSource(source);
            _eventSourceKindCleanupBuffer.Add(pair.Key);
        }

        for (int i = 0; i < _eventSourceKindCleanupBuffer.Count; i++)
            _activeEventSources.Remove(_eventSourceKindCleanupBuffer[i]);

        _eventSourceKindCleanupBuffer.Clear();
    }

    void ReleaseAllOptionalEventSources()
    {
        foreach (var pair in _activeEventSources)
            ReleaseOptionalEventSource(pair.Value);

        _activeEventSources.Clear();
        _autoAddedEventSources.Clear();
        _requiredEventSourceKinds.Clear();
        _eventSourceRequests.Clear();
        _eventSourceKindRequests.Clear();
        _eventSourceKindCleanupBuffer.Clear();
    }

    void ReleaseOptionalEventSource(PassiveEventSource source)
    {
        if (source == null)
            return;

        source.ClearRequests();

        if (!_autoAddedEventSources.Contains(source))
            return;

        _autoAddedEventSources.Remove(source);
        DestroyOptionalEventSource(source);
    }

    static void DestroyOptionalEventSource(PassiveEventSource source)
    {
        if (source == null)
            return;

        if (Application.isPlaying)
            Destroy(source);
        else
            DestroyImmediate(source);
    }

    void AddPassivesFromList(List<PassiveDefinition> passives)
    {
        if (passives == null)
            return;

        for (int i = 0; i < passives.Count; i++)
            AddPassive(new EquippedPassive(passives[i]));
    }

    void AddPassivesFromList(List<EquippedPassive> passives)
    {
        if (passives == null)
            return;

        for (int i = 0; i < passives.Count; i++)
            AddPassive(passives[i]);
    }

    void AddPassivesFromProviders()
    {
        RebuildPassiveProviders();

        _providerPassiveBuffer.Clear();

        for (int i = 0; i < _passiveProviders.Count; i++)
        {
            var provider = _passiveProviders[i];
            if (provider == null)
                continue;

            provider.AppendPassiveDefinitions(_providerPassiveBuffer);
        }

        AddPassivesFromList(_providerPassiveBuffer);
        _providerPassiveBuffer.Clear();
    }

    void RebuildPassiveProviders()
    {
        _passiveProviders.Clear();

        var behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null || behaviours[i] == this)
                continue;

            if (behaviours[i] is IPassiveDefinitionProvider provider)
                _passiveProviders.Add(provider);
        }
    }

    void AddPassive(in EquippedPassive equipped)
    {
        PassiveDefinition definition = equipped.Definition;
        if (definition == null)
            return;

        switch (definition)
        {
            case AlwaysOnPassiveDef alwaysOn:
                AddAlwaysOnPassive(alwaysOn);
                break;

            case TriggeredPassiveDef triggered:
                AddTriggeredPassive(triggered, equipped.Upgrades);
                break;

            case CustomPassiveDef:
                _customPassives.Add(equipped);
                break;
        }
    }

    void AddAlwaysOnPassive(AlwaysOnPassiveDef definition)
    {
        if (definition == null || definition.modifiers == null)
            return;

        string modifierKey = $"passive:{definition.RuntimeId}";
        for (int i = 0; i < definition.modifiers.Count; i++)
        {
            var modifier = definition.modifiers[i];
            if (modifier == null)
                continue;

            _alwaysOnModifiers.Add(new RuntimeStatModifier(
                modifier.statType,
                modifier.operation,
                modifier.value,
                modifierKey));
        }
    }

    void AddTriggeredPassive(TriggeredPassiveDef definition, SkillUpgradeStatSnapshot upgrades)
    {
        if (definition == null)
            return;

        var runtime = new TriggeredPassiveRuntime(definition, upgrades);
        _triggeredPassives.Add(runtime);
    }

    void UnloadCustomPassives()
    {
        for (int i = 0; i < _customPassives.Count; i++)
        {
            if (_customPassives[i].Definition is not CustomPassiveDef definition || definition.behaviors == null)
                continue;

            for (int j = 0; j < definition.behaviors.Count; j++)
                definition.behaviors[j]?.OnUnequipped(this, definition);
        }
    }

    bool CleanupExpiredModifiers(double now)
    {
        bool removed = false;

        for (int i = _activeModifiers.Count - 1; i >= 0; i--)
        {
            if (_activeModifiers[i] == null || !_activeModifiers[i].IsExpired(now))
                continue;

            _activeModifiers.RemoveAt(i);
            removed = true;
        }

        return removed;
    }

    void CleanupOldChainState(double now)
    {
        if (chainStateTtlSeconds <= 0f || _chainLastSeenTime.Count == 0)
            return;

        _chainCleanupBuffer.Clear();

        foreach (var pair in _chainLastSeenTime)
        {
            if (now - pair.Value > chainStateTtlSeconds)
                _chainCleanupBuffer.Add(pair.Key);
        }

        for (int i = 0; i < _chainCleanupBuffer.Count; i++)
        {
            ulong chainId = _chainCleanupBuffer[i];
            _chainLastSeenTime.Remove(chainId);
            _executionsByChain.Remove(chainId);
        }
    }

    void TouchChain(ulong chainId, double time)
    {
        if (chainId == 0)
            return;

        _chainLastSeenTime[chainId] = time;
        if (!_executionsByChain.ContainsKey(chainId))
            _executionsByChain.Add(chainId, new HashSet<RuleExecutionKey>());
    }

    bool RegisterRuleExecution(PassiveEventContext context, string passiveId, TriggeredPassiveRule rule)
    {
        if (context.ChainId == 0)
            return true;

        TouchChain(context.ChainId, context.Time);

        int targetId = rule.oncePerTargetPerChain
            ? context.TargetInstanceId
            : int.MinValue;

        var key = new RuleExecutionKey(context.ChainId, passiveId, rule.RuntimeRuleId, targetId);
        var bucket = _executionsByChain[context.ChainId];
        if (bucket.Contains(key))
            return false;

        bucket.Add(key);
        return true;
    }

    bool MatchesOriginFilter(PassiveOriginFilter filter, PassiveEventOrigin origin)
    {
        return filter switch
        {
            PassiveOriginFilter.ExternalOnly => origin == PassiveEventOrigin.External,
            PassiveOriginFilter.NonPassive => origin != PassiveEventOrigin.Passive,
            PassiveOriginFilter.PassiveOnly => origin == PassiveEventOrigin.Passive,
            PassiveOriginFilter.Any => true,
            _ => false
        };
    }

    bool MatchesEventSource(TriggeredPassiveRule rule, PassiveEventContext context)
    {
        if (rule == null || rule.eventSourceKind == PassiveEventSourceKind.None)
            return true;

        return string.Equals(context.EventSourceId, rule.RuntimeEventSourceId, StringComparison.Ordinal);
    }

    GameObject ResolveTargetObject(PassiveTargetSelector selector, PassiveEventContext context)
    {
        if (selector == PassiveTargetSelector.EventTarget)
            return context.Target;

        return gameObject;
    }

    string ResolveModifierKey(string passiveId, string ruleId, PassiveActionDefinition action)
    {
        if (action != null && !string.IsNullOrWhiteSpace(action.modifierKeyOverride))
            return action.modifierKeyOverride;

        return ResolveDefaultActionId(passiveId, ruleId, action);
    }

    string ResolveAppliedById(string passiveId, string ruleId, PassiveActionDefinition action)
    {
        if (action != null && !string.IsNullOrWhiteSpace(action.appliedByIdOverride))
            return action.appliedByIdOverride;

        return ResolveDefaultActionId(passiveId, ruleId, action);
    }

    string ResolveEmittedEventSourceId(string passiveId, string ruleId, PassiveActionDefinition action)
    {
        if (action != null && !string.IsNullOrWhiteSpace(action.emittedEventSourceIdOverride))
            return action.emittedEventSourceIdOverride;

        return ResolveDefaultActionId(passiveId, ruleId, action);
    }

    string ResolveDefaultActionId(string passiveId, string ruleId, PassiveActionDefinition action)
    {
        string actionId = action != null && !string.IsNullOrWhiteSpace(action.actionId) ? action.actionId : "action";

        return $"{passiveId}:{ruleId}:{actionId}";
    }

    ActivePassiveModifier FindActiveModifier(string modifierKey, double now)
    {
        for (int i = 0; i < _activeModifiers.Count; i++)
        {
            var modifier = _activeModifiers[i];
            if (modifier == null || modifier.IsExpired(now))
                continue;

            if (string.Equals(modifier.ModifierKey, modifierKey, StringComparison.Ordinal))
                return modifier;
        }

        return null;
    }

    ActivePassiveModifier CreateActiveModifier(PassiveActionDefinition action, string modifierKey, int stacks, int maxStacks, double now)
    {
        return new ActivePassiveModifier
        {
            ModifierKey = modifierKey,
            Modifiers = action.modifiers,
            Stacks = stacks,
            MaxStacks = maxStacks,
            ExpiresAt = ResolveExpiry(action.durationSeconds, now)
        };
    }

    static double ResolveExpiry(float durationSeconds, double now)
    {
        return durationSeconds <= 0f ? double.PositiveInfinity : now + durationSeconds;
    }

    sealed class TriggeredPassiveRuntime
    {
        public TriggeredPassiveRuntime(TriggeredPassiveDef definition, SkillUpgradeStatSnapshot upgrades)
        {
            Definition = definition;
            Upgrades = upgrades;
            RuleStates = new List<TriggeredRuleState>();

            if (definition == null || definition.rules == null)
                return;

            for (int i = 0; i < definition.rules.Count; i++)
                RuleStates.Add(new TriggeredRuleState(definition.rules[i]));
        }

        public TriggeredPassiveDef Definition { get; }
        public SkillUpgradeStatSnapshot Upgrades { get; }
        public List<TriggeredRuleState> RuleStates { get; }
    }

    sealed class TriggeredRuleState
    {
        public TriggeredRuleState(TriggeredPassiveRule rule)
        {
            Rule = rule;
        }

        public TriggeredPassiveRule Rule { get; }
        public int Counter { get; private set; }
        public double WindowExpiresAt { get; private set; }
        public double CooldownReadyAt { get; private set; }

        public void RecordEvent(double time)
        {
            if (Rule == null)
                return;

            if (Rule.countWindowSeconds > 0f && Counter > 0 && time > WindowExpiresAt)
                Counter = 0;

            if (Counter == 0 && Rule.countWindowSeconds > 0f)
                WindowExpiresAt = time + Rule.countWindowSeconds;

            Counter++;
        }

        public bool IsReady(double time)
        {
            if (Rule == null)
                return false;

            return Counter >= Mathf.Max(1, Rule.requiredCount) && time >= CooldownReadyAt;
        }

        public void Consume(double time)
        {
            if (Rule == null)
                return;

            CooldownReadyAt = time + Rule.cooldownSeconds;

            if (Rule.counterConsumeMode == PassiveCounterConsumeMode.CarryOver)
                Counter = Mathf.Max(0, Counter - Mathf.Max(1, Rule.requiredCount));
            else
                Counter = 0;

            if (Counter == 0)
                WindowExpiresAt = 0d;
            else if (Rule.countWindowSeconds > 0f)
                WindowExpiresAt = time + Rule.countWindowSeconds;
        }

        public void CopyStateFrom(TriggeredRuleState other)
        {
            if (other == null)
                return;

            Counter = other.Counter;
            WindowExpiresAt = other.WindowExpiresAt;
            CooldownReadyAt = other.CooldownReadyAt;
        }
    }

    readonly struct RuleStateKey : IEquatable<RuleStateKey>
    {
        public RuleStateKey(string passiveId, string ruleId)
        {
            PassiveId = passiveId ?? string.Empty;
            RuleId = ruleId ?? string.Empty;
        }

        public string PassiveId { get; }
        public string RuleId { get; }

        public bool Equals(RuleStateKey other)
        {
            return string.Equals(PassiveId, other.PassiveId, StringComparison.Ordinal) &&
                   string.Equals(RuleId, other.RuleId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is RuleStateKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (PassiveId.GetHashCode() * 397) ^ RuleId.GetHashCode();
            }
        }
    }

    sealed class ActivePassiveModifier
    {
        public string ModifierKey;
        public List<PassiveStatModifier> Modifiers;
        public int Stacks;
        public int MaxStacks;
        public double ExpiresAt;

        public bool IsExpired(double now)
        {
            return !double.IsPositiveInfinity(ExpiresAt) && now >= ExpiresAt;
        }
    }

    readonly struct RuleExecutionKey : IEquatable<RuleExecutionKey>
    {
        public RuleExecutionKey(ulong chainId, string passiveId, string ruleId, int targetId)
        {
            ChainId = chainId;
            PassiveId = passiveId ?? string.Empty;
            RuleId = ruleId ?? string.Empty;
            TargetId = targetId;
        }

        public ulong ChainId { get; }
        public string PassiveId { get; }
        public string RuleId { get; }
        public int TargetId { get; }

        public bool Equals(RuleExecutionKey other)
        {
            return ChainId == other.ChainId &&
                   TargetId == other.TargetId &&
                   string.Equals(PassiveId, other.PassiveId, StringComparison.Ordinal) &&
                   string.Equals(RuleId, other.RuleId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is RuleExecutionKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ChainId.GetHashCode();
                hash = (hash * 397) ^ (PassiveId != null ? PassiveId.GetHashCode() : 0);
                hash = (hash * 397) ^ (RuleId != null ? RuleId.GetHashCode() : 0);
                hash = (hash * 397) ^ TargetId;
                return hash;
            }
        }
    }
}
