using UnityEngine;

[CreateAssetMenu(
    fileName = "CharacterPlacementPolicy",
    menuName = "Game/Combat/Character Placement Policy")]
public sealed class CharacterPlacementPolicyDef : ScriptableObject
{
    [Header("Support")]
    public bool requireNavMesh;
    public bool requireGroundSupport;
    [Min(0.05f)] public float navMeshSampleDistance = 0.75f;
    public int navMeshAreaMask = -1;
    public LayerMask groundLayers = Physics.DefaultRaycastLayers;
    [Min(0.1f)] public float groundRaycastHeight = 2f;
    [Min(0.1f)] public float groundRaycastDistance = 8f;

    [Header("Collision")]
    public LayerMask worldCollisionLayers = Physics.DefaultRaycastLayers;
    public LayerMask actorCollisionLayers = ~0;
    public QueryTriggerInteraction collisionTriggerInteraction = QueryTriggerInteraction.Ignore;
    [Min(0f)] public float collisionPadding = 0.05f;

    [Header("Evaluation")]
    [Min(1)] public int maxDetailedCandidates = 3;
    [Min(1)] public int maxTrajectorySamples = 240;
    [Min(0f)] public float targetContactWindowBefore = 0.05f;
    [Min(0f)] public float targetContactWindowAfter = 0.05f;
}
