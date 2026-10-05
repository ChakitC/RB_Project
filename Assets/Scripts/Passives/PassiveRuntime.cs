using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One installation. Definition assets never hold counters or live actor references.</summary>
public sealed class PassiveRuntime
{
    public PassiveGrant Grant { get; private set; }
    public readonly PassiveController Controller;
    public event Action StateChanged;
    public event Action<PassiveStep, PassiveResult, PassiveEventContext> EffectResolved;
    public event Action<string, PassiveAttackPhase, float> AttackChanged;
    internal void NotifyEffect(PassiveStep step, PassiveResult result, PassiveEventContext evt, PassiveExecution execution)
    {
        if (result.Status == PassiveOutcome.Succeeded && !string.IsNullOrEmpty(step.successSignal))
            Feedback(step.successSignal, string.IsNullOrEmpty(step.successValueVariable) ? 0 : Mathf.RoundToInt(execution.PeekVariable(step.successValueVariable)));
        EffectResolved?.Invoke(step, result, evt);
    }
    public void Feedback(string signal, int value) { if (!string.IsNullOrEmpty(signal)) Grant.Options?.Feedback?.Invoke(signal, value); }
    internal void NotifyAttack(string id, PassiveAttackPhase phase, float percent) => AttackChanged?.Invoke(id, phase, percent);
    readonly int life;
    readonly Dictionary<PassiveEventType, List<PassiveRecipeRule>> eventRules = new();
    Dictionary<string, RuleState> states;
    Dictionary<string, float> variables;
    Dictionary<string, List<RuntimeStatModifier>> equipped;
    Dictionary<string, DynamicEntry> dynamicEntries;
    Dictionary<string, Vector2> conversions;
    Dictionary<string, AttackEntry> attacks;
    Dictionary<string, ExpiryEntry> expiries;
    Dictionary<string, (CharacteContext context, int life)> targets;
    Dictionary<string, int> variableEpochs;
    bool faulted;
    readonly Dictionary<PassiveRuleTrigger, List<PassiveRecipeRule>> phaseRules = new();
    readonly Dictionary<PassiveRecipeRule, PassiveExecution> executions = new();
    public bool HasPhase(PassiveRuleTrigger phase) => phaseRules.ContainsKey(phase);
    double Now => Grant.Options?.UsesWorldSlow == false ? Controller.GameClock : Controller.Clock;
    public bool IsAlive => !faulted && Controller != null && Controller.isActiveAndEnabled && Controller.Owner != null && Controller.Owner.LifeGeneration == life;
    public bool NeedsCombat => eventRules.Count != 0;
    public bool NeedsResource { get; private set; }
    public bool NeedsResourceSpent { get; private set; }
    public bool NeedsClock
    {
        get
        {
            if (faulted) return false;
            if (expiries != null) foreach (var expiry in expiries.Values) if (expiry.Active) return true;
            if (states != null) foreach (var state in states.Values)
                if (state.ReadyAt > Now || state.Counter > 0 && state.WindowEnd > Now) return true;
            return false;
        }
    }
    bool initialized;
    public int StateCount => (states?.Count ?? 0) + (variables?.Count ?? 0);
    public float StaminaRatio => Controller.Owner.StaminaSystem != null && Controller.Owner.StaminaSystem.Max > 0
        ? Mathf.Clamp01(Controller.Owner.StaminaSystem.Current / Controller.Owner.StaminaSystem.Max) : 1f;
    public Func<float> RandomValue = () => UnityEngine.Random.value;

    public PassiveRuntime(PassiveController controller, PassiveGrant grant)
    { Controller = controller; Grant = grant; life = controller.Owner.LifeGeneration; }

