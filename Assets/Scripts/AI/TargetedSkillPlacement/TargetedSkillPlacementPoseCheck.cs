using UnityEngine;

// Diagnostic only. It observes the placement beat without releasing payloads or moving actors.
internal sealed class TargetedSkillPlacementPoseCheck
{
    readonly CharacterAnimBrain brain;
    readonly Transform actor;
    readonly int requestId;
    readonly float placementTime;
    readonly TargetedSkillPlacementResult placement;
    readonly TargetedSkillRootMotionTrajectory trajectory;
    bool completed;

    TargetedSkillPlacementPoseCheck(CharacterAnimBrain brain, Transform actor, int requestId,
        float placementTime, TargetedSkillPlacementResult placement, TargetedSkillRootMotionTrajectory trajectory)
    {
        this.brain = brain;
        this.actor = actor;
        this.requestId = requestId;
        this.placementTime = placementTime;
        this.placement = placement;
        this.trajectory = trajectory;
    }

    public static TargetedSkillPlacementPoseCheck Create(CharacterAnimBrain brain, Transform actor,
        int requestId, SkillGemDefinition skill, float placementTime, TargetedSkillPlacementResult placement,
        bool preserveVerticalMotion = false)
    {
        if (!placement.UsesRootMotion || brain == null || actor == null || skill == null)
            return null;
        if (!brain.TryResolveSkillAnimationClip(skill, out AnimationClip clip) ||
            !brain.TryGetRootMotionSamplingAnimator(out Animator animator))
            return null;
        TargetedSkillRootMotionTrajectory trajectory;
        bool sampled = preserveVerticalMotion
            ? TargetedSkillRootMotionTrajectoryCache.TryGetWithVerticalMotion(clip, animator, out trajectory, out _)
            : TargetedSkillRootMotionTrajectoryCache.TryGet(clip, animator, out trajectory, out _);
        if (!sampled) return null;

        return new TargetedSkillPlacementPoseCheck(brain, actor, requestId, placementTime, placement, trajectory);
    }

    // Called after animation/root motion. Compare at the observed sample time so crossing the
    // placement beat between frames does not falsely report the subsequent movement as drift.
    public void Tick()
    {
        if (completed)
            return;
        if (brain == null || actor == null || !brain.TryGetActiveSkillNormalizedTime(requestId, out float time))
        {
            completed = true;
            return;
        }
        if (time < placementTime)
            return;

        completed = true;
        if (!TryGetExpectedPose(trajectory, placement, placementTime, time, out Vector3 position, out Quaternion rotation))
            return;
        Vector3 delta = actor.position - position;
        if (!trajectory.PlacementInput.PreserveVerticalMotion) delta.y = 0f;
        float yawError = Mathf.Abs(Mathf.DeltaAngle(actor.eulerAngles.y, rotation.eulerAngles.y));
        if (delta.magnitude > 0.05f || yawError > 2f)
            Debug.LogWarning($"[TargetedSkillPlacement] Placement pose drift for '{actor.name}' request {requestId} " +
                $"at {time:0.###} (placement={placementTime:0.###}, positionError={delta.magnitude:0.####}m, yawError={yawError:0.###}deg).", actor);
    }

    internal static bool TryGetExpectedPose(TargetedSkillRootMotionTrajectory trajectory,
        TargetedSkillPlacementResult placement, float placementTime, float observedTime,
        out Vector3 position, out Quaternion rotation)
    {
        position = placement.ImpactPosition;
        rotation = placement.ImpactRotation;
        if (trajectory == null || !trajectory.TrySample(placementTime, out var impact) ||
            !trajectory.TrySample(observedTime, out var observed))
            return false;

        // Derive the attack origin from A, including any Utility lead-in already composed into A.
        Quaternion attackRotation = placement.ImpactRotation * Quaternion.Inverse(Quaternion.Euler(0f, impact.localYaw, 0f));
        position += attackRotation * (observed.localPosition - impact.localPosition);
        rotation = attackRotation * Quaternion.Euler(0f, observed.localYaw, 0f);
        return true;
    }
}
