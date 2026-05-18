using UnityEngine;

[CreateAssetMenu(fileName = "ChainAttackTeleportProfile", menuName = "Game/Chain Attack/Teleport Profile")]
public sealed class ChainAttackTeleportProfileDef : ScriptableObject
{
    static readonly float[] DefaultOrientationAngles = { 0f, 45f, -45f, 90f, -90f };

    [Header("Teleport")]
    [Tooltip("Use the target anchor rotation as the base facing. Disable to use the actor's current rotation as the base.")]
    public bool useAnchorRotationAsBase = true;
    [Tooltip("Require the resolved teleport point to be near NavMesh. When a probe collider is supplied, the probe footprint is also checked against NavMesh.")]
    public bool requireNavMeshAtAnchor = true;
    [Tooltip("Maximum distance used by NavMesh.SamplePosition when snapping the raw teleport point onto NavMesh.")]
    [Min(0.05f)] public float navMeshSampleDistance = 0.75f;
    [Tooltip("Local offset from the target anchor before yaw candidates are applied. If this is zero, yaw changes rotation only and the position stays on the anchor.")]
    public Vector3 anchorPositionOffset = Vector3.zero;

    [Header("Orientation Probe")]
    [Tooltip("Try orientation candidates and clearance checks before accepting a teleport pose.")]
    public bool probeOrientation = true;
    [Tooltip("If configured orientation angles are blocked, sweep fallback yaw angles before failing.")]
    public bool allowFallbackToBaseRotation = true;
    [Tooltip("Yaw angles, in degrees, tested around the base rotation. These only move the position when Anchor Position Offset is non-zero.")]
    public float[] orientationAngles = { 0f, 45f, -45f, 90f, -90f };
    [Tooltip("Local center offset for the legacy clearance box, relative to the resolved teleport pose.")]
    public Vector3 clearanceCenterOffset = new Vector3(0f, 1f, 0.45f);
    [Tooltip("Half extents for the legacy clearance box. Set any component to zero to disable the clearance box.")]
    public Vector3 clearanceHalfExtents = new Vector3(0.35f, 0.9f, 0.75f);
    [Tooltip("Layers treated as blocking obstacles for overlap, path, and clearance checks. Leave as Nothing to skip obstacle-layer blocking and rely on NavMesh/probe checks only.")]
    public LayerMask obstacleLayers = 0;
    [Tooltip("Controls whether obstacle overlap checks include trigger colliders.")]
    public QueryTriggerInteraction obstacleTriggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Debug")]
    [Tooltip("Log teleport candidate tests, NavMesh sampling, obstacle hits, and rejection reasons for this profile.")]
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
