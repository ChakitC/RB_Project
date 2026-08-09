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

        public StatusApplicationSpec spec = new();
    }

    /// <summary>Wrapper ของ unconditional status — โครงเดียวกับ ConditionalStatus แต่ไม่มี upgrade gate.</summary>
    [Serializable]
    public sealed class StatusApplication
    {
        public StatusApplicationSpec spec = new();
    }

    [SerializeField] private HealTargetMode target = HealTargetMode.Self;
    [SerializeField] private List<StatusApplication> statusSpecApplications = new();
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
        float fallbackDuration = stats != null && stats.effectDuration > 0f ? stats.effectDuration : 0f;

        if (target == HealTargetMode.Self)
            ExecuteSelf(ctx, healPower, fallbackDuration);
        else
            ExecuteAllies(ctx, healPower, fallbackDuration);
    }

    void ExecuteSelf(SkillCastContext ctx, float healPower, float fallbackDuration)
    {
        CharacteContext casterContext = ctx.CasterRoot.GetComponent<CharacteContext>();
        if (casterContext == null)
            return;

        casterContext.ResolveReferences();

        if (healPower > 0f)
            casterContext.HealthSystem?.Heal(healPower);

        ApplyStatuses(ctx, casterContext.StatusEffects, ctx.CasterObject, fallbackDuration);
    }

    void ExecuteAllies(SkillCastContext ctx, float healPower, float fallbackDuration)
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

            ApplyStatuses(ctx, targetContext.StatusEffects, ctx.CasterObject, fallbackDuration);
        }
    }

    void ApplyStatuses(SkillCastContext ctx, StatusEffectController controller, GameObject source, float fallbackDuration)
    {
        if (controller == null)
            return;

        // ทั้ง unconditional และ conditional ใช้ duration precedence เดียวกัน:
        // spec override > skill effectDuration > StatusEffectDef.duration
        if (statusSpecApplications != null)
        {
            for (int i = 0; i < statusSpecApplications.Count; i++)
            {
                StatusApplicationSpec spec = statusSpecApplications[i]?.spec;
                if (spec?.effect == null)
                    continue;

                controller.ApplyEffect(spec, source, fallbackDuration);
            }
        }

        if (conditionalApplications != null)
        {
            for (int i = 0; i < conditionalApplications.Count; i++)
            {
                ConditionalStatus conditional = conditionalApplications[i];
                if (conditional == null || !ctx.HasUpgrade(conditional.requiredUpgradeId))
                    continue;

                StatusApplicationSpec resolvedSpec = conditional.spec;
                if (resolvedSpec?.effect == null)
                    continue;

                controller.ApplyEffect(resolvedSpec, source, fallbackDuration);
            }
        }
    }
}
