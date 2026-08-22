using UnityEngine;

/// <summary>
/// Tuning for placing the party inside a room. The values stay serialized on
/// <see cref="MapRunController"/> so existing scene and prefab authoring keeps working; this struct
/// only carries them into <see cref="PartyRoomTransitionService"/>.
/// </summary>
public readonly struct PartyWarpSettings
{
    public const string DefaultCollisionLayerName = "Terrain";

    public readonly float NavMeshSampleRadius;
    public readonly LayerMask CollisionMask;
    public readonly float CollisionPadding;
    public readonly float GroundClearance;
    public readonly float SafeSearchRadius;
    public readonly int SafeSearchSteps;

    public PartyWarpSettings(
        float navMeshSampleRadius,
        LayerMask collisionMask,
        float collisionPadding,
        float groundClearance,
        float safeSearchRadius,
        int safeSearchSteps)
    {
        NavMeshSampleRadius = navMeshSampleRadius;
        CollisionMask = collisionMask;
        CollisionPadding = collisionPadding;
        GroundClearance = groundClearance;
        SafeSearchRadius = safeSearchRadius;
        SafeSearchSteps = safeSearchSteps;
    }
}
