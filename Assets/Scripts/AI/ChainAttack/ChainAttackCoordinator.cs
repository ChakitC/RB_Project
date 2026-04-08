using System;
using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-107)]
public sealed class ChainAttackCoordinator : MonoBehaviour
{
    sealed class ActiveChainRuntime
    {
        public ChainAttackSequenceDef sequenceDef;
        public GameObject targetObject;
        public Transform targetTransform;
        public Transform targetAnchor;
        public float startedAt;
        public int pendingTrackedStepCompletions;
        public bool hadLateStepFailure;
    }

    [SerializeField] private PlayerContext playerContext;
    [SerializeField] private FieldAllyManager fieldAllyManager;
    [SerializeField] private AllyHelperManager allyHelperManager;
    [SerializeField] private bool logCoordinator;

    Coroutine _activeRoutine;
    ActiveChainRuntime _activeRuntime;

    public bool IsSequenceActive => _activeRoutine != null;
    public Transform LockedTarget => _activeRuntime != null ? _activeRuntime.targetTransform : null;

    void Awake()
    {
        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();

        if (fieldAllyManager == null)
            fieldAllyManager = GetComponent<FieldAllyManager>();

        if (allyHelperManager == null && playerContext != null)
            allyHelperManager = playerContext.allyHelper;

        if (playerContext != null && playerContext.chainAttackCoordinator == null)
            playerContext.chainAttackCoordinator = this;
    }

    public bool TryStartSequence(ChainAttackSequenceDef sequenceDef)
    {
        return TryStartSequence(sequenceDef, (Transform)null);
    }

    public bool TryStartSequence(ChainAttackSequenceDef sequenceDef, GameObject explicitTargetObject)
    {
        return TryStartSequence(sequenceDef, explicitTargetObject != null ? explicitTargetObject.transform : null);
    }

    public bool TryStartSequence(ChainAttackSequenceDef sequenceDef, PassiveEventContext context)
    {
        return TryStartSequence(sequenceDef, context.Target != null ? context.Target.transform : null);
    }

    public bool TryStartSequence(ChainAttackSequenceDef sequenceDef, Transform explicitTargetTransform)
    {
        if (_activeRoutine != null || sequenceDef == null || !sequenceDef.HasAnySteps)
            return false;

        if (!ChainAttackTargetingUtility.TryResolveLockedTarget(
                playerContext,
                sequenceDef,
                explicitTargetTransform,
                allyHelperManager != null ? allyHelperManager.HelperObject : null,
                out GameObject targetObject,
                out Transform targetTransform,
                out Transform targetAnchor))
        {
            Log(sequenceDef, "Sequence start failed: no valid locked target was resolved.");
            return false;
        }

        _activeRuntime = new ActiveChainRuntime
        {
            sequenceDef = sequenceDef,
            targetObject = targetObject,
            targetTransform = targetTransform,
            targetAnchor = targetAnchor,
            startedAt = Time.time,
        };

        _activeRoutine = StartCoroutine(RunSequence(_activeRuntime));
        Log(sequenceDef, $"Started sequence '{sequenceDef.RuntimeId}' on target '{targetObject.name}'.");
        return true;
    }

