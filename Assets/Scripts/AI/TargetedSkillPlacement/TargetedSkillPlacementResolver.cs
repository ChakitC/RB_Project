using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

internal static class TargetedSkillPlacementResolver
{
    public static bool TryResolve(
        ChainAttackTeleportProfileDef profile,
        Transform targetAnchor,
        Quaternion fallbackBaseRotation,
        AnimationClip attackClip,
        Animator samplingAnimator,
        float impactNormalized,
        bool faceTarget,
        bool requireNavMesh,
        float navMeshSampleDistance,
        Collider probeCollider,
        Transform probeRoot,
        Transform targetRoot,
        out TargetedSkillPlacementResult result,
        Vector3? preferredActorPosition = null,
        float noWarpStartDistance = 0f,
        float noWarpTargetDistance = 0f)
    {
        if (profile == null)
        {
            result = TargetedSkillPlacementResult.Failed("Teleport profile is missing.");
            return false;
        }

        if (targetAnchor == null)
        {
            result = TargetedSkillPlacementResult.Failed("Target anchor is missing.");
            return false;
        }

        if (attackClip == null)
        {
            result = TargetedSkillPlacementResult.Failed("Attack AnimationClip is missing.");
            return false;
        }

        if (samplingAnimator == null)
        {
            result = TargetedSkillPlacementResult.Failed("Root-motion sampling Animator is missing.");
            return false;
        }

        if (!TargetedSkillRootMotionTrajectoryCache.TryGet(
                attackClip,
                samplingAnimator,
                out TargetedSkillRootMotionTrajectory trajectory,
                out string samplingFailureReason))
        {
            result = TargetedSkillPlacementResult.Failed(
                $"Root-motion sampling failed for clip '{attackClip.name}': {samplingFailureReason}");
            return false;
        }

        if (trajectory == null || !trajectory.HasMeaningfulMotion)
        {
            if (!ChainAttackTeleportUtility.TryResolveTeleportPose(
                    profile,
                    targetAnchor,
                    fallbackBaseRotation,
                    out Vector3 legacyPosition,
                    out Quaternion legacyRotation,
                    requireNavMesh,
                    navMeshSampleDistance,
                    probeCollider,
                    probeRoot,
                    preferredActorPosition: preferredActorPosition))
            {
                result = TargetedSkillPlacementResult.Failed(
                    $"Legacy teleport placement failed for clip '{attackClip.name}'.");
                return false;
            }

            result = TargetedSkillPlacementResult.Legacy(legacyPosition, legacyRotation);
            if (TryBuildNoWarpLegacy(
                    profile, legacyPosition, legacyRotation, preferredActorPosition,
                    noWarpStartDistance, requireNavMesh, navMeshSampleDistance,
                    probeCollider, probeRoot, out TargetedSkillPlacementResult noWarpLegacy))
            {
                result = noWarpLegacy;
            }
            else if (TryBuildNoWarpInPlace(
                    profile, targetAnchor, preferredActorPosition, noWarpTargetDistance,
                    requireNavMesh, navMeshSampleDistance, probeCollider, probeRoot, targetRoot,
                    out TargetedSkillPlacementResult inPlaceLegacy))
            {
                result = inPlaceLegacy;
            }
            return true;
        }

        if (!TryResolveRootMotion(
                profile,
                targetAnchor,
                fallbackBaseRotation,
                trajectory,
                Mathf.Clamp(impactNormalized, 0f, 0.999f),
                faceTarget,
                requireNavMesh,
                navMeshSampleDistance,
                probeCollider,
                probeRoot,
                targetRoot,
                preferredActorPosition,
                out Vector3 startPosition,
                out Quaternion startRotation,
                out Vector3 impactPosition,
                out Quaternion impactRotation,
                out float acceptedYaw,
                out string placementFailureReason))
        {
            result = TargetedSkillPlacementResult.Failed(
                $"Root-motion placement failed for clip '{attackClip.name}': {placementFailureReason}");
            return false;
        }

        result = TargetedSkillPlacementResult.RootMotion(
            startPosition,
            startRotation,
            impactPosition,
            impactRotation,
            acceptedYaw);
        if (TryBuildNoWarpRootMotion(
                profile, trajectory, Mathf.Clamp(impactNormalized, 0f, 0.999f),
                startPosition, startRotation, acceptedYaw, preferredActorPosition,
                noWarpStartDistance, requireNavMesh, navMeshSampleDistance,
                probeCollider, probeRoot, targetRoot,
                out TargetedSkillPlacementResult noWarpRoot))
        {
            result = noWarpRoot;
        }
        else if (TryBuildNoWarpInPlace(
                profile, targetAnchor, preferredActorPosition, noWarpTargetDistance,
                requireNavMesh, navMeshSampleDistance, probeCollider, probeRoot, targetRoot,
                out TargetedSkillPlacementResult inPlaceRoot))
        {
            result = inPlaceRoot;
        }
        return true;
    }

