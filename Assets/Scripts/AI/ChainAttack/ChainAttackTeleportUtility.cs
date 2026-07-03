using UnityEngine;
using UnityEngine.AI;

public static class ChainAttackTeleportUtility
{
    const float MinProbeExtent = 0.001f;
    static readonly Collider[] OverlapBuffer = new Collider[64];
    static readonly RaycastHit[] CastHitBuffer = new RaycastHit[32];

    public static bool TryResolveTeleportPose(
        ChainAttackTeleportProfileDef profile,
        Transform anchorTransform,
        Quaternion fallbackBaseRotation,
        out Vector3 teleportPosition,
        out Quaternion teleportRotation,
        bool? requireNavMeshAtAnchorOverride = null,
        float? navMeshSampleDistanceOverride = null,
        Collider probeCollider = null,
        Transform probeRoot = null,
        System.Func<Vector3, Quaternion, bool> poseValidator = null,
        Vector3? preferredActorPosition = null)
    {
        teleportPosition = Vector3.zero;
        teleportRotation = Quaternion.identity;

        if (profile == null)
            return false;

        ChainAttackTeleportRuntimeConfig config = new(
            profile.useAnchorRotationAsBase,
            requireNavMeshAtAnchorOverride ?? profile.requireNavMeshAtAnchor,
            navMeshSampleDistanceOverride ?? profile.navMeshSampleDistance,
            profile.anchorPositionOffset,
            profile.clearanceCenterOffset,
            profile.clearanceHalfExtents,
            profile.obstacleLayers,
            profile.obstacleTriggerInteraction,
            profile.HasClearanceProbe,
            probeCollider,
            probeRoot,
            null,
            profile.debugLogging,
            profile.name);

        return TryResolveTeleportPose(config, anchorTransform, fallbackBaseRotation, poseValidator, preferredActorPosition, out teleportPosition, out teleportRotation);
    }

