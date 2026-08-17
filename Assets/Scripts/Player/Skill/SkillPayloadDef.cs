using System.Collections.Generic;
using UnityEngine;

public enum SkillHelperFacingMode
{
    KeepCurrentFacing = 0,
    FaceDetectedTargetOnCast = 1,
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

    public abstract void Execute(SkillCastContext context);

    /// <summary>
    /// Runs the payload and reports whether it produced a gameplay effect.
    /// Only a successful execution commits the cast transaction (energy, charge, cooldown).
    /// Payloads that cannot fail keep overriding <see cref="Execute"/> alone and stay successful.
    /// </summary>
    public virtual SkillExecutionResult ExecuteWithResult(SkillCastContext context)
    {
        Execute(context);
        return SkillExecutionResult.Succeeded;
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
