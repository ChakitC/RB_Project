using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public enum HealTargetMode
{
    Self,
    Allies,
}

// Payload replacement for the legacy HealAreaStep (see Docs/ARCHITECTURE/
// NODE_CENTRIC_PAYLOAD_AUTHORING_PLAN.md section 10). Runtime behavior is an exact copy --
// self-heal, ally discovery through CharacteContext, and status application are unchanged.
[HideMonoScript]
public sealed class HealAreaSkillPayloadDef : SkillPayloadDef
{
    [Serializable]
    public sealed class StatusApplication
    {
        public StatusApplicationSpec spec = new();
    }

    [PropertyOrder(-20)]
    [InfoBox("Heals the caster or nearby allies and applies configured status effects.")]
    [SerializeField, BoxGroup("Setup")]
    [LabelText("Target")]
    private HealTargetMode target = HealTargetMode.Self;

    [SerializeField, BoxGroup("Setup")]
    [LabelText("Status Effects")]
    [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
    private List<StatusApplication> statusSpecApplications = new();

    [SerializeField, BoxGroup("Upgrades")]
    [LabelText("Conditional Status Effects")]
    [SkillStatusRouteTarget(nameof(ResolvedConditionalStatusTarget), "Heal Area")]
    private ConditionalStatusRoute conditionalStatuses = new();

    public HealTargetMode Target => target;
    public IReadOnlyList<StatusApplication> StatusSpecApplications => statusSpecApplications;
    public ConditionalStatusRoute ConditionalStatuses => conditionalStatuses;

    /// <summary>Target ของ route ตามโหมด heal ที่ payload นี้ถูก author ไว้.</summary>
    private SkillStatusTarget ResolvedConditionalStatusTarget =>
        target == HealTargetMode.Allies
            ? SkillStatusTarget.Allies
            : SkillStatusTarget.Self;

    public override void CollectUpgradeIds(List<string> ids)
    {
        conditionalStatuses?.CollectUpgradeIds(ids);
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            return;

        if (statusSpecApplications != null)
        {
            for (int i = 0; i < statusSpecApplications.Count; i++)
            {
                StatusApplicationSpec spec = statusSpecApplications[i]?.spec;
                if (spec?.effect != null)
                    spec.CollectValidationIssues(issues, $"statusSpecApplications[{i}]");
            }
        }

        conditionalStatuses?.CollectValidationIssues(issues, "conditionalStatuses");
    }

    public override void Execute(SkillCastContext context)
    {
        if (context == null || context.CasterRoot == null)
            return;

        FinalSkillStats stats = context.SkillStats;
        float healPower = stats != null ? stats.healPower : 0f;
        float fallbackDuration = stats != null && stats.effectDuration > 0f ? stats.effectDuration : 0f;

        if (target == HealTargetMode.Self)
            ExecuteSelf(context, healPower, fallbackDuration);
        else
            ExecuteAllies(context, healPower, fallbackDuration);
    }

    void ExecuteSelf(SkillCastContext context, float healPower, float fallbackDuration)
    {
        CharacteContext casterContext = context.CasterRoot.GetComponent<CharacteContext>();
        if (casterContext == null)
            return;

        casterContext.ResolveReferences();

        if (healPower > 0f)
            casterContext.HealthSystem?.Heal(healPower);

        ApplyStatuses(context, casterContext.StatusEffects, context.CasterObject, fallbackDuration);
    }

    void ExecuteAllies(SkillCastContext context, float healPower, float fallbackDuration)
    {
        Transform casterRoot = context.CasterRoot;
        FinalSkillStats stats = context.SkillStats;
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

            ApplyStatuses(context, targetContext.StatusEffects, context.CasterObject, fallbackDuration);
        }
    }

    void ApplyStatuses(SkillCastContext context, StatusEffectController controller, GameObject source, float fallbackDuration)
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

        conditionalStatuses?.ApplyUnlocked(context, controller, source, fallbackDuration);
    }
}
