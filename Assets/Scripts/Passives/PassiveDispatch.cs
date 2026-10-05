using System.Collections.Generic;
using UnityEngine;

/// <summary>Only passive work is deferred. Other CombatEventBus subscribers remain synchronous.</summary>
public static class PassiveDispatch
{
    static readonly Queue<(PassiveController owner, int life, CharacteContext target, int targetLife, PassiveEventContext evt, PassiveRuleTrigger trigger)> queue = new();
    static readonly Dictionary<ulong, Chain> chains = new();
    static bool draining;
    static bool hasCurrent;
    static PassiveEventContext current;
    public static bool TryGetCurrent(out PassiveEventContext evt) { evt = current; return hasCurrent; }
    static readonly List<ulong> expired = new();
    const int MaxSteps = 1024;

    public static void Enqueue(PassiveController owner, in PassiveEventContext evt, PassiveRuleTrigger trigger)
    {
        if (owner == null || owner.Owner == null) return;
        if (evt.Depth > owner.MaxEventDepth) { owner.Trace("chain", "depth limit"); return; }
        var target = CharacterContextModuleLookup.ResolveContext(evt.Target);
        queue.Enqueue((owner, owner.Owner.LifeGeneration, target, target != null ? target.LifeGeneration : 0, evt, trigger));
        Drain();
    }
    static void Drain()
    {
        if (draining) return;
        draining = true;
        try
        {
            Cleanup();
            while (queue.Count > 0)
            {
                var work = queue.Dequeue();
                if (work.owner == null || !work.owner.isActiveAndEnabled || work.owner.Owner == null || work.owner.Owner.LifeGeneration != work.life) continue;
                if (work.target != null && (!work.target.isActiveAndEnabled || work.target.LifeGeneration != work.targetLife)) continue;
                var chain = Get(work.evt.ChainId);
                chain.Retention = Mathf.Max(chain.Retention, work.owner.ChainRetentionSeconds);
                chain.Owners.Add(work.owner.GetInstanceID());
                string fact = work.owner.GetInstanceID() + "/" + work.life + "/" + work.evt.FactId + "/" + work.trigger;
                if (work.evt.FactId != 0 && !chain.Facts.Add(fact)) { work.owner.Trace("chain", "duplicate fact"); continue; }
                if (chain.Steps >= MaxSteps) continue;
                hasCurrent = true; current = work.evt;
                try { work.owner.Dispatch(work.evt, work.trigger); }
                finally { hasCurrent = false; current = default; }
            }
        }
        finally { draining = false; queue.Clear(); }
    }
    public static InlineScope BeginInline(in PassiveEventContext evt) => new(evt);
    public readonly struct InlineScope : System.IDisposable
    {
        readonly bool ownsQueue, previousHasCurrent;
        readonly PassiveEventContext previous;
        public InlineScope(in PassiveEventContext evt)
        {
            ownsQueue = !draining; previousHasCurrent = hasCurrent; previous = current;
            draining = true; hasCurrent = true; current = evt;
        }
        public void Dispose()
        {
            hasCurrent = previousHasCurrent; current = previous;
            if (ownsQueue) { draining = false; Drain(); }
        }
    }
    public static bool TryStep(ulong chainId)
    {
        if (chainId == 0) return true;
        var chain = Get(chainId);
        if (++chain.Steps <= MaxSteps) return true;
        if (!chain.Warned) { chain.Warned = true; Debug.LogWarning("[Passive] Chain step budget exceeded: " + chainId); }
        return false;
    }
    public static bool ClaimRule(PassiveController owner, string rule, in PassiveEventContext evt, bool perTarget)
    {
        if (evt.ChainId == 0) return true;
        bool claimed = Get(evt.ChainId).Rules.Add(owner.GetInstanceID() + "/" + owner.Owner.LifeGeneration + "/" + rule + "/" + (perTarget ? evt.TargetInstanceId : 0));
        if (!claimed) owner.Trace(rule, "rule already executed in chain");
        return claimed;
    }
    public static void ReleaseOwner(PassiveController owner)
    {
        if (owner == null) return;
        int id = owner.GetInstanceID();
        string prefix = id + "/";
        expired.Clear();
        foreach (var pair in chains)
        {
            pair.Value.Owners.Remove(id);
            pair.Value.Facts.RemoveWhere(value => value.StartsWith(prefix, System.StringComparison.Ordinal));
            pair.Value.Rules.RemoveWhere(value => value.StartsWith(prefix, System.StringComparison.Ordinal));
            if (pair.Value.Owners.Count == 0) expired.Add(pair.Key);
        }
        foreach (ulong chain in expired) chains.Remove(chain);
    }
    static Chain Get(ulong id)
    {
        if (!chains.TryGetValue(id, out var chain)) chains.Add(id, chain = new Chain());
        chain.Seen = Time.realtimeSinceStartupAsDouble;
        return chain;
    }
    static void Cleanup()
    {
        expired.Clear();
        double now = Time.realtimeSinceStartupAsDouble;
        foreach (var pair in chains) if (now - pair.Value.Seen > pair.Value.Retention) expired.Add(pair.Key);
        foreach (ulong id in expired) chains.Remove(id);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() { queue.Clear(); chains.Clear(); expired.Clear(); draining = false; hasCurrent = false; current = default; }
    sealed class Chain
    {
        public int Steps;
        public double Seen;
        public float Retention = 10;
        public bool Warned;
        public readonly HashSet<int> Owners = new();
        public readonly HashSet<string> Facts = new();
        public readonly HashSet<string> Rules = new();
    }
}
