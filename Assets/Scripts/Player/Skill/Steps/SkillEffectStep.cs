using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public abstract class SkillEffectStep
{
    [SerializeField] private string requiredUpgradeId;

    public bool IsEnabled(SkillCastContext ctx) =>
        string.IsNullOrWhiteSpace(requiredUpgradeId) || ctx.HasUpgrade(requiredUpgradeId);

    public virtual void CollectUpgradeIds(List<string> ids)
    {
        SkillUpgradeIdCollection.AddUnique(ids, requiredUpgradeId);
    }

    public abstract void Execute(SkillCastContext ctx);
}