    public void Rebuild(PassiveGrant grant)
    {
        Grant = new PassiveGrant(grant.Definition, grant.SourceId, grant.GrantId, grant.Upgrades?.CreateReadOnlyCopy(), grant.Options);
        phaseRules.Clear();
        executions.Clear();
        eventRules.Clear(); equipped?.Clear(); dynamicEntries?.Clear(); conversions?.Clear(); attacks?.Clear();
        NeedsResource = NeedsResourceSpent = false;
        foreach (var rule in Grant.Definition.recipe.rules)
        {
            var execution = new PassiveExecution(this, default, rule.id) { Refreshing = initialized };
            executions.Add(rule, execution);
            float cooldown = Mathf.Max(0, rule.cooldown.Resolve(execution));
            if (states != null && states.TryGetValue(rule.id, out var state))
                state.ReadyAt = Math.Min(state.ReadyAt, Now + cooldown);
            switch (rule.trigger)
            {
                case PassiveRuleTrigger.Combat:
                    if (!eventRules.TryGetValue(rule.eventType, out var list)) eventRules.Add(rule.eventType, list = new());
                    list.Add(rule); break;
                case PassiveRuleTrigger.ResourceChanged: NeedsResource = true; break;
                case PassiveRuleTrigger.ResourceSpent: NeedsResourceSpent = true; break;
                case PassiveRuleTrigger.BeforeShot:
                case PassiveRuleTrigger.ShotCommitted:
                case PassiveRuleTrigger.BeforeDamage:
                    if (!phaseRules.TryGetValue(rule.trigger, out var phases)) phaseRules.Add(rule.trigger, phases = new());
                    phases.Add(rule); break;
                case PassiveRuleTrigger.Equipped:
                    if (rule.condition.Evaluate(execution)) execution.Run(rule.steps);
                    break;
            }
        }
        if (dynamicEntries != null) foreach (var entry in dynamicEntries.Values)
            NeedsResource |= entry.Step.value.UsesResource || entry.Step.condition.UsesResource;
        initialized = true;
    }

    public void Handle(in PassiveEventContext evt, PassiveRuleTrigger trigger)
    {
        if (!IsAlive || !Accepts(evt)) return;
        if (trigger == PassiveRuleTrigger.Combat)
        {
            if (!eventRules.TryGetValue(evt.Type, out var rules)) return;
            foreach (var rule in rules) Evaluate(rule, evt);
        }
        else foreach (var rule in Grant.Definition.recipe.rules)
            if (rule.trigger == trigger) Evaluate(rule, evt);
    }

    bool Accepts(in PassiveEventContext evt) => string.IsNullOrEmpty(Grant.Options?.WeaponInstanceId) ||
        evt.Metadata.WeaponInstanceId == Grant.Options.WeaponInstanceId && !evt.Metadata.IsWeaponAffixGenerated;

    public void HandlePhase(PassiveWeaponPhase phase, PassiveRuleTrigger trigger, in PassiveEventContext evt)
    {
        if (!IsAlive || !Accepts(evt) || !phaseRules.TryGetValue(trigger, out var rules)) return;
        foreach (var rule in rules) Evaluate(rule, evt, phase);
    }

    void Evaluate(PassiveRecipeRule rule, in PassiveEventContext evt, PassiveWeaponPhase phase = null)
    {
        if (!IsAlive || !MatchesOrigin(rule.originFilter, evt.Origin)) return;
        if (rule.requireTarget && evt.Target == null || rule.requireAttackId && string.IsNullOrEmpty(evt.AttackId)) return;
        if (!string.IsNullOrEmpty(rule.eventSourceId) && rule.eventSourceId != evt.EventSourceId) return;
        var execution = executions[rule];
        execution.Refreshing = false; execution.SetEvent(evt, phase);
        if (!rule.condition.Evaluate(execution)) { Controller.Trace(rule.id, "condition not met"); return; }
        int required = Mathf.Max(1, Mathf.RoundToInt(rule.count.Resolve(execution)));
        float window = Mathf.Max(0, rule.window.Resolve(execution));
        float cooldown = Mathf.Max(0, rule.cooldown.Resolve(execution));
        RuleState state = null;
        if (required > 1 || window > 0 || cooldown > 0 || rule.once != PassiveOnceScope.None || rule.consume == PassiveConsumePolicy.OnSuccess)
        {
            states ??= new();
            if (!states.TryGetValue(rule.id, out state)) states.Add(rule.id, state = new());
        }
        double now = Now;
        string onceKey = rule.once == PassiveOnceScope.Dash ? (evt.Metadata.DashExecutionId != 0 ? evt.Metadata.DashExecutionId.ToString() : null) : evt.AttackId;
        if (state != null)
        {
            if (state.Busy || rule.once != PassiveOnceScope.None && (string.IsNullOrEmpty(onceKey) || state.Once == onceKey)) return;
            if (window > 0 && state.Counter > 0 && now > state.WindowEnd) state.Counter = 0;
            if (state.Counter == 0) state.WindowEnd = now + window;
            state.Counter++;
            Controller.RefreshTiming();
            if (state.Counter < required || now < state.ReadyAt) { Controller.Trace(rule.id, "counter/cooldown gate"); return; }
        }
        float chance = Mathf.Clamp01(rule.chance.Resolve(execution));
        if (chance <= 0 || chance < 1 && RandomValue() > chance) return;
        if (!PassiveDispatch.ClaimRule(Controller, execution.Key, evt, rule.oncePerTargetPerChain)) return;
        if (state != null) state.Busy = true;
        bool attempted = rule.consume == PassiveConsumePolicy.OnAttempt;
        if (attempted) Consume();
        try
        {
            var result = execution.Run(rule.steps);
            Controller.Trace(rule.id, result.Status.ToString());
            if (result.Status == PassiveOutcome.Succeeded)
            {
                if (!attempted) Consume();
                Controller.NotifyPassiveTriggered(Grant.Definition);
            }
        }
        finally { if (state != null) state.Busy = false; Controller.RefreshTiming(); }

        void Consume()
        {
            if (state == null) return;
            state.ReadyAt = now + cooldown;
            state.Counter = rule.counterConsumeMode == PassiveCounterConsumeMode.CarryOver ? Mathf.Max(0, state.Counter - required) : 0;
            if (state.Counter > 0) state.WindowEnd = now + window;
            if (rule.once != PassiveOnceScope.None) state.Once = onceKey;
        }
    }

