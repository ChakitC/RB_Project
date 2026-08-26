using UnityEngine;
using UnityEngine.AI;

public readonly struct CharacterPlacementRuntimePolicy
{
    public static CharacterPlacementRuntimePolicy CreateDefault(
        bool requireNavMesh,
        float navMeshSampleDistance,
        QueryTriggerInteraction collisionTriggerInteraction,
        float collisionPadding = 0.05f,
        float targetContactWindowBefore = 0.05f,
        float targetContactWindowAfter = 0.05f)
    {
        return new CharacterPlacementRuntimePolicy(
            requireNavMesh,
            navMeshSampleDistance,
            NavMesh.AllAreas,
            requireGroundSupport: false,
            groundLayers: Physics.DefaultRaycastLayers,
            groundRaycastHeight: 2f,
            groundRaycastDistance: 8f,
            collisionTriggerInteraction,
            collisionPadding,
            maxDetailedCandidates: 3,
            maxTrajectorySamples: 240,
            targetContactWindowBefore,
            targetContactWindowAfter);
    }

    public CharacterPlacementRuntimePolicy(
        bool requireNavMesh,
        float navMeshSampleDistance,
        int navMeshAreaMask,
        bool requireGroundSupport,
        LayerMask groundLayers,
        float groundRaycastHeight,
        float groundRaycastDistance,
        QueryTriggerInteraction collisionTriggerInteraction,
        float collisionPadding,
        int maxDetailedCandidates,
        int maxTrajectorySamples,
        float targetContactWindowBefore,
        float targetContactWindowAfter)
    {
        HasValue = true;
        RequireNavMesh = requireNavMesh;
        NavMeshSampleDistance = Mathf.Max(0.05f, navMeshSampleDistance);
        NavMeshAreaMask = navMeshAreaMask;
        RequireGroundSupport = requireGroundSupport;
        GroundLayers = groundLayers;
        GroundRaycastHeight = Mathf.Max(0.1f, groundRaycastHeight);
        GroundRaycastDistance = Mathf.Max(0.1f, groundRaycastDistance);
        CollisionTriggerInteraction = collisionTriggerInteraction;
        CollisionPadding = Mathf.Max(0f, collisionPadding);
        MaxDetailedCandidates = Mathf.Max(1, maxDetailedCandidates);
        MaxTrajectorySamples = Mathf.Max(1, maxTrajectorySamples);
        TargetContactWindowBefore = Mathf.Max(0f, targetContactWindowBefore);
        TargetContactWindowAfter = Mathf.Max(0f, targetContactWindowAfter);
    }

    public bool HasValue { get; }
    public bool RequireNavMesh { get; }
    public float NavMeshSampleDistance { get; }
    public int NavMeshAreaMask { get; }
    public bool RequireGroundSupport { get; }
    public LayerMask GroundLayers { get; }
    public float GroundRaycastHeight { get; }
    public float GroundRaycastDistance { get; }
    public QueryTriggerInteraction CollisionTriggerInteraction { get; }
    public float CollisionPadding { get; }
    public int MaxDetailedCandidates { get; }
    public int MaxTrajectorySamples { get; }
    public float TargetContactWindowBefore { get; }
    public float TargetContactWindowAfter { get; }
}
