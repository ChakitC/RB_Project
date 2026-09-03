using System;
using UnityEngine;
using UnityEngine.AI;

internal sealed class FieldAllyTransitionController
{
    // Closest first: the authored flank offset is 1.35m, so these bracket it and then give ground.
    static readonly float[] FallbackRadii = { 1.5f, 2f, 2.75f, 3.5f };
    const int FallbackYawSamples = 12;

    // Share of the pre-teleport window spent fading out. The rest is the actor still fully visible
    // playing its wind-up, which is what makes the warp read as a departure rather than a blink.
    const float WarpFadeWindowFraction = 0.7f;
    const float MinimumWarpFadeSeconds = 0.06f;

    static readonly float[] AttackAnimationSampleFractions = { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 0.98f };
    static readonly float[] UtilityPostTeleportSampleFractions = { 0f, 0.33f, 0.66f, 0.98f };
    const float DuplicateSampleTimeEpsilon = 0.0005f;

    readonly FieldAllyMember owner;
    readonly CharacterPlaybackAutoHideSchedule _autoHide = new();

    bool _visualHiddenForChainTransition;
    bool _autoHideTimedToCastMoment;
    Action _pendingDeactivateOnDisappear;

    public FieldAllyTransitionController(FieldAllyMember owner)
    {
        this.owner = owner;
    }

    public void StartChainVisualLifecycle(int requestId, bool hideOnAnimationComplete) =>
        StartChainVisualLifecycle(requestId, hideOnAnimationComplete, hideAtTeleportCastMoment: false);

    /// <summary>
    /// <paramref name="hideAtTeleportCastMoment"/> says what the fade has to be finished by. A
    /// warp-out teleports the actor at its cast moment, so the fade is timed to that. Every other
    /// playback — the attack above all — fires something else at its cast moment and should keep
    /// fading toward the end of the clip; timing an attack's fade to its cast moment would make the
    /// actor vanish on the hit and play the rest of the swing invisible.
    /// </summary>
    public void StartChainVisualLifecycle(
        int requestId,
        bool hideOnAnimationComplete,
        bool hideAtTeleportCastMoment)
    {
        if (owner.VisibilityRef == null || !owner.GameObjectRef.activeInHierarchy)
            return;

        ClearPendingDeactivate();
        ClearAutoHide();
        owner.VisibilityRef.Appear();
        _visualHiddenForChainTransition = false;
        _autoHideTimedToCastMoment = hideAtTeleportCastMoment;

        if (hideOnAnimationComplete && requestId > 0)
            _autoHide.Start(requestId);
    }

    public void Tick()
    {
        if (!_autoHide.IsPending)
            return;

        CharacterVisibilityController visibility = owner.VisibilityRef;
        CharacterAnimBrain animBrain = owner.AnimBrainRef;
        if (visibility == null ||
            animBrain == null ||
            owner.GameObjectRef == null ||
            !owner.GameObjectRef.activeInHierarchy)
        {
            ClearAutoHide();
            return;
        }

        _autoHide.Advance(visibility);
        int requestId = _autoHide.RequestId;

        if (!animBrain.TryGetActiveChainPlaybackTiming(
                requestId,
                out float remainingDuration,
                out float totalDuration))
        {
            return;
        }

        // The actor is teleported at the playback's cast moment, not at the end of the clip, so the
        // fade has to be measured against that. Feeding the clip's remaining time straight in used
        // to start the fade too late: with a warp-out cast point of 0.95 the character was still
        // partly visible when it snapped away, which reads as a pop rather than a fade.
        float timeUntilTeleport = remainingDuration;
        float fadeDuration = -1f;

        if (_autoHideTimedToCastMoment &&
            animBrain.TryGetActiveChainCastPointNormalized(requestId, out float castPointNormalized) &&
            float.IsFinite(totalDuration) &&
            totalDuration > 0f)
        {
            timeUntilTeleport = Mathf.Max(0f, remainingDuration - (1f - castPointNormalized) * totalDuration);

            // Size the fade to the animation rather than to a fixed number. The window is everything
            // before the actor is teleported; spending a fixed slice of it means a warp-out that
            // triggers early in its clip gets no visible fade at all, while a late one fades long
            // before it needs to. WarpFadeWindowFraction of that window keeps the pacing the same
            // whatever the clip length or cast point is.
            float preWarpWindow = Mathf.Max(0f, castPointNormalized * totalDuration);
            fadeDuration = Mathf.Clamp(
                preWarpWindow * WarpFadeWindowFraction,
                MinimumWarpFadeSeconds,
                Mathf.Max(MinimumWarpFadeSeconds, preWarpWindow));
        }

        _autoHide.TryBeginHide(visibility, timeUntilTeleport, totalDuration, fadeDuration);
    }

