using System.Collections.Generic;
using UnityEngine;

internal sealed class FieldAllySequenceRunner
{
    readonly FieldAllyMember owner;
    readonly FieldAllyAutonomyScope autonomyScope;
    readonly FieldAllyTransitionController transitionController;
    readonly ChainSkillCastBridge skillCastBridge;

    PendingSequenceExecution _pendingExecution;
    DeferredSequenceCleanup _deferredCleanup;
    object _reservationOwner;
    int _nextRequestId = 1;
    int _nextExecutionId = 1;
    int _lastCompletedExecutionId;
    bool _lastCompletedExecutionSucceeded;
    bool _lastCompletedExecutionHadDeferredCleanup;

    public FieldAllySequenceRunner(
        FieldAllyMember owner,
        FieldAllyAutonomyScope autonomyScope,
        FieldAllyTransitionController transitionController,
        ChainSkillCastBridge skillCastBridge)
    {
        this.owner = owner;
        this.autonomyScope = autonomyScope;
        this.transitionController = transitionController;
        this.skillCastBridge = skillCastBridge;
    }

    public bool IsReserved => _reservationOwner != null;
    public bool HasActiveSequenceExecution => _pendingExecution != null;
    public bool HasDeferredSequenceCleanup => _deferredCleanup != null;
    public int ActiveSequenceExecutionId => _pendingExecution != null ? _pendingExecution.executionId : 0;
    public bool LastExecutionSucceeded { get; private set; }
    public bool IsBusy =>
        _pendingExecution != null ||
        _deferredCleanup != null ||
        (owner.AnimBrainRef != null && owner.AnimBrainRef.IsExclusiveLocomotionActive);

    public void Tick()
    {
        ProcessDeferredExecutionPhase();
    }

    public void ResetOnDisable()
    {
        CancelDeferredSequenceCleanup();
        CleanupActiveExecution(success: false);
        _reservationOwner = null;
        transitionController.ClearVisualState();
    }

    public bool TryReserve(object ownerToken)
    {
        if (ownerToken == null)
            return false;

        if (_reservationOwner != null && !ReferenceEquals(_reservationOwner, ownerToken))
            return false;

        _reservationOwner = ownerToken;
        return true;
    }

    public void ReleaseReservation(object ownerToken)
    {
        if (ownerToken == null || !ReferenceEquals(_reservationOwner, ownerToken))
            return;

        _reservationOwner = null;
    }

    public bool IsSequenceExecutionReadyToContinue(int executionId)
    {
        if (executionId <= 0)
            return false;

        if (_pendingExecution != null && _pendingExecution.executionId == executionId)
            return _pendingExecution.continueReleased;

        return _lastCompletedExecutionId == executionId;
    }

    public bool TryGetCompletedSequenceExecutionResult(int executionId, out bool success, out bool hasDeferredCleanup)
    {
        if (executionId > 0 && _lastCompletedExecutionId == executionId)
        {
            success = _lastCompletedExecutionSucceeded;
            hasDeferredCleanup = _lastCompletedExecutionHadDeferredCleanup;
            return true;
        }

        success = false;
        hasDeferredCleanup = false;
        return false;
    }

