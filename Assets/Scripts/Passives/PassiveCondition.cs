using System;
using System.Collections.Generic;
using UnityEngine;

public enum PassiveConditionKind { Always, All, Any, Not, HealthBelow, StaminaBelow, HasStatus, HasUpgrade, EventSource, OriginPassive, LastSucceeded, EventType, LastFailed, LastSkipped, NumberAtLeast, NumberGreater, NumberAtMost, AmmoConsumed, LastRound, HeadHit, EnteredChainReady, TrackedTarget, Chance }

[Serializable]
public sealed class PassiveCondition
{
    public PassiveConditionKind kind;
    public bool eventTarget;
    public PassiveEventType eventType;
    public PassiveNumber threshold = new(0.7f);
    public string id;
    public PassiveNumber number = new();
    public StatusEffectDef status;
    public int minimumStacks = 1;
    public List<PassiveCondition> children = new();
    public bool UsesResource
    {
        get
        {
            if (kind == PassiveConditionKind.StaminaBelow || threshold.UsesResource || number.UsesResource) return true;
            foreach (var child in children) if (child != null && child.UsesResource) return true;
            return false;
        }
    }
    public bool IsStatic
    {
        get
        {
            if (kind == PassiveConditionKind.Always || kind == PassiveConditionKind.HasUpgrade) return true;
            if (kind == PassiveConditionKind.NumberAtLeast || kind == PassiveConditionKind.NumberGreater || kind == PassiveConditionKind.NumberAtMost)
                return number.IsStatic && threshold.IsStatic;
            if (kind != PassiveConditionKind.All && kind != PassiveConditionKind.Any && kind != PassiveConditionKind.Not) return false;
            foreach (var child in children) if (child == null || !child.IsStatic) return false;
            return true;
        }
    }

    static bool Below(float ratio, float threshold) => ratio < threshold && !Mathf.Approximately(ratio, threshold);

    // Only a fully static upgrade expression can suppress an event monitor at loadout time.
    // Dynamic HP/status/event conditions must remain subscribed until the actual fact arrives.
    public bool CanMatchWithUpgrades(PassiveExecution execution) => IsUpgradeExpression() ? Evaluate(execution) : true;
    bool IsUpgradeExpression()
    {
        if (kind == PassiveConditionKind.Always || kind == PassiveConditionKind.HasUpgrade) return true;
        if (kind != PassiveConditionKind.All && kind != PassiveConditionKind.Any && kind != PassiveConditionKind.Not) return false;
        foreach (var child in children) if (child == null || !child.IsUpgradeExpression()) return false;
        return true;
    }

    public bool Evaluate(PassiveExecution execution)
    {
        var actor = eventTarget ? execution.EventTarget : execution.Owner;
        switch (kind)
        {
            case PassiveConditionKind.Chance:
                float chance = Mathf.Clamp01(threshold.Resolve(execution));
                return chance >= 1 || chance > 0 && execution.Runtime.RandomValue() <= chance;
            case PassiveConditionKind.NumberAtLeast: return number.Resolve(execution, true) >= threshold.Resolve(execution, true);
            case PassiveConditionKind.NumberGreater: return number.Resolve(execution, true) > threshold.Resolve(execution, true);
            case PassiveConditionKind.NumberAtMost: return number.Resolve(execution, true) <= threshold.Resolve(execution, true);
            case PassiveConditionKind.AmmoConsumed: return execution.Event.Metadata.AmmoConsumed;
            case PassiveConditionKind.LastRound: return execution.Event.Metadata.IsLastRound;
            case PassiveConditionKind.HeadHit: return execution.Event.Metadata.HitZone == CharacterHitZone.Head;
            case PassiveConditionKind.EnteredChainReady: return execution.Event.Metadata.EnteredChainReady;
            case PassiveConditionKind.TrackedTarget: return execution.Runtime.MatchesTarget(execution.ScopedKey(id), execution.EventTarget);
            case PassiveConditionKind.All:
                foreach (var child in children) if (child == null || !child.Evaluate(execution)) return false;
                return true;
            case PassiveConditionKind.Any:
                foreach (var child in children) if (child != null && child.Evaluate(execution)) return true;
                return false;
            case PassiveConditionKind.Not: return children.Count == 1 && !children[0].Evaluate(execution);
            case PassiveConditionKind.HealthBelow:
                return actor != null && actor.HealthSystem != null && actor.HealthSystem.maximumHealth > 0 &&
                    Below(actor.HealthSystem.currentHealth / actor.HealthSystem.maximumHealth, threshold.Resolve(execution));
            case PassiveConditionKind.StaminaBelow:
                return actor != null && actor.StaminaSystem != null && actor.StaminaSystem.Max > 0 &&
                    Below(actor.StaminaSystem.Current / actor.StaminaSystem.Max, threshold.Resolve(execution));
            case PassiveConditionKind.HasStatus:
                if (actor == null || actor.StatusEffects == null || status == null) return false;
                foreach (var effect in actor.StatusEffects.ActiveEffects)
                    if (effect.Definition == status && effect.CurrentStacks >= minimumStacks) return true;
                return false;
            case PassiveConditionKind.HasUpgrade: return execution.Grant.Upgrades?.HasUpgrade(id) == true;
            case PassiveConditionKind.EventSource: return execution.Event.EventSourceId == id;
            case PassiveConditionKind.OriginPassive: return execution.Event.OriginPassiveId == id;
            case PassiveConditionKind.EventType: return execution.Event.Type == eventType;
            case PassiveConditionKind.LastSucceeded: return execution.Last.Status == PassiveOutcome.Succeeded;
            case PassiveConditionKind.LastFailed: return execution.Last.Status == PassiveOutcome.Failed;
            case PassiveConditionKind.LastSkipped: return execution.Last.Status == PassiveOutcome.Skipped;
            default: return true;
        }
    }
}