    IEnumerator RunSequence(ActiveChainRuntime runtime)
    {
        bool completedSuccessfully = true;

        for (int i = 0; i < runtime.sequenceDef.steps.Length; i++)
        {
            if (runtime.hadLateStepFailure && runtime.sequenceDef.stopWhenAnyStepFails)
            {
                completedSuccessfully = false;
                break;
            }

            ChainAttackStepDef step = runtime.sequenceDef.steps[i];
            if (step == null)
                continue;

            if (!IsRuntimeStillValid(runtime))
            {
                completedSuccessfully = false;
                break;
            }

            if (step.delayBefore > 0f)
                yield return new WaitForSeconds(step.delayBefore);

            if (runtime.hadLateStepFailure && runtime.sequenceDef.stopWhenAnyStepFails)
            {
                completedSuccessfully = false;
                break;
            }

            bool stepSucceeded = false;
            yield return RunStep(runtime, step, result => stepSucceeded = result);

            if (!stepSucceeded && runtime.sequenceDef.stopWhenAnyStepFails)
            {
                completedSuccessfully = false;
                break;
            }

            if (runtime.hadLateStepFailure && runtime.sequenceDef.stopWhenAnyStepFails)
            {
                completedSuccessfully = false;
                break;
            }

            float interval = step.delayAfter > 0f
                ? step.delayAfter
                : runtime.sequenceDef.defaultStepIntervalSeconds;

            if (interval > 0f && i < runtime.sequenceDef.steps.Length - 1)
                yield return new WaitForSeconds(interval);
        }

        if (fieldAllyManager != null)
        {
            fieldAllyManager.FinalizeSequenceReservations(runtime, interrupted: !completedSuccessfully);
            while (fieldAllyManager.HasOwnedSequenceWork(runtime) || runtime.pendingTrackedStepCompletions > 0)
                yield return null;
        }
        else
        {
            while (runtime.pendingTrackedStepCompletions > 0)
                yield return null;
        }

        if (runtime.hadLateStepFailure)
            completedSuccessfully = false;

        Log(
            runtime.sequenceDef,
            completedSuccessfully
                ? $"Sequence '{runtime.sequenceDef.RuntimeId}' completed."
                : $"Sequence '{runtime.sequenceDef.RuntimeId}' ended early.");

        _activeRoutine = null;
        _activeRuntime = null;
    }

    IEnumerator RunStep(ActiveChainRuntime runtime, ChainAttackStepDef step, Action<bool> onFinished)
    {
        if (step.actorRole == ChainActorRole.Helper)
        {
            yield return RunHelperStep(runtime, step, onFinished);
            yield break;
        }

        if (runtime.sequenceDef.cancelIfLockedTargetDies &&
            !ChainAttackTargetingUtility.IsTargetAlive(runtime.targetTransform))
        {
            Log(runtime.sequenceDef, $"Step '{step.RuntimeId}' aborted because the locked target is no longer alive.");
            onFinished(false);
            yield break;
        }

        if (runtime.targetTransform == null && step.skipIfTargetMissing)
        {
            Log(runtime.sequenceDef, $"Step '{step.RuntimeId}' skipped because there is no locked target.");
            onFinished(true);
            yield break;
        }

        if (fieldAllyManager == null || !fieldAllyManager.TryGetMember(step.actorRole, out FieldAllyMember member) || member == null)
        {
            Log(runtime.sequenceDef, $"Step '{step.RuntimeId}' has no registered actor for role '{step.actorRole}'.");
            onFinished(step.skipIfActorUnavailable);
            yield break;
        }

        if (!member.TryReserve(runtime))
        {
            Log(runtime.sequenceDef, $"Step '{step.RuntimeId}' could not reserve actor '{member.name}'.");
            onFinished(step.skipIfActorUnavailable);
            yield break;
        }

        bool started = member.TryStartSequenceStep(step, runtime.targetTransform);
        if (!started)
        {
            member.ReleaseReservation(runtime);
            Log(runtime.sequenceDef, $"Step '{step.RuntimeId}' failed to start on actor '{member.name}'.");
            onFinished(false);
            yield break;
        }

        int executionId = member.ActiveSequenceExecutionId;
        if (executionId <= 0)
        {
            member.ReleaseReservation(runtime);
            Log(runtime.sequenceDef, $"Step '{step.RuntimeId}' started without a valid execution id on actor '{member.name}'.");
            onFinished(false);
            yield break;
        }

        while (true)
        {
            if (member.TryGetCompletedSequenceExecutionResult(executionId, out bool success, out bool hasDeferredCleanup))
            {
                if (!hasDeferredCleanup)
                    member.ReleaseReservation(runtime);

                onFinished(success);
                yield break;
            }

            if (member.IsSequenceExecutionReadyToContinue(executionId))
            {
                TrackStepCompletion(runtime, member, executionId, step);
                onFinished(true);
                yield break;
            }

            yield return null;
        }
    }

