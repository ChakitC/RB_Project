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
    readonly List<CustomPassiveDef> _customPassives = new();
    readonly List<IPassiveDefinitionProvider> _passiveProviders = new();
    readonly List<PassiveDefinition> _providerPassiveBuffer = new();
    readonly Dictionary<ulong, HashSet<RuleExecutionKey>> _executionsByChain = new();
    readonly Dictionary<ulong, double> _chainLastSeenTime = new();
    readonly List<ulong> _chainCleanupBuffer = new();

    long _nextIndependentModifierId;
    bool _subscribed;

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
    }

    void OnDisable()
    {
        UnsubscribeFromEvents();
        UnloadCustomPassives();
        _activeModifiers.Clear();
        _executionsByChain.Clear();
        _chainLastSeenTime.Clear();
        statsHub?.MarkDirty();
    }

    void Update()
    {
        double now = Time.timeAsDouble;

        if (CleanupExpiredModifiers(now))
            statsHub?.MarkDirty();

        CleanupOldChainState(now);
    }

    public void RefreshPassiveLoadout()
    {
        UnloadCustomPassives();

        _alwaysOnModifiers.Clear();
        _activeModifiers.Clear();
        _triggeredPassives.Clear();
        _customPassives.Clear();

        if (!TryAddPassivesFromSkillManager())
            AddPassivesFromList(ctx != null && ctx.baseStats != null ? ctx.baseStats.passives : null);

        AddPassivesFromList(runtimePassives);
        AddPassivesFromProviders();
        AddPassivesFromList(extraPassives);

        for (int i = 0; i < _customPassives.Count; i++)
        {
            var definition = _customPassives[i];
            if (definition == null || definition.behaviors == null)
                continue;

            for (int j = 0; j < definition.behaviors.Count; j++)
                definition.behaviors[j]?.OnEquipped(this, definition);
        }

        statsHub?.RebuildModifierProviders();
        statsHub?.MarkDirty();
    }

    bool TryAddPassivesFromSkillManager()
    {
        CharacterSkillManager skillManager = ctx != null ? ctx.SkillManager : null;
        if (skillManager == null || !skillManager.HasConfiguredPassiveSlots)
            return false;

        List<PassiveDefinition> equippedPassives = new();
        skillManager.AppendConfiguredPassiveDefinitions(equippedPassives);
        if (equippedPassives.Count == 0)
            return false;

        AddPassivesFromList(equippedPassives);
        return true;
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
                    modifier.SourceId));
            }
        }
    }

    public bool TryApplyRuntimeModifier(PassiveActionDefinition action, string sourceId, double now)
    {
        if (action == null || action.modifiers == null || action.modifiers.Count == 0)
            return false;

        CleanupExpiredModifiers(now);

        int maxStacks = action.maxStacks <= 0 ? int.MaxValue : action.maxStacks;
        int grantedStacks = Mathf.Clamp(action.grantedStacks, 1, maxStacks);
        string resolvedSourceId = string.IsNullOrWhiteSpace(sourceId)
            ? $"passive:{GetInstanceID()}:modifier"
            : sourceId;

        switch (action.stackPolicy)
        {
            case PassiveModifierStackPolicy.Independent:
                _activeModifiers.Add(CreateActiveModifier(action, $"{resolvedSourceId}:inst:{++_nextIndependentModifierId}", grantedStacks, maxStacks, now));
                return true;

            case PassiveModifierStackPolicy.IgnoreWhileActive:
            {
                var existing = FindActiveModifier(resolvedSourceId, now);
                if (existing != null)
                    return false;

                _activeModifiers.Add(CreateActiveModifier(action, resolvedSourceId, grantedStacks, maxStacks, now));
                return true;
            }

            case PassiveModifierStackPolicy.RefreshDuration:
            {
                var existing = FindActiveModifier(resolvedSourceId, now);
                if (existing == null)
                {
                    _activeModifiers.Add(CreateActiveModifier(action, resolvedSourceId, grantedStacks, maxStacks, now));
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
                var existing = FindActiveModifier(resolvedSourceId, now);
                if (existing == null)
                {
                    _activeModifiers.Add(CreateActiveModifier(action, resolvedSourceId, grantedStacks, maxStacks, now));
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
                var existing = FindActiveModifier(resolvedSourceId, now);
                if (existing == null)
                {
                    _activeModifiers.Add(CreateActiveModifier(action, resolvedSourceId, grantedStacks, maxStacks, now));
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
        string sourceId = null,
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
            sourceId,
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
            var definition = _customPassives[i];
            if (definition == null || definition.behaviors == null)
                continue;

            for (int j = 0; j < definition.behaviors.Count; j++)
                definition.behaviors[j]?.OnPassiveEvent(this, definition, context);
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

            if (!MatchesOriginFilter(rule.originFilter, context.Origin))
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

            string sourceId = ResolveActionSourceId(passiveId, ruleId, action);
            GameObject targetObject = ResolveTargetObject(action.targetSelector, context);

            switch (action.actionType)
            {
                case PassiveActionType.GrantModifier:
                    ApplyModifierAction(action, targetObject, sourceId, context.Time);
                    break;

                case PassiveActionType.ApplyStatusEffect:
                    ApplyStatusEffectAction(action, targetObject, sourceId, context, passiveId, ruleId);
                    break;

                case PassiveActionType.EmitEvent:
                    PublishChildEvent(context, action.emittedEventType, targetObject, sourceId, action.emittedValue, passiveId, ruleId);
                    break;
            }
        }
    }

    void ApplyModifierAction(PassiveActionDefinition action, GameObject targetObject, string sourceId, double now)
    {
        if (action == null || targetObject == null)
            return;

        var targetController = targetObject.GetComponentInParent<PassiveController>();
        if (targetController == null)
            return;

        if (targetController.TryApplyRuntimeModifier(action, sourceId, now))
            targetController.statsHub?.MarkDirty();
    }

    void ApplyStatusEffectAction(
        PassiveActionDefinition action,
        GameObject targetObject,
        string sourceId,
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
            sourceId,
            context.ChainId,
            context.Depth + 1,
            PassiveEventOrigin.Passive,
            passiveId,
            ruleId);
    }

    void AddPassivesFromList(List<PassiveDefinition> passives)
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

    void AddPassive(PassiveDefinition definition)
    {
        if (definition == null)
            return;

        switch (definition)
        {
            case AlwaysOnPassiveDef alwaysOn:
                AddAlwaysOnPassive(alwaysOn);
                break;

            case TriggeredPassiveDef triggered:
                AddTriggeredPassive(triggered);
                break;

            case CustomPassiveDef custom:
                _customPassives.Add(custom);
                break;
        }
    }

    void AddAlwaysOnPassive(AlwaysOnPassiveDef definition)
    {
        if (definition == null || definition.modifiers == null)
            return;

        string sourceId = $"passive:{definition.RuntimeId}";
        for (int i = 0; i < definition.modifiers.Count; i++)
        {
            var modifier = definition.modifiers[i];
            if (modifier == null)
                continue;

            _alwaysOnModifiers.Add(new RuntimeStatModifier(
                modifier.statType,
                modifier.operation,
                modifier.value,
                sourceId));
        }
    }

    void AddTriggeredPassive(TriggeredPassiveDef definition)
    {
        if (definition == null)
            return;

        var runtime = new TriggeredPassiveRuntime(definition);
        _triggeredPassives.Add(runtime);
    }

    void UnloadCustomPassives()
    {
        for (int i = 0; i < _customPassives.Count; i++)
        {
            var definition = _customPassives[i];
            if (definition == null || definition.behaviors == null)
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

    GameObject ResolveTargetObject(PassiveTargetSelector selector, PassiveEventContext context)
    {
        if (selector == PassiveTargetSelector.EventTarget)
            return context.Target;

        return gameObject;
    }

    string ResolveActionSourceId(string passiveId, string ruleId, PassiveActionDefinition action)
    {
        if (action != null && !string.IsNullOrWhiteSpace(action.sourceIdOverride))
            return action.sourceIdOverride;

        string actionId = action != null && !string.IsNullOrWhiteSpace(action.actionId)
            ? action.actionId
            : "action";

        return $"{passiveId}:{ruleId}:{actionId}";
    }

    ActivePassiveModifier FindActiveModifier(string sourceId, double now)
    {
        for (int i = 0; i < _activeModifiers.Count; i++)
        {
            var modifier = _activeModifiers[i];
            if (modifier == null || modifier.IsExpired(now))
                continue;

            if (string.Equals(modifier.SourceId, sourceId, StringComparison.Ordinal))
                return modifier;
        }

        return null;
    }

    ActivePassiveModifier CreateActiveModifier(PassiveActionDefinition action, string sourceId, int stacks, int maxStacks, double now)
    {
        return new ActivePassiveModifier
        {
            SourceId = sourceId,
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
        public TriggeredPassiveRuntime(TriggeredPassiveDef definition)
        {
            Definition = definition;
            RuleStates = new List<TriggeredRuleState>();

            if (definition == null || definition.rules == null)
                return;

            for (int i = 0; i < definition.rules.Count; i++)
                RuleStates.Add(new TriggeredRuleState(definition.rules[i]));
        }

        public TriggeredPassiveDef Definition { get; }
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
    }

    sealed class ActivePassiveModifier
    {
        public string SourceId;
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