    static bool TryResolveRootMotion(
        ChainAttackTeleportProfileDef profile,
        Transform anchorTransform,
        Quaternion fallbackBaseRotation,
        TargetedSkillRootMotionTrajectory trajectory,
        float impactNormalized,
        bool faceTarget,
        bool requireNavMesh,
        float navMeshSampleDistance,
        Collider probeCollider,
        Transform probeRoot,
        Transform targetRoot,
        Vector3? preferredActorPosition,
        out Vector3 startPosition,
        out Quaternion startRotation,
        out Vector3 impactPosition,
        out Quaternion impactRotation,
        out float acceptedYaw,
        out string failureReason)
    {
        startPosition = Vector3.zero;
        startRotation = Quaternion.identity;
        impactPosition = Vector3.zero;
        impactRotation = Quaternion.identity;
        acceptedYaw = 0f;
        failureReason = null;

        if (trajectory == null ||
            !trajectory.HasSamples ||
            !trajectory.TrySample(impactNormalized, out TargetedSkillRootMotionSample impactSample))
        {
            failureReason = "Sampled impact pose is invalid.";
            return false;
        }

        Quaternion baseRotation = profile.useAnchorRotationAsBase
            ? PlanarRotation(anchorTransform.rotation)
            : PlanarRotation(fallbackBaseRotation);
        string lastRejectionReason = null;

        float baseYaw = TargetedSkillSnapSidePriority.ResolveBaseYaw(
            anchorTransform, profile.anchorPositionOffset, preferredActorPosition);

        float[] offsets = TargetedSkillSnapSidePriority.CandidateYawOffsets;
        for (int i = 0; i < offsets.Length; i++)
        {
            float yaw = baseYaw + offsets[i];
            Quaternion candidateYaw = Quaternion.AngleAxis(yaw, Vector3.up);
            Quaternion candidateImpactRotation = candidateYaw * baseRotation;
            Vector3 localOffset = candidateYaw * profile.anchorPositionOffset;
            Vector3 candidateImpactPosition = anchorTransform.TransformPoint(localOffset);

            if (faceTarget)
            {
                Vector3 lookDirection = anchorTransform.position - candidateImpactPosition;
                lookDirection.y = 0f;
                if (lookDirection.sqrMagnitude > 0.0001f)
                    candidateImpactRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }

            if (requireNavMesh)
            {
                if (!NavMesh.SamplePosition(
                        candidateImpactPosition,
                        out NavMeshHit impactNavHit,
                        Mathf.Max(0.05f, navMeshSampleDistance),
                        NavMesh.AllAreas))
                {
                    lastRejectionReason = $"yaw={yaw:0.###}: impact pose is off NavMesh";
                    Log(profile, $"Rejected yaw={yaw:0.###}: impact pose is off NavMesh.");
                    continue;
                }

                candidateImpactPosition = impactNavHit.position;
            }

            Quaternion impactLocalRotation = Quaternion.Euler(0f, impactSample.localYaw, 0f);
            Quaternion candidateStartRotation =
                candidateImpactRotation * Quaternion.Inverse(impactLocalRotation);
            Vector3 candidateStartPosition =
                candidateImpactPosition - candidateStartRotation * impactSample.localPosition;

            if (!ValidateTrajectory(
                    profile,
                    trajectory,
                    candidateStartPosition,
                    candidateStartRotation,
                    impactNormalized,
                    candidateImpactPosition,
                    candidateImpactRotation,
                    requireNavMesh,
                    navMeshSampleDistance,
                    probeCollider,
                    probeRoot,
                    targetRoot,
                    yaw,
                    out string rejectionReason))
            {
                lastRejectionReason = rejectionReason;
                continue;
            }

            startPosition = candidateStartPosition;
            startRotation = candidateStartRotation;
            impactPosition = candidateImpactPosition;
            impactRotation = candidateImpactRotation;
            acceptedYaw = yaw;
            Log(
                profile,
                $"Accepted yaw={yaw:0.###}: start={startPosition}, impact={impactPosition}, " +
                $"impactN={impactNormalized:0.###}.");
            return true;
        }

        failureReason = lastRejectionReason ?? "Every 15-degree candidate was blocked.";
        Log(profile, $"Root-motion placement failed: {failureReason}.");
        return false;
    }

