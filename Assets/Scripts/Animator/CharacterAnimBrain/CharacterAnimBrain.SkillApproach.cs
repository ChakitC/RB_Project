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

    internal bool CanBeginSkillImpactApproach(int requestId, float impactNormalized) =>
        TryGetActiveSkillNormalizedTime(requestId, out _) && skill != null && skill.CanBeginImpactApproach(impactNormalized);

    internal bool TryBeginSkillImpactApproach(int requestId, float impactNormalized)
    {
        if (!TryGetActiveSkillNormalizedTime(requestId, out _) || skill == null ||
            !skill.TryBeginImpactApproach(impactNormalized)) return false;
        skillApproachRootMotion = _rootMotion;
        ClearRootMotionPolicy();
        return true;
    }

    internal bool TryAdvanceSkillImpactApproach(int requestId, float progress, out bool ready)
    {
        ready = false;
        return TryGetActiveSkillNormalizedTime(requestId, out _) && skill != null &&
            skill.TryAdvanceImpactApproach(progress, out ready);
    }

    internal bool TryHoldSkillApproach(int requestId) =>
        TryGetActiveSkillNormalizedTime(requestId, out _) && skill != null && skill.TryHoldApproach();

    internal bool TryBeginEnemyCooperativeStruggle(int requestId, AnimationClip clip, float fadeSeconds) =>
        TryGetActiveSkillNormalizedTime(requestId, out _) && skill != null && skill.TryBeginEnemyStruggle(clip, fadeSeconds);
}
