using UnityEngine;

[CreateAssetMenu(fileName = "HelperChainAttackSequence", menuName = "Game/Helper Proc/Chain Attack Sequence")]
public sealed class HelperChainAttackSequenceDef : ScriptableObject
{
    static readonly float[] DefaultOrientationAngles = { 0f, 45f, -45f, 90f, -90f };

    [Header("Target Search")]
    [Min(0.1f)] public float aimSearchRadius = 3f;
    public LayerMask targetLayers = ~0;
    public QueryTriggerInteraction targetTriggerInteraction = QueryTriggerInteraction.Ignore;
    public bool requireAimLineOfSight;
    public LayerMask aimObstacleLayers = 0;

    [Header("Teleport")]
    public bool hideHelperAtWarpCastMoment = true;
    public bool useAnchorRotationAsBase = true;
    public bool requireNavMeshAtAnchor = true;
    [Min(0.05f)] public float navMeshSampleDistance = 0.75f;
    public Vector3 anchorPositionOffset = Vector3.zero;

    [Header("Orientation Probe")]
    public bool probeOrientation = true;
    public bool allowFallbackToBaseRotation = true;
    public float[] orientationAngles = { 0f, 45f, -45f, 90f, -90f };
    public Vector3 clearanceCenterOffset = new Vector3(0f, 1f, 0.45f);
    public Vector3 clearanceHalfExtents = new Vector3(0.35f, 0.9f, 0.75f);
    public LayerMask obstacleLayers = 0;
    public QueryTriggerInteraction obstacleTriggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Debug")]
    public bool debugLogging;

    public float[] GetOrientationAngles()
    {
        if (orientationAngles != null && orientationAngles.Length > 0)
            return orientationAngles;

        return DefaultOrientationAngles;
    }

    public bool HasClearanceProbe =>
        probeOrientation &&
        obstacleLayers != 0 &&
        clearanceHalfExtents.x > 0f &&
        clearanceHalfExtents.y > 0f &&
        clearanceHalfExtents.z > 0f;
}