    public bool TryStartSequenceStep(ChainAttackStepDef step, Transform lockedTarget)
    {
        if (step == null)
            return false;

        owner.CacheReferences();

        if (owner.AnimBrainRef == null || _reservationOwner == null)
            return false;

        if (step.requireActorAlive && !owner.IsAliveActor)
            return false;

        if (owner.IsActorInKnockback())
        {
            owner.LogExecution($"Step '{step.RuntimeId}' cannot start because actor '{owner.ActorName}' is in knockback.");
            return false;
        }

        if (IsBusy)
            return false;

        if (lockedTarget == null && step.skipIfTargetMissing)
            return false;

        LastExecutionSucceeded = false;
        autonomyScope.Apply();
        skillCastBridge.ClearAimOverrides();

        if (!skillCastBridge.TryResolveAttackSkill(
                step,
                out SkillInstance resolvedRuntimeSkill,
                out SkillGemDefinition resolvedSkillDef,
                out int resolvedSkillLevel))
        {
            owner.LogExecution($"Step '{step.RuntimeId}' cannot start because no chain skill is configured for actor '{owner.ActorName}'.");
            autonomyScope.Restore();
            return false;
        }

        PendingSequenceExecution execution = new()
        {
            executionId = NextExecutionId(),
            owner = _reservationOwner,
            step = step,
            lockedTarget = lockedTarget,
            attackRuntimeSkill = resolvedRuntimeSkill,
            attackSkillDef = resolvedSkillDef,
            attackSkillLevel = resolvedSkillLevel,
            ignoreResourceCosts = step.ignoreResourceCosts,
            recordedOriginPosition = owner.TransformRef.position,
            recordedOriginRotation = owner.TransformRef.rotation,
            phase = SequenceExecutionPhase.None,
        };

        _pendingExecution = execution;
        skillCastBridge.ApplyChainAimTargetOverride(execution.lockedTarget);

        if (step.enterMode == ChainActorEnterMode.UtilityWarpInToTarget)
            return TryStartEnterUtility(execution);

        if (!transitionController.TryApplyEntryMovement(execution))
        {
            CleanupActiveExecution(success: false);
            return false;
        }

        return TryStartAttack(execution);
    }

    public void FinalizeSequenceParticipation(object ownerToken, bool interrupted)
    {
        if (ownerToken == null || _deferredCleanup == null || !ReferenceEquals(_deferredCleanup.owner, ownerToken))
            return;

        DeferredSequenceCleanup cleanup = _deferredCleanup;
        _deferredCleanup = null;

        switch (cleanup.exitMode)
        {
            case ChainActorExitMode.ReturnToRecordedOriginOnSequenceEnd:
                CompleteDeferredReturn(cleanup);
                return;

            case ChainActorExitMode.ReturnToRecordedOriginViaUtilityOnSequenceEnd:
                if (TryStartReturnUtility(
                        cleanup.owner,
                        cleanup.returnPosition,
                        cleanup.returnRotation,
                        releaseReservationOnComplete: true))
                {
                    return;
                }

                LastExecutionSucceeded = false;
                ReleaseReservation(cleanup.owner);
                autonomyScope.Restore();
                return;

            case ChainActorExitMode.FadeOutAndDeactivateOnSequenceEnd:
                ExecuteFadeOutCleanup(cleanup.owner);
                return;

            case ChainActorExitMode.ReturnToRecordedOriginThenWarpInOnSequenceEnd:
                if (TryStartReturnWarpInAtRecordedOrigin(
                        cleanup.owner,
                        cleanup.returnPosition,
                        cleanup.returnRotation,
                        releaseReservationOnComplete: true))
                {
                    return;
                }

                LastExecutionSucceeded = false;
                ReleaseReservation(cleanup.owner);
                autonomyScope.Restore();
                return;

            default:
                if (interrupted)
                    ReleaseReservation(cleanup.owner);
                autonomyScope.Restore();
                return;
        }
    }

    public bool OwnsSequenceWork(object ownerToken)
    {
        if (ownerToken == null)
            return false;

        if (_pendingExecution != null && ReferenceEquals(_pendingExecution.owner, ownerToken))
            return true;

        if (_deferredCleanup != null && ReferenceEquals(_deferredCleanup.owner, ownerToken))
            return true;

        return _reservationOwner != null && ReferenceEquals(_reservationOwner, ownerToken);
    }

    public bool TryDescribeOwnedSequenceWork(object ownerToken, out string description)
    {
        description = null;

        if (!OwnsSequenceWork(ownerToken))
            return false;

        List<string> states = new();

        if (_pendingExecution != null && ReferenceEquals(_pendingExecution.owner, ownerToken))
        {
            string stepId = _pendingExecution.step != null
                ? _pendingExecution.step.RuntimeId
                : "unknown-step";
            states.Add($"pending phase={_pendingExecution.phase} step={stepId}");
        }

        if (_deferredCleanup != null && ReferenceEquals(_deferredCleanup.owner, ownerToken))
            states.Add($"deferred exit={_deferredCleanup.exitMode}");

        if (_reservationOwner != null && ReferenceEquals(_reservationOwner, ownerToken))
            states.Add("reserved");

        description = $"{owner.ActorRoleValue}:{owner.ActorName} [{string.Join(", ", states)}]";
        return true;
    }

