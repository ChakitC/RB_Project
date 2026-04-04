using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public static class ChainAttackTeleportUtility
{
    public static bool TryResolveTeleportPose(
        ChainAttackTeleportProfileDef profile,
        Transform anchorTransform,
        Quaternion fallbackBaseRotation,
        out Vector3 teleportPosition,
        out Quaternion teleportRotation)
    {
        teleportPosition = Vector3.zero;
        teleportRotation = Quaternion.identity;

        if (profile == null)
            return false;

        ChainAttackTeleportRuntimeConfig config = new(
            profile.useAnchorRotationAsBase,
            profile.requireNavMeshAtAnchor,
            profile.navMeshSampleDistance,
            profile.anchorPositionOffset,
            profile.probeOrientation,
            profile.allowFallbackToBaseRotation,
            profile.GetOrientationAngles(),
            profile.clearanceCenterOffset,
            profile.clearanceHalfExtents,
            profile.obstacleLayers,
            profile.obstacleTriggerInteraction,
            profile.HasClearanceProbe);

        return TryResolveTeleportPose(config, anchorTransform, fallbackBaseRotation, out teleportPosition, out teleportRotation);
    }

    public static bool TryResolveTeleportPose(
        HelperChainAttackSequenceDef profile,
        Transform anchorTransform,
        Quaternion fallbackBaseRotation,
        out Vector3 teleportPosition,
        out Quaternion teleportRotation)
    {
        teleportPosition = Vector3.zero;
        teleportRotation = Quaternion.identity;

        if (profile == null)
            return false;

        ChainAttackTeleportRuntimeConfig config = new(
            profile.useAnchorRotationAsBase,
            profile.requireNavMeshAtAnchor,
            profile.navMeshSampleDistance,
            profile.anchorPositionOffset,
            profile.probeOrientation,
            profile.allowFallbackToBaseRotation,
            profile.GetOrientationAngles(),
            profile.clearanceCenterOffset,
            profile.clearanceHalfExtents,
            profile.obstacleLayers,
            profile.obstacleTriggerInteraction,
            profile.HasClearanceProbe);

        return TryResolveTeleportPose(config, anchorTransform, fallbackBaseRotation, out teleportPosition, out teleportRotation);
    }

    static bool TryResolveTeleportPose(
        ChainAttackTeleportRuntimeConfig config,
        Transform anchorTransform,
        Quaternion fallbackBaseRotation,
        out Vector3 teleportPosition,
        out Quaternion teleportRotation)
    {
        teleportPosition = Vector3.zero;
        teleportRotation = Quaternion.identity;

        if (anchorTransform == null)
            return false;

        Quaternion baseRotation = config.useAnchorRotationAsBase
            ? anchorTransform.rotation
            : fallbackBaseRotation;

        bool shouldResolveCandidates =
            config.probeOrientation &&
            config.orientationAngles != null &&
            config.orientationAngles.Length > 0;

        if (!shouldResolveCandidates)
        {
            return TryResolveTeleportPoseCandidate(
                config,
                anchorTransform,
                baseRotation,
                0f,
                requireClearance: config.hasClearanceProbe,
                out teleportPosition,
                out teleportRotation);
        }

        List<Vector3> candidatePositions = new();
        List<Quaternion> candidateRotations = new();

        for (int i = 0; i < config.orientationAngles.Length; i++)
        {
            if (!TryResolveTeleportPoseCandidate(
                    config,
                    anchorTransform,
                    baseRotation,
                    config.orientationAngles[i],
                    requireClearance: config.hasClearanceProbe,
                    out Vector3 candidatePosition,
                    out Quaternion candidateRotation))
            {
                continue;
            }

            candidatePositions.Add(candidatePosition);
            candidateRotations.Add(candidateRotation);
        }

        if (candidatePositions.Count > 0)
        {
            int selectedIndex = Random.Range(0, candidatePositions.Count);
            teleportPosition = candidatePositions[selectedIndex];
            teleportRotation = candidateRotations[selectedIndex];
            return true;
        }

        if (!config.allowFallbackToBaseRotation)
            return false;

        return TryResolveTeleportPoseCandidate(
            config,
            anchorTransform,
            baseRotation,
            0f,
            requireClearance: false,
            out teleportPosition,
            out teleportRotation);
    }

    static bool TryResolveTeleportPoseCandidate(
        ChainAttackTeleportRuntimeConfig config,
        Transform anchorTransform,
        Quaternion baseRotation,
        float yawAngle,
        bool requireClearance,
        out Vector3 teleportPosition,
        out Quaternion teleportRotation)
    {
        teleportPosition = Vector3.zero;
        teleportRotation = baseRotation;

        if (anchorTransform == null)
            return false;

        Quaternion yawRotation = Quaternion.AngleAxis(yawAngle, Vector3.up);
        teleportRotation = yawRotation * baseRotation;

        Vector3 localOffset = yawRotation * config.anchorPositionOffset;
        teleportPosition = anchorTransform.TransformPoint(localOffset);

        if (config.requireNavMeshAtAnchor)
        {
            if (!NavMesh.SamplePosition(
                    teleportPosition,
                    out NavMeshHit navHit,
                    Mathf.Max(0.05f, config.navMeshSampleDistance),
                    NavMesh.AllAreas))
            {
                return false;
            }

            teleportPosition = navHit.position;
        }

        if (requireClearance && !IsTeleportPoseClear(config, teleportPosition, teleportRotation))
            return false;

        return true;
    }

    static bool IsTeleportPoseClear(
        ChainAttackTeleportRuntimeConfig config,
        Vector3 teleportPosition,
        Quaternion rotation)
    {
        if (!config.hasClearanceProbe)
            return true;

        Vector3 center = teleportPosition + rotation * config.clearanceCenterOffset;
        return !Physics.CheckBox(
            center,
            config.clearanceHalfExtents,
            rotation,
            config.obstacleLayers,
            config.obstacleTriggerInteraction);
    }

    readonly struct ChainAttackTeleportRuntimeConfig
    {
        public readonly bool useAnchorRotationAsBase;
        public readonly bool requireNavMeshAtAnchor;
        public readonly float navMeshSampleDistance;
        public readonly Vector3 anchorPositionOffset;
        public readonly bool probeOrientation;
        public readonly bool allowFallbackToBaseRotation;
        public readonly float[] orientationAngles;
        public readonly Vector3 clearanceCenterOffset;
        public readonly Vector3 clearanceHalfExtents;
        public readonly LayerMask obstacleLayers;
        public readonly QueryTriggerInteraction obstacleTriggerInteraction;
        public readonly bool hasClearanceProbe;

        public ChainAttackTeleportRuntimeConfig(
            bool useAnchorRotationAsBase,
            bool requireNavMeshAtAnchor,
            float navMeshSampleDistance,
            Vector3 anchorPositionOffset,
            bool probeOrientation,
            bool allowFallbackToBaseRotation,
            float[] orientationAngles,
            Vector3 clearanceCenterOffset,
            Vector3 clearanceHalfExtents,
            LayerMask obstacleLayers,
            QueryTriggerInteraction obstacleTriggerInteraction,
            bool hasClearanceProbe)
        {
            this.useAnchorRotationAsBase = useAnchorRotationAsBase;
            this.requireNavMeshAtAnchor = requireNavMeshAtAnchor;
            this.navMeshSampleDistance = navMeshSampleDistance;
            this.anchorPositionOffset = anchorPositionOffset;
            this.probeOrientation = probeOrientation;
            this.allowFallbackToBaseRotation = allowFallbackToBaseRotation;
            this.orientationAngles = orientationAngles;
            this.clearanceCenterOffset = clearanceCenterOffset;
            this.clearanceHalfExtents = clearanceHalfExtents;
            this.obstacleLayers = obstacleLayers;
            this.obstacleTriggerInteraction = obstacleTriggerInteraction;
            this.hasClearanceProbe = hasClearanceProbe;
        }
    }
}
