using System.Collections.Generic;
using UnityEngine;

public enum SkillHelperFacingMode
{
    KeepCurrentFacing = 0,
    FaceDetectedTargetOnCast = 1,
}

public readonly struct SkillTargetPlacementSpec
{
    public SkillTargetPlacementSpec(float targetStandOffDistanceAtCastPoint)
    {
        TargetStandOffDistanceAtCastPoint = Mathf.Max(0f, targetStandOffDistanceAtCastPoint);
    }

    public float TargetStandOffDistanceAtCastPoint { get; }
}

public abstract class SkillPayloadDef : ScriptableObject
{
    [Header("Execution Behavior")]
    [SerializeField] private SkillHelperFacingMode helperFacingMode = SkillHelperFacingMode.KeepCurrentFacing;
    [SerializeField] private ChainStepContinueMode chainContinueMode = ChainStepContinueMode.OnStepComplete;
    [SerializeField, Range(0f, 1f)] private float chainContinueNormalizedTime = 1f;

    public SkillHelperFacingMode HelperFacingMode => helperFacingMode;
    public virtual bool RequiresSkillTimelineEvents => false;
    public virtual bool HasExecutionPresentationAssets => false;

    public virtual bool TryGetTargetPlacement(out SkillTargetPlacementSpec placement)
    {
        placement = default;
        return false;
    }

    public virtual void CollectTimelineEventNames(List<CombatTimelineEventName> eventNames)
    {
    }

    public virtual void CollectUpgradeIds(List<string> ids)
    {
    }

    public virtual void CollectValidationIssues(List<string> issues)
    {
    }

    public ChainStepContinueMode GetChainContinueMode()
    {
        return chainContinueMode;
    }

    public float GetChainContinueNormalizedTime()
    {
        if (!float.IsFinite(chainContinueNormalizedTime))
            return 1f;

        return Mathf.Clamp(chainContinueNormalizedTime, 0f, 0.999f);
    }

    /// <summary>
    /// Runs the payload and reports whether it produced a gameplay effect.
    ///
    /// A cast is a transaction, and this result is what decides whether it commits: energy, charge,
    /// and cooldown are only spent for a payload that says it did something. Every payload must
    /// answer for itself - there is no "assume it worked" default, because a payload that silently
    /// returns early would otherwise charge the player for nothing.
    ///
    /// A valid cast that simply found no target still counts as a success and still costs: missing
    /// is a gameplay outcome, not a refusal.
    /// </summary>
    public abstract SkillExecutionResult ExecuteWithResult(SkillCastContext context);

    /// <summary>
    /// Fire-and-forget wrapper for callers that do not inspect the result. Not virtual: the result
    /// is the contract, so overriding this instead would let a payload skip reporting.
    /// </summary>
    public void Execute(SkillCastContext context)
    {
        ExecuteWithResult(context);
    }
}

public static class SkillUpgradeIdCollection
{
    public static void AddUnique(List<string> ids, string id)
    {
        if (ids == null || string.IsNullOrWhiteSpace(id))
            return;

        string trimmed = id.Trim();
        if (!ids.Contains(trimmed))
            ids.Add(trimmed);
    }
}
