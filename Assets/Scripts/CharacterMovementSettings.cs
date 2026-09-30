using UnityEngine;

/// <summary>Collision settings supplied by the behaviour requesting displacement.</summary>
public struct CharacterMovementSettings
{
    public int CollisionMask;
    public float Padding;
    public QueryTriggerInteraction Triggers;
    public bool Slide;
    public int IgnoreLayers;
    public Collider FallbackBody;
    public bool AllowAgentShape;

    public static CharacterMovementSettings Default => new CharacterMovementSettings
    {
        CollisionMask = ~0,
        Padding = 0.02f,
        Triggers = QueryTriggerInteraction.Ignore
    };
}
