using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    RootMotionPolicy skillApproachRootMotion;
    // The original skill/timeline keeps running, so release and costs still settle normally.
    // Only its displacement is handed to the approach motor. The owner must end this request.
    internal bool TryBeginSkillApproach(int requestId, float endNormalized, float duration)
    {
        if (duration <= 0f || !TryGetActiveSkillNormalizedTime(requestId, out _) || skill == null ||
            !skill.TryBeginApproach(endNormalized, duration)) return false;
        skillApproachRootMotion = _rootMotion;
        ClearRootMotionPolicy();
        return true;
    }

    internal bool TryEndSkillApproach(int requestId)
    {
        if (!TryGetActiveSkillNormalizedTime(requestId, out _) || skill == null || !skill.TryEndApproach()) return false;
        PublishRootMotionPolicy(skillApproachRootMotion);
        return true;
    }
}
