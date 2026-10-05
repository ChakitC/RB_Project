using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-112)]
public sealed class PassiveController : MonoBehaviour, IStatModifierProvider
{
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private StatsHub statsHub;
    [SerializeField] private CombatEventBus combatEventBus;
    [SerializeField] private StatusEffectController statusEffectController;
    [SerializeField] private List<PassiveDefinition> runtimePassives = new();
    [SerializeField] private List<PassiveDefinition> extraPassives = new();
    [SerializeField, HideInInspector] private List<string> runtimeGrantIds = new();
    [SerializeField, HideInInspector] private List<string> extraGrantIds = new();
    [SerializeField, HideInInspector] private List<PassiveDefinition> runtimeGrantDefinitions = new();
    [SerializeField, HideInInspector] private List<PassiveDefinition> extraGrantDefinitions = new();
    [SerializeField] private bool diagnostics;
    public string LastDiagnostic { get; private set; }
    internal void Trace(string rule, string reason) { if (diagnostics) LastDiagnostic = rule + ": " + reason; }
    [SerializeField, Min(1)] private int maxEventDepth = 8;
    [SerializeField, Min(0f)] private float chainStateTtlSeconds = 10f;
    Dictionary<string, PassiveRuntime> runtimes;
    List<ActivePassiveModifier> _activeModifiers;
    List<PassiveGrant> explicitGrants;
    readonly Dictionary<PassiveEventSourceKind, PassiveEventSource> _activeEventSources = new();
    readonly HashSet<PassiveEventSource> _autoAddedEventSources = new();
    readonly HashSet<PassiveEventSourceKind> _requiredEventSourceKinds = new();
    readonly List<PassiveEventSourceRequest> _eventSourceRequests = new();
    readonly List<PassiveEventSourceRequest> _eventSourceKindRequests = new();
    readonly List<PassiveEventSourceKind> _eventSourceKindCleanupBuffer = new();
    CharacterSkillManager skillManager;
    CombatEventBus subscribedBus;
    StaminaSystem subscribedResource;
    long _nextIndependentModifierId;
    bool rebuilding, refreshPending;
    int dispatchDepth;
    public event Action StatModifiersChanged;
    public event Action<PassiveDefinition> PassiveTriggered;
    public event Action RuntimeLoadoutChanged;
    public void AppendRuntimes(List<PassiveRuntime> buffer)
    { if (runtimes != null) foreach (var runtime in runtimes.Values) buffer.Add(runtime); }
    public CharacteContext Owner => ctx;
    public double Clock { get; private set; }
    public double GameClock { get; private set; }
    public int MaxEventDepth => maxEventDepth;
    public float ChainRetentionSeconds => chainStateTtlSeconds;
    public int RuntimeCount => runtimes?.Count ?? 0;
    public int RuntimeStateCount
    { get { int count = 0; if (runtimes != null) foreach (var runtime in runtimes.Values) count += runtime.StateCount; return count; } }
    public bool HasCombatSubscription => subscribedBus != null;
    public bool RequiresTick { get; private set; }
    public bool ContainsRuntime(PassiveRuntime runtime) => runtimes != null && runtimes.TryGetValue(runtime.Grant.Key, out var current) && ReferenceEquals(runtime, current);
    void Awake() => ResolveReferences();
    void OnEnable() { ResolveReferences(); RefreshPassiveLoadout(); }
    void OnDisable()
    {
        Unsubscribe();
        if (skillManager != null) skillManager.PassiveLoadoutChanged -= RefreshPassiveLoadout;
        skillManager = null;
        runtimes = null; _activeModifiers = null; Clock = GameClock = 0; RequiresTick = false;
        PassiveDispatch.ReleaseOwner(this);
        PassiveTicker.SetRequired(this, false);
        ReleaseAllOptionalEventSources(); NotifyStatModifiersChanged();
        RuntimeLoadoutChanged?.Invoke();
    }
    void OnValidate()
    {
        ReconcileIds(runtimePassives, runtimeGrantIds, runtimeGrantDefinitions);
        ReconcileIds(extraPassives, extraGrantIds, extraGrantDefinitions);
    }
    static void ReconcileIds(List<PassiveDefinition> definitions, List<string> ids, List<PassiveDefinition> previous)
    {
        if (previous.Count == 0) { EnsureIds(ids, definitions.Count); previous.AddRange(definitions); return; }
        var oldIds = new List<string>(ids);
        var oldDefinitions = new List<PassiveDefinition>(previous);
        ids.Clear();
        foreach (var definition in definitions)
        {
            int index = oldDefinitions.IndexOf(definition);
            ids.Add(index >= 0 && index < oldIds.Count ? oldIds[index] : Guid.NewGuid().ToString("N"));
            if (index >= 0) oldDefinitions[index] = null;
        }
        previous.Clear(); previous.AddRange(definitions);
        EnsureIds(ids, definitions.Count);
    }
    static void EnsureIds(List<string> ids, int count)
    {
        while (ids.Count < count) ids.Add(Guid.NewGuid().ToString("N"));
        if (ids.Count > count) ids.RemoveRange(count, ids.Count - count);
        var seen = new HashSet<string>();
        for (int i = 0; i < ids.Count; i++) if (string.IsNullOrWhiteSpace(ids[i]) || !seen.Add(ids[i])) { ids[i] = Guid.NewGuid().ToString("N"); seen.Add(ids[i]); }
    }
    void ResolveReferences()
    {
        if (!ctx) ctx = CharacterContextModuleLookup.ResolveContext(gameObject);
        ctx?.ResolveReferences();
        if (ctx == null) return;
        ctx.PassiveController = this;
        statsHub = ctx.StatsHub; combatEventBus = ctx.CombatEventBus; statusEffectController = ctx.StatusEffects;
        if (skillManager == ctx.SkillManager) return;
        if (skillManager != null) skillManager.PassiveLoadoutChanged -= RefreshPassiveLoadout;
        skillManager = ctx.SkillManager;
        if (skillManager != null) skillManager.PassiveLoadoutChanged += RefreshPassiveLoadout;
    }
    public void RefreshPassiveLoadout()
    {
        if (!isActiveAndEnabled) return;
        if (rebuilding || dispatchDepth > 0) { refreshPending = true; return; }
        rebuilding = true;
        try
        {
            ResolveReferences(); if (ctx == null) return;
            OnValidate();
            var grants = new List<PassiveGrant>();
            if (skillManager != null)
            {
                var equipped = new List<EquippedPassive>(); skillManager.AppendConfiguredPassiveDefinitions(equipped);
                for (int i = 0; i < equipped.Count; i++) grants.Add(new PassiveGrant(equipped[i].Definition, "skill", equipped[i].GrantId ?? i.ToString(), equipped[i].Upgrades));
            }
            if (explicitGrants != null) grants.AddRange(explicitGrants);
            else AddList(runtimePassives, runtimeGrantIds, "runtime", grants);
            AddList(extraPassives, extraGrantIds, "extra", grants);
            foreach (var component in ctx.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || !component.isActiveAndEnabled) continue;
                if (CharacterContextModuleLookup.ResolveContext(component.gameObject) != ctx && component != ctx.AccessoryLoadout) continue;
                if (component is IPassiveGrantProvider provider) provider.AppendPassiveGrants(grants);
                else if (component is IPassiveDefinitionProvider legacy)
                {
                    var definitions = new List<PassiveDefinition>(); legacy.AppendPassiveDefinitions(definitions);
                    for (int i = 0; i < definitions.Count; i++) grants.Add(new PassiveGrant(definitions[i], "provider:" + component.GetInstanceID(), i.ToString()));
                }
            }
            var next = grants.Count > 0 ? new Dictionary<string, PassiveRuntime>() : null;
            foreach (var grant in grants)
            {
                if (grant.Definition == null) continue;
                var errors = PassiveRecipeValidator.Validate(grant.Definition.recipe);
                if (grant.Definition.recipe != null) grant.Options?.Validate(grant.Definition.recipe, errors);
                if (errors.Count > 0) { Debug.LogError("[Passive] " + grant.Definition.name + ": " + string.Join("; ", errors), grant.Definition); continue; }
                if (next.ContainsKey(grant.Key)) { Debug.LogError("[Passive] Duplicate grant " + grant.Key, this); continue; }
                PassiveRuntime runtime = null; runtimes?.TryGetValue(grant.Key, out runtime);
                runtime ??= new PassiveRuntime(this, grant);
                next.Add(grant.Key, runtime);
                try { runtime.Rebuild(grant); } catch (Exception error) { runtime.Fail(error); }
            }
            runtimes = next?.Count > 0 ? next : null;
            if (runtimes == null) PassiveDispatch.ReleaseOwner(this);
            RefreshOptionalSources(); RefreshSubscriptions();
            statsHub?.RebuildModifierProviders(); NotifyStatModifiersChanged();
        }
        finally { rebuilding = false; }
        refreshPending = false;
        RuntimeLoadoutChanged?.Invoke();
    }
    static void AddList(List<PassiveDefinition> list, List<string> ids, string source, List<PassiveGrant> output)
    { EnsureIds(ids, list.Count); for (int i = 0; i < list.Count; i++) if (list[i] != null) output.Add(new PassiveGrant(list[i], source, ids[i])); }
    public void SetRuntimePassives(IReadOnlyList<PassiveDefinition> passives)
    {
        explicitGrants = null;
        var previous = new List<PassiveDefinition>(runtimePassives); var oldIds = new List<string>(runtimeGrantIds);
        runtimePassives.Clear(); runtimeGrantIds.Clear();
        if (passives != null) foreach (var definition in passives)
        {
            if (definition == null) continue;
            int index = previous.IndexOf(definition); runtimePassives.Add(definition);
            runtimeGrantIds.Add(index >= 0 && index < oldIds.Count ? oldIds[index] : Guid.NewGuid().ToString("N"));
            if (index >= 0) previous[index] = null;
        }
        runtimeGrantDefinitions.Clear(); runtimeGrantDefinitions.AddRange(runtimePassives);
        RefreshPassiveLoadout();
    }
    public void SetRuntimeGrants(IReadOnlyList<PassiveGrant> grants)
    { explicitGrants = grants != null ? new List<PassiveGrant>(grants) : null; RefreshPassiveLoadout(); }
    void Unsubscribe()
    {
        if (subscribedBus != null) subscribedBus.EventPublished -= OnCombat;
        if (subscribedResource != null) { subscribedResource.OnStaminaChanged -= OnResourceChanged; subscribedResource.ResourceSpent -= OnResourceSpent; }
        subscribedBus = null; subscribedResource = null;
    }
    void RefreshSubscriptions()
    {
        bool combat = false, changed = false, spent = false;
        if (runtimes != null) foreach (var runtime in runtimes.Values)
        { if (runtime.IsAlive) { combat |= runtime.NeedsCombat; changed |= runtime.NeedsResource; spent |= runtime.NeedsResourceSpent; } }
        Unsubscribe();
        if (combat && combatEventBus != null) { subscribedBus = combatEventBus; subscribedBus.EventPublished += OnCombat; }
        if ((changed || spent) && ctx.StaminaSystem != null)
        {
            subscribedResource = ctx.StaminaSystem;
            if (changed) subscribedResource.OnStaminaChanged += OnResourceChanged;
            if (spent) subscribedResource.ResourceSpent += OnResourceSpent;
        }
        RefreshTiming();
    }
    internal void RefreshTiming()
    {
        bool timers = false;
        if (runtimes != null) foreach (var runtime in runtimes.Values) timers |= runtime.NeedsClock;
        RequiresTick = timers || HasTimedModifiers();
        PassiveTicker.SetRequired(this, RequiresTick);
    }
    PassiveEventContext ResourceEvent(PassiveEventType type, float value, string source, ulong fact = 0)
    {
        bool child = PassiveDispatch.TryGetCurrent(out var parent);
        return new(type, ctx.gameObject, ctx.gameObject, ctx.gameObject, source, child ? parent.AttackId : null, value, Time.timeAsDouble,
            child ? parent.ChainId : CombatEventBus.NextChainId(), child ? parent.Depth + 1 : 0,
            child ? PassiveEventOrigin.Passive : PassiveEventOrigin.External, parent.OriginPassiveId, parent.OriginRuleId, parent.Metadata,
            fact == 0 ? CombatEventBus.NextFactId() : fact, parent.FollowUpProvenance);
    }
    void OnResourceChanged(float current, float maximum)
    {
        NotifyStatModifiersChanged();
        PassiveDispatch.Enqueue(this, ResourceEvent(PassiveEventType.ResourceChanged, current, "stamina"), PassiveRuleTrigger.ResourceChanged);
    }
    void OnResourceSpent(ResourceSpentEvent evt) => PassiveDispatch.Enqueue(this,
        ResourceEvent(PassiveEventType.ResourceSpent, evt.Amount, evt.Reason, evt.FactId), PassiveRuleTrigger.ResourceSpent);
    void OnCombat(PassiveEventContext evt) => PassiveDispatch.Enqueue(this, evt, PassiveRuleTrigger.Combat);
    public void Dispatch(in PassiveEventContext evt, PassiveRuleTrigger trigger)
    {
        if (runtimes == null) return;
        dispatchDepth++;
        try { foreach (var runtime in runtimes.Values) { try { runtime.Handle(evt, trigger); } catch (Exception error) { runtime.Fail(error); } } }
        finally { dispatchDepth--; }
        if (refreshPending) RefreshPassiveLoadout();
    }
    public PassiveAttackReservation ReserveNextAttack(string attackId)
    {
        if (runtimes == null || string.IsNullOrEmpty(attackId)) return null;
        PassiveAttackReservation reservation = null;
        foreach (var runtime in runtimes.Values)
        { if (!runtime.HasNextAttack) continue; reservation ??= new PassiveAttackReservation(attackId); runtime.ReserveAttack(reservation); }
        return reservation;
    }
    public PassiveWeaponPhase PrepareWeaponShot(ref WeaponShotBuildContext shot)
    {
        if (!HasWeaponPhase(PassiveRuleTrigger.BeforeShot) && !HasWeaponPhase(PassiveRuleTrigger.ShotCommitted)) return null;
        var phase = new PassiveWeaponPhase(this, shot);
        try { ProcessWeaponPhase(phase, PassiveRuleTrigger.BeforeShot); shot = phase.Shot; return phase; }
        catch { phase.Dispose(); throw; }
    }
    public bool HasWeaponPhase(PassiveRuleTrigger phase)
    { if (runtimes != null) foreach (var runtime in runtimes.Values) if (runtime.HasPhase(phase)) return true; return false; }
    public void ModifyWeaponDamage(ref WeaponDamageBuildContext damage)
    {
        if (!HasWeaponPhase(PassiveRuleTrigger.BeforeDamage)) return;
        using var phase = new PassiveWeaponPhase(this, damage);
        ProcessWeaponPhase(phase, PassiveRuleTrigger.BeforeDamage); damage = phase.Damage;
    }
    internal void ProcessWeaponPhase(PassiveWeaponPhase phase, PassiveRuleTrigger trigger)
    {
        if (runtimes == null) return;
        var evt = phase.Shot.PassiveContext;
        if (phase.IsDamage) evt = new PassiveEventContext(PassiveEventType.None, ctx.gameObject, ctx.gameObject,
            phase.Damage.Target, null, phase.Damage.AttackId, phase.Damage.Damage, Time.timeAsDouble, 0, 0,
            PassiveEventOrigin.External, null, null, new CombatEventMetadata(weaponInstanceId: phase.Damage.WeaponInstanceId));
        using var inline = PassiveDispatch.BeginInline(evt);
        dispatchDepth++;
        try
        {
            foreach (var runtime in runtimes.Values)
            {
                var shot = phase.Shot; var damage = phase.Damage; int checkpoint = phase.ReservationCount;
                try { runtime.HandlePhase(phase, trigger, evt); }
                catch (Exception error)
                {
                    phase.Shot = shot; phase.Damage = damage; phase.RollbackFrom(checkpoint); runtime.Fail(error);
                }
            }
        }
        finally { dispatchDepth--; }
        if (refreshPending) RefreshPassiveLoadout();
    }
    public bool HasConversions
    { get { if (runtimes != null) foreach (var runtime in runtimes.Values) if (runtime.HasConversions) return true; return false; } }
    public int ConversionSignature
    {
        get
        {
            int hash = 17;
            if (runtimes != null) foreach (var runtime in runtimes.Values)
                if (runtime.HasConversions) unchecked { hash = hash * 31 + runtime.ConversionSignature; }
            return hash;
        }
    }
    public void AppendConversions(List<Vector2> buffer)
    { if (runtimes != null) foreach (var runtime in runtimes.Values) runtime.AppendConversions(buffer); }
    public void AppendStatModifiers(List<RuntimeStatModifier> buffer)
    {
        if (buffer == null) return;
        if (runtimes != null) foreach (var runtime in runtimes.Values)
        {
            int start = buffer.Count;
            try { runtime.AppendModifiers(buffer); }
            catch (Exception error) { buffer.RemoveRange(start, buffer.Count - start); runtime.Fail(error); }
        }
        if (_activeModifiers == null) return;
        foreach (var modifier in _activeModifiers) if (!modifier.IsExpired(Clock)) foreach (var stat in modifier.Modifiers)
            buffer.Add(new RuntimeStatModifier(stat.statType, stat.operation, stat.value * modifier.Stacks, modifier.ModifierKey));
    }
    public void Tick(float deltaTime)
    {
        GameClock += Mathf.Max(0, deltaTime);
        Clock += Mathf.Max(0, deltaTime) * (ctx != null && ctx.UsesWorldSlow && TimeSlowManager.Instance != null ? TimeSlowManager.Instance.WorldTimeScale : 1f);
        if (CleanupExpiredModifiers(Clock)) NotifyStatModifiersChanged();
        if (runtimes != null) foreach (var runtime in runtimes.Values)
            try { runtime.Tick(); } catch (Exception error) { runtime.Fail(error); }
        RefreshTiming();
    }
    bool CleanupExpiredModifiers(double now)
    {
        if (_activeModifiers == null) return false;
        bool changed = false;
        for (int i = _activeModifiers.Count - 1; i >= 0; i--) if (_activeModifiers[i].IsExpired(now)) { _activeModifiers.RemoveAt(i); changed = true; }
        if (_activeModifiers.Count == 0) _activeModifiers = null;
        return changed;
    }
    bool HasTimedModifiers()
    { if (_activeModifiers != null) foreach (var modifier in _activeModifiers) if (!double.IsPositiveInfinity(modifier.ExpiresAt)) return true; return false; }
    internal void NotifyStatModifiersChanged() { StatModifiersChanged?.Invoke(); statsHub?.MarkDirty(); }
    public void NotifyPassiveTriggered(PassiveDefinition definition) { if (definition != null) PassiveTriggered?.Invoke(definition); }
    public bool PublishChildEvent(in PassiveEventContext parent, PassiveEventType type, GameObject target = null,
        string eventSourceId = null, float value = 0, string originPassiveId = null, string originRuleId = null)
    {
        if (combatEventBus == null || parent.Depth >= maxEventDepth) return false;
        combatEventBus.Publish(combatEventBus.CreateChildContext(parent, type, ctx.gameObject, target, eventSourceId,
            parent.AttackId, value, PassiveEventOrigin.Passive, originPassiveId, originRuleId, metadata: parent.Metadata));
        return true;
    }
    void RefreshOptionalSources()
    {
        _eventSourceRequests.Clear(); _requiredEventSourceKinds.Clear();
        if (runtimes != null) foreach (var runtime in runtimes.Values) foreach (var rule in runtime.Grant.Definition.recipe.rules)
        {
            if (rule.optionalSource == PassiveEventSourceKind.None || !rule.condition.CanMatchWithUpgrades(new PassiveExecution(runtime, default, rule.id))) continue;
            _requiredEventSourceKinds.Add(rule.optionalSource);
            _eventSourceRequests.Add(new PassiveEventSourceRequest(rule.optionalSource, rule.eventType, rule.eventSourceId, rule.sourceDistance, rule.sourceValue));
        }
        foreach (var kind in _requiredEventSourceKinds)
        {
            var source = EnsureEventSource(kind); if (source == null) continue;
            _eventSourceKindRequests.Clear(); foreach (var request in _eventSourceRequests) if (request.Kind == kind) _eventSourceKindRequests.Add(request);
            source.ApplyRequests(ctx, combatEventBus, _eventSourceKindRequests);
        }
        ReleaseUnusedOptionalEventSources();
    }

    public bool TryApplyRuntimeModifier(PassiveActionDefinition action, string modifierKey, double now, SkillUpgradeStatSnapshot upgrades = null)
    {
        if (action == null) return false;
        var snapshot = new PassiveActionDefinition
        {
            durationSeconds = action.durationSeconds, grantedStacks = action.grantedStacks,
            maxStacks = action.maxStacks, stackPolicy = action.stackPolicy
        };
        foreach (var modifier in action.modifiers)
        {
            float value = modifier.value;
            if (upgrades != null && upgrades.TryGetAggregate(modifier.statType, out float add, out float multiply)) value = (value + add) * multiply;
            snapshot.modifiers.Add(new PassiveStatModifier { statType = modifier.statType, operation = modifier.operation, value = value });
        }
        bool result = ApplyModifierCore(snapshot, modifierKey, Clock);
        if (result) { NotifyStatModifiersChanged(); RefreshSubscriptions(); }
        return result;
    }
    bool ApplyModifierCore(PassiveActionDefinition action, string modifierKey, double now, SkillUpgradeStatSnapshot upgrades = null)
    {
        if (action == null || action.modifiers == null || action.modifiers.Count == 0)
            return false;

        CleanupExpiredModifiers(now);
        _activeModifiers ??= new();

        int maxStacks = action.maxStacks <= 0 ? int.MaxValue : action.maxStacks;
        int grantedStacks = Mathf.Clamp(action.grantedStacks, 1, maxStacks);
        string resolvedModifierKey = string.IsNullOrWhiteSpace(modifierKey)
            ? $"passive:{GetInstanceID()}:modifier"
            : modifierKey;

        switch (action.stackPolicy)
        {
            case PassiveModifierStackPolicy.Independent:
                _activeModifiers.Add(CreateActiveModifier(action, $"{resolvedModifierKey}:inst:{++_nextIndependentModifierId}", grantedStacks, maxStacks, now, upgrades));
                return true;

            case PassiveModifierStackPolicy.IgnoreWhileActive:
            {
                var existing = FindActiveModifier(resolvedModifierKey, now);
                if (existing != null)
                    return false;

                _activeModifiers.Add(CreateActiveModifier(action, resolvedModifierKey, grantedStacks, maxStacks, now, upgrades));
                return true;
            }

            case PassiveModifierStackPolicy.RefreshDuration:
            {
                var existing = FindActiveModifier(resolvedModifierKey, now);
                if (existing == null)
                {
                    _activeModifiers.Add(CreateActiveModifier(action, resolvedModifierKey, grantedStacks, maxStacks, now, upgrades));
                    return true;
                }

                existing.Stacks = Mathf.Clamp(Mathf.Max(existing.Stacks, grantedStacks), 1, maxStacks);
                existing.MaxStacks = maxStacks;
                existing.ExpiresAt = ResolveExpiry(action.durationSeconds, now);
                existing.Modifiers = action.modifiers;
                existing.Upgrades = upgrades;
                return true;
            }

            case PassiveModifierStackPolicy.AddStacks:
            {
                var existing = FindActiveModifier(resolvedModifierKey, now);
                if (existing == null)
                {
                    _activeModifiers.Add(CreateActiveModifier(action, resolvedModifierKey, grantedStacks, maxStacks, now, upgrades));
                    return true;
                }

                existing.Stacks = Mathf.Clamp(existing.Stacks + grantedStacks, 1, maxStacks);
                existing.MaxStacks = maxStacks;
                existing.ExpiresAt = ResolveExpiry(action.durationSeconds, now);
                existing.Modifiers = action.modifiers;
                existing.Upgrades = upgrades;
                return true;
            }

            case PassiveModifierStackPolicy.Replace:
            default:
            {
                var existing = FindActiveModifier(resolvedModifierKey, now);
                if (existing == null)
                {
                    _activeModifiers.Add(CreateActiveModifier(action, resolvedModifierKey, grantedStacks, maxStacks, now, upgrades));
                    return true;
                }

                existing.Stacks = grantedStacks;
                existing.MaxStacks = maxStacks;
                existing.ExpiresAt = ResolveExpiry(action.durationSeconds, now);
                existing.Modifiers = action.modifiers;
                existing.Upgrades = upgrades;
                return true;
            }
        }
    }

    PassiveEventSource EnsureEventSource(PassiveEventSourceKind kind)
    {
        if (kind == PassiveEventSourceKind.None)
            return null;

        if (_activeEventSources.TryGetValue(kind, out PassiveEventSource activeSource) && activeSource != null)
            return activeSource;

        PassiveEventSource source = FindEventSource(kind, out bool ambiguous);
        if (ambiguous) return null;
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

    PassiveEventSource FindEventSource(PassiveEventSourceKind kind, out bool ambiguous)
    {
        ambiguous = false;
        PassiveEventSource match = null;
        PassiveEventSource[] sources = ctx != null
            ? ctx.GetComponentsInChildren<PassiveEventSource>(true)
            : GetComponentsInChildren<PassiveEventSource>(true);

        for (int i = 0; i < sources.Length; i++)
        {
            PassiveEventSource source = sources[i];
            if (source == null || source.Kind != kind || CharacterContextModuleLookup.ResolveContext(source.gameObject) != ctx) continue;
            if (match != null)
            {
                ambiguous = true;
                Debug.LogError("[Passive] Multiple owned event sources for " + kind + "; keep one explicit source per actor.", this);
                return null;
            }
            match = source;
        }

        return match;
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

    ActivePassiveModifier CreateActiveModifier(PassiveActionDefinition action, string modifierKey, int stacks, int maxStacks, double now, SkillUpgradeStatSnapshot upgrades)
    {
        return new ActivePassiveModifier
        {
            ModifierKey = modifierKey,
            Modifiers = action.modifiers,
            Stacks = stacks,
            MaxStacks = maxStacks,
            ExpiresAt = ResolveExpiry(action.durationSeconds, now),
            Upgrades = upgrades
        };
    }

    static double ResolveExpiry(float durationSeconds, double now)
    {
        return durationSeconds <= 0f ? double.PositiveInfinity : now + durationSeconds;
    }

    sealed class ActivePassiveModifier
    {
        public string ModifierKey;
        public List<PassiveStatModifier> Modifiers;
        public int Stacks;
        public int MaxStacks;
        public double ExpiresAt;
        public SkillUpgradeStatSnapshot Upgrades;

        public bool IsExpired(double now)
        {
            return !double.IsPositiveInfinity(ExpiresAt) && now >= ExpiresAt;
        }
    }



}
