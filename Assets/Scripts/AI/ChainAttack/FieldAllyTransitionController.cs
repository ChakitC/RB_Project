using UnityEngine;
using UnityEngine.AI;

internal sealed class FieldAllyTransitionController
{
    static readonly float[] AttackAnimationSampleFractions = { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 0.98f };
    static readonly float[] UtilityPostTeleportSampleFractions = { 0f, 0.33f, 0.66f, 0.98f };
    const float DuplicateSampleTimeEpsilon = 0.0005f;

    readonly FieldAllyMember owner;

    bool _visualHiddenForChainTransition;

    public FieldAllyTransitionController(FieldAllyMember owner)
    {
        this.owner = owner;
    }

    public void StartChainVisualLifecycle(bool hideOnAnimationComplete)
    {
        if (owner.ActorFaderRef == null || !owner.GameObjectRef.activeInHierarchy)
            return;

        owner.ActorFaderRef.BeginAnimationLifecycle(hideOnAnimationComplete);
        _visualHiddenForChainTransition = false;
    }

    public void HideVisualForTeleport()
    {
        if (owner.ActorFaderRef == null || !owner.GameObjectRef.activeInHierarchy)
            return;

        owner.ActorFaderRef.SetHiddenImmediate();
        _visualHiddenForChainTransition = true;
    }

    public void RevealVisualAfterTeleportIfNeeded()
    {
        if (!_visualHiddenForChainTransition)
            return;

        _visualHiddenForChainTransition = false;

        if (owner.ActorFaderRef == null || !owner.GameObjectRef.activeInHierarchy)
            return;

        owner.ActorFaderRef.BeginAnimationLifecycle(hideOnAnimationComplete: false);
    }

    public void RecoverVisibleStateAfterInterruptedExecution()
    {
        _visualHiddenForChainTransition = false;

        if (owner.ActorFaderRef == null || !owner.GameObjectRef.activeInHierarchy)
            return;

        owner.ActorFaderRef.BeginAnimationLifecycle(hideOnAnimationComplete: false);
    }

    public void ClearVisualState()
    {
        _visualHiddenForChainTransition = false;
    }

    public void FadeOutAndDeactivate()
    {
        if (owner.GameObjectRef == null || !owner.GameObjectRef.activeSelf)
            return;

        if (owner.ActorFaderRef != null)
            owner.ActorFaderRef.FadeOutThenDeactivate();
        else
            owner.GameObjectRef.SetActive(false);
    }

    public bool TryApplyEntryMovement(PendingSequenceExecution execution)
    {
        if (execution == null || execution.step == null)
            return false;

        ChainAttackStepDef step = execution.step;

        if (step.enterMode == ChainActorEnterMode.InstantTeleportToTarget ||
            (step.moveMode == ChainActorMoveMode.WarpToLockedTargetAnchor &&
             step.teleportProfile != null))
        {
            return TryTeleportToEntryPose(execution);
        }

        if (step.moveMode == ChainActorMoveMode.WarpToLockedTargetAnchor)
            return TryWarpToLockedTargetAnchor(step, ResolveExecutionTargetAnchor(execution));

        return true;
    }

    public bool TryTeleportToEntryPose(PendingSequenceExecution execution)
    {
        Transform targetAnchor = ResolveExecutionTargetAnchor(execution);
        if (execution == null || execution.step == null || targetAnchor == null)
            return false;

        if (!TryApplyTargetedPlacement(execution, targetAnchor, out bool placementHandled))
            return false;

        if (placementHandled)
            return true;

        if (execution.step.teleportProfile != null)
        {
            HideVisualForTeleport();

            bool foundSafePose = TryResolveEntryTeleportPose(
                execution.step,
                targetAnchor,
                out _,
                out _,
                (candidatePosition, candidateRotation) =>
                {
                    TeleportActorTo(candidatePosition, candidateRotation);
                    if (ValidateEntryCandidatePose(execution))
                        return true;

                    TeleportActorTo(execution.recordedOriginPosition, execution.recordedOriginRotation);
                    return false;
                });

            if (!foundSafePose)
            {
                TeleportActorTo(execution.recordedOriginPosition, execution.recordedOriginRotation);
                RevealVisualAfterTeleportIfNeeded();
                return false;
            }

            return true;
        }

        if (!TryResolveEntryTeleportPose(
                execution.step,
                targetAnchor,
                out Vector3 teleportPosition,
                out Quaternion teleportRotation))
        {
            return false;
        }

        HideVisualForTeleport();
        TeleportActorTo(teleportPosition, teleportRotation);
        if (!ValidateEntryCandidatePose(execution))
        {
            TeleportActorTo(execution.recordedOriginPosition, execution.recordedOriginRotation);
            RevealVisualAfterTeleportIfNeeded();
            return false;
        }

        return true;
    }

