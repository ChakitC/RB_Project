using System.Collections.Generic;
using UnityEngine;

public sealed class PassiveExecution
{
    public readonly PassiveRuntime Runtime;
    readonly PassiveGrant previewGrant;
    public PassiveGrant Grant => Runtime != null ? Runtime.Grant : previewGrant;
    public PassiveEventContext Event { get; private set; }
    public readonly string Path;
    public readonly string VariableScope;
    readonly PassiveExecution parent;
    readonly PassiveFragmentStep invocation;
    Dictionary<PassiveStep, PassiveExecution> children;
    Dictionary<PassiveFragmentStep, PassiveExecution> fragments;
    string cachedKey;
    public PassiveResult Last;
    public bool Refreshing;
    public PassiveWeaponPhase WeaponPhase;
    public CharacteContext Owner => Runtime?.Controller.Owner;
    CharacteContext targetContext;
    int targetLife;
    public CharacteContext EventTarget => targetContext != null && targetContext.isActiveAndEnabled && targetContext.LifeGeneration == targetLife ? targetContext : null;
    public string Key => cachedKey ??= Grant.Key + "/" + Path;
    public PassiveExecution(PassiveGrant previewGrant)
    { this.previewGrant = previewGrant; Path = "preview"; VariableScope = ""; }

    public PassiveExecution(PassiveRuntime runtime, in PassiveEventContext evt, string path,
        string variableScope = "", PassiveExecution parent = null, PassiveFragmentStep invocation = null)
    {
        Runtime = runtime; Event = evt; Path = path; VariableScope = variableScope;
        this.parent = parent; this.invocation = invocation;
        previewGrant = parent != null ? parent.Grant : default;
        SetEvent(evt, null);
    }
    internal void SetEvent(in PassiveEventContext evt, PassiveWeaponPhase phase)
    {
        Event = evt; WeaponPhase = phase; Last = default;
        targetContext = CharacterContextModuleLookup.ResolveContext(evt.Target);
        targetLife = targetContext != null ? targetContext.LifeGeneration : 0;
    }

    public PassiveExecution Enter(PassiveFragmentStep call)
    {
        fragments ??= new();
        if (!fragments.TryGetValue(call, out var fragmentExecution))
            fragments.Add(call, fragmentExecution = new(Runtime, Event, Path + "/fragment:" + call.id, Path, this, call));
        fragmentExecution.CopyEvent(this); return fragmentExecution;
    }
    void CopyEvent(PassiveExecution source)
    {
        Event = source.Event; Last = source.Last; Refreshing = source.Refreshing; WeaponPhase = source.WeaponPhase;
        targetContext = source.targetContext; targetLife = source.targetLife;
    }

    public PassiveResult Run(List<PassiveStep> steps)
    {
        bool succeeded = false;
        if (steps == null) return new(PassiveOutcome.Failed);
        foreach (var step in steps)
        {
            if (step == null || !Runtime.IsAlive || !PassiveDispatch.TryStep(Event.ChainId)) break;
            if (Refreshing && !step.RunOnRefresh) continue;
            children ??= new();
            if (!children.TryGetValue(step, out var next))
                children.Add(step, next = new PassiveExecution(Runtime, Event, Path + "/" + step.id, VariableScope, parent, invocation));
            next.CopyEvent(this);
            Last = step.Execute(next);
            Runtime.NotifyEffect(step, Last, Event, next);
            succeeded |= Last.Status == PassiveOutcome.Succeeded;
        }
        return succeeded ? PassiveResult.Success() : Last;
    }

    public float NumberParameter(string id)
    {
        if (invocation != null) foreach (var binding in invocation.parameterBindings)
            if (binding.local == id) return parent.NumberParameter(binding.parent);
        return invocation == null && Grant.Options?.TryGetNumber(id, out float number) == true ? number : Parameter(id)?.number ?? 0f;
    }
    public StatusEffectDef StatusParameter(string id)
    {
        if (invocation != null) foreach (var binding in invocation.parameterBindings)
            if (binding.local == id) return parent.StatusParameter(binding.parent);
        return Parameter(id)?.status;
    }
    PassiveParameter Parameter(string id)
    {
        if (invocation == null)
        {
            foreach (var entry in Grant.Definition.recipe.parameters) if (entry.id == id) return entry;
            return null;
        }
        foreach (var entry in invocation.overrides) if (entry.id == id) return entry;
        foreach (var entry in invocation.fragment.parameters) if (entry.id == id) return entry;
        return null;
    }

    string VariableKey(string id, out PassiveVariable definition)
    {
        definition = null;
        if (invocation != null)
        {
            foreach (var binding in invocation.bindings)
                if (binding.local == id) return parent.VariableKey(binding.parent, out definition);
            foreach (var variable in invocation.fragment.variables)
                if (variable.id == id) { definition = variable; return VariableScope + "/" + id; }
        }
        else
        {
            foreach (var variable in Runtime.Grant.Definition.recipe.variables)
                if (variable.id == id) { definition = variable; return id; }
        }
        return null;
    }

    public string ResolveVariableKey(string id) => VariableKey(id, out _);
    public string ScopedKey(string id) => string.IsNullOrEmpty(VariableScope) ? id : VariableScope + "/" + id;
    public float PeekVariable(string id)
    {
        string key = VariableKey(id, out var definition);
        return key == null ? 0 : Runtime.PeekVariable(key, definition);
    }
    public float ReadVariable(string id)
    {
        string key = VariableKey(id, out var definition);
        return key == null ? 0 : Runtime.ReadVariable(key, definition);
    }
    public void WriteVariable(string id, float value)
    {
        string key = VariableKey(id, out var definition);
        if (key != null) Runtime.WriteVariable(key, definition, value);
    }
}