    public void HideVisualForTeleport()
    {
        ClearAutoHide();

        if (owner.VisibilityRef == null || !owner.GameObjectRef.activeInHierarchy)
            return;

        owner.VisibilityRef.ConcealForTeleport();
        _visualHiddenForChainTransition = true;
    }

    public void RevealVisualAfterTeleportIfNeeded()
    {
        if (!_visualHiddenForChainTransition)
            return;

        _visualHiddenForChainTransition = false;

        if (owner.VisibilityRef == null || !owner.GameObjectRef.activeInHierarchy)
            return;

        // The actor was concealed for the snap. Reveal through the visibility transition so the
        // chain entry/return keeps the fade-in that the old chain fader provided.
        owner.VisibilityRef.Appear();
    }

    public void RecoverVisibleStateAfterInterruptedExecution()
    {
        _visualHiddenForChainTransition = false;
        ClearAutoHide();

        if (owner.VisibilityRef == null || !owner.GameObjectRef.activeInHierarchy)
            return;

        ClearPendingDeactivate();
        // Recovery can happen after a failed/aborted teleport. Continue from the current dither
        // value instead of popping the actor fully visible in the same frame.
        owner.VisibilityRef.Appear();
    }

    public void ClearVisualState()
    {
        _visualHiddenForChainTransition = false;
        ClearAutoHide();
        ClearPendingDeactivate();
    }

    public void FadeOutAndDeactivate()
    {
        if (owner.GameObjectRef == null || !owner.GameObjectRef.activeSelf)
            return;

        ClearAutoHide();
        CharacterVisibilityController visibility = owner.VisibilityRef;
        if (visibility == null)
        {
            owner.GameObjectRef.SetActive(false);
            return;
        }

        // CharacterVisibilityController never disables the actor itself - the sequence owner does,
        // once the fade-out has actually reached full dither.
        ClearPendingDeactivate();

        GameObject actor = owner.GameObjectRef;
        if (visibility.IsHidden)
        {
            actor.SetActive(false);
            return;
        }

        Action handler = null;
        handler = () =>
        {
            visibility.Disappeared -= handler;
            if (_pendingDeactivateOnDisappear == handler)
                _pendingDeactivateOnDisappear = null;

            if (actor != null && actor.activeSelf)
                actor.SetActive(false);
        };

        _pendingDeactivateOnDisappear = handler;
        visibility.Disappeared += handler;

        if (!visibility.IsDisappearing)
            visibility.Disappear();
    }

    void ClearAutoHide()
    {
        _autoHide.Cancel();
    }

    // Drops a fade-out completion handler that is no longer wanted (interrupt, restart), so a later
    // Disappear cannot deactivate the actor behind the sequence's back.
    void ClearPendingDeactivate()
    {
        if (_pendingDeactivateOnDisappear == null)
            return;

        if (owner.VisibilityRef != null)
            owner.VisibilityRef.Disappeared -= _pendingDeactivateOnDisappear;

        _pendingDeactivateOnDisappear = null;
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

        // Targeted placement is the path that actually runs for a step with a teleport profile, so it
        // needs the in-place fallback too — it restores the actor to its origin before failing, which
        // is exactly the state the fallback expects.
        if (!TryApplyTargetedPlacement(execution, targetAnchor, out bool placementHandled))
            return AcceptInPlaceAttackFallback(execution);

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
                return AcceptInPlaceAttackFallback(execution);
            }

