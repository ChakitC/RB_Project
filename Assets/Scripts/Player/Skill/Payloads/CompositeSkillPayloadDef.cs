using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[HideMonoScript]
public sealed class CompositeSkillPayloadDef : SkillPayloadDef
{
    [SerializeReference, BoxGroup("Setup"), ListDrawerSettings(DefaultExpandedState = true)]
    private List<SkillEffectStep> steps = new();

    public IReadOnlyList<SkillEffectStep> Steps => steps;

    public void SetStepPayload(int index, SkillPayloadDef payload)
    {
        if (steps == null || index < 0 || index >= steps.Count)
            return;

        if (steps[index] is PayloadStep payloadStep)
            payloadStep.SetPayload(payload);
    }

    public override bool RequiresSkillTimelineEvents
    {
        get
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (GetStepPayload(steps[i])?.RequiresSkillTimelineEvents == true)
                    return true;
            }

            return false;
        }
    }

    public override bool HasExecutionPresentationAssets
    {
        get
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (GetStepPayload(steps[i])?.HasExecutionPresentationAssets == true)
                    return true;
            }

            return false;
        }
    }

    public override void CollectTimelineEventNames(List<CombatTimelineEventName> eventNames)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            GetStepPayload(steps[i])?.CollectTimelineEventNames(eventNames);
        }
    }

    public override void CollectUpgradeIds(List<string> ids)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            steps[i]?.CollectUpgradeIds(ids);
        }
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            return;

        bool sawProjectileOrHitboxPayload = false;
        bool sawAnimationOwningPayload = false;

        for (int i = 0; i < steps.Count; i++)
        {
            SkillEffectStep step = steps[i];
            if (step is not PayloadStep payloadStep)
                continue;

            SkillPayloadDef wrapped = payloadStep.Payload;
            if (wrapped == null)
            {
                issues.Add($"Composite step {i} is a PayloadStep with no payload assigned.");
                continue;
            }

            if (wrapped is CompositeSkillPayloadDef)
            {
                issues.Add($"Composite step {i} wraps another CompositeSkillPayloadDef, which is not supported.");
                continue;
            }

            if (wrapped is ProjectileSkillPayloadDef || wrapped is PrefabHitboxSkillPayloadDef)
            {
                if (sawProjectileOrHitboxPayload)
                    issues.Add("More than one wrapped payload competes for the same stat channel (projectile / hitbox).");
                sawProjectileOrHitboxPayload = true;
            }

            if (wrapped is MorphSkillPayloadDef)
            {
                if (sawAnimationOwningPayload)
                    issues.Add("More than one wrapped payload owns the character's animator (MorphSkillPayloadDef).");
                sawAnimationOwningPayload = true;
            }

            // GetChainContinueNormalizedTime() clamps its stored value to a 0.999f ceiling, so an
            // untouched default (backing field = 1f) reads back as 0.999f, not 1f.
            if (wrapped.HelperFacingMode != SkillHelperFacingMode.KeepCurrentFacing ||
                wrapped.GetChainContinueMode() != ChainStepContinueMode.OnStepComplete ||
                !Mathf.Approximately(wrapped.GetChainContinueNormalizedTime(), 0.999f))
            {
                issues.Add($"Composite step {i} wraps a payload with non-default helperFacingMode/chainContinue* values. Copy those values up to the composite root — the composite is the unique owner.");
            }

            wrapped.CollectValidationIssues(issues);
        }
    }

    public override void Execute(SkillCastContext context)
    {
        if (context == null)
            return;

        for (int i = 0; i < steps.Count; i++)
        {
            SkillEffectStep step = steps[i];
            if (step == null || !step.IsEnabled(context))
                continue;

            try
            {
                step.Execute(context);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    static SkillPayloadDef GetStepPayload(SkillEffectStep step)
    {
        return step is PayloadStep payloadStep ? payloadStep.Payload : null;
    }
}