    public void OnChainCastMomentReached(int requestId)
    {
        if (_pendingExecution == null)
            return;

        if (requestId == _pendingExecution.enterRequestId)
        {
            HandleEnterCastMoment(requestId);
            return;
        }

        if (requestId == _pendingExecution.attackRequestId)
        {
            HandleAttackCastMoment(requestId);
            return;
        }

        if (requestId == _pendingExecution.exitRequestId)
            HandleExitCastMoment(requestId);
    }

    public void OnChainAdvanceMomentReached(int requestId)
    {
        if (_pendingExecution == null ||
            requestId != _pendingExecution.attackRequestId ||
            _pendingExecution.phase != SequenceExecutionPhase.WaitingForAttackComplete ||
            !_pendingExecution.attackPayloadReleased)
        {
            return;
        }

        ReleaseContinueSignalIfNeeded(_pendingExecution, "attack normalized time");
    }

    public void OnChainPlaybackInterrupted(int requestId)
    {
        if (_pendingExecution == null)
            return;

        if (requestId != _pendingExecution.enterRequestId &&
            requestId != _pendingExecution.attackRequestId &&
            requestId != _pendingExecution.exitRequestId)
        {
            return;
        }

        owner.LogExecution($"Sequence request {requestId} was interrupted during phase '{_pendingExecution.phase}'.");
        CleanupActiveExecution(success: false);
    }

    public void OnChainPlaybackCompleted(int requestId)
    {
        if (_pendingExecution == null)
            return;

        if (requestId != _pendingExecution.enterRequestId &&
            requestId != _pendingExecution.attackRequestId &&
            requestId != _pendingExecution.exitRequestId)
        {
            return;
        }

        switch (_pendingExecution.phase)
        {
            case SequenceExecutionPhase.WaitingForEnterComplete:
                QueueAttackStart("utility animation completed");
                return;

            case SequenceExecutionPhase.WaitingForAttackComplete:
                HandleAttackCompleted();
                return;

            case SequenceExecutionPhase.WaitingForExitComplete:
                CleanupActiveExecution(success: true);
                return;
        }
    }

    bool TryStartEnterUtility(PendingSequenceExecution execution)
    {
        if (execution == null || owner.AnimBrainRef == null)
            return false;

        execution.enterRequestId = NextRequestId();
        SetExecutionPhase(execution, SequenceExecutionPhase.WaitingForEnterCastMoment);

        bool started = owner.AnimBrainRef.TryPlayChainUtilityWarpOut(execution.enterRequestId);
        if (started)
        {
            transitionController.StartChainVisualLifecycle(hideOnAnimationComplete: true);
            owner.LogExecution($"Started utility warp-out for step '{execution.step.RuntimeId}' (request {execution.enterRequestId}).");
            return true;
        }

        if (!execution.step.allowFallbackToInstantTeleportIfUtilityUnavailable)
        {
            CleanupActiveExecution(success: false);
            return false;
        }

        owner.LogExecution($"Utility warp-out is unavailable for '{execution.step.RuntimeId}'. Falling back to instant teleport.");

        if (!transitionController.TryTeleportToEntryPose(execution))
        {
            CleanupActiveExecution(success: false);
            return false;
        }

        if (execution.step.faceLockedTargetOnStart)
            transitionController.FaceTarget(execution.lockedTarget);

        return TryStartAttack(execution);
    }

