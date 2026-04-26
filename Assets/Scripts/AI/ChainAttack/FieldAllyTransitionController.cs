using UnityEngine;
using UnityEngine.AI;

internal sealed class FieldAllyTransitionController
{
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

        if (step.enterMode == ChainActorEnterMode.InstantTeleportToTarget)
            return TryTeleportToEntryPose(execution);

        if (step.moveMode == ChainActorMoveMode.WarpToLockedTargetAnchor)
            return TryWarpToLockedTargetAnchor(step, ResolveExecutionTargetAnchor(execution));

        return true;
    }

    public bool TryTeleportToEntryPose(PendingSequenceExecution execution)
    {
        Transform targetAnchor = ResolveExecutionTargetAnchor(execution);
        if (execution == null || execution.step == null || targetAnchor == null)
            return false;

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
        return true;
    }

    public bool TryResolveEntryTeleportPose(
        ChainAttackStepDef step,
        Transform lockedTarget,
        out Vector3 teleportPosition,
        out Quaternion teleportRotation)
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
                step.warpNavMeshSampleDistance);
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
        return true;
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
}