    static bool ValidateTrajectory(
        ChainAttackTeleportProfileDef profile,
        TargetedSkillRootMotionTrajectory trajectory,
        Vector3 startPosition,
        Quaternion startRotation,
        float impactNormalized,
        Vector3 impactPosition,
        Quaternion impactRotation,
        bool requireNavMesh,
        float navMeshSampleDistance,
        Collider probeCollider,
        Transform probeRoot,
        Transform targetRoot,
        float yaw,
        out string rejectionReason)
    {
        rejectionReason = null;

        if (!ChainAttackTeleportUtility.IsProbePoseClear(
                profile,
                startPosition,
                startRotation,
                probeCollider,
                probeRoot))
        {
            rejectionReason = $"yaw={yaw:0.###}: start pose overlaps an obstacle";
            return false;
        }

        if (!ChainAttackTeleportUtility.IsProbePoseClear(
                profile,
                impactPosition,
                impactRotation,
                probeCollider,
                probeRoot,
                targetRoot))
        {
            rejectionReason = $"yaw={yaw:0.###}: impact pose overlaps a non-target obstacle";
            return false;
        }

        if (requireNavMesh)
        {
            if (!ChainAttackTeleportUtility.IsProbePoseOnNavMesh(
                    profile,
                    startPosition,
                    startRotation,
                    probeCollider,
                    probeRoot,
                    navMeshSampleDistance))
            {
                rejectionReason = $"yaw={yaw:0.###}: start pose footprint is off NavMesh";
                return false;
            }

            if (!ChainAttackTeleportUtility.IsProbePoseOnNavMesh(
                    profile,
                    impactPosition,
                    impactRotation,
                    probeCollider,
                    probeRoot,
                    navMeshSampleDistance))
            {
                rejectionReason = $"yaw={yaw:0.###}: impact pose footprint is off NavMesh";
                return false;
            }
        }

        IReadOnlyList<TargetedSkillRootMotionSample> samples = trajectory.Samples;
        Vector3 previousPosition = startPosition;
        Quaternion previousRotation = startRotation;
        float previousNormalizedTime = 0f;

        for (int i = 0; i < samples.Count; i++)
        {
            TargetedSkillRootMotionSample sample = samples[i];
            Vector3 worldPosition = startPosition + startRotation * sample.localPosition;
            Quaternion worldRotation =
                startRotation * Quaternion.Euler(0f, sample.localYaw, 0f);
            bool segmentContainsImpact =
                previousNormalizedTime < impactNormalized &&
                sample.normalizedTime >= impactNormalized;
            Transform allowedTargetRoot = segmentContainsImpact ? targetRoot : null;

            if (!ChainAttackTeleportUtility.IsProbePoseClear(
                    profile,
                    worldPosition,
                    worldRotation,
                    probeCollider,
                    probeRoot,
                    allowedTargetRoot))
            {
                rejectionReason =
                    $"yaw={yaw:0.###}: obstacle overlap at trajectory time {sample.normalizedTime:0.###}";
                return false;
            }

            if (requireNavMesh &&
                !ChainAttackTeleportUtility.IsProbePoseOnNavMesh(
                    profile,
                    worldPosition,
                    worldRotation,
                    probeCollider,
                    probeRoot,
                    navMeshSampleDistance))
            {
                rejectionReason =
                    $"yaw={yaw:0.###}: trajectory footprint is off NavMesh at {sample.normalizedTime:0.###}";
                return false;
            }

            if (i > 0 &&
                !ChainAttackTeleportUtility.IsProbeSweepClear(
                    profile,
                    previousPosition,
                    previousRotation,
                    worldPosition,
                    worldRotation,
                    probeCollider,
                    probeRoot,
                    allowedTargetRoot))
            {
                rejectionReason =
                    $"yaw={yaw:0.###}: obstacle blocks trajectory segment ending at {sample.normalizedTime:0.###}";
                return false;
            }

            previousPosition = worldPosition;
            previousRotation = worldRotation;
            previousNormalizedTime = sample.normalizedTime;
        }

        if (trajectory.TrySample(
                impactNormalized,
                out TargetedSkillRootMotionSample resolvedImpactSample))
        {
            Vector3 resolvedImpactPosition =
                startPosition + startRotation * resolvedImpactSample.localPosition;
            Quaternion resolvedImpactRotation =
                startRotation * Quaternion.Euler(0f, resolvedImpactSample.localYaw, 0f);

            if ((resolvedImpactPosition - impactPosition).sqrMagnitude > 0.0025f ||
                Mathf.Abs(Mathf.DeltaAngle(
                    resolvedImpactRotation.eulerAngles.y,
                    impactRotation.eulerAngles.y)) > 1f)
            {
                rejectionReason = $"yaw={yaw:0.###}: computed impact pose does not match target pose";
                return false;
            }
        }

        return true;
    }