    public static bool TryResolveTeleportPose(
        HelperChainAttackSequenceDef profile,
        Transform anchorTransform,
        Quaternion fallbackBaseRotation,
        out Vector3 teleportPosition,
        out Quaternion teleportRotation,
        Collider probeCollider = null,
        Transform probeRoot = null,
        System.Func<Vector3, Quaternion, bool> poseValidator = null,
        Vector3? preferredActorPosition = null)
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
            profile.clearanceCenterOffset,
            profile.clearanceHalfExtents,
            profile.obstacleLayers,
            profile.obstacleTriggerInteraction,
            profile.HasClearanceProbe,
            probeCollider,
            probeRoot,
            null,
            profile.debugLogging,
            profile.name);

        return TryResolveTeleportPose(config, anchorTransform, fallbackBaseRotation, poseValidator, preferredActorPosition, out teleportPosition, out teleportRotation);
    }

    public static bool IsCurrentProbeColliderClear(
        Collider probeCollider,
        Transform probeRoot,
        LayerMask obstacleLayers,
        QueryTriggerInteraction obstacleTriggerInteraction,
        bool debugLogging = false,
        string debugName = null)
    {
        if (probeCollider == null || obstacleLayers.value == 0)
            return true;

        ChainAttackTeleportRuntimeConfig config = new(
            useAnchorRotationAsBase: false,
            requireNavMeshAtAnchor: false,
            navMeshSampleDistance: 0.05f,
            anchorPositionOffset: Vector3.zero,
            clearanceCenterOffset: Vector3.zero,
            clearanceHalfExtents: Vector3.zero,
            obstacleLayers,
            obstacleTriggerInteraction,
            hasClearanceProbe: false,
            probeCollider,
            probeRoot,
            allowedOverlapRoot: null,
            debugLogging,
            debugName);

        return IsCurrentProbeColliderClear(config);
    }

    public static bool IsProbePoseClear(
        ChainAttackTeleportProfileDef profile,
        Vector3 worldPosition,
        Quaternion worldRotation,
        Collider probeCollider,
        Transform probeRoot,
        Transform allowedOverlapRoot = null)
    {
        if (profile == null)
            return false;

        ChainAttackTeleportRuntimeConfig config = CreateRuntimeConfig(
            profile,
            profile.requireNavMeshAtAnchor,
            profile.navMeshSampleDistance,
            probeCollider,
            probeRoot,
            allowedOverlapRoot);

        return IsTeleportPoseClear(
            config,
            worldPosition,
            worldRotation,
            requireLegacyClearance: profile.HasClearanceProbe);
    }

    public static bool IsProbePoseOnNavMesh(
        ChainAttackTeleportProfileDef profile,
        Vector3 worldPosition,
        Quaternion worldRotation,
        Collider probeCollider,
        Transform probeRoot,
        float navMeshSampleDistance)
    {
        if (profile == null)
            return false;

        ChainAttackTeleportRuntimeConfig config = CreateRuntimeConfig(
            profile,
            true,
            navMeshSampleDistance,
            probeCollider,
            probeRoot);

        if (!NavMesh.SamplePosition(
                worldPosition,
                out _,
                Mathf.Max(0.05f, navMeshSampleDistance),
                NavMesh.AllAreas))
        {
            return false;
        }

        return !config.HasProbeCollider ||
               IsProbeColliderOnNavMesh(config, worldPosition, worldRotation);
    }

    public static bool IsProbeSweepClear(
        ChainAttackTeleportProfileDef profile,
        Vector3 startPosition,
        Quaternion startRotation,
        Vector3 endPosition,
        Quaternion endRotation,
        Collider probeCollider,
        Transform probeRoot,
        Transform allowedOverlapRoot = null)
    {
        if (profile == null || profile.obstacleLayers.value == 0)
            return true;

        ChainAttackTeleportRuntimeConfig config = CreateRuntimeConfig(
            profile,
            profile.requireNavMeshAtAnchor,
            profile.navMeshSampleDistance,
            probeCollider,
            probeRoot,
            allowedOverlapRoot);

        GetProbeSweepPose(config, startPosition, startRotation, out Vector3 startCenter, out float startRadius);
        GetProbeSweepPose(config, endPosition, endRotation, out Vector3 endCenter, out float endRadius);

        Vector3 direction = endCenter - startCenter;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
            return true;

        float radius = Mathf.Max(0.05f, Mathf.Max(startRadius, endRadius));
        int hitCount = Physics.SphereCastNonAlloc(
            startCenter,
            radius,
            direction / distance,
            CastHitBuffer,
            distance,
            profile.obstacleLayers,
            profile.obstacleTriggerInteraction);

        return !HasBlockingCastHit(hitCount, config);
    }

    static ChainAttackTeleportRuntimeConfig CreateRuntimeConfig(
        ChainAttackTeleportProfileDef profile,
        bool requireNavMeshAtAnchor,
        float navMeshSampleDistance,
        Collider probeCollider,
        Transform probeRoot,
        Transform allowedOverlapRoot = null)
    {
        return new ChainAttackTeleportRuntimeConfig(
            profile.useAnchorRotationAsBase,
            requireNavMeshAtAnchor,
            navMeshSampleDistance,
            profile.anchorPositionOffset,
            profile.clearanceCenterOffset,
            profile.clearanceHalfExtents,
            profile.obstacleLayers,
            profile.obstacleTriggerInteraction,
            profile.HasClearanceProbe,
            probeCollider,
            probeRoot,
            allowedOverlapRoot,
            profile.debugLogging,
            profile.name);
    }

    static bool TryResolveTeleportPose(
        ChainAttackTeleportRuntimeConfig config,
        Transform anchorTransform,
        Quaternion fallbackBaseRotation,
        System.Func<Vector3, Quaternion, bool> poseValidator,
        Vector3? preferredActorPosition,
        out Vector3 teleportPosition,
        out Quaternion teleportRotation)
    {
        teleportPosition = Vector3.zero;
        teleportRotation = Quaternion.identity;

        if (anchorTransform == null)
        {
            Log(config, "Teleport pose resolve failed: anchorTransform is null.");
            return false;
        }

        Quaternion baseRotation = config.useAnchorRotationAsBase
            ? anchorTransform.rotation
            : fallbackBaseRotation;

        float baseYaw = TargetedSkillSnapSidePriority.ResolveBaseYaw(
            anchorTransform, config.anchorPositionOffset, preferredActorPosition);

        float[] offsets = TargetedSkillSnapSidePriority.CandidateYawOffsets;
        for (int i = 0; i < offsets.Length; i++)
        {
            float yaw = baseYaw + offsets[i];

            if (TryResolveTeleportPoseCandidate(
                    config,
                    anchorTransform,
                    baseRotation,
                    yaw,
                    config.hasClearanceProbe,
                    out teleportPosition,
                    out teleportRotation) &&
                IsResolvedTeleportPoseAccepted(config, yaw, teleportPosition, teleportRotation, poseValidator))
            {
                return true;
            }
        }

        Log(config, "Teleport pose resolve failed: all swept yaw candidates were blocked.");
        return false;
    }

    static bool IsResolvedTeleportPoseAccepted(
        ChainAttackTeleportRuntimeConfig config,
        float yawAngle,
        Vector3 teleportPosition,
        Quaternion teleportRotation,
        System.Func<Vector3, Quaternion, bool> poseValidator)
    {
        if (poseValidator == null)
        {
            Log(config, $"Accepted yaw={yawAngle:0.###}: no current pose validator was provided.");
            return true;
        }

        if (poseValidator(teleportPosition, teleportRotation))
        {
            Log(config, $"Accepted yaw={yawAngle:0.###}: current pose validator passed after applying actor pose.");
            return true;
        }

        Log(config, $"Rejected yaw={yawAngle:0.###}: current pose validator rejected candidate after applying actor pose.");
        return false;
    }

    static bool TryResolveTeleportPoseCandidate(
        ChainAttackTeleportRuntimeConfig config,
        Transform anchorTransform,
        Quaternion baseRotation,
        float yawAngle,
        bool requireLegacyClearance,
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

        Log(
            config,
            $"Testing yaw={yawAngle:0.###} rawPosition={teleportPosition} rotationY={teleportRotation.eulerAngles.y:0.###} " +
            $"offset={config.anchorPositionOffset} probe={DescribeProbe(config)} obstacleMask={config.obstacleLayers.value}.");

        if (!IsTeleportPoseClear(config, teleportPosition, teleportRotation, requireLegacyClearance))
        {
            LogNearbyColliders(config, teleportPosition, "raw rejected pose");
            Log(config, $"Rejected yaw={yawAngle:0.###}: raw pose overlaps obstacle or clearance volume.");
            return false;
        }

        if (!IsAnchorPathClear(config, anchorTransform.position, teleportPosition, teleportRotation))
        {
            Log(config, $"Rejected yaw={yawAngle:0.###}: obstacle blocks path from anchor to raw pose.");
            return false;
        }

        if (config.requireNavMeshAtAnchor)
        {
            if (!NavMesh.SamplePosition(
                    teleportPosition,
                    out NavMeshHit navHit,
                    Mathf.Max(0.05f, config.navMeshSampleDistance),
                    NavMesh.AllAreas))
            {
                Log(
                    config,
                    $"Rejected yaw={yawAngle:0.###}: NavMesh.SamplePosition failed at rawPosition={teleportPosition} " +
                    $"sampleDistance={Mathf.Max(0.05f, config.navMeshSampleDistance):0.###}.");
                return false;
            }

            Log(config, $"NavMesh sampled yaw={yawAngle:0.###}: {teleportPosition} -> {navHit.position}.");
            teleportPosition = navHit.position;

            if (config.HasProbeCollider && !IsProbeColliderOnNavMesh(config, teleportPosition, teleportRotation))
            {
                Log(config, $"Rejected yaw={yawAngle:0.###}: probe footprint is not fully on NavMesh at {teleportPosition}.");
                return false;
            }
        }

        if (!IsTeleportPoseClear(config, teleportPosition, teleportRotation, requireLegacyClearance))
        {
            LogNearbyColliders(config, teleportPosition, "sampled rejected pose");
            Log(config, $"Rejected yaw={yawAngle:0.###}: sampled pose overlaps obstacle or clearance volume at {teleportPosition}.");
            return false;
        }

        if (!IsAnchorPathClear(config, anchorTransform.position, teleportPosition, teleportRotation))
        {
            Log(config, $"Rejected yaw={yawAngle:0.###}: obstacle blocks path from anchor to sampled pose.");
            return false;
        }

        LogNearbyColliders(config, teleportPosition, "candidate pose");
        Log(config, $"Candidate passed yaw={yawAngle:0.###}: position={teleportPosition} rotationY={teleportRotation.eulerAngles.y:0.###}.");
        return true;
    }

    static bool IsTeleportPoseClear(
        ChainAttackTeleportRuntimeConfig config,
        Vector3 teleportPosition,
        Quaternion rotation,
        bool requireLegacyClearance)
    {
        if (config.HasProbeCollider && !IsProbeColliderClear(config, teleportPosition, rotation))
            return false;

        if (!requireLegacyClearance || !config.hasClearanceProbe)
            return true;

        Vector3 center = teleportPosition + rotation * config.clearanceCenterOffset;
        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            config.clearanceHalfExtents,
            OverlapBuffer,
            rotation,
            config.obstacleLayers,
            config.obstacleTriggerInteraction);
        return !HasBlockingOverlap(hitCount, config);
    }

    static bool IsAnchorPathClear(
        ChainAttackTeleportRuntimeConfig config,
        Vector3 anchorPosition,
        Vector3 teleportPosition,
        Quaternion rotation)
    {
        if (config.obstacleLayers.value == 0)
            return true;

        Vector3 start = anchorPosition + Vector3.up * Mathf.Max(0.05f, config.clearanceCenterOffset.y);
        Vector3 end = teleportPosition + rotation * config.clearanceCenterOffset;
        Vector3 direction = end - start;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
            return true;

        float radius = ResolvePathProbeRadius(config);
        int hitCount = Physics.SphereCastNonAlloc(
            start,
            radius,
            direction / distance,
            CastHitBuffer,
            distance,
            config.obstacleLayers,
            config.obstacleTriggerInteraction);

        return !HasBlockingCastHit(hitCount, config);
    }

    static void GetProbeSweepPose(
        ChainAttackTeleportRuntimeConfig config,
        Vector3 worldPosition,
        Quaternion worldRotation,
        out Vector3 center,
        out float radius)
    {
        center = worldPosition + worldRotation * config.clearanceCenterOffset;
        radius = ResolvePathProbeRadius(config);

        Collider probeCollider = config.probeCollider;
        if (probeCollider is BoxCollider box)
        {
            GetBoxProbePose(
                config,
                box,
                worldPosition,
                worldRotation,
                out center,
                out _,
                out Vector3 halfExtents,
                out _,
                out _,
                out _);
            radius = Mathf.Max(0.05f, Mathf.Min(halfExtents.x, halfExtents.z));
            return;
        }

        if (probeCollider is CapsuleCollider capsule)
        {
            GetCapsuleProbePose(
                config,
                capsule,
                worldPosition,
                worldRotation,
                out center,
                out _,
                out _,
                out radius,
                out _,
                out _,
                out _);
            return;
        }

        if (probeCollider is SphereCollider sphere)
        {
            GetSphereProbePose(
                config,
                sphere,
                worldPosition,
                worldRotation,
                out center,
                out radius,
                out _,
                out _);
            return;
        }

        if (probeCollider == null)
            return;

        GetBoundsProbePose(
            config,
            probeCollider,
            worldPosition,
            worldRotation,
            out center,
            out _,
            out Vector3 boundsHalfExtents);
        radius = Mathf.Max(0.05f, Mathf.Min(boundsHalfExtents.x, boundsHalfExtents.z));
    }

    static bool IsProbeColliderOnNavMesh(
        ChainAttackTeleportRuntimeConfig config,
        Vector3 teleportPosition,
        Quaternion rotation)
    {
        if (!config.HasProbeCollider)
            return true;

        Collider probeCollider = config.probeCollider;
        if (probeCollider is BoxCollider box)
            return IsBoxProbeOnNavMesh(config, box, teleportPosition, rotation);

        if (probeCollider is CapsuleCollider capsule)
            return IsCapsuleProbeOnNavMesh(config, capsule, teleportPosition, rotation);

        if (probeCollider is SphereCollider sphere)
            return IsSphereProbeOnNavMesh(config, sphere, teleportPosition, rotation);

        return IsBoundsProbeOnNavMesh(config, probeCollider, teleportPosition, rotation);
    }

    static bool IsProbeColliderClear(
        ChainAttackTeleportRuntimeConfig config,
        Vector3 teleportPosition,
        Quaternion rotation)
    {
        if (!config.HasProbeCollider || config.obstacleLayers.value == 0)
            return true;

        Physics.SyncTransforms();

        Collider probeCollider = config.probeCollider;
        if (probeCollider is BoxCollider box)
        {
            GetBoxProbePose(
                config,
                box,
                teleportPosition,
                rotation,
                out Vector3 center,
                out Quaternion boxRotation,
                out Vector3 halfExtents,
                out _,
                out _,
                out _);

            if (!HasPositiveExtents(halfExtents))
                return false;

            int hitCount = Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                OverlapBuffer,
                boxRotation,
                config.obstacleLayers,
                config.obstacleTriggerInteraction);
            return !HasBlockingOverlap(hitCount, config);
        }

        if (probeCollider is CapsuleCollider capsule)
        {
            GetCapsuleProbePose(
                config,
                capsule,
                teleportPosition,
                rotation,
                out _,
                out Vector3 pointA,
                out Vector3 pointB,
                out float radius,
                out _,
                out _,
                out _);

            if (radius <= MinProbeExtent)
                return false;

            int hitCount = Physics.OverlapCapsuleNonAlloc(
                pointA,
                pointB,
                radius,
                OverlapBuffer,
                config.obstacleLayers,
                config.obstacleTriggerInteraction);
            return !HasBlockingOverlap(hitCount, config);
        }

        if (probeCollider is SphereCollider sphere)
        {
            GetSphereProbePose(
                config,
                sphere,
                teleportPosition,
                rotation,
                out Vector3 center,
                out float radius,
                out _,
                out _);

            if (radius <= MinProbeExtent)
                return false;

            int hitCount = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                OverlapBuffer,
                config.obstacleLayers,
                config.obstacleTriggerInteraction);
            return !HasBlockingOverlap(hitCount, config);
        }

        GetBoundsProbePose(
            config,
            probeCollider,
            teleportPosition,
            rotation,
            out Vector3 boundsCenter,
            out Quaternion boundsRotation,
            out Vector3 boundsHalfExtents);

        if (!HasPositiveExtents(boundsHalfExtents))
            return false;

        int boundsHitCount = Physics.OverlapBoxNonAlloc(
            boundsCenter,
            boundsHalfExtents,
            OverlapBuffer,
            boundsRotation,
            config.obstacleLayers,
            config.obstacleTriggerInteraction);
        return !HasBlockingOverlap(boundsHitCount, config);
    }

    static bool IsCurrentProbeColliderClear(ChainAttackTeleportRuntimeConfig config)
    {
        if (!config.HasProbeCollider || config.obstacleLayers.value == 0)
            return true;

        Physics.SyncTransforms();

        Collider probeCollider = config.probeCollider;
        Bounds bounds = probeCollider.bounds;
        Vector3 halfExtents = bounds.extents + Vector3.one * 0.01f;
        int hitCount = Physics.OverlapBoxNonAlloc(
            bounds.center,
            halfExtents,
            OverlapBuffer,
            Quaternion.identity,
            config.obstacleLayers,
            config.obstacleTriggerInteraction);

        bool penetrates = HasCurrentProbePenetration(hitCount, config);
        if (penetrates)
            Log(config, $"Current probe '{probeCollider.name}' penetrates obstacle using actual collider shape.");

        return !penetrates;
    }

    static bool IsCurrentProbeColliderClearUsingOverlapShape(ChainAttackTeleportRuntimeConfig config)
    {
        Collider probeCollider = config.probeCollider;
        if (probeCollider is BoxCollider box)
        {
            GetCurrentBoxProbePose(
                box,
                out Vector3 center,
                out Quaternion boxRotation,
                out Vector3 halfExtents);

            if (!HasPositiveExtents(halfExtents))
                return false;

            int hitCount = Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                OverlapBuffer,
                boxRotation,
                config.obstacleLayers,
                config.obstacleTriggerInteraction);
            bool blocked = HasBlockingOverlap(hitCount, config);
            if (blocked)
                Log(config, $"Current probe '{probeCollider.name}' overlaps obstacle as BoxCollider.");
            return !blocked;
        }

        if (probeCollider is CapsuleCollider capsule)
        {
            GetCurrentCapsuleProbePose(
                capsule,
                out Vector3 pointA,
                out Vector3 pointB,
                out float radius);

            if (radius <= MinProbeExtent)
                return false;

            int hitCount = Physics.OverlapCapsuleNonAlloc(
                pointA,
                pointB,
                radius,
                OverlapBuffer,
                config.obstacleLayers,
                config.obstacleTriggerInteraction);
            bool blocked = HasBlockingOverlap(hitCount, config);
            if (blocked)
                Log(config, $"Current probe '{probeCollider.name}' overlaps obstacle as CapsuleCollider.");
            return !blocked;
        }

        if (probeCollider is SphereCollider sphere)
        {
            GetCurrentSphereProbePose(
                sphere,
                out Vector3 center,
                out float radius);

            if (radius <= MinProbeExtent)
                return false;

            int hitCount = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                OverlapBuffer,
                config.obstacleLayers,
                config.obstacleTriggerInteraction);
            bool blocked = HasBlockingOverlap(hitCount, config);
            if (blocked)
                Log(config, $"Current probe '{probeCollider.name}' overlaps obstacle as SphereCollider.");
            return !blocked;
        }

        Bounds bounds = probeCollider.bounds;
        int boundsHitCount = Physics.OverlapBoxNonAlloc(
            bounds.center,
            bounds.extents,
            OverlapBuffer,
            Quaternion.identity,
            config.obstacleLayers,
            config.obstacleTriggerInteraction);
        bool boundsBlocked = HasBlockingOverlap(boundsHitCount, config);
        if (boundsBlocked)
            Log(config, $"Current probe '{probeCollider.name}' overlaps obstacle using bounds fallback.");
        return !boundsBlocked;
    }

    static bool IsBoxProbeOnNavMesh(
        ChainAttackTeleportRuntimeConfig config,
        BoxCollider box,
        Vector3 teleportPosition,
        Quaternion rotation)
    {
        GetBoxProbePose(
            config,
            box,
            teleportPosition,
            rotation,
            out Vector3 center,
            out _,
            out Vector3 halfExtents,
            out Vector3 right,
            out Vector3 up,
            out Vector3 forward);

        if (!HasPositiveExtents(halfExtents))
            return false;

        Vector3 bottomCenter = center - up * halfExtents.y;
        Vector3 planarRight = ResolvePlanarAxis(right, Vector3.right) * halfExtents.x;
        Vector3 planarForward = ResolvePlanarAxis(forward, Vector3.forward) * halfExtents.z;

        return IsNavMeshPointValid(config, bottomCenter) &&
               IsNavMeshPointValid(config, bottomCenter + planarRight + planarForward) &&
               IsNavMeshPointValid(config, bottomCenter + planarRight - planarForward) &&
               IsNavMeshPointValid(config, bottomCenter - planarRight + planarForward) &&
               IsNavMeshPointValid(config, bottomCenter - planarRight - planarForward);
    }

    static bool IsCapsuleProbeOnNavMesh(
        ChainAttackTeleportRuntimeConfig config,
        CapsuleCollider capsule,
        Vector3 teleportPosition,
        Quaternion rotation)
    {
        GetCapsuleProbePose(
            config,
            capsule,
            teleportPosition,
            rotation,
            out _,
            out Vector3 pointA,
            out Vector3 pointB,
            out float radius,
            out _,
            out Vector3 sideA,
            out Vector3 sideB);

        if (radius <= MinProbeExtent)
            return false;

        Vector3 lowerSegmentCenter = pointA.y <= pointB.y ? pointA : pointB;
        Vector3 footCenter = lowerSegmentCenter - Vector3.up * radius;
        Vector3 planarA = ResolvePlanarAxis(sideA, Vector3.right) * radius;
        Vector3 planarB = ResolvePlanarAxis(sideB, Vector3.forward) * radius;

        return IsNavMeshPointValid(config, footCenter) &&
               IsNavMeshPointValid(config, footCenter + planarA) &&
               IsNavMeshPointValid(config, footCenter - planarA) &&
               IsNavMeshPointValid(config, footCenter + planarB) &&
               IsNavMeshPointValid(config, footCenter - planarB);
    }

    static bool IsSphereProbeOnNavMesh(
        ChainAttackTeleportRuntimeConfig config,
        SphereCollider sphere,
        Vector3 teleportPosition,
        Quaternion rotation)
    {
        GetSphereProbePose(
            config,
            sphere,
            teleportPosition,
            rotation,
            out Vector3 center,
            out float radius,
            out Vector3 right,
            out Vector3 forward);

        if (radius <= MinProbeExtent)
            return false;

        Vector3 bottomCenter = center - Vector3.up * radius;
        Vector3 planarRight = ResolvePlanarAxis(right, Vector3.right) * radius;
        Vector3 planarForward = ResolvePlanarAxis(forward, Vector3.forward) * radius;

        return IsNavMeshPointValid(config, bottomCenter) &&
               IsNavMeshPointValid(config, bottomCenter + planarRight) &&
               IsNavMeshPointValid(config, bottomCenter - planarRight) &&
               IsNavMeshPointValid(config, bottomCenter + planarForward) &&
               IsNavMeshPointValid(config, bottomCenter - planarForward);
    }

    static bool IsBoundsProbeOnNavMesh(
        ChainAttackTeleportRuntimeConfig config,
        Collider probeCollider,
        Vector3 teleportPosition,
        Quaternion rotation)
    {
        GetBoundsProbePose(
            config,
            probeCollider,
            teleportPosition,
            rotation,
            out Vector3 center,
            out Quaternion boundsRotation,
            out Vector3 halfExtents);

        if (!HasPositiveExtents(halfExtents))
            return false;

        Vector3 right = boundsRotation * Vector3.right;
        Vector3 forward = boundsRotation * Vector3.forward;
        Vector3 bottomCenter = center - Vector3.up * halfExtents.y;
        Vector3 planarRight = ResolvePlanarAxis(right, Vector3.right) * halfExtents.x;
        Vector3 planarForward = ResolvePlanarAxis(forward, Vector3.forward) * halfExtents.z;

        return IsNavMeshPointValid(config, bottomCenter) &&
               IsNavMeshPointValid(config, bottomCenter + planarRight + planarForward) &&
               IsNavMeshPointValid(config, bottomCenter + planarRight - planarForward) &&
               IsNavMeshPointValid(config, bottomCenter - planarRight + planarForward) &&
               IsNavMeshPointValid(config, bottomCenter - planarRight - planarForward);
    }

    static void GetBoxProbePose(
        ChainAttackTeleportRuntimeConfig config,
        BoxCollider box,
        Vector3 teleportPosition,
        Quaternion rotation,
        out Vector3 center,
        out Quaternion boxRotation,
        out Vector3 halfExtents,
        out Vector3 right,
        out Vector3 up,
        out Vector3 forward)
    {
        Matrix4x4 rootMatrix = BuildCandidateRootMatrix(config, teleportPosition, rotation);
        center = TransformColliderPoint(config, box, rootMatrix, box.center);

        Vector3 x = TransformColliderVector(config, box, rootMatrix, Vector3.right * box.size.x * 0.5f);
        Vector3 y = TransformColliderVector(config, box, rootMatrix, Vector3.up * box.size.y * 0.5f);
        Vector3 z = TransformColliderVector(config, box, rootMatrix, Vector3.forward * box.size.z * 0.5f);

        right = SafeDirection(x, rotation * Vector3.right);
        up = SafeDirection(y, rotation * Vector3.up);
        forward = SafeDirection(z, rotation * Vector3.forward);
        halfExtents = new Vector3(x.magnitude, y.magnitude, z.magnitude);
        boxRotation = BuildRotation(forward, up, rotation);
    }

    static void GetCurrentBoxProbePose(
        BoxCollider box,
        out Vector3 center,
        out Quaternion boxRotation,
        out Vector3 halfExtents)
    {
        center = box.transform.TransformPoint(box.center);

        Vector3 x = box.transform.TransformVector(Vector3.right * box.size.x * 0.5f);
        Vector3 y = box.transform.TransformVector(Vector3.up * box.size.y * 0.5f);
        Vector3 z = box.transform.TransformVector(Vector3.forward * box.size.z * 0.5f);

        Vector3 up = SafeDirection(y, box.transform.up);
        Vector3 forward = SafeDirection(z, box.transform.forward);
        halfExtents = new Vector3(x.magnitude, y.magnitude, z.magnitude);
        boxRotation = BuildRotation(forward, up, box.transform.rotation);
    }

    static void GetCapsuleProbePose(
        ChainAttackTeleportRuntimeConfig config,
        CapsuleCollider capsule,
        Vector3 teleportPosition,
        Quaternion rotation,
        out Vector3 center,
        out Vector3 pointA,
        out Vector3 pointB,
        out float radius,
        out Vector3 axis,
        out Vector3 sideA,
        out Vector3 sideB)
    {
        Matrix4x4 rootMatrix = BuildCandidateRootMatrix(config, teleportPosition, rotation);
        center = TransformColliderPoint(config, capsule, rootMatrix, capsule.center);

        Vector3 axisLocal = CapsuleAxis(capsule.direction);
        Vector3 sideALocal = CapsuleSideA(capsule.direction);
        Vector3 sideBLocal = CapsuleSideB(capsule.direction);

        Vector3 axisVector = TransformColliderVector(config, capsule, rootMatrix, axisLocal);
        Vector3 sideAVector = TransformColliderVector(config, capsule, rootMatrix, sideALocal);
        Vector3 sideBVector = TransformColliderVector(config, capsule, rootMatrix, sideBLocal);

        axis = SafeDirection(axisVector, rotation * axisLocal);
        sideA = SafeDirection(sideAVector, rotation * sideALocal);
        sideB = SafeDirection(sideBVector, rotation * sideBLocal);

        float axisScale = Mathf.Max(MinProbeExtent, axisVector.magnitude);
        float radiusScale = Mathf.Max(sideAVector.magnitude, sideBVector.magnitude);
        radius = Mathf.Max(MinProbeExtent, capsule.radius * radiusScale);
        float height = Mathf.Max(capsule.height * axisScale, radius * 2f);
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);

        pointA = center + axis * halfSegment;
        pointB = center - axis * halfSegment;
    }

    static void GetCurrentCapsuleProbePose(
        CapsuleCollider capsule,
        out Vector3 pointA,
        out Vector3 pointB,
        out float radius)
    {
        Vector3 center = capsule.transform.TransformPoint(capsule.center);

        Vector3 axisLocal = CapsuleAxis(capsule.direction);
        Vector3 sideALocal = CapsuleSideA(capsule.direction);
        Vector3 sideBLocal = CapsuleSideB(capsule.direction);

        Vector3 axisVector = capsule.transform.TransformVector(axisLocal);
        Vector3 sideAVector = capsule.transform.TransformVector(sideALocal);
        Vector3 sideBVector = capsule.transform.TransformVector(sideBLocal);

        Vector3 axis = SafeDirection(axisVector, capsule.transform.TransformDirection(axisLocal));
        float axisScale = Mathf.Max(MinProbeExtent, axisVector.magnitude);
        float radiusScale = Mathf.Max(sideAVector.magnitude, sideBVector.magnitude);
        radius = Mathf.Max(MinProbeExtent, capsule.radius * radiusScale);
        float height = Mathf.Max(capsule.height * axisScale, radius * 2f);
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);

        pointA = center + axis * halfSegment;
        pointB = center - axis * halfSegment;
    }

    static void GetSphereProbePose(
        ChainAttackTeleportRuntimeConfig config,
        SphereCollider sphere,
        Vector3 teleportPosition,
        Quaternion rotation,
        out Vector3 center,
        out float radius,
        out Vector3 right,
        out Vector3 forward)
    {
        Matrix4x4 rootMatrix = BuildCandidateRootMatrix(config, teleportPosition, rotation);
        center = TransformColliderPoint(config, sphere, rootMatrix, sphere.center);

        Vector3 x = TransformColliderVector(config, sphere, rootMatrix, Vector3.right);
        Vector3 y = TransformColliderVector(config, sphere, rootMatrix, Vector3.up);
        Vector3 z = TransformColliderVector(config, sphere, rootMatrix, Vector3.forward);

        right = SafeDirection(x, rotation * Vector3.right);
        forward = SafeDirection(z, rotation * Vector3.forward);
        float radiusScale = Mathf.Max(x.magnitude, Mathf.Max(y.magnitude, z.magnitude));
        radius = Mathf.Max(MinProbeExtent, sphere.radius * radiusScale);
    }

    static void GetCurrentSphereProbePose(
        SphereCollider sphere,
        out Vector3 center,
        out float radius)
    {
        center = sphere.transform.TransformPoint(sphere.center);

        Vector3 x = sphere.transform.TransformVector(Vector3.right);
        Vector3 y = sphere.transform.TransformVector(Vector3.up);
        Vector3 z = sphere.transform.TransformVector(Vector3.forward);

        float radiusScale = Mathf.Max(x.magnitude, Mathf.Max(y.magnitude, z.magnitude));
        radius = Mathf.Max(MinProbeExtent, sphere.radius * radiusScale);
    }

    static void GetBoundsProbePose(
        ChainAttackTeleportRuntimeConfig config,
        Collider probeCollider,
        Vector3 teleportPosition,
        Quaternion rotation,
        out Vector3 center,
        out Quaternion boundsRotation,
        out Vector3 halfExtents)
    {
        Matrix4x4 rootMatrix = BuildCandidateRootMatrix(config, teleportPosition, rotation);
        Transform root = ResolveProbeRoot(config);
        Bounds bounds = probeCollider.bounds;
        Vector3 rootLocalCenter = root != null
            ? root.InverseTransformPoint(bounds.center)
            : bounds.center;

        center = rootMatrix.MultiplyPoint3x4(rootLocalCenter);
        halfExtents = bounds.extents;
        boundsRotation = rotation;
    }

    static Matrix4x4 BuildCandidateRootMatrix(
        ChainAttackTeleportRuntimeConfig config,
        Vector3 teleportPosition,
        Quaternion rotation)
    {
        Transform root = ResolveProbeRoot(config);
        Vector3 scale = root != null ? root.lossyScale : Vector3.one;
        return Matrix4x4.TRS(teleportPosition, rotation, scale);
    }

    static Vector3 TransformColliderPoint(
        ChainAttackTeleportRuntimeConfig config,
        Collider probeCollider,
        Matrix4x4 rootMatrix,
        Vector3 colliderLocalPoint)
    {
        Transform root = ResolveProbeRoot(config);
        Vector3 worldPoint = probeCollider.transform.TransformPoint(colliderLocalPoint);
        Vector3 rootLocalPoint = root != null
            ? root.InverseTransformPoint(worldPoint)
            : worldPoint;

        return rootMatrix.MultiplyPoint3x4(rootLocalPoint);
    }

    static Vector3 TransformColliderVector(
        ChainAttackTeleportRuntimeConfig config,
        Collider probeCollider,
        Matrix4x4 rootMatrix,
        Vector3 colliderLocalVector)
    {
        Transform root = ResolveProbeRoot(config);
        Vector3 worldVector = probeCollider.transform.TransformVector(colliderLocalVector);
        Vector3 rootLocalVector = root != null
            ? root.InverseTransformVector(worldVector)
            : worldVector;

        return rootMatrix.MultiplyVector(rootLocalVector);
    }

    static Transform ResolveProbeRoot(ChainAttackTeleportRuntimeConfig config)
    {
        if (config.probeRoot != null)
            return config.probeRoot;

        return config.probeCollider != null ? config.probeCollider.transform : null;
    }

    static bool IsNavMeshPointValid(ChainAttackTeleportRuntimeConfig config, Vector3 point)
    {
        return NavMesh.SamplePosition(
            point,
            out _,
            Mathf.Max(0.05f, config.navMeshSampleDistance),
            NavMesh.AllAreas);
    }

    static bool HasBlockingOverlap(int hitCount, ChainAttackTeleportRuntimeConfig config)
    {
        bool bufferFilled = hitCount >= OverlapBuffer.Length;
        for (int i = 0; i < hitCount && i < OverlapBuffer.Length; i++)
        {
            Collider hit = OverlapBuffer[i];
            OverlapBuffer[i] = null;

            if (hit == null ||
                IsSelfProbeCollider(hit, config) ||
                IsAllowedOverlapCollider(hit, config))
                continue;

            Log(
                config,
                $"Blocking overlap hit '{hit.name}' layer='{LayerMask.LayerToName(hit.gameObject.layer)}' " +
                $"type={hit.GetType().Name} root='{hit.transform.root.name}'.");
            return true;
        }

        if (bufferFilled)
            Log(config, $"Overlap buffer filled with {hitCount} hits; treating pose as blocked.");

        return bufferFilled;
    }

    static bool HasCurrentProbePenetration(int hitCount, ChainAttackTeleportRuntimeConfig config)
    {
        bool bufferFilled = hitCount >= OverlapBuffer.Length;
        Collider probeCollider = config.probeCollider;
        for (int i = 0; i < hitCount && i < OverlapBuffer.Length; i++)
        {
            Collider hit = OverlapBuffer[i];
            OverlapBuffer[i] = null;

            if (hit == null ||
                IsSelfProbeCollider(hit, config) ||
                IsAllowedOverlapCollider(hit, config))
                continue;

            bool penetrates = Physics.ComputePenetration(
                probeCollider,
                probeCollider.transform.position,
                probeCollider.transform.rotation,
                hit,
                hit.transform.position,
                hit.transform.rotation,
                out Vector3 direction,
                out float distance);

            if (penetrates && distance > MinProbeExtent)
            {
                Log(
                    config,
                    $"Current probe penetration hit '{hit.name}' layer='{LayerMask.LayerToName(hit.gameObject.layer)}' " +
                    $"type={hit.GetType().Name} root='{hit.transform.root.name}' distance={distance:0.####} direction={direction}.");
                return true;
            }

            Log(
                config,
                $"Current probe bounds overlap hit '{hit.name}' layer='{LayerMask.LayerToName(hit.gameObject.layer)}' " +
                $"type={hit.GetType().Name} root='{hit.transform.root.name}' without ComputePenetration; treating pose as blocked.");
            return true;
        }

        if (bufferFilled)
            Log(config, $"Current probe overlap buffer filled with {hitCount} hits; treating pose as blocked.");

        return bufferFilled;
    }

    static bool HasBlockingCastHit(int hitCount, ChainAttackTeleportRuntimeConfig config)
    {
        bool bufferFilled = hitCount >= CastHitBuffer.Length;
        for (int i = 0; i < hitCount && i < CastHitBuffer.Length; i++)
        {
            Collider hit = CastHitBuffer[i].collider;
            CastHitBuffer[i] = default;

            if (hit == null ||
                IsSelfProbeCollider(hit, config) ||
                IsAllowedOverlapCollider(hit, config))
                continue;

            Log(
                config,
                $"Blocking anchor path hit '{hit.name}' layer='{LayerMask.LayerToName(hit.gameObject.layer)}' " +
                $"type={hit.GetType().Name} root='{hit.transform.root.name}'.");
            return true;
        }

        if (bufferFilled)
            Log(config, $"Anchor path cast buffer filled with {hitCount} hits; treating pose as blocked.");

        return bufferFilled;
    }

    static bool IsSelfProbeCollider(Collider hit, ChainAttackTeleportRuntimeConfig config)
    {
        if (hit == null)
            return false;

        if (hit == config.probeCollider)
            return true;

        Transform root = ResolveProbeRoot(config);
        return root != null && hit.transform != null && hit.transform.IsChildOf(root);
    }

    static bool IsAllowedOverlapCollider(Collider hit, ChainAttackTeleportRuntimeConfig config)
    {
        Transform allowedRoot = config.allowedOverlapRoot;
        return hit != null &&
               hit.transform != null &&
               allowedRoot != null &&
               (hit.transform == allowedRoot || hit.transform.IsChildOf(allowedRoot));
    }

    static bool HasPositiveExtents(Vector3 extents)
    {
        return extents.x > MinProbeExtent &&
               extents.y > MinProbeExtent &&
               extents.z > MinProbeExtent;
    }

    static Vector3 ResolvePlanarAxis(Vector3 axis, Vector3 fallback)
    {
        Vector3 planarAxis = Vector3.ProjectOnPlane(axis, Vector3.up);
        if (planarAxis.sqrMagnitude > 0.0001f)
            return planarAxis.normalized;

        return fallback;
    }

    static Vector3 SafeDirection(Vector3 value, Vector3 fallback)
    {
        if (value.sqrMagnitude > 0.0001f)
            return value.normalized;

        if (fallback.sqrMagnitude > 0.0001f)
            return fallback.normalized;

        return Vector3.forward;
    }

    static Quaternion BuildRotation(Vector3 forward, Vector3 up, Quaternion fallback)
    {
        if (forward.sqrMagnitude <= 0.0001f || up.sqrMagnitude <= 0.0001f)
            return fallback;

        return Quaternion.LookRotation(forward, up);
    }

    static Vector3 CapsuleAxis(int direction)
    {
        return direction switch
        {
            0 => Vector3.right,
            2 => Vector3.forward,
            _ => Vector3.up,
        };
    }

    static Vector3 CapsuleSideA(int direction)
    {
        return direction switch
        {
            0 => Vector3.up,
            2 => Vector3.right,
            _ => Vector3.right,
        };
    }

    static Vector3 CapsuleSideB(int direction)
    {
        return direction switch
        {
            0 => Vector3.forward,
            2 => Vector3.up,
            _ => Vector3.forward,
        };
    }

    static float ResolvePathProbeRadius(ChainAttackTeleportRuntimeConfig config)
    {
        float radius = config.hasClearanceProbe
            ? Mathf.Min(config.clearanceHalfExtents.x, config.clearanceHalfExtents.z)
            : 0.1f;

        Collider probeCollider = config.probeCollider;
        if (probeCollider is CapsuleCollider capsule)
            radius = Mathf.Max(radius, capsule.radius * ResolveMaxPlanarScale(capsule.transform));
        else if (probeCollider is SphereCollider sphere)
            radius = Mathf.Max(radius, sphere.radius * ResolveMaxPlanarScale(sphere.transform));
        else if (probeCollider is BoxCollider box)
            radius = Mathf.Max(radius, Mathf.Min(box.size.x, box.size.z) * 0.5f * ResolveMaxPlanarScale(box.transform));

        return Mathf.Max(0.05f, radius);
    }

    static float ResolveMaxPlanarScale(Transform transform)
    {
        if (transform == null)
            return 1f;

        Vector3 scale = transform.lossyScale;
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
    }

    static string DescribeProbe(ChainAttackTeleportRuntimeConfig config)
    {
        if (config.probeCollider == null)
            return "null";

        return $"{config.probeCollider.GetType().Name}:{config.probeCollider.name}";
    }

    static void LogNearbyColliders(ChainAttackTeleportRuntimeConfig config, Vector3 position, string reason)
    {
        if (!config.debugLogging)
            return;

        int hitCount = Physics.OverlapSphereNonAlloc(
            position + Vector3.up * Mathf.Max(0.05f, config.clearanceCenterOffset.y),
            Mathf.Max(0.25f, ResolvePathProbeRadius(config)),
            OverlapBuffer,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        int logged = 0;
        for (int i = 0; i < hitCount && i < OverlapBuffer.Length; i++)
        {
            Collider hit = OverlapBuffer[i];
            OverlapBuffer[i] = null;

            if (hit == null ||
                IsSelfProbeCollider(hit, config) ||
                IsAllowedOverlapCollider(hit, config))
                continue;

            int layerMask = 1 << hit.gameObject.layer;
            bool inObstacleMask = (config.obstacleLayers.value & layerMask) != 0;
            Log(
                config,
                $"Nearby collider at {reason}: '{hit.name}' layer='{LayerMask.LayerToName(hit.gameObject.layer)}' " +
                $"type={hit.GetType().Name} inObstacleMask={inObstacleMask}.");
            logged++;
        }

        if (logged == 0)
            Log(config, $"Nearby collider check at {reason}: no non-self colliders found.");
    }

    static void Log(ChainAttackTeleportRuntimeConfig config, string message)
    {
        if (!config.debugLogging)
            return;

        string profileName = string.IsNullOrWhiteSpace(config.debugName)
            ? "unnamed"
            : config.debugName;
        Debug.Log($"[ChainAttackTeleportUtility:{profileName}] {message}");
    }

    readonly struct ChainAttackTeleportRuntimeConfig
    {
        public readonly bool useAnchorRotationAsBase;
        public readonly bool requireNavMeshAtAnchor;
        public readonly float navMeshSampleDistance;
        public readonly Vector3 anchorPositionOffset;
        public readonly Vector3 clearanceCenterOffset;
        public readonly Vector3 clearanceHalfExtents;
        public readonly LayerMask obstacleLayers;
        public readonly QueryTriggerInteraction obstacleTriggerInteraction;
        public readonly bool hasClearanceProbe;
        public readonly Collider probeCollider;
        public readonly Transform probeRoot;
        public readonly Transform allowedOverlapRoot;
        public readonly bool debugLogging;
        public readonly string debugName;
        public bool HasProbeCollider => probeCollider != null;

        public ChainAttackTeleportRuntimeConfig(
            bool useAnchorRotationAsBase,
            bool requireNavMeshAtAnchor,
            float navMeshSampleDistance,
            Vector3 anchorPositionOffset,
            Vector3 clearanceCenterOffset,
            Vector3 clearanceHalfExtents,
            LayerMask obstacleLayers,
            QueryTriggerInteraction obstacleTriggerInteraction,
            bool hasClearanceProbe,
            Collider probeCollider,
            Transform probeRoot,
            Transform allowedOverlapRoot,
            bool debugLogging,
            string debugName)
        {
            this.useAnchorRotationAsBase = useAnchorRotationAsBase;
            this.requireNavMeshAtAnchor = requireNavMeshAtAnchor;
            this.navMeshSampleDistance = navMeshSampleDistance;
            this.anchorPositionOffset = anchorPositionOffset;
            this.clearanceCenterOffset = clearanceCenterOffset;
            this.clearanceHalfExtents = clearanceHalfExtents;
            this.obstacleLayers = obstacleLayers;
            this.obstacleTriggerInteraction = obstacleTriggerInteraction;
            this.hasClearanceProbe = hasClearanceProbe;
            this.probeCollider = probeCollider;
            this.probeRoot = probeRoot;
            this.allowedOverlapRoot = allowedOverlapRoot;
            this.debugLogging = debugLogging;
            this.debugName = debugName;
        }
    }
}