    bool TryStartAttack(PendingSequenceExecution execution)
    {
        if (execution == null ||
            execution.step == null ||
            execution.attackSkillDef == null ||
            owner.AnimBrainRef == null)
        {
            return false;
        }

        if (!transitionController.CanAttackLockedTarget(execution))
        {
            CleanupActiveExecution(success: false);
            return false;
        }

        if (!skillCastBridge.TryResolveAttackSkillUser(execution, out ISkillUser attackSkillUser))
        {
            CleanupActiveExecution(success: false);
            return false;
        }

        execution.attackSkillUser = attackSkillUser;

        if (execution.step.faceLockedTargetOnStart)
            transitionController.FaceTarget(execution.lockedTarget);

        execution.attackPayloadReleased = false;
        execution.attackRequestId = NextRequestId();
        SetExecutionPhase(execution, SequenceExecutionPhase.WaitingForAttackCastMoment);

        bool started = owner.AnimBrainRef.TryPlayChainSkill(
            execution.attackRequestId,
            execution.attackSkillDef,
            execution.attackSkillDef.GetCastPointNormalized(),
            ShouldRequestAttackAdvanceMoment(execution),
            ResolveAttackContinueNormalizedTime(execution));

        if (started)
        {
            transitionController.StartChainVisualLifecycle(
                FieldAllyTransitionController.ShouldAutoHideNearAttackEnd(execution.step.exitMode));
            owner.LogExecution(
                $"Started attack for step '{execution.step.RuntimeId}' with skill '{execution.attackSkillDef.name}' " +
                $"(request {execution.attackRequestId}, castUser={skillCastBridge.DescribeSkillUser(execution.attackSkillUser)}).");
            return true;
        }

        CleanupActiveExecution(success: false);
        return false;
    }

    void HandleEnterCastMoment(int requestId)
    {
        if (_pendingExecution == null || _pendingExecution.phase != SequenceExecutionPhase.WaitingForEnterCastMoment)
            return;

        if (!transitionController.TryTeleportToEntryPose(_pendingExecution))
        {
            owner.LogExecution($"Step '{_pendingExecution.step.RuntimeId}' failed to resolve a warp-in pose.");
            if (owner.AnimBrainRef != null)
            {
                owner.AnimBrainRef.CancelChainPlaybackRequest(requestId);
                return;
            }

            CleanupActiveExecution(success: false);
            return;
        }

        if (_pendingExecution.step.faceLockedTargetOnStart)
            transitionController.FaceTarget(_pendingExecution.lockedTarget);

        SetExecutionPhase(_pendingExecution, SequenceExecutionPhase.WaitingForEnterComplete);
        owner.LogExecution($"Teleported '{owner.ActorName}' into chain attack pose for step '{_pendingExecution.step.RuntimeId}'.");
    }

    void HandleAttackCastMoment(int requestId)
    {
        if (_pendingExecution == null || _pendingExecution.phase != SequenceExecutionPhase.WaitingForAttackCastMoment)
            return;

        if (_pendingExecution.step.faceLockedTargetOnCast)
            transitionController.FaceTarget(_pendingExecution.lockedTarget);

        if (!skillCastBridge.TryReleaseAttackPayload(_pendingExecution, owner.AnimBrainRef, requestId))
        {
            if (_pendingExecution.attackSkillUser == null)
            {
                owner.LogExecution($"Skill '{_pendingExecution.attackSkillDef.name}' failed because no cast user could be resolved.");
            }
            else
            {
                owner.LogExecution(
                    $"Skill '{_pendingExecution.attackSkillDef.name}' failed at cast moment " +
                    $"with castUser={skillCastBridge.DescribeSkillUser(_pendingExecution.attackSkillUser)}.");
            }

            if (owner.AnimBrainRef != null)
            {
                owner.AnimBrainRef.CancelChainPlaybackRequest(requestId);
                return;
            }

            CleanupActiveExecution(success: false);
            return;
        }

        SetExecutionPhase(_pendingExecution, SequenceExecutionPhase.WaitingForAttackComplete);

        if (ResolveAttackContinueMode(_pendingExecution) == ChainStepContinueMode.OnAttackCastMoment)
            ReleaseContinueSignalIfNeeded(_pendingExecution, "attack cast moment");
    }