    static bool TryBuildNoWarpRootMotion(
        ChainAttackTeleportProfileDef profile,
        TargetedSkillRootMotionTrajectory trajectory,
        float impactNormalized,
        Vector3 idealStartPosition,
        Quaternion startRotation,
        float acceptedYaw,
        Vector3? preferredActorPosition,
        float noWarpStartDistance,
        bool requireNavMesh,
        float navMeshSampleDistance,
        Collider probeCollider,
        Transform probeRoot,
        Transform targetRoot,
        out TargetedSkillPlacementResult result)
    {
        result = default;

        if (noWarpStartDistance <= 0f || !preferredActorPosition.HasValue)
            return false;

        Vector3 currentStart = preferredActorPosition.Value;
        if (PlanarDistance(currentStart, idealStartPosition) > noWarpStartDistance)
            return false;

        if (trajectory == null ||
            !trajectory.TrySample(impactNormalized, out TargetedSkillRootMotionSample impactSample))
            return false;

        Vector3 currentImpactPosition = currentStart + startRotation * impactSample.localPosition;
        Quaternion currentImpactRotation = startRotation * Quaternion.Euler(0f, impactSample.localYaw, 0f);

        if (!ValidateTrajectory(
                profile, trajectory, currentStart, startRotation, impactNormalized,
                currentImpactPosition, currentImpactRotation,
                requireNavMesh, navMeshSampleDistance, probeCollider, probeRoot, targetRoot,
                acceptedYaw, out string rejectionReason))
        {
            Log(profile, $"No-warp current pose rejected: {rejectionReason}.");
            return false;
        }

        result = TargetedSkillPlacementResult.RootMotion(
            currentStart, startRotation, currentImpactPosition, currentImpactRotation, acceptedYaw,
            requiresPositionSnap: false);
        Log(profile, $"No-warp current pose accepted: start={currentStart} impact={currentImpactPosition}.");
        return true;
    }

    static bool TryBuildNoWarpLegacy(
        ChainAttackTeleportProfileDef profile,
        Vector3 legacyPosition,
        Quaternion legacyRotation,
        Vector3? preferredActorPosition,
        float noWarpStartDistance,
        bool requireNavMesh,
        float navMeshSampleDistance,
        Collider probeCollider,
        Transform probeRoot,
        out TargetedSkillPlacementResult result)
    {
        result = default;

        if (noWarpStartDistance <= 0f || !preferredActorPosition.HasValue)
            return false;

        Vector3 currentPos = preferredActorPosition.Value;
        if (PlanarDistance(currentPos, legacyPosition) > noWarpStartDistance)
            return false;

        if (!ChainAttackTeleportUtility.IsProbePoseClear(
                profile, currentPos, legacyRotation, probeCollider, probeRoot))
            return false;

        if (requireNavMesh &&
            !ChainAttackTeleportUtility.IsProbePoseOnNavMesh(
                profile, currentPos, legacyRotation, probeCollider, probeRoot, navMeshSampleDistance))
            return false;

        result = TargetedSkillPlacementResult.Legacy(currentPos, legacyRotation, requiresPositionSnap: false);
        return true;
    }

    static bool TryBuildNoWarpInPlace(
        ChainAttackTeleportProfileDef profile,
        Transform anchorTransform,
        Vector3? preferredActorPosition,
        float noWarpTargetDistance,
        bool requireNavMesh,
        float navMeshSampleDistance,
        Collider probeCollider,
        Transform probeRoot,
        Transform targetRoot,
        out TargetedSkillPlacementResult result)
    {
        result = default;

        if (noWarpTargetDistance <= 0f || !preferredActorPosition.HasValue || anchorTransform == null)
            return false;

        Vector3 currentPos = preferredActorPosition.Value;
        if (PlanarDistance(currentPos, anchorTransform.position) > noWarpTargetDistance)
            return false;

        Vector3 lookDir = anchorTransform.position - currentPos;
        lookDir.y = 0f;
        Quaternion rotation = lookDir.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(lookDir.normalized, Vector3.up)
            : (probeRoot != null ? PlanarRotation(probeRoot.rotation) : Quaternion.identity);

        if (!ChainAttackTeleportUtility.IsProbePoseClear(
                profile, currentPos, rotation, probeCollider, probeRoot, targetRoot))
            return false;

        if (requireNavMesh &&
            !ChainAttackTeleportUtility.IsProbePoseOnNavMesh(
                profile, currentPos, rotation, probeCollider, probeRoot, navMeshSampleDistance))
            return false;

        result = TargetedSkillPlacementResult.Legacy(currentPos, rotation, requiresPositionSnap: false);
        Log(profile, $"No-warp in-place (near target) accepted: pos={currentPos}.");
        return true;
    }

    static float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    static Quaternion PlanarRotation(Quaternion rotation)
    {
        return Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
    }

    static void Log(ChainAttackTeleportProfileDef profile, string message)
    {
        if (profile == null || !profile.debugLogging)
            return;

        Debug.Log($"[TargetedSkillPlacement:{profile.name}] {message}");
    }
}