    public static bool MatchesOrigin(PassiveOriginFilter filter, PassiveEventOrigin origin) => filter switch
    {
        PassiveOriginFilter.ExternalOnly => origin == PassiveEventOrigin.External,
        PassiveOriginFilter.NonPassive => origin != PassiveEventOrigin.Passive,
        PassiveOriginFilter.PassiveOnly => origin == PassiveEventOrigin.Passive,
        PassiveOriginFilter.Any => true,
        _ => false
    };

    public float ReadVariable(string key, PassiveVariable definition)
    {
        if (definition.persistent && Grant.Options?.StateStore != null) return PeekVariable(key, definition);
        variables ??= new();
        if (!variables.TryGetValue(key, out float value)) variables.Add(key, value = Mathf.Clamp(definition.initial, 0, definition.maximum));
        return value;
    }
    public void WriteVariable(string key, PassiveVariable definition, float value)
    {
        float before = PeekVariable(key, definition);
        value = Mathf.Clamp(float.IsFinite(value) ? value : 0, 0, definition.maximum);
        if (definition.persistent && Grant.Options?.StateStore != null)
        {
            Grant.Options.StateStore.Write(key, value);
            if (before != value) Changed();
            return;
        }
        variables ??= new();
        variables[key] = Mathf.Clamp(float.IsFinite(value) ? value : 0, 0, definition.maximum);
        if (before != variables[key]) Changed();
    }
    void Changed() { StateChanged?.Invoke(); if (dynamicEntries?.Count > 0) Controller.NotifyStatModifiersChanged(); }
    public float PeekVariable(string key, PassiveVariable definition)
    {
        if (definition.persistent && Grant.Options?.StateStore?.TryRead(key, out float stored) == true)
            return Mathf.Clamp(stored, 0, definition.maximum);
        return variables != null && variables.TryGetValue(key, out float value) ? value : Mathf.Clamp(definition.initial, 0, definition.maximum);
    }
    public void SetExpiry(PassiveExecution execution, float duration, List<string> reset, string signal = null)
    {
        expiries ??= new();
        if (!expiries.TryGetValue(execution.Key, out var entry)) expiries.Add(execution.Key, entry = new());
        entry.Execution = execution; entry.Variables = reset; entry.End = Now + Mathf.Max(.01f, duration); entry.Active = true;
        entry.Signal = signal;
        Controller.RefreshTiming();
    }
    public void Tick()
    {
        if (!IsAlive || expiries == null) return;
        foreach (var expiry in expiries.Values)
        {
            if (!expiry.Active || Now < expiry.End) continue;
            expiry.Active = false;
            foreach (var variable in expiry.Variables)
            {
                string key = expiry.Execution.ResolveVariableKey(variable);
                if (key != null && variableEpochs != null && variableEpochs.ContainsKey(key)) variableEpochs[key]++;
                expiry.Execution.WriteVariable(variable, 0);
            }
            Feedback(expiry.Signal, 0);
        }
    }
    public int VariableEpoch(string key)
    {
        variableEpochs ??= new();
        if (!variableEpochs.TryGetValue(key, out int value)) variableEpochs.Add(key, 0);
        return value;
    }
    public void Fail(Exception exception)
    {
        faulted = true;
        Controller.NotifyStatModifiersChanged(); Controller.RefreshTiming();
        Debug.LogException(new InvalidOperationException("Passive grant '" + Grant.Key + "' disabled after execution failure.", exception), Controller);
    }
    public void TrackTarget(string key, CharacteContext target)
    { targets ??= new(); targets[key] = (target, target.LifeGeneration); }
    public bool MatchesTarget(string key, CharacteContext target) => target != null && targets != null &&
        targets.TryGetValue(key, out var entry) && entry.context == target && entry.life == target.LifeGeneration;
    public bool TryGetVariable(string id, out float value, out float maximum)
    {
        foreach (var definition in Grant.Definition.recipe.variables)
            if (definition.id == id)
            { value = PeekVariable(id, definition); maximum = definition.maximum; return true; }
        value = maximum = 0; return false;
    }
    public float GetNextAttackBonusPercent(string variableId)
    {
        if (attacks != null) foreach (var entry in attacks.Values)
            if (entry.Execution.ResolveVariableKey(entry.Step.variable) == variableId)
                return entry.Execution.PeekVariable(entry.Step.variable) * Mathf.Max(0, entry.Step.damagePercentPerUnit.Resolve(entry.Execution, true));
        return 0;
    }