    void HandleExitCastMoment(int requestId)
    {
        if (_pendingExecution == null || _pendingExecution.phase != SequenceExecutionPhase.WaitingForExitCastMoment)
            return;

        transitionController.HideVisualForTeleport();
        transitionController.TeleportActorTo(_pendingExecution.recordedOriginPosition, _pendingExecution.recordedOriginRotation);
        transitionController.RevealVisualAfterTeleportIfNeeded();
        SetExecutionPhase(_pendingExecution, SequenceExecutionPhase.WaitingForExitComplete);
        owner.LogExecution($"Returned '{owner.ActorName}' to its recorded origin for step '{_pendingExecution.step.RuntimeId}'.");
    }

    void HandleAttackCompleted()
    {
        if (_pendingExecution == null)
            return;

        if (!_pendingExecution.attackPayloadReleased)
        {
            CleanupActiveExecution(success: false);
            return;
        }

        switch (_pendingExecution.step.exitMode)
        {
            case ChainActorExitMode.KeepAtCurrentPosition:
                CleanupActiveExecution(success: true);
                return;

            case ChainActorExitMode.ReturnToRecordedOrigin:
                transitionController.HideVisualForTeleport();
                transitionController.TeleportActorTo(
                    _pendingExecution.recordedOriginPosition,
                    _pendingExecution.recordedOriginRotation);
                transitionController.RevealVisualAfterTeleportIfNeeded();
                CleanupActiveExecution(success: true);
                return;

            case ChainActorExitMode.ReturnToRecordedOriginThenWarpIn:
                QueueImmediateReturnUtilityStart();
                return;

            case ChainActorExitMode.ReturnToRecordedOriginOnSequenceEnd:
            case ChainActorExitMode.ReturnToRecordedOriginViaUtilityOnSequenceEnd:
            case ChainActorExitMode.FadeOutAndDeactivateOnSequenceEnd:
            case ChainActorExitMode.ReturnToRecordedOriginThenWarpInOnSequenceEnd:
                QueueDeferredCleanup(_pendingExecution);
                CleanupActiveExecution(success: true);
                return;

            case ChainActorExitMode.ReturnToRecordedOriginViaUtility:
                QueueImmediateReturnUtilityStart();
                return;

            case ChainActorExitMode.FadeOutAndDeactivate:
                ExecuteFadeOutCleanup(_pendingExecution.owner);
                return;
        }
    }

    void QueueDeferredCleanup(PendingSequenceExecution execution)
    {
        if (execution == null)
            return;

        _deferredCleanup = new DeferredSequenceCleanup
        {
            owner = execution.owner,
            exitMode = execution.step.exitMode,
            returnPosition = execution.recordedOriginPosition,
            returnRotation = execution.recordedOriginRotation,
        };
    }

