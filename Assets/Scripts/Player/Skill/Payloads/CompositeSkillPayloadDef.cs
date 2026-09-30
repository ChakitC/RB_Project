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

    [SerializeField, BoxGroup("Placement")]
    private bool overrideTargetPlacement;
    [SerializeField, BoxGroup("Placement"), ShowIf(nameof(overrideTargetPlacement)), MinValue(0f)]
    private float targetStandOffDistanceAtCastPoint = 1f;

    // Authoring-only mutators. Normal designer flows must go through NodeAbilityAuthoringService,
    // which wraps these in an Undo group alongside the embedded child payload and binding id.
    public void AddStep(SkillEffectStep step)
    {
        if (step == null)
            return;

        steps.Add(step);
    }

    public bool RemoveStep(SkillEffectStep step)
    {
        return step != null && steps.Remove(step);
    }

    public int IndexOfStep(SkillEffectStep step)
    {
        return step == null ? -1 : steps.IndexOf(step);
    }

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

    public override bool TryGetTargetPlacement(out SkillTargetPlacementSpec placement)
    {
        placement = default;
        if (overrideTargetPlacement)
        {
            if (!float.IsFinite(targetStandOffDistanceAtCastPoint) || targetStandOffDistanceAtCastPoint <= 0f)
                return false;
            placement = new SkillTargetPlacementSpec(targetStandOffDistanceAtCastPoint);
            return true;
        }

        bool found = false;
        for (int i = 0; i < steps.Count; i++)
        {
            SkillPayloadDef payload = GetStepPayload(steps[i]);
            if (payload == null || !payload.TryGetTargetPlacement(out var candidate))
                continue;
            // Legacy inference is only valid for one unconditional provider. Placement is
            // decided before execution and must not depend on a locked upgrade or list order.
            if (found || !string.IsNullOrWhiteSpace(steps[i].RequiredUpgradeId))
            {
                placement = default;
                return false;
            }
            placement = candidate;
            found = true;
        }
        return found;
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

    public override bool RequiresTimelineEventAfterCastPoint(CombatTimelineEventName eventName)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            SkillPayloadDef child = GetStepPayload(steps[i]);
            // Nested composites are invalid authoring data; do not follow a possible cycle.
            if (child != null && child is not CompositeSkillPayloadDef &&
                child.RequiresTimelineEventAfterCastPoint(eventName))
                return true;
        }

        return false;
    }

    public override void CollectValidationIssues(List<string> issues)
        => CollectCompositeValidationIssues(issues, includeChildren: true);

    internal void CollectStructureValidationIssues(List<string> issues)
        => CollectCompositeValidationIssues(issues, includeChildren: false);

    private void CollectCompositeValidationIssues(List<string> issues, bool includeChildren)
    {
        if (issues == null)
            return;

        bool sawProjectileOrHitboxPayload = false;
        bool sawAnimationOwningPayload = false;
        int placementProviders = 0;
        var ownedPayloads = new HashSet<SkillPayloadDef>();

        if (overrideTargetPlacement &&
            (!float.IsFinite(targetStandOffDistanceAtCastPoint) || targetStandOffDistanceAtCastPoint <= 0f))
            issues.Add("Composite root target placement distance must be finite and greater than 0.");

        for (int i = 0; i < steps.Count; i++)
        {
            SkillEffectStep step = steps[i];
            if (step == null)
            {
                issues.Add($"Composite step {i} has no step data assigned.");
                continue;
            }

            if (step is not PayloadStep payloadStep)
                continue;

            SkillPayloadDef wrapped = payloadStep.Payload;
            if (wrapped == null)
            {
                issues.Add($"Composite step {i} is a PayloadStep with no payload assigned.");
                continue;
            }

            if (!ownedPayloads.Add(wrapped))
            {
                issues.Add($"Composite step {i} reuses a payload already owned by another PayloadStep. Each PayloadStep must own a unique embedded payload.");
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

            if (!overrideTargetPlacement && wrapped.TryGetTargetPlacement(out _))
            {
                placementProviders++;
                if (!string.IsNullOrWhiteSpace(step.RequiredUpgradeId))
                    issues.Add($"Composite step {i} gates target placement behind an upgrade. Configure Target Placement on the composite root.");
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

            if (includeChildren) wrapped.CollectValidationIssues(issues);
        }
        if (placementProviders > 1)
            issues.Add("Multiple composite payloads provide target placement. Configure Target Placement on the composite root; child order cannot select placement.");
    }

    /// <summary>
    /// Sibling steps are independent: a failing step never stops the ones after it, and the
    /// composite counts as successful as soon as one enabled step produced a gameplay effect.
    /// </summary>
    public override SkillExecutionResult ExecuteWithResult(SkillCastContext context)
    {
        if (context == null)
        {
            return SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingRuntimeContext,
                "Composite payload executed without a cast context.");
        }

        bool anySucceeded = false;
        bool anyEnabled = false;
        bool hasFailure = false;
        SkillExecutionResult firstFailure = default;

        for (int i = 0; i < steps.Count; i++)
        {
            SkillEffectStep step = steps[i];
            if (step == null || !step.IsEnabled(context))
                continue;

            anyEnabled = true;

            SkillExecutionResult stepResult;
            try
            {
                stepResult = step.ExecuteWithResult(context);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                stepResult = SkillExecutionResult.Failed(
                    SkillExecutionFailureReason.Rejected,
                    $"Composite step {i} threw: {e.Message}");
            }

            if (stepResult.Success)
            {
                anySucceeded = true;
                continue;
            }

            if (!hasFailure)
            {
                hasFailure = true;
                firstFailure = stepResult;
            }
        }

        if (anySucceeded)
            return SkillExecutionResult.Succeeded;

        if (hasFailure)
            return firstFailure;

        return SkillExecutionResult.Failed(
            SkillExecutionFailureReason.NoEffect,
            anyEnabled
                ? "Composite payload has no gameplay step that reported a result."
                : "Composite payload has no enabled step for this upgrade selection.");
    }

    static SkillPayloadDef GetStepPayload(SkillEffectStep step)
    {
        // Validation reports nested composites. Aggregation must not follow a malformed cycle.
        return step is PayloadStep payloadStep && payloadStep.Payload is not CompositeSkillPayloadDef
            ? payloadStep.Payload : null;
    }
}
