using System;
using System.Collections.Generic;

[Serializable]
public sealed class PassiveStatusStep : PassiveStep
{
    public PassiveTarget target = new();
    public StatusApplicationSpec spec = new();
    public string statusParameter;
    public string appliedById;
    public override PassiveResult Execute(PassiveExecution execution)
    {
        // A per-use copy protects the shared asset when a fragment overrides the status.
        var resolved = spec.Clone();
        resolved.effect = string.IsNullOrEmpty(statusParameter) ? spec.effect : execution.StatusParameter(statusParameter);
        if (resolved.effect == null) return new(PassiveOutcome.Failed);
        List<CharacteContext> succeeded = null;
        foreach (var actor in target.Resolve(execution))
        {
            if (actor.StatusEffects == null) continue;
            var instance = actor.StatusEffects.ApplyEffect(resolved, execution.Owner.gameObject,
                string.IsNullOrWhiteSpace(appliedById) ? execution.Key : appliedById,
                execution.Event.ChainId, execution.Event.Depth + 1, PassiveEventOrigin.Passive,
                execution.Runtime.Grant.Definition.RuntimeId, execution.Path,
                followUpProvenance: execution.Event.FollowUpProvenance);
            if (instance != null) (succeeded ??= new()).Add(actor);
        }
        return new PassiveResult(0, succeeded);
    }
}