    void ProcessDeferredExecutionPhase()
    {
        if (_pendingExecution == null || owner.AnimBrainRef == null)
            return;

        switch (_pendingExecution.phase)
        {
            case SequenceExecutionPhase.WaitingForEnterCastMoment:
                if (HasPhaseTimedOut(_pendingExecution, owner.UtilityRecoveryTimeoutSeconds))
                {
                    owner.LogExecution(
                        $"Utility warp-out cast moment timed out for step '{_pendingExecution.step.RuntimeId}'. " +
                        "Forcing teleport recovery.");
                    HandleEnterCastMoment(_pendingExecution.enterRequestId);
                }
                return;

            case SequenceExecutionPhase.WaitingForEnterComplete:
                if (!owner.AnimBrainRef.IsUtilityActive)
                {
                    QueueAttackStart("utility animation state exited without completion callback");
                    return;
                }

                if (HasPhaseTimedOut(_pendingExecution, owner.UtilityRecoveryTimeoutSeconds))
                {
                    owner.LogExecution(
                        $"Utility warp-out completion timed out for step '{_pendingExecution.step.RuntimeId}'. " +
                        "Forcing attack handoff.");
                    owner.AnimBrainRef.CancelChainPlaybackRequest(_pendingExecution.enterRequestId);
                    QueueAttackStart("utility completion timeout recovery");
                }
                return;

            case SequenceExecutionPhase.WaitingForAttackStart:
                TryStartAttack(_pendingExecution);
                return;

            case SequenceExecutionPhase.WaitingForAttackCastMoment:
                if (!owner.AnimBrainRef.IsChainPlaybackActive)
                {
                    owner.LogExecution(
                        $"Attack cast moment for step '{_pendingExecution.step.RuntimeId}' was missed. " +
                        "Forcing cast recovery.");
                    HandleAttackCastMoment(_pendingExecution.attackRequestId);
                    return;
                }

                if (HasPhaseTimedOut(_pendingExecution, owner.AttackCastRecoveryTimeoutSeconds))
                {
                    owner.LogExecution(
                        $"Attack cast moment timed out for step '{_pendingExecution.step.RuntimeId}'. " +
                        "Forcing skill payload release.");
                    HandleAttackCastMoment(_pendingExecution.attackRequestId);
                }
                return;

            case SequenceExecutionPhase.WaitingForAttackComplete:
                if (!owner.AnimBrainRef.IsChainPlaybackActive)
                {
                    owner.LogExecution(
                        $"Attack for step '{_pendingExecution.step.RuntimeId}' finished via animation state exit recovery.");
                    HandleAttackCompleted();
                }
                return;

            case SequenceExecutionPhase.WaitingForExitStart:
                bool startedExit = _pendingExecution.step != null &&
                                   _pendingExecution.step.exitMode == ChainActorExitMode.ReturnToRecordedOriginThenWarpIn
                    ? TryStartReturnWarpInAtRecordedOrigin(
                        _pendingExecution.owner,
                        _pendingExecution.recordedOriginPosition,
                        _pendingExecution.recordedOriginRotation,
                        releaseReservationOnComplete: false)
                    : TryStartReturnUtility(
                        _pendingExecution.owner,
                        _pendingExecution.recordedOriginPosition,
                        _pendingExecution.recordedOriginRotation,
                        releaseReservationOnComplete: false);

                if (!startedExit)
                    CleanupActiveExecution(success: false);
                return;

            case SequenceExecutionPhase.WaitingForExitCastMoment:
                if (HasPhaseTimedOut(_pendingExecution, owner.UtilityRecoveryTimeoutSeconds))
                {
                    owner.LogExecution($"Return utility cast moment timed out for '{owner.ActorName}'. Forcing return teleport recovery.");
                    HandleExitCastMoment(_pendingExecution.exitRequestId);
                }
                return;

            case SequenceExecutionPhase.WaitingForExitComplete:
                if (!owner.AnimBrainRef.IsUtilityActive)
                {
                    owner.LogExecution($"Return utility for '{owner.ActorName}' finished via animation state exit recovery.");
                    CleanupActiveExecution(success: true);
                    return;
                }

                if (HasPhaseTimedOut(_pendingExecution, owner.UtilityRecoveryTimeoutSeconds))
                {
                    owner.LogExecution($"Return utility completion timed out for '{owner.ActorName}'. Forcing return cleanup.");
                    owner.AnimBrainRef.CancelChainPlaybackRequest(_pendingExecution.exitRequestId);
                    CleanupActiveExecution(success: true);
                }
                return;
        }
    }

    void QueueAttackStart(string reason)
    {
        if (_pendingExecution == null)
            return;

        _pendingExecution.enterRequestId = 0;
        SetExecutionPhase(_pendingExecution, SequenceExecutionPhase.WaitingForAttackStart);
        owner.LogExecution($"Queued attack start for step '{_pendingExecution.step.RuntimeId}' after {reason}.");
    }

    void QueueImmediateReturnUtilityStart()
    {
        if (_pendingExecution == null)
            return;

        _pendingExecution.attackRequestId = 0;
        SetExecutionPhase(_pendingExecution, SequenceExecutionPhase.WaitingForExitStart);
        owner.LogExecution($"Queued return utility for step '{_pendingExecution.step.RuntimeId}'.");
    }