    bool TryApplyTargetedPlacement(
        PendingSequenceExecution execution,
        Transform targetAnchor,
        out bool handled)
    {
        handled = false;

        if (execution == null ||
            execution.step == null ||
            execution.step.teleportProfile == null ||
            execution.attackSkillDef == null ||
            owner.AnimBrainRef == null)
        {
            return true;
        }

        handled = true;
        if (execution.placementResult.IsValid)
        {
            HideVisualForTeleport();
            TeleportActorTo(
                execution.placementResult.StartPosition,
                execution.placementResult.StartRotation);
            if (execution.placementResult.UsesRootMotion ||
                ValidateEntryCandidatePose(execution))
            {
                return true;
            }

            TeleportActorTo(execution.recordedOriginPosition, execution.recordedOriginRotation);
            RevealVisualAfterTeleportIfNeeded();
            return false;
        }

        if (!owner.AnimBrainRef.TryResolveChainSkillAnimationClip(
                execution.attackSkillDef,
                out AnimationClip attackClip) ||
            attackClip == null)
        {
            owner.LogExecution(
                $"Targeted placement could not resolve the attack clip for step '{execution.step.RuntimeId}'.");
            handled = true;
            return false;
        }

        if (!owner.AnimBrainRef.TryGetRootMotionSamplingAnimator(out Animator samplingAnimator))
        {
            owner.LogExecution(
                $"Targeted placement failed for step '{execution.step.RuntimeId}': missing sampling Animator.");
            handled = true;
            return false;
        }

        bool faceTarget =
            execution.step.faceLockedTargetOnStart ||
            execution.step.faceLockedTargetOnCast;

        if (!TargetedSkillPlacementResolver.TryResolve(
                execution.step.teleportProfile,
                targetAnchor,
                owner.TransformRef.rotation,
                attackClip,
                samplingAnimator,
                execution.attackSkillDef.GetCastPointNormalized(),
                faceTarget,
                execution.step.requireNavMeshAtWarpPoint,
                execution.step.warpNavMeshSampleDistance,
                owner.ChainTeleportProbeColliderRef,
                owner.TransformRef,
                execution.lockedTarget,
                out TargetedSkillPlacementResult placementResult))
        {
            owner.LogExecution(
                $"Step '{execution.step.RuntimeId}' targeted placement failed: " +
                placementResult.FailureReason);
            return false;
        }

        HideVisualForTeleport();
        TeleportActorTo(placementResult.StartPosition, placementResult.StartRotation);
        if (!placementResult.UsesRootMotion && !ValidateEntryCandidatePose(execution))
        {
            TeleportActorTo(execution.recordedOriginPosition, execution.recordedOriginRotation);
            RevealVisualAfterTeleportIfNeeded();
            return false;
        }

        execution.placementResult = placementResult;
        owner.LogExecution(
            $"Resolved targeted entry for step '{execution.step.RuntimeId}' mode={placementResult.Mode} " +
            $"yaw={placementResult.AcceptedYaw:0.###}, start={placementResult.StartPosition}, " +
            $"impact={placementResult.ImpactPosition}.");
        return true;
    }

    public bool TryResolveEntryTeleportPose(
        ChainAttackStepDef step,
        Transform lockedTarget,
        out Vector3 teleportPosition,
        out Quaternion teleportRotation,
        System.Func<Vector3, Quaternion, bool> poseValidator = null)
    {
        teleportPosition = Vector3.zero;
        teleportRotation = Quaternion.identity;

        if (step == null || lockedTarget == null)
            return false;

        if (step.teleportProfile != null)
        {
            if (!ChainAttackTargetingUtility.TryResolveTargetAnchor(lockedTarget, out Transform anchorTransform))
                return false;

            return ChainAttackTeleportUtility.TryResolveTeleportPose(
                step.teleportProfile,
                anchorTransform,
                owner.TransformRef.rotation,
                out teleportPosition,
                out teleportRotation,
                step.requireNavMeshAtWarpPoint,
                step.warpNavMeshSampleDistance,
                owner.ChainTeleportProbeColliderRef,
                owner.TransformRef,
                poseValidator);
        }

        return TryResolveLegacyWarpPose(step, lockedTarget, out teleportPosition, out teleportRotation);
    }

