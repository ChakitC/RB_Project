using UnityEngine;

public enum ImpactReactionKind
{
    None = 0,
    Root = 1,
    MiniStun = 2,
    Stun = 3,
}

public readonly struct KnockbackData
{
    public KnockbackData(
        Vector3 direction,
        float distance,
        float duration,
        Vector3 hitPoint,
        ImpactReactionKind reaction = ImpactReactionKind.None,
        bool interruptActions = true,
        bool flattenToGround = true)
    {
        if (flattenToGround)
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);

        Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        Distance = Mathf.Max(0f, distance);
        Duration = Mathf.Max(0f, duration);
        HitPoint = hitPoint;
        Reaction = reaction;
        InterruptActions = interruptActions;
        FlattenToGround = flattenToGround;
    }

    public Vector3 Direction { get; }
    public float Distance { get; }
    public float Duration { get; }
    public Vector3 HitPoint { get; }
    public ImpactReactionKind Reaction { get; }
    public bool InterruptActions { get; }
    public bool FlattenToGround { get; }

    public bool IsValid =>
        Direction.sqrMagnitude > 0.0001f &&
        Distance > 0.001f &&
        Duration > 0.001f;

    public static KnockbackData FromOrigin(
        Vector3 origin,
        Vector3 targetPoint,
        float distance,
        float duration,
        ImpactReactionKind reaction = ImpactReactionKind.None,
        bool interruptActions = true,
        bool flattenToGround = true)
    {
        return new KnockbackData(
            targetPoint - origin,
            distance,
            duration,
            targetPoint,
            reaction,
            interruptActions,
            flattenToGround);
    }
}
