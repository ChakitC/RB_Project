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
    [Header("Player Protection")]
    [SerializeField] private bool protectPlayerDuringSequence = true;
    [SerializeField] private bool makePlayerInvincibleDuringSequence = true;
    [SerializeField] private bool makePlayerUntargetableDuringSequence = true;
    [SerializeField] private bool ignorePlayerCollisionDuringSequence = true;
    [SerializeField] private LayerMask playerProtectionExcludeLayers;
    [SerializeField] private bool logCoordinator;

    Coroutine _activeRoutine;
    ActiveChainRuntime _activeRuntime;
    int _playerInvincibilityToken;
    int _playerUntargetableToken;
    int _playerCollisionIgnoreToken;
    bool _playerProtectionApplied;

    public bool IsSequenceActive => _activeRoutine != null;
    public Transform LockedTarget => _activeRuntime != null ? _activeRuntime.targetTransform : null;
    public event Action<ChainAttackSequenceDef, GameObject, bool> SequenceFinished;
    float WorldNow => TimeSlowManager.Instance.WorldTime;

    void Awake()
    {
        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();

        playerContext?.ResolveReferences();

        if (fieldAllyManager == null)
            fieldAllyManager = playerContext != null ? playerContext.fieldAllyManager : null;
        if (fieldAllyManager == null)
            fieldAllyManager = GetComponent<FieldAllyManager>();

        if (allyHelperManager == null && playerContext != null)
            allyHelperManager = playerContext.allyHelper;

        if (playerContext != null && playerContext.chainAttackCoordinator == null)
            playerContext.chainAttackCoordinator = this;
    }

    void OnDisable()
    {
        RestorePlayerProtection();
    }

    void OnDestroy()
    {
        RestorePlayerProtection();
    }

    public bool TryStartSequence(ChainAttackSequenceDef sequenceDef)
    {
        return TryStartSequence(sequenceDef, (Transform)null);
    }

    public bool CanStartSequence(ChainAttackSequenceDef sequenceDef)
    {
        return CanStartSequence(sequenceDef, (Transform)null);
    }

    public bool TryStartSequence(ChainAttackSequenceDef sequenceDef, GameObject explicitTargetObject)
    {
        return TryStartSequence(sequenceDef, explicitTargetObject != null ? explicitTargetObject.transform : null);
    }

    public bool TryStartSequence(ChainAttackSequenceDef sequenceDef, PassiveEventContext context)
    {
        return TryStartSequence(sequenceDef, context.Target != null ? context.Target.transform : null);
    }

    public bool CanStartSequence(ChainAttackSequenceDef sequenceDef, GameObject explicitTargetObject)
    {
        return CanStartSequence(sequenceDef, explicitTargetObject != null ? explicitTargetObject.transform : null);
    }

    public bool CanStartSequence(ChainAttackSequenceDef sequenceDef, PassiveEventContext context)
    {
        return CanStartSequence(sequenceDef, context.Target != null ? context.Target.transform : null);
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
            startedAt = WorldNow,
        };

        if (ShouldProtectPlayer(runtime: _activeRuntime))
            ApplyPlayerProtection();
        _activeRoutine = StartCoroutine(RunSequence(_activeRuntime));
        Log(sequenceDef, $"Started sequence '{sequenceDef.RuntimeId}' on target '{targetObject.name}'.");
        return true;
    }

    public bool CanStartSequence(ChainAttackSequenceDef sequenceDef, Transform explicitTargetTransform)
    {
        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();

        playerContext?.ResolveReferences();

        if (allyHelperManager == null && playerContext != null)
            allyHelperManager = playerContext.allyHelper;

        if (_activeRoutine != null || sequenceDef == null || !sequenceDef.HasAnySteps)
            return false;

        if (playerContext == null)
            return false;

        return ChainAttackTargetingUtility.TryResolveLockedTarget(
            playerContext,
            sequenceDef,
            explicitTargetTransform,
            allyHelperManager != null ? allyHelperManager.HelperObject : null,
            out _,
            out _,
            out _);
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
                yield return WaitForWorldSeconds(step.delayBefore);

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
                yield return WaitForWorldSeconds(interval);
        }

        while (runtime.pendingTrackedStepCompletions > 0)
            yield return null;

        if (runtime.hadLateStepFailure)
            completedSuccessfully = false;

        if (fieldAllyManager != null)
        {
            fieldAllyManager.FinalizeSequenceReservations(runtime, interrupted: !completedSuccessfully);
            while (fieldAllyManager.HasOwnedSequenceWork(runtime))
                yield return null;
        }

        Log(
            runtime.sequenceDef,
            completedSuccessfully
                ? $"Sequence '{runtime.sequenceDef.RuntimeId}' completed."
                : $"Sequence '{runtime.sequenceDef.RuntimeId}' ended early.");

        var finishedDef = runtime.sequenceDef;
        var finishedTarget = runtime.targetObject;

        RestorePlayerProtection();
        _activeRoutine = null;
        _activeRuntime = null;

        SequenceFinished?.Invoke(finishedDef, finishedTarget, completedSuccessfully);
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

        bool started = member.TryStartSequenceStep(step, runtime.targetTransform, runtime.targetAnchor);
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

        while (runtime.pendingTrackedStepCompletions > 0 &&
               allyHelperManager.ActiveChainAttackExecutionId > 0)
        {
            if (!IsRuntimeStillValid(runtime))
            {
                onFinished(false);
                yield break;
            }

            yield return null;
        }

        ChainStepContinueMode helperContinueMode = ResolveHelperContinueMode(step);
        float helperContinueNormalizedTime = ResolveHelperContinueNormalizedTime(step, helperContinueMode);
        bool started = step.helperChainAttackSequence != null
            ? allyHelperManager.TryStartChainAttackHelperToTarget(
                step.helperChainAttackSequence,
                step.skillDef,
                runtime.targetAnchor != null ? runtime.targetAnchor : runtime.targetTransform,
                step.helperHideOnComplete,
                helperContinueMode,
                helperContinueNormalizedTime)
            : allyHelperManager.TrySummonAllyHelper(
                step.skillDef,
                step.helperHideOnComplete);

        if (!started)
        {
            Log(runtime.sequenceDef, $"Helper step '{step.RuntimeId}' failed to start.");
            onFinished(false);
            yield break;
        }

        if (step.helperChainAttackSequence == null || helperContinueMode == ChainStepContinueMode.OnStepComplete)
        {
            while (allyHelperManager.IsHelperBusy)
                yield return null;

            onFinished(allyHelperManager.LastExecutionSucceeded);
            yield break;
        }

        int executionId = allyHelperManager.ActiveChainAttackExecutionId;
        if (executionId <= 0)
        {
            while (allyHelperManager.IsHelperBusy)
                yield return null;

            onFinished(allyHelperManager.LastExecutionSucceeded);
            yield break;
        }

        while (true)
        {
            if (allyHelperManager.TryGetCompletedChainAttackExecutionResult(executionId, out bool success))
            {
                onFinished(success);
                yield break;
            }

            if (allyHelperManager.IsChainAttackExecutionReadyToContinue(executionId))
            {
                TrackHelperStepCompletion(runtime, allyHelperManager, executionId, step);
                onFinished(true);
                yield break;
            }

            yield return null;
        }
    }

    void TrackHelperStepCompletion(
        ActiveChainRuntime runtime,
        AllyHelperManager helperManager,
        int executionId,
        ChainAttackStepDef step)
    {
        if (runtime == null || helperManager == null || executionId <= 0)
            return;

        runtime.pendingTrackedStepCompletions++;
        StartCoroutine(WaitForTrackedHelperStepCompletion(runtime, helperManager, executionId, step));
    }

    IEnumerator WaitForTrackedHelperStepCompletion(
        ActiveChainRuntime runtime,
        AllyHelperManager helperManager,
        int executionId,
        ChainAttackStepDef step)
    {
        bool success = false;

        while (helperManager != null &&
               !helperManager.TryGetCompletedChainAttackExecutionResult(executionId, out success))
        {
            yield return null;
        }

        if (runtime != null && runtime.pendingTrackedStepCompletions > 0)
            runtime.pendingTrackedStepCompletions--;

        if (runtime == null)
            yield break;

        if (helperManager == null)
        {
            runtime.hadLateStepFailure = true;
            yield break;
        }

        if (!success)
        {
            runtime.hadLateStepFailure = true;
            Log(runtime.sequenceDef, $"Helper step '{step.RuntimeId}' failed after releasing its early continue signal.");
        }
    }

    static ChainStepContinueMode ResolveHelperContinueMode(ChainAttackStepDef step)
    {
        if (step == null)
            return ChainStepContinueMode.OnStepComplete;

        if (step.UsesStepContinueOverride)
            return step.continueMode;

        return step.skillDef != null && step.skillDef.payload != null
            ? step.skillDef.payload.GetChainContinueMode()
            : ChainStepContinueMode.OnStepComplete;
    }

    static float ResolveHelperContinueNormalizedTime(ChainAttackStepDef step, ChainStepContinueMode continueMode)
    {
        if (continueMode == ChainStepContinueMode.OnAttackCastMoment)
        {
            return step?.skillDef != null
                ? step.skillDef.GetCastPointNormalized()
                : step != null ? step.ClampedContinueNormalizedTime : 1f;
        }

        if (continueMode == ChainStepContinueMode.OnAttackNormalizedTime)
        {
            if (step != null && step.UsesStepContinueOverride)
                return step.ClampedContinueNormalizedTime;

            return step?.skillDef != null && step.skillDef.payload != null
                ? step.skillDef.payload.GetChainContinueNormalizedTime()
                : 1f;
        }

        return 1f;
    }

    bool IsRuntimeStillValid(ActiveChainRuntime runtime)
    {
        if (runtime == null || runtime.sequenceDef == null)
            return false;

        if (WorldNow - runtime.startedAt > runtime.sequenceDef.maxSequenceDurationSeconds)
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

    IEnumerator WaitForWorldSeconds(float seconds)
    {
        float remaining = Mathf.Max(0f, seconds);
        while (remaining > 0f)
        {
            remaining -= TimeSlowManager.Instance.WorldDeltaTime;
            yield return null;
        }
    }

    void ApplyPlayerProtection()
    {
        if (_playerProtectionApplied || !protectPlayerDuringSequence || playerContext == null)
            return;

        if (makePlayerInvincibleDuringSequence && playerContext.HealthSystem != null)
            _playerInvincibilityToken = playerContext.HealthSystem.AcquireInvincibilityToken();

        if (makePlayerUntargetableDuringSequence)
        {
            AITargetInfo targetInfo = ResolvePlayerTargetInfo();
            if (targetInfo != null)
                _playerUntargetableToken = targetInfo.AcquireUntargetableToken();
        }

        if (ignorePlayerCollisionDuringSequence && playerContext.DashSystem != null)
            _playerCollisionIgnoreToken = playerContext.DashSystem.AcquireExternalCollisionIgnoreToken(
                ResolvePlayerProtectionExcludeLayers());

        _playerProtectionApplied = true;
    }

    bool ShouldProtectPlayer(ActiveChainRuntime runtime)
    {
        if (runtime == null || runtime.sequenceDef == null || runtime.sequenceDef.steps == null)
            return false;

        for (int i = 0; i < runtime.sequenceDef.steps.Length; i++)
        {
            ChainAttackStepDef step = runtime.sequenceDef.steps[i];
            if (step != null && step.actorRole == ChainActorRole.Player)
                return true;
        }

        return false;
    }

    void RestorePlayerProtection()
    {
        if (!_playerProtectionApplied)
            return;

        if (_playerCollisionIgnoreToken != 0 && playerContext != null && playerContext.DashSystem != null)
            playerContext.DashSystem.ReleaseExternalCollisionIgnoreToken(_playerCollisionIgnoreToken);

        AITargetInfo targetInfo = ResolvePlayerTargetInfo();
        if (_playerUntargetableToken != 0 && targetInfo != null)
            targetInfo.ReleaseUntargetableToken(_playerUntargetableToken);

        if (_playerInvincibilityToken != 0 && playerContext != null && playerContext.HealthSystem != null)
            playerContext.HealthSystem.ReleaseInvincibilityToken(_playerInvincibilityToken);

        _playerCollisionIgnoreToken = 0;
        _playerUntargetableToken = 0;
        _playerInvincibilityToken = 0;
        _playerProtectionApplied = false;
    }

    AITargetInfo ResolvePlayerTargetInfo()
    {
        if (playerContext == null)
            return null;

        AITargetInfo targetInfo = playerContext.GetComponent<AITargetInfo>();
        if (targetInfo != null)
            return targetInfo;

        return playerContext.GetComponentInChildren<AITargetInfo>(true);
    }

    LayerMask ResolvePlayerProtectionExcludeLayers()
    {
        if (playerProtectionExcludeLayers.value != 0)
            return playerProtectionExcludeLayers;

        return LayerMask.GetMask("Enemy", "EnemyBullet", "Ally");
    }
}
