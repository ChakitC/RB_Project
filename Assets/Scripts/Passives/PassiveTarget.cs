using System;
using System.Collections.Generic;
using UnityEngine;

public enum PassiveTargetMode { Self, EventTarget, AroundSelf, AroundEventTarget }
public enum PassiveTargetFilter { Any, SameIdentity, DifferentIdentity }

[Serializable]
public sealed class PassiveTarget
{
    public PassiveTargetMode mode;
    public PassiveTargetFilter filter;
    public float radius = 5f;

    public IEnumerable<CharacteContext> Resolve(PassiveExecution execution)
    {
        var center = mode == PassiveTargetMode.EventTarget || mode == PassiveTargetMode.AroundEventTarget
            ? execution.EventTarget : execution.Owner;
        if (center == null) yield break;
        if (mode == PassiveTargetMode.Self || mode == PassiveTargetMode.EventTarget)
        { yield return center; yield break; }
        // Snapshot candidates: applying an effect can disable an actor and change the registry.
        var candidates = new List<CharacteContext>(CharacterContextRegistry.ActiveContexts);
        foreach (var actor in candidates)
        {
            if (actor == null || !actor.isActiveAndEnabled || (actor.HealthSystem != null && !actor.HealthSystem.IsAlive)) continue;
            if ((actor.transform.position - center.transform.position).sqrMagnitude > radius * radius) continue;
            bool same = actor.TargetIdentity.Equals(execution.Owner.TargetIdentity);
            if (filter == PassiveTargetFilter.SameIdentity && !same || filter == PassiveTargetFilter.DifferentIdentity && same) continue;
            yield return actor;
        }
    }
}
