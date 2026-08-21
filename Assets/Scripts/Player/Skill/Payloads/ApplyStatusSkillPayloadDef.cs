using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[HideMonoScript]
public sealed class ApplyStatusSkillPayloadDef : SkillPayloadDef
{
    [Serializable]
    public sealed class StatusApplication
    {
        [LabelText("Status Effect")]
        public StatusApplicationSpec spec = new();
    }

    [PropertyOrder(-20)]
    [InfoBox("Applies one or more status effects directly to the caster through StatusEffectController.")]
    [SerializeField, BoxGroup("Setup")]
    [LabelText("Status Effects")]
    [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
    private List<StatusApplication> applications = new();

    [SerializeField, BoxGroup("Setup"), ToggleLeft]
    [LabelText("Prefer Caster Root")]
    private bool preferCasterRoot = true;

    [PropertyOrder(-10)]
    [SerializeField, BoxGroup("Upgrades")]
    [LabelText("Conditional Status Effects")]
    [SkillStatusRouteTarget(SkillStatusTarget.Self, "Apply Status")]
    private ConditionalStatusRoute conditionalStatuses = new();

    public IReadOnlyList<StatusApplication> Applications => applications;
    public bool PreferCasterRoot => preferCasterRoot;
    public ConditionalStatusRoute ConditionalStatuses => conditionalStatuses;

    public override SkillExecutionResult ExecuteWithResult(SkillCastContext context)
    {
        if (context == null)
        {
            return SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingRuntimeContext,
                "Apply Status payload executed without a cast context.");
        }

        StatusEffectController controller = ResolveController(context);
        if (controller == null)
        {
            Debug.LogWarning(
                $"Skill '{context.SkillDef?.name ?? name}' could not find a {nameof(StatusEffectController)} on the caster.",
                this);
            return SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingRuntimeContext,
                "Caster has no StatusEffectController.");
        }

        GameObject source = context.CasterObject != null
            ? context.CasterObject
            : controller.gameObject;

        float skillFallbackDuration = 0f;
        FinalSkillStats stats = context.SkillStats;
        if (stats != null && stats.effectDuration > 0f)
            skillFallbackDuration = stats.effectDuration;

        bool appliedAny = false;

        if (applications != null && applications.Count > 0)
        {
            for (int i = 0; i < applications.Count; i++)
            {
                StatusApplication application = applications[i];
                StatusApplicationSpec resolvedSpec = application?.spec;
                if (resolvedSpec?.effect == null)
                    continue;

                controller.ApplyEffect(resolvedSpec, source, skillFallbackDuration);
                appliedAny = true;
            }
        }

        if (conditionalStatuses != null &&
            conditionalStatuses.ApplyUnlocked(context, controller, source, skillFallbackDuration))
        {
            appliedAny = true;
        }

        if (appliedAny)
            return SkillExecutionResult.Succeeded;

        Debug.LogError($"Skill payload '{name}' is missing its status effect configuration.", this);
        return SkillExecutionResult.Failed(
            SkillExecutionFailureReason.MissingAuthoringData,
            "Apply Status payload has no status effect it could apply.");
    }

    public override void CollectUpgradeIds(List<string> ids)
    {
        conditionalStatuses?.CollectUpgradeIds(ids);
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            return;

        bool hasConfiguredApplication = false;
        if (applications != null)
        {
            for (int i = 0; i < applications.Count; i++)
            {
                StatusApplicationSpec resolvedSpec = applications[i]?.spec;
                if (resolvedSpec?.effect != null)
                {
                    hasConfiguredApplication = true;
                    resolvedSpec.CollectValidationIssues(issues, $"applications[{i}]");
                }
            }
        }

        if (conditionalStatuses != null && conditionalStatuses.HasConfiguredApplication)
        {
            hasConfiguredApplication = true;
            conditionalStatuses.CollectValidationIssues(issues, "conditionalStatuses");
        }

        if (!hasConfiguredApplication)
            issues.Add("Apply Status payload has no status effect configured.");
    }

    StatusEffectController ResolveController(SkillCastContext context)
    {
        if (context == null)
            return null;

        // The cast already resolved the caster's hub once; reuse it rather than repeating a
        // self/parent/child sweep that only matches some prefab layouts.
        if (context.CasterStatusEffects != null)
            return context.CasterStatusEffects;

        if (preferCasterRoot && context.CasterRoot != null)
        {
            StatusEffectController rootController = FindController(context.CasterRoot.gameObject);
            if (rootController != null)
                return rootController;
        }

        if (context.CasterObject != null)
        {
            StatusEffectController casterController = FindController(context.CasterObject);
            if (casterController != null)
                return casterController;
        }

        if (context.CasterRoot != null)
            return FindController(context.CasterRoot.gameObject);

        return null;
    }

    static StatusEffectController FindController(GameObject target)
    {
        if (target == null)
            return null;

        StatusEffectController controller = target.GetComponent<StatusEffectController>();
        if (controller != null)
            return controller;

        controller = target.GetComponentInParent<StatusEffectController>();
        if (controller != null)
            return controller;

        return target.GetComponentInChildren<StatusEffectController>();
    }
}
