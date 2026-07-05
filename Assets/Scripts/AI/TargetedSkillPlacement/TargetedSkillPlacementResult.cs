using UnityEngine;

public enum TargetedSkillPlacementMode
{
    None,
    LegacyTeleport,
    RootMotionTrajectory,
}

public readonly struct TargetedSkillPlacementResult
{
    public readonly TargetedSkillPlacementMode Mode;
    public readonly Vector3 StartPosition;
    public readonly Quaternion StartRotation;
    public readonly Vector3 ImpactPosition;
    public readonly Quaternion ImpactRotation;
    public readonly float AcceptedYaw;
    public readonly bool RequiresPositionSnap;
    public readonly string FailureReason;

    public bool IsValid => Mode != TargetedSkillPlacementMode.None;
    public bool UsesRootMotion => Mode == TargetedSkillPlacementMode.RootMotionTrajectory;

    TargetedSkillPlacementResult(
        TargetedSkillPlacementMode mode,
        Vector3 startPosition,
        Quaternion startRotation,
        Vector3 impactPosition,
        Quaternion impactRotation,
        float acceptedYaw,
        bool requiresPositionSnap,
        string failureReason)
    {
        Mode = mode;
        StartPosition = startPosition;
        StartRotation = startRotation;
        ImpactPosition = impactPosition;
        ImpactRotation = impactRotation;
        AcceptedYaw = acceptedYaw;
        RequiresPositionSnap = requiresPositionSnap;
        FailureReason = failureReason;
    }

    public static TargetedSkillPlacementResult Legacy(
        Vector3 position,
        Quaternion rotation,
        bool requiresPositionSnap = true)
    {
        return new TargetedSkillPlacementResult(
            TargetedSkillPlacementMode.LegacyTeleport,
            position,
            rotation,
            position,
            rotation,
            0f,
            requiresPositionSnap,
            null);
    }

    public static TargetedSkillPlacementResult RootMotion(
        Vector3 startPosition,
        Quaternion startRotation,
        Vector3 impactPosition,
        Quaternion impactRotation,
        float acceptedYaw,
        bool requiresPositionSnap = true)
    {
        return new TargetedSkillPlacementResult(
            TargetedSkillPlacementMode.RootMotionTrajectory,
            startPosition,
            startRotation,
            impactPosition,
            impactRotation,
            acceptedYaw,
            requiresPositionSnap,
            null);
    }

    public static TargetedSkillPlacementResult Failed(string failureReason)
    {
        return new TargetedSkillPlacementResult(
            TargetedSkillPlacementMode.None,
            Vector3.zero,
            Quaternion.identity,
            Vector3.zero,
            Quaternion.identity,
            0f,
            true,
            string.IsNullOrWhiteSpace(failureReason)
                ? "Targeted skill placement failed."
                : failureReason);
    }
}