    public bool TryResolveLegacyWarpPose(
        ChainAttackStepDef step,
        Transform lockedTarget,
        out Vector3 finalPosition,
        out Quaternion finalRotation)
    {
        finalPosition = Vector3.zero;
        finalRotation = Quaternion.identity;

        if (step == null || lockedTarget == null)
            return false;

        if (!ChainAttackTargetingUtility.TryResolveTargetAnchor(lockedTarget, out Transform anchorTransform))
            return false;

        Quaternion baseRotation = step.useTargetAnchorRotation
            ? anchorTransform.rotation
            : owner.TransformRef.rotation;
        finalRotation = Quaternion.AngleAxis(step.warpYawOffset, Vector3.up) * baseRotation;
        finalPosition = anchorTransform.TransformPoint(step.warpOffset);

        if (step.requireNavMeshAtWarpPoint)
        {
            if (!NavMesh.SamplePosition(
                    finalPosition,
                    out NavMeshHit navHit,
                    Mathf.Max(0.05f, step.warpNavMeshSampleDistance),
                    NavMesh.AllAreas))
            {
                return false;
            }

            finalPosition = navHit.position;
        }

        return true;
    }

    public bool CanAttackLockedTarget(PendingSequenceExecution execution)
    {
        if (execution == null || execution.step == null)
            return false;

        Transform targetTransform = execution.lockedTarget != null
            ? execution.lockedTarget
            : execution.lockedTargetAnchor;

        if (targetTransform == null)
            return !execution.step.skipIfTargetMissing;

        return ChainAttackTargetingUtility.IsTargetAlive(targetTransform);
    }

    public bool TryWarpToLockedTargetAnchor(ChainAttackStepDef step, Transform lockedTarget)
    {
        if (!TryResolveLegacyWarpPose(step, lockedTarget, out Vector3 finalPosition, out Quaternion finalRotation))
            return false;

        HideVisualForTeleport();
        TeleportActorTo(finalPosition, finalRotation);
        if (!IsCurrentProbeClearAfterTeleport(step))
            return false;

        return true;
    }

    bool ValidateEntryCandidatePose(PendingSequenceExecution execution)
    {
        if (execution == null || execution.step == null)
            return false;

        if (!IsCurrentProbeClearAfterTeleport(execution.step))
            return false;

        return ValidateEntryAnimationSamples(execution);
    }

    bool ValidateEntryAnimationSamples(PendingSequenceExecution execution)
    {
        ChainAttackStepDef step = execution != null ? execution.step : null;
        ChainAttackTeleportProfileDef profile = step != null ? step.teleportProfile : null;
        if (step == null || profile == null || profile.obstacleLayers.value == 0)
            return true;

        CharacterAnimBrain animBrain = owner.AnimBrainRef;
        if (animBrain == null)
        {
            LogAnimationValidation(profile, "Skipped animation pose validation because AnimBrain is missing.");
            return true;
        }

        if (!animBrain.TryGetAnimationSamplingRoot(out GameObject sampleRoot) || sampleRoot == null)
        {
            LogAnimationValidation(profile, "Skipped animation pose validation because no Animator sample root was available.");
            return true;
        }

        if (ShouldValidateUtilityWarpOutTail(execution) &&
            animBrain.TryResolveChainUtilityWarpOutAnimationClip(
                out AnimationClip utilityWarpOutClip,
                out float utilityCastPointNormalized))
        {
            float utilityEndNormalized = utilityCastPointNormalized < 0.98f ? 0.98f : 1f;
            if (!ValidateAnimationClipSamples(
                    step,
                    sampleRoot,
                    utilityWarpOutClip,
                    "utility warp-out tail",
                    utilityCastPointNormalized,
                    utilityEndNormalized,
                    UtilityPostTeleportSampleFractions))
            {
                return false;
            }
        }

        if (!animBrain.TryResolveChainSkillAnimationClip(execution.attackSkillDef, out AnimationClip attackClip))
        {
            LogAnimationValidation(profile, "Skipped attack animation pose validation because no chain skill clip was resolved.");
            return true;
        }

        return ValidateAnimationClipSamples(
            step,
            sampleRoot,
            attackClip,
            "attack skill",
            0f,
            0.98f,
            AttackAnimationSampleFractions);
    }

