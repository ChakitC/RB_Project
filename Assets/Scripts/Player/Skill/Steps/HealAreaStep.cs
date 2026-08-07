using System;
using System.Collections.Generic;
using UnityEngine;

public enum HealTargetMode
{
    Self,
    Allies,
}

[Serializable]
public sealed class HealAreaStep : SkillEffectStep
{
    [Serializable]
    public sealed class ConditionalStatus
    {
        public string requiredUpgradeId;
        public StatusEffectDef effect;
        [Min(1)] public int stacks = 1;
    }

    [SerializeField] private HealTargetMode target = HealTargetMode.Self;
    [SerializeField] private List<StatusEffectDef> statusApplications = new();
    [SerializeField] private List<ConditionalStatus> conditionalApplications = new();

    public override void CollectUpgradeIds(List<string> ids)
    {
        base.CollectUpgradeIds(ids);
        if (conditionalApplications == null)
            return;

        for (int i = 0; i < conditionalApplications.Count; i++)
        {
            ConditionalStatus conditional = conditionalApplications[i];
            if (conditional != null)
                SkillUpgradeIdCollection.AddUnique(ids, conditional.requiredUpgradeId);
        }
    }

    public override void Execute(SkillCastContext ctx)
    {
        if (ctx == null || ctx.CasterRoot == null)
            return;

        FinalSkillStats stats = ctx.SkillStats;
        float healPower = stats != null ? stats.healPower : 0f;
        float durationOverride = stats != null && stats.effectDuration > 0f ? stats.effectDuration : 0f;

        if (target == HealTargetMode.Self)
            ExecuteSelf(ctx, healPower, durationOverride);
        else
            ExecuteAllies(ctx, healPower, durationOverride);
    }

    void ExecuteSelf(SkillCastContext ctx, float healPower, float durationOverride)
    {
        CharacteContext casterContext = ctx.CasterRoot.GetComponent<CharacteContext>();
        if (casterContext == null)
            return;

        casterContext.ResolveReferences();

        if (healPower > 0f)
            casterContext.HealthSystem?.Heal(healPower);

        ApplyStatuses(ctx, casterContext.StatusEffects, ctx.CasterObject, durationOverride);
    }

    void ExecuteAllies(SkillCastContext ctx, float healPower, float durationOverride)
    {
        Transform casterRoot = ctx.CasterRoot;
        FinalSkillStats stats = ctx.SkillStats;
        float radius = stats != null ? stats.areaRadius : 0f;
        if (radius <= 0f)
            return;

        float radiusSqr = radius * radius;
        CharacteContext[] targetContexts = UnityEngine.Object.FindObjectsByType<CharacteContext>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < targetContexts.Length; i++)
        {
            CharacteContext targetContext = targetContexts[i];
            if (targetContext == null || targetContext.transform == casterRoot)
                continue;

            if (targetContext.TargetIdentity != AITargetIdentity.Player &&
                targetContext.TargetIdentity != AITargetIdentity.Companion)
                continue;

            if ((targetContext.transform.position - casterRoot.position).sqrMagnitude > radiusSqr)
                continue;

            targetContext.ResolveReferences();

            if (healPower > 0f)
                targetContext.HealthSystem?.Heal(healPower);

            ApplyStatuses(ctx, targetContext.StatusEffects, ctx.CasterObject, durationOverride);
        }
    }

    void ApplyStatuses(SkillCastContext ctx, StatusEffectController controller, GameObject source, float durationOverride)
    {
        if (controller == null)
            return;

        if (statusApplications != null)
        {
            for (int i = 0; i < statusApplications.Count; i++)
            {
                StatusEffectDef effect = statusApplications[i];
                if (effect == null)
                    continue;

                controller.ApplyEffect(effect, source, 1, durationOverride);
            }
        }

        if (conditionalApplications != null)
        {
            for (int i = 0; i < conditionalApplications.Count; i++)
            {
                ConditionalStatus conditional = conditionalApplications[i];
                if (conditional == null || conditional.effect == null || !ctx.HasUpgrade(conditional.requiredUpgradeId))
                    continue;

                controller.ApplyEffect(conditional.effect, source, Mathf.Max(1, conditional.stacks), durationOverride);
            }
        }
    }
}