    bool TryStartReturnUtility(
        object ownerToken,
        Vector3 returnPosition,
        Quaternion returnRotation,
        bool releaseReservationOnComplete)
    {
        if (owner.AnimBrainRef == null)
            return false;

        if (_pendingExecution == null)
        {
            _pendingExecution = new PendingSequenceExecution
            {
                owner = ownerToken,
                step = new ChainAttackStepDef { stepId = "deferred_return" },
                recordedOriginPosition = returnPosition,
                recordedOriginRotation = returnRotation,
            };
        }

        _pendingExecution.owner = ownerToken;
        _pendingExecution.recordedOriginPosition = returnPosition;
        _pendingExecution.recordedOriginRotation = returnRotation;
        _pendingExecution.releaseReservationOnComplete = releaseReservationOnComplete;
        _pendingExecution.exitRequestId = NextRequestId();
        SetExecutionPhase(_pendingExecution, SequenceExecutionPhase.WaitingForExitCastMoment);

        bool started = owner.AnimBrainRef.TryPlayChainUtilityWarpOut(_pendingExecution.exitRequestId);
        if (started)
        {
            transitionController.StartChainVisualLifecycle(hideOnAnimationComplete: true);
            owner.LogExecution($"Started return utility for '{owner.ActorName}' (request {_pendingExecution.exitRequestId}).");
            return true;
        }

        transitionController.HideVisualForTeleport();
        transitionController.TeleportActorTo(returnPosition, returnRotation);
        transitionController.RevealVisualAfterTeleportIfNeeded();
        CleanupActiveExecution(success: true);
        return true;
    }

    bool TryStartReturnWarpInAtRecordedOrigin(
        object ownerToken,
        Vector3 returnPosition,
        Quaternion returnRotation,
        bool releaseReservationOnComplete)
    {
        if (owner.AnimBrainRef == null)
            return false;

        if (_pendingExecution == null)
        {
            _pendingExecution = new PendingSequenceExecution
            {
                owner = ownerToken,
                step = new ChainAttackStepDef { stepId = "deferred_return_warp_in" },
                recordedOriginPosition = returnPosition,
                recordedOriginRotation = returnRotation,
            };
        }

        _pendingExecution.owner = ownerToken;
        _pendingExecution.recordedOriginPosition = returnPosition;
        _pendingExecution.recordedOriginRotation = returnRotation;
        _pendingExecution.releaseReservationOnComplete = releaseReservationOnComplete;
        _pendingExecution.attackRequestId = 0;
        _pendingExecution.exitRequestId = NextRequestId();

        transitionController.HideVisualForTeleport();
        transitionController.TeleportActorTo(returnPosition, returnRotation);

        bool started = owner.AnimBrainRef.TryPlayChainUtilityWarpIn(_pendingExecution.exitRequestId);
        if (!started)
        {
            _pendingExecution.exitRequestId = 0;
            transitionController.RevealVisualAfterTeleportIfNeeded();
            owner.LogExecution(
                $"Return warp-in could not start for '{owner.ActorName}'. Falling back to immediate reveal at the recorded origin.");
            CleanupActiveExecution(success: true);
            return true;
        }

        SetExecutionPhase(_pendingExecution, SequenceExecutionPhase.WaitingForExitComplete);
        transitionController.StartChainVisualLifecycle(hideOnAnimationComplete: false);
        owner.LogExecution($"Started return warp-in for '{owner.ActorName}' (request {_pendingExecution.exitRequestId}).");
        return true;
    }

    void CompleteDeferredReturn(DeferredSequenceCleanup cleanup)
    {
        if (cleanup == null)
            return;

        transitionController.HideVisualForTeleport();
        transitionController.TeleportActorTo(cleanup.returnPosition, cleanup.returnRotation);
        transitionController.RevealVisualAfterTeleportIfNeeded();
        ReleaseReservation(cleanup.owner);
        autonomyScope.Restore();
    }

    void ExecuteFadeOutCleanup(object ownerToken)
    {
        ReleaseReservation(ownerToken);
        autonomyScope.Restore();
        transitionController.FadeOutAndDeactivate();
    }