            return true;
        }

        if (!TryResolveEntryTeleportPose(
                execution.step,
                targetAnchor,
                out Vector3 teleportPosition,
                out Quaternion teleportRotation))
        {
            return AcceptInPlaceAttackFallback(execution);
        }

        HideVisualForTeleport();
        TeleportActorTo(teleportPosition, teleportRotation);
        if (!ValidateEntryCandidatePose(execution))
        {
            TeleportActorTo(execution.recordedOriginPosition, execution.recordedOriginRotation);
            RevealVisualAfterTeleportIfNeeded();
            return AcceptInPlaceAttackFallback(execution);
        }

        return true;
    }

    /// <summary>
    /// Sweeps a ring of positions around the target for somewhere legal to stand, closest radius
    /// first. Uses the step's own teleport profile for the clearance rules, so it respects the same
    /// obstacle layers as the authored pose — it only relaxes *where* the actor may stand, never
    /// whether the spot is safe.
    /// </summary>
    bool TryTeleportNearTarget(PendingSequenceExecution execution)
    {
        ChainAttackTeleportProfileDef profile = execution.step.teleportProfile;
        Transform anchor = ResolveExecutionTargetAnchor(execution);
        if (profile == null || anchor == null)
            return false;

        Vector3 anchorPosition = anchor.position;

        for (int radiusStep = 0; radiusStep < FallbackRadii.Length; radiusStep++)
        {
            float radius = FallbackRadii[radiusStep];

            for (int i = 0; i < FallbackYawSamples; i++)
            {
                float yaw = i * (360f / FallbackYawSamples);
                Vector3 offset = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * radius;
                Vector3 candidate = anchorPosition + offset;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 1f, NavMesh.AllAreas))
                    continue;

                candidate = navHit.position;

                Vector3 toTarget = anchorPosition - candidate;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 0.0001f)
                    continue;

                Quaternion rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

                if (!ChainAttackTeleportUtility.IsProbePoseClear(
                        profile,
                        candidate,
                        rotation,
                        owner.ChainTeleportProbeColliderRef,
                        owner.TransformRef,
                        allowedOverlapRoot: execution.lockedTarget))
                {
                    continue;
                }

                // Conceal first: the caller already revealed the actor when the authored pose failed,
                // so teleporting straight away would move it in full view.
                HideVisualForTeleport();
                TeleportActorTo(candidate, rotation);
                RevealVisualAfterTeleportIfNeeded();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Last resort when no safe warp-in pose exists — the target is against a wall, in a corner, or
    /// otherwise leaves nowhere legal to stand. The actor stays where it is and attacks from there.
    ///
    /// Failing the step instead is what made chains unreliable: with stopWhenAnyStepFails the whole
    /// sequence aborted over map geometry, and the target got a free Stagger with nobody attacking.
    /// A degraded attack from range beats losing the entire chain.
    /// </summary>
    bool AcceptInPlaceAttackFallback(PendingSequenceExecution execution)
    {
        if (execution?.step == null || !execution.step.allowInPlaceAttackIfEntryPoseBlocked)
            return false;

        // Measured: a chain step that lands next to the target does roughly an order of magnitude
        // more damage than one fired from wherever the actor happened to be standing. So before
        // settling for an in-place attack, look for any legal spot close to the target — the
        // authored flank offset failing does not mean every nearby position is blocked.
        if (TryTeleportNearTarget(execution))
        {
            execution.usedInPlaceFallback = true;
            owner.LogExecution(
                $"Step '{execution.step.RuntimeId}' could not use its authored flank pose; " +
                $"'{owner.ActorName}' moved to a nearby fallback pose instead.");
            return true;
        }

        RevealVisualAfterTeleportIfNeeded();
        execution.usedInPlaceFallback = true;

        // The warp pose is what aims the actor, and these steps commonly leave both facing flags off
        // because of that. Skipping the warp therefore also skips the aiming, and a projectile chain
        // skill fires along the actor's forward — straight past the target. Aim it here.
        if (!execution.placementResult.UsesRootMotion)
            FaceTarget(execution);

        owner.LogExecution(
            $"Step '{execution.step.RuntimeId}' found no safe warp-in pose; attacking in place from " +
            $"'{owner.ActorName}'s current position instead (facing forced toward the target).");
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

        AnimationClip utilityWarpOutClip = null;
        float utilityCastPointNormalized = 0f;
        if (ShouldValidateUtilityWarpOutTail(execution))
        {
            owner.AnimBrainRef.TryResolveChainUtilityWarpOutAnimationClip(
                out utilityWarpOutClip,
                out utilityCastPointNormalized);
        }

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
                out TargetedSkillPlacementResult placementResult,
                reservations: owner.PlacementReservations,
                leadInClip: utilityWarpOutClip,
                leadInStartNormalized: utilityCastPointNormalized))
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
                requireNavMeshAtAnchorOverride: true,
                navMeshSampleDistanceOverride: step.warpNavMeshSampleDistance,
                probeCollider: owner.ChainTeleportProbeColliderRef,
                probeRoot: owner.TransformRef,
                poseValidator: poseValidator,
                reservations: owner.PlacementReservations);
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
        ActorPoseSnapper.Snap(actorTransform, owner.ActorCharacterControllerRef,
                              owner.ActorRigidbodyRef, worldPosition, worldRotation);
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