    void TrackStepCompletion(
        ActiveChainRuntime runtime,
        FieldAllyMember member,
        int executionId,
        ChainAttackStepDef step)
    {
        if (runtime == null || member == null || executionId <= 0)
            return;

        runtime.pendingTrackedStepCompletions++;
        StartCoroutine(WaitForTrackedStepCompletion(runtime, member, executionId, step));
    }

    IEnumerator WaitForTrackedStepCompletion(
        ActiveChainRuntime runtime,
        FieldAllyMember member,
        int executionId,
        ChainAttackStepDef step)
    {
        bool success = false;
        bool hasDeferredCleanup = false;

        while (member != null &&
               !member.TryGetCompletedSequenceExecutionResult(executionId, out success, out hasDeferredCleanup))
        {
            yield return null;
        }

        if (runtime != null && runtime.pendingTrackedStepCompletions > 0)
            runtime.pendingTrackedStepCompletions--;

        if (runtime == null)
            yield break;

        if (member == null)
        {
            runtime.hadLateStepFailure = true;
            yield break;
        }

        if (!hasDeferredCleanup)
            member.ReleaseReservation(runtime);

        if (!success)
        {
            runtime.hadLateStepFailure = true;
            Log(runtime.sequenceDef, $"Step '{step.RuntimeId}' failed after releasing its early continue signal.");
        }
    }

    IEnumerator RunHelperStep(ActiveChainRuntime runtime, ChainAttackStepDef step, Action<bool> onFinished)
    {
        if (allyHelperManager == null)
        {
            Log(runtime.sequenceDef, $"Helper step '{step.RuntimeId}' failed because AllyHelperManager is missing.");
            onFinished(step.skipIfActorUnavailable);
            yield break;
        }

        if (runtime.targetTransform == null && step.skipIfTargetMissing)
        {
            Log(runtime.sequenceDef, $"Helper step '{step.RuntimeId}' skipped because there is no locked target.");
            onFinished(true);
            yield break;
        }

        bool started = step.helperChainAttackSequence != null
            ? allyHelperManager.TryStartChainAttackHelperToTarget(
                step.helperChainAttackSequence,
                step.skillDef,
                runtime.targetTransform,
                step.ClampedSkillLevel,
                step.helperHideOnComplete)
            : allyHelperManager.TrySummonAllyHelper(
                step.skillDef,
                step.ClampedSkillLevel,
                step.helperHideOnComplete);

        if (!started)
        {
            Log(runtime.sequenceDef, $"Helper step '{step.RuntimeId}' failed to start.");
            onFinished(false);
            yield break;
        }

        while (allyHelperManager.IsHelperBusy)
            yield return null;

        onFinished(allyHelperManager.LastExecutionSucceeded);
    }

    bool IsRuntimeStillValid(ActiveChainRuntime runtime)
    {
        if (runtime == null || runtime.sequenceDef == null)
            return false;

        if (Time.time - runtime.startedAt > runtime.sequenceDef.maxSequenceDurationSeconds)
        {
            Log(runtime.sequenceDef, "Sequence cancelled because it exceeded maxSequenceDurationSeconds.");
            return false;
        }

        if (!runtime.sequenceDef.cancelIfLockedTargetDies)
            return true;

        if (runtime.targetTransform == null)
            return false;

        if (!ChainAttackTargetingUtility.IsTargetAlive(runtime.targetTransform))
        {
            Log(runtime.sequenceDef, "Sequence cancelled because the locked target is no longer alive.");
            return false;
        }

        return true;
    }

    void Log(ChainAttackSequenceDef sequenceDef, string message)
    {
        if (!logCoordinator && (sequenceDef == null || !sequenceDef.debugLogging))
            return;

        Debug.Log($"[ChainAttackCoordinator] {message}", this);
    }
}
