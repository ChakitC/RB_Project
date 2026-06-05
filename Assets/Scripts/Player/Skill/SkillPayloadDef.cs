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
}
