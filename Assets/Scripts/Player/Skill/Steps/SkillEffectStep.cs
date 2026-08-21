using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public abstract class SkillEffectStep
{
    [SerializeField] private string requiredUpgradeId;

    // Authoring-only mutator. Normal designer flows must go through NodeAbilityAuthoringService,
    // which keeps this in sync with the granting node's grantedUpgradeIds as one transaction.
    public string RequiredUpgradeId
    {
        get => requiredUpgradeId;
        set => requiredUpgradeId = value;
    }

    public bool IsEnabled(SkillCastContext ctx) =>
        string.IsNullOrWhiteSpace(requiredUpgradeId) || ctx.HasUpgrade(requiredUpgradeId);

    public virtual void CollectUpgradeIds(List<string> ids)
    {
        SkillUpgradeIdCollection.AddUnique(ids, requiredUpgradeId);
    }

    /// <summary>
    /// Runs the step and reports whether it produced a gameplay effect. See
    /// <see cref="SkillPayloadDef.ExecuteWithResult"/>.
    /// </summary>
    public abstract SkillExecutionResult ExecuteWithResult(SkillCastContext ctx);

    /// <summary>Fire-and-forget wrapper. See <see cref="SkillPayloadDef.Execute"/>.</summary>
    public void Execute(SkillCastContext ctx)
    {
        ExecuteWithResult(ctx);
    }
}
