using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

internal static class TargetedSkillPlacementResolver
{
    static readonly float[] CandidateYawAngles =
    {
        0f,
        15f, -15f,
        30f, -30f,
        45f, -45f,
        60f, -60f,
        75f, -75f,
        90f, -90f,
        105f, -105f,
        120f, -120f,
        135f, -135f,
        150f, -150f,
        165f, -165f,
        180f,
    };

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
        out TargetedSkillPlacementResult result)
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
                    probeRoot))
            {
                result = TargetedSkillPlacementResult.Failed(
                    $"Legacy teleport placement failed for clip '{attackClip.name}'.");
                return false;
            }

            result = TargetedSkillPlacementResult.Legacy(legacyPosition, legacyRotation);
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

        for (int i = 0; i < CandidateYawAngles.Length; i++)
        {
            float yaw = CandidateYawAngles[i];
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