    bool ValidateAnimationClipSamples(
        ChainAttackStepDef step,
        GameObject sampleRoot,
        AnimationClip clip,
        string label,
        float startNormalized,
        float endNormalized,
        float[] sampleFractions)
    {
        ChainAttackTeleportProfileDef profile = step != null ? step.teleportProfile : null;
        if (step == null || sampleRoot == null || clip == null || sampleFractions == null || sampleFractions.Length == 0)
            return true;

        if (clip.length <= 0.0001f)
            return true;

        TransformPoseSnapshot[] snapshots = CaptureTransformHierarchy(sampleRoot.transform);
        float start = Mathf.Clamp01(startNormalized);
        float end = Mathf.Clamp01(endNormalized);
        if (end < start)
            end = start;

        float previousSampleTime = -1f;

        try
        {
            for (int i = 0; i < sampleFractions.Length; i++)
            {
                float normalized = Mathf.Lerp(start, end, Mathf.Clamp01(sampleFractions[i]));
                float sampleTime = Mathf.Clamp(normalized * clip.length, 0f, clip.length);
                if (previousSampleTime >= 0f &&
                    Mathf.Abs(sampleTime - previousSampleTime) <= DuplicateSampleTimeEpsilon)
                {
                    continue;
                }

                previousSampleTime = sampleTime;
                clip.SampleAnimation(sampleRoot, sampleTime);
                Physics.SyncTransforms();

                if (IsCurrentProbeClearAfterTeleport(step))
                    continue;

                LogAnimationValidation(
                    profile,
                    $"Rejected {label} sample for clip '{clip.name}' at normalized={normalized:0.###} time={sampleTime:0.###} because the probe overlaps obstacle layers.");
                return false;
            }
        }
        finally
        {
            RestoreTransformHierarchy(snapshots);
            Physics.SyncTransforms();
        }

        LogAnimationValidation(profile, $"Animation pose validation passed for {label} clip '{clip.name}'.");
        return true;
    }

    bool ShouldValidateUtilityWarpOutTail(PendingSequenceExecution execution)
    {
        return execution != null &&
               execution.step != null &&
               execution.step.enterMode == ChainActorEnterMode.UtilityWarpInToTarget &&
               owner.AnimBrainRef != null &&
               owner.AnimBrainRef.IsChainUtilityPlaybackActive;
    }

    void LogAnimationValidation(ChainAttackTeleportProfileDef profile, string message)
    {
        if (profile == null || !profile.debugLogging)
            return;

        Debug.Log($"[ChainAttackAnimationPoseValidator:{profile.name}] {message}", owner.GameObjectRef);
    }

    public void TeleportActorTo(Vector3 worldPosition, Quaternion worldRotation)
    {
        ApplyActorPose(worldPosition, worldRotation);

        if (owner.AgentRef == null || !owner.AgentRef.enabled)
            return;

        if (owner.AgentRef.isOnNavMesh)
        {
            owner.AgentRef.nextPosition = worldPosition;
            return;
        }

        if (NavMesh.SamplePosition(worldPosition, out NavMeshHit navHit, 1f, NavMesh.AllAreas))
        {
            owner.AgentRef.Warp(navHit.position);
            ApplyActorPose(navHit.position, worldRotation);
            owner.AgentRef.nextPosition = navHit.position;
        }
    }

    public void SyncAgentToTransform()
    {
        if (owner.AgentRef == null || !owner.AgentRef.enabled)
            return;

        Vector3 syncPosition = owner.TransformRef.position;

        if (owner.AgentRef.isOnNavMesh)
        {
            owner.AgentRef.Warp(syncPosition);
            owner.AgentRef.nextPosition = syncPosition;
            return;
        }

        if (NavMesh.SamplePosition(syncPosition, out NavMeshHit navHit, 1f, NavMesh.AllAreas))
        {
            syncPosition = navHit.position;
            ApplyActorPose(navHit.position, owner.TransformRef.rotation);
            owner.AgentRef.Warp(navHit.position);
            owner.AgentRef.nextPosition = navHit.position;
        }
    }

    void ApplyActorPose(Vector3 worldPosition, Quaternion worldRotation)
    {
        Transform actorTransform = owner.TransformRef;
        if (actorTransform == null)
            return;

        owner.RefreshCollisionReferences();

        CharacterController characterController = owner.ActorCharacterControllerRef;
        bool restoreCharacterController = characterController != null && characterController.enabled;
        if (restoreCharacterController)
            characterController.enabled = false;

        Rigidbody rigidbody = owner.ActorRigidbodyRef;
        if (rigidbody != null)
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.position = worldPosition;
            rigidbody.rotation = worldRotation;
        }