    public void SetEquippedModifier(string key, List<PassiveStatModifier> modifiers)
    {
        equipped ??= new();
        var values = new List<RuntimeStatModifier>();
        foreach (var modifier in modifiers)
        {
            float value = modifier.value;
            if (Grant.Upgrades != null && Grant.Upgrades.TryGetAggregate(modifier.statType, out float add, out float multiply)) value = (value + add) * multiply;
            values.Add(new RuntimeStatModifier(modifier.statType, modifier.operation, value, key));
        }
        equipped[key] = values;
    }
    public void SetDynamic(PassiveExecution execution, PassiveDynamicModifierStep step)
    { dynamicEntries ??= new(); dynamicEntries[execution.Key] = new DynamicEntry { Execution = execution, Step = step }; }
    public void SetConversion(string key, float fraction, float ratio)
    { conversions ??= new(); conversions[key] = new Vector2(Mathf.Clamp01(fraction), Mathf.Max(0, ratio)); }
    public void SetNextAttack(PassiveExecution execution, PassiveNextAttackStep step)
    { attacks ??= new(); attacks[execution.Key] = new AttackEntry { Execution = execution, Step = step }; }

    public void AppendModifiers(List<RuntimeStatModifier> buffer)
    {
        if (faulted) return;
        if (equipped != null) foreach (var list in equipped.Values) buffer.AddRange(list);
        if (dynamicEntries != null) foreach (var pair in dynamicEntries)
        {
            var entry = pair.Value;
            if (entry.Step.condition.Evaluate(entry.Execution)) buffer.Add(new RuntimeStatModifier(entry.Step.stat,
                entry.Step.operation, entry.Step.value.Resolve(entry.Execution), pair.Key));
        }
    }
    public bool HasConversions => conversions?.Count > 0;
    public int ConversionSignature
    {
        get
        {
            int hash = 17;
            if (conversions != null) foreach (var value in conversions.Values)
                unchecked { hash = hash * 31 + value.GetHashCode(); }
            return hash;
        }
    }
    public void AppendConversions(List<Vector2> buffer)
    { if (conversions != null) foreach (var value in conversions.Values) buffer.Add(value); }

    public void ReserveAttack(PassiveAttackReservation reservation)
    {
        if (attacks == null) return;
        var used = new HashSet<string>();
        foreach (var entry in attacks.Values)
        {
            string key = entry.Execution.ResolveVariableKey(entry.Step.variable);
            if (key == null || !used.Add(key)) continue;
            float value = entry.Execution.ReadVariable(entry.Step.variable);
            if (value <= 0) continue;
            entry.Execution.WriteVariable(entry.Step.variable, 0);
            reservation.Add(entry.Execution, entry.Step.variable, value,
                value * Mathf.Max(0, entry.Step.damagePercentPerUnit.Resolve(entry.Execution)));
        }
    }
    public bool HasNextAttack => IsAlive && attacks?.Count > 0;

    sealed class RuleState
    {
        public int Counter;
        public double WindowEnd, ReadyAt;
        public bool Busy;
        public string Once;
    }
    sealed class DynamicEntry { public PassiveExecution Execution; public PassiveDynamicModifierStep Step; }
    sealed class AttackEntry { public PassiveExecution Execution; public PassiveNextAttackStep Step; }
    sealed class ExpiryEntry { public PassiveExecution Execution; public List<string> Variables; public string Signal; public double End; public bool Active; }
}
