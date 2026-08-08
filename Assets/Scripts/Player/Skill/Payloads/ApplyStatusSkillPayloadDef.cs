using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[HideMonoScript]
public sealed class ApplyStatusSkillPayloadDef : SkillPayloadDef
{
    // Legacy-field fallback (lazy resolve, NOT ISerializationCallbackReceiver — see the "Legacy Migration"
    // block at the bottom of StatusEffects/StatusApplicationSpec.cs for why) — Aires_Skill_3.asset /
    // Rector_Skill_2.asset have real `effect`/`stacks` data serialized flat under this class.
    [Serializable]
    public sealed class StatusApplication
    {
        [SerializeField, HideInInspector] StatusEffectDef effect;
        [SerializeField, HideInInspector] int stacks;

        [LabelText("Status Effect")]
        public StatusApplicationSpec spec = new();

        /// <summary>Call from Execute()/CollectValidationIssues() only — never during deserialization.</summary>
        public StatusApplicationSpec ResolvedSpec()
        {
            if (spec != null && spec.effect != null)
                return spec;

            if (effect == null)
                return spec;

            return new StatusApplicationSpec
            {
                effect = effect,
                stacks = stacks > 0 ? stacks : 1,
                modifiers = spec?.modifiers,
                durationOverride = spec?.durationOverride ?? 0f,
                tickDamageOverride = spec?.tickDamageOverride ?? 0f,
            };
        }
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

    // See StatusApplication above — same legacy-field fallback reason (Aires_Skill_3.asset has real data).
    [Serializable]
    public sealed class ConditionalStatus
    {
        public string requiredUpgradeId;

        [SerializeField, HideInInspector] StatusEffectDef effect;
        [SerializeField, HideInInspector] int stacks;

        public StatusApplicationSpec spec = new();

        /// <summary>Call from Execute()/CollectValidationIssues() only — never during deserialization.</summary>
        public StatusApplicationSpec ResolvedSpec()
        {
            if (spec != null && spec.effect != null)
                return spec;

            if (effect == null)
                return spec;

            return new StatusApplicationSpec
            {
                effect = effect,
                stacks = stacks > 0 ? stacks : 1,
                modifiers = spec?.modifiers,
                durationOverride = spec?.durationOverride ?? 0f,
                tickDamageOverride = spec?.tickDamageOverride ?? 0f,
            };
        }
    }

    [PropertyOrder(-10)]
    [SerializeField, BoxGroup("Upgrades")]
    [LabelText("Conditional Status Effects")]
    [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
    private List<ConditionalStatus> conditionalApplications = new();

    public override void Execute(SkillCastContext context)
    {
        if (context == null)
        {
            return;
        }

        StatusEffectController controller = ResolveController(context);
        if (controller == null)
        {
            Debug.LogWarning(
                $"Skill '{context.SkillDef?.name ?? name}' could not find a {nameof(StatusEffectController)} on the caster.",
                this);
            return;
        }

        GameObject source = context.CasterObject != null
            ? context.CasterObject
            : controller.gameObject;

        float skillDurationOverride = 0f;
        FinalSkillStats stats = context.SkillStats;
        if (stats != null && stats.effectDuration > 0f)
            skillDurationOverride = stats.effectDuration;

        bool appliedAny = false;

        if (applications != null && applications.Count > 0)
        {
            for (int i = 0; i < applications.Count; i++)
            {
                StatusApplication application = applications[i];
                StatusApplicationSpec resolvedSpec = application?.ResolvedSpec();
                if (resolvedSpec?.effect == null)
                    continue;

                controller.ApplyEffect(resolvedSpec.ResolveWithDurationFallback(skillDurationOverride), source);
                appliedAny = true;
            }
        }

        if (conditionalApplications != null && conditionalApplications.Count > 0)
        {
            for (int i = 0; i < conditionalApplications.Count; i++)
            {
                ConditionalStatus conditional = conditionalApplications[i];
                if (conditional == null || !context.HasUpgrade(conditional.requiredUpgradeId))
                    continue;

                StatusApplicationSpec resolvedSpec = conditional.ResolvedSpec();
                if (resolvedSpec?.effect == null)
                    continue;

                controller.ApplyEffect(resolvedSpec.ResolveWithDurationFallback(skillDurationOverride), source);
                appliedAny = true;
            }
        }

        if (!appliedAny)
            Debug.LogError($"Skill payload '{name}' is missing its status effect configuration.", this);
    }

    public override void CollectUpgradeIds(List<string> ids)
    {
        if (conditionalApplications == null)
            return;

        for (int i = 0; i < conditionalApplications.Count; i++)
        {
            ConditionalStatus conditional = conditionalApplications[i];
            if (conditional != null)
                SkillUpgradeIdCollection.AddUnique(ids, conditional.requiredUpgradeId);
        }
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
                StatusApplicationSpec resolvedSpec = applications[i]?.ResolvedSpec();
                if (resolvedSpec?.effect != null)
                {
                    hasConfiguredApplication = true;
                    resolvedSpec.CollectValidationIssues(issues, $"applications[{i}]");
                }
            }
        }

        if (conditionalApplications != null)
        {
            for (int i = 0; i < conditionalApplications.Count; i++)
            {
                StatusApplicationSpec resolvedSpec = conditionalApplications[i]?.ResolvedSpec();
                if (resolvedSpec?.effect != null)
                {
                    hasConfiguredApplication = true;
                    resolvedSpec.CollectValidationIssues(issues, $"conditionalApplications[{i}]");
                }
            }
        }

        if (!hasConfiguredApplication)
            issues.Add("Apply Status payload has no status effect configured.");
    }

    StatusEffectController ResolveController(SkillCastContext context)
    {
        if (context == null)
            return null;

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