        actorTransform.SetPositionAndRotation(worldPosition, worldRotation);
        Physics.SyncTransforms();

        if (restoreCharacterController && characterController != null)
            characterController.enabled = true;
    }

    public void FaceTarget(Transform lockedTarget)
    {
        if (lockedTarget == null)
            return;

        Transform targetAnchor = lockedTarget;
        if (ChainAttackTargetingUtility.TryResolveTargetAnchor(lockedTarget, out Transform anchorTransform))
            targetAnchor = anchorTransform;

        Transform origin = owner.SkillUserProxyRef != null && owner.SkillUserProxyRef.CastOrigin != null
            ? owner.SkillUserProxyRef.CastOrigin
            : owner.TransformRef;
        Vector3 lookDirection = targetAnchor.position - origin.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude <= 0.001f)
            return;

        ApplyActorPose(owner.TransformRef.position, Quaternion.LookRotation(lookDirection.normalized, Vector3.up));

        if (owner.AgentRef != null && owner.AgentRef.enabled && owner.AgentRef.isOnNavMesh)
            owner.AgentRef.nextPosition = owner.TransformRef.position;
    }

    public void FaceTarget(PendingSequenceExecution execution)
    {
        FaceTarget(ResolveExecutionTargetAnchor(execution));
    }

    static Transform ResolveExecutionTargetAnchor(PendingSequenceExecution execution)
    {
        if (execution == null)
            return null;

        if (execution.lockedTargetAnchor != null)
            return execution.lockedTargetAnchor;

        if (ChainAttackTargetingUtility.TryResolveTargetAnchor(execution.lockedTarget, out Transform anchorTransform))
            return anchorTransform;

        return execution.lockedTarget;
    }

    public static bool ShouldAutoHideNearAttackEnd(ChainActorExitMode exitMode)
    {
        switch (exitMode)
        {
            case ChainActorExitMode.ReturnToRecordedOrigin:
            case ChainActorExitMode.ReturnToRecordedOriginViaUtility:
            case ChainActorExitMode.ReturnToRecordedOriginThenWarpIn:
            case ChainActorExitMode.ReturnToRecordedOriginThenWarpInOnSequenceEnd:
            case ChainActorExitMode.FadeOutAndDeactivate:
                return true;

            default:
                return false;
        }
    }

    bool IsCurrentProbeClearAfterTeleport(ChainAttackStepDef step)
    {
        ChainAttackTeleportProfileDef profile = step != null ? step.teleportProfile : null;
        if (profile == null || profile.obstacleLayers.value == 0)
            return true;

        bool clear = ChainAttackTeleportUtility.IsCurrentProbeColliderClear(
            owner.ChainTeleportProbeColliderRef,
            owner.TransformRef,
            profile.obstacleLayers,
            profile.obstacleTriggerInteraction,
            profile.debugLogging,
            profile.name);

        if (!clear)
            owner.LogExecution($"Teleport pose rejected after applying actor pose because current probe overlaps obstacle layers for step '{step.RuntimeId}'.");

        return clear;
    }

    static TransformPoseSnapshot[] CaptureTransformHierarchy(Transform root)
    {
        if (root == null)
            return new TransformPoseSnapshot[0];

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        TransformPoseSnapshot[] snapshots = new TransformPoseSnapshot[transforms.Length];
        for (int i = 0; i < transforms.Length; i++)
            snapshots[i] = new TransformPoseSnapshot(transforms[i]);

        return snapshots;
    }

    static void RestoreTransformHierarchy(TransformPoseSnapshot[] snapshots)
    {
        if (snapshots == null)
            return;

        for (int i = 0; i < snapshots.Length; i++)
            snapshots[i].Restore();
    }

    readonly struct TransformPoseSnapshot
    {
        readonly Transform transform;
        readonly Vector3 localPosition;
        readonly Quaternion localRotation;
        readonly Vector3 localScale;

        public TransformPoseSnapshot(Transform transform)
        {
            this.transform = transform;
            localPosition = transform != null ? transform.localPosition : Vector3.zero;
            localRotation = transform != null ? transform.localRotation : Quaternion.identity;
            localScale = transform != null ? transform.localScale : Vector3.one;
        }

        public void Restore()
        {
            if (transform == null)
                return;

            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            transform.localScale = localScale;
        }
    }
}
