using UnityEngine;

/// <summary>One committed targeting result. Events observe this complete value.</summary>
public readonly struct AITargetSnapshot
{
    public readonly AITargetHandle Target;
    public readonly AITargetHandle Visible;
    public readonly AITargetHandle LastSeen;
    public readonly Vector3 AimPosition;
    public readonly Vector3 LastSeenPosition;
    public readonly float LastSeenTime;
    public readonly float TargetDistance;
    public readonly float VisibleTargetDistance;
    public readonly bool HasLineOfSight;
    public readonly bool IsCurrentVisible;
    public readonly AITargetSelectionReason Reason;

    public Transform CurrentTarget => Target.LegacyTransform;
    public CharacteContext CurrentTargetContext => Target.IsLive ? Target.Context : null;
    public Transform VisibleTarget => Visible.LegacyTransform;
    public Transform LastSeenTarget => LastSeen.LegacyTransform;
    public bool HasLiveTarget => CurrentTarget != null;
    public bool HasAnyTarget => HasLiveTarget;

    public AITargetSnapshot(AITargetHandle target, AITargetHandle visible, AITargetHandle lastSeen,
        Vector3 aim, Vector3 lastPosition, float lastTime, float distance, float visibleDistance,
        bool lineOfSight, bool currentVisible, AITargetSelectionReason reason)
    {
        Target = target;
        Visible = visible;
        LastSeen = lastSeen;
        AimPosition = aim;
        LastSeenPosition = lastPosition;
        LastSeenTime = lastTime;
        TargetDistance = distance;
        VisibleTargetDistance = visibleDistance;
        HasLineOfSight = lineOfSight;
        IsCurrentVisible = currentVisible;
        Reason = reason;
    }

    public static AITargetSnapshot Empty => new AITargetSnapshot(default, default, default,
        Vector3.zero, Vector3.zero, float.NegativeInfinity, 0f, 0f, false, false, AITargetSelectionReason.None);
}
