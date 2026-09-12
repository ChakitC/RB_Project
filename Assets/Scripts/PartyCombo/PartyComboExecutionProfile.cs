using UnityEngine;

public enum PartyComboPlacementPolicy
{
    KeepCurrentPosition = 0,
    PlaceNearEventTarget = 1,
}

public enum PartyComboExitPolicy
{
    KeepAtCurrentPosition = 0,
    ReturnToRecordedOrigin = 1,
}

[CreateAssetMenu(fileName = "PartyComboExecution", menuName = "Game/Party Combo/Execution Profile")]
public sealed class PartyComboExecutionProfile : ScriptableObject
{
    [Header("Placement")]
    public PartyComboPlacementPolicy placementPolicy = PartyComboPlacementPolicy.KeepCurrentPosition;
    [Min(0f)] public float desiredRange = 2f;
    public Vector3 localOffset;
    public bool requireNavMesh = true;
    [Min(0.05f)] public float navMeshSampleDistance = 1f;
    public bool requireUnobstructedPosition = true;

    [Header("Execution")]
    [Min(0.1f)] public float startTimeoutSeconds = 2f;
    [Min(0.1f)] public float recoveryTimeoutSeconds = 4f;
    public bool protectActorDuringExecution = true;
    public PartyComboExitPolicy exitPolicy = PartyComboExitPolicy.ReturnToRecordedOrigin;
}
