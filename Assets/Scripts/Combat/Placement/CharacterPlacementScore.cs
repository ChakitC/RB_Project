using System;

public readonly struct CharacterPlacementScore : IComparable<CharacterPlacementScore>
{
    public CharacterPlacementScore(
        float maxWorldPenetration,
        float totalWorldPenetration,
        float maxActorPenetration,
        float totalActorPenetration,
        int collisionSampleCount,
        float preferredAngleError,
        float navMeshSnapDistance,
        int authoredCandidateOrder)
    {
        MaxWorldPenetration = maxWorldPenetration;
        TotalWorldPenetration = totalWorldPenetration;
        MaxActorPenetration = maxActorPenetration;
        TotalActorPenetration = totalActorPenetration;
        CollisionSampleCount = collisionSampleCount;
        PreferredAngleError = preferredAngleError;
        NavMeshSnapDistance = navMeshSnapDistance;
        AuthoredCandidateOrder = authoredCandidateOrder;
    }

    public float MaxWorldPenetration { get; }
    public float TotalWorldPenetration { get; }
    public float MaxActorPenetration { get; }
    public float TotalActorPenetration { get; }
    public int CollisionSampleCount { get; }
    public float PreferredAngleError { get; }
    public float NavMeshSnapDistance { get; }
    public int AuthoredCandidateOrder { get; }

    public int CompareTo(CharacterPlacementScore other)
    {
        int comparison = Compare(MaxWorldPenetration, other.MaxWorldPenetration);
        if (comparison != 0) return comparison;
        comparison = Compare(TotalWorldPenetration, other.TotalWorldPenetration);
        if (comparison != 0) return comparison;
        comparison = Compare(MaxActorPenetration, other.MaxActorPenetration);
        if (comparison != 0) return comparison;
        comparison = Compare(TotalActorPenetration, other.TotalActorPenetration);
        if (comparison != 0) return comparison;
        comparison = CollisionSampleCount.CompareTo(other.CollisionSampleCount);
        if (comparison != 0) return comparison;
        comparison = Compare(PreferredAngleError, other.PreferredAngleError);
        if (comparison != 0) return comparison;
        comparison = Compare(NavMeshSnapDistance, other.NavMeshSnapDistance);
        if (comparison != 0) return comparison;
        return AuthoredCandidateOrder.CompareTo(other.AuthoredCandidateOrder);
    }

    public bool IsBetterThan(CharacterPlacementScore other)
    {
        return CompareTo(other) < 0;
    }

    static int Compare(float a, float b)
    {
        if (float.IsNaN(a)) a = float.PositiveInfinity;
        if (float.IsNaN(b)) b = float.PositiveInfinity;
        return a.CompareTo(b);
    }
}