    void CleanupActiveExecution(bool success)
    {
        bool shouldReleaseReservation =
            _pendingExecution != null &&
            _pendingExecution.releaseReservationOnComplete &&
            _pendingExecution.owner != null;
        bool hasDeferredCleanup =
            _pendingExecution != null &&
            _deferredCleanup != null &&
            _pendingExecution.owner != null &&
            ReferenceEquals(_deferredCleanup.owner, _pendingExecution.owner);

        object ownerToRelease = shouldReleaseReservation ? _pendingExecution.owner : null;

        if (_pendingExecution != null)
        {
            _lastCompletedExecutionId = _pendingExecution.executionId;
            _lastCompletedExecutionSucceeded = success;
            _lastCompletedExecutionHadDeferredCleanup = hasDeferredCleanup;
        }

        _pendingExecution = null;
        LastExecutionSucceeded = success;
        skillCastBridge.ClearAimOverrides();

        if (!success)
            transitionController.RecoverVisibleStateAfterInterruptedExecution();

        if (_deferredCleanup == null)
            autonomyScope.Restore();

        if (ownerToRelease != null)
            ReleaseReservation(ownerToRelease);
    }

    void ReleaseContinueSignalIfNeeded(PendingSequenceExecution execution, string reason)
    {
        if (execution == null || execution.continueReleased)
            return;

        if (ResolveAttackContinueMode(execution) == ChainStepContinueMode.OnStepComplete)
            return;

        execution.continueReleased = true;
        owner.LogExecution($"Released chain continue signal for step '{execution.step.RuntimeId}' at {reason}.");
    }

    void SetExecutionPhase(PendingSequenceExecution execution, SequenceExecutionPhase phase)
    {
        if (execution == null)
            return;

        execution.phase = phase;
        execution.phaseStartedAt = Time.time;
    }

    bool HasPhaseTimedOut(PendingSequenceExecution execution, float timeoutSeconds)
    {
        if (execution == null)
            return false;

        float timeout = Mathf.Max(0.1f, timeoutSeconds);
        return execution.phaseStartedAt > 0f && Time.time - execution.phaseStartedAt >= timeout;
    }

    void CancelDeferredSequenceCleanup()
    {
        _deferredCleanup = null;
    }

    static bool ShouldRequestAttackAdvanceMoment(PendingSequenceExecution execution)
    {
        return ResolveAttackContinueMode(execution) == ChainStepContinueMode.OnAttackNormalizedTime;
    }

    static ChainStepContinueMode ResolveAttackContinueMode(PendingSequenceExecution execution)
    {
        if (execution == null)
            return ChainStepContinueMode.OnStepComplete;

        if (execution.step != null && execution.step.UsesStepContinueOverride)
            return execution.step.continueMode;

        return execution.attackSkillDef != null && execution.attackSkillDef.payload != null
            ? execution.attackSkillDef.payload.GetChainContinueMode()
            : ChainStepContinueMode.OnStepComplete;
    }

    static float ResolveConfiguredContinueNormalizedTime(PendingSequenceExecution execution)
    {
        if (execution == null)
            return 1f;

        if (execution.step != null && execution.step.UsesStepContinueOverride)
            return execution.step.ClampedContinueNormalizedTime;

        return execution.attackSkillDef != null && execution.attackSkillDef.payload != null
            ? execution.attackSkillDef.payload.GetChainContinueNormalizedTime()
            : 1f;
    }

    static float ResolveAttackContinueNormalizedTime(PendingSequenceExecution execution)
    {
        return ResolveAttackContinueMode(execution) switch
        {
            ChainStepContinueMode.OnAttackCastMoment => execution?.attackSkillDef != null
                ? execution.attackSkillDef.GetCastPointNormalized()
                : ResolveConfiguredContinueNormalizedTime(execution),
            ChainStepContinueMode.OnAttackNormalizedTime => ResolveConfiguredContinueNormalizedTime(execution),
            _ => 1f,
        };
    }

    int NextRequestId()
    {
        if (_nextRequestId == int.MaxValue)
            _nextRequestId = 1;

        return _nextRequestId++;
    }

    int NextExecutionId()
    {
        if (_nextExecutionId == int.MaxValue)
            _nextExecutionId = 1;

        return _nextExecutionId++;
    }
}
