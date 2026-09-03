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

    // Extra budget the cleanup waits get on top of maxSequenceDurationSeconds, so a sequence that
    // ran right up to its limit still gets a moment to unwind before the watchdog cuts it off.
    const float SequenceCleanupGraceSeconds = 2f;

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

        // Every wait past this point is bounded. maxSequenceDurationSeconds used to be checked only
        // at the top of the step loop, so a step or a cleanup that never reported back left
        // IsSequenceActive true forever — and blockWhileChainBusy then locked out every future chain
        // in the run.
        while (runtime.pendingTrackedStepCompletions > 0)
        {
            if (HasExceededDuration(runtime, SequenceCleanupGraceSeconds))
            {
                LogTimeout(runtime, "waiting for tracked step completions");
                completedSuccessfully = false;
                break;
            }

            yield return null;
        }

        if (runtime.hadLateStepFailure)
            completedSuccessfully = false;

        if (fieldAllyManager != null)
        {
            fieldAllyManager.FinalizeSequenceReservations(runtime, interrupted: !completedSuccessfully);
            while (fieldAllyManager.HasOwnedSequenceWork(runtime))
            {
                if (HasExceededDuration(runtime, SequenceCleanupGraceSeconds))
                {
                    LogTimeout(runtime, "waiting for field ally cleanup");
                    completedSuccessfully = false;
                    break;
                }

                yield return null;
            }
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

        // A downed or dead party member is exactly what skipIfActorUnavailable describes, but it is
        // registered and reservable, so it used to sail past both unavailability gates and fail deep
        // inside TryStartSequenceStep instead — which the coordinator can only read as a hard step
        // failure. With stopWhenAnyStepFails set, one member being down aborted the whole chain and
        // the target went straight to Stagger with nobody having attacked.
        if (step.requireActorAlive && !member.IsAlive)
        {
            Log(
                runtime.sequenceDef,
                $"Step '{step.RuntimeId}' treated as unavailable: actor '{member.name}' is down or dead.");
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

            if (HasExceededDuration(runtime))
            {
                member.ReleaseReservation(runtime);
                LogTimeout(runtime, $"waiting for step '{step.RuntimeId}' on actor '{member.name}'");
                onFinished(false);
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
        bool timedOut = false;

        while (member != null &&
               !member.TryGetCompletedSequenceExecutionResult(executionId, out success, out hasDeferredCleanup))
        {
            if (HasExceededDuration(runtime, SequenceCleanupGraceSeconds))
            {
                LogTimeout(runtime, $"waiting for tracked step '{step.RuntimeId}' on actor '{member.name}'");
                timedOut = true;
                break;
            }

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

        if (timedOut || !hasDeferredCleanup)
            member.ReleaseReservation(runtime);

        if (timedOut)
        {
            runtime.hadLateStepFailure = true;
            yield break;
        }

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

        SkillGemDefinition helperSkillDef = ResolveHelperStepSkill(step);

        // Continue timing is read off the skill that is actually cast, so an ActorDefault helper step
        // takes its cast moment from the helper's own skill rather than from an unrelated one.
        ChainStepContinueMode helperContinueMode = ResolveHelperContinueMode(step, helperSkillDef);
        float helperContinueNormalizedTime =
            ResolveHelperContinueNormalizedTime(step, helperSkillDef, helperContinueMode);

        if (helperSkillDef == null)
        {
            Log(
                runtime.sequenceDef,
                $"Helper step '{step.RuntimeId}' has no chain skill: skillSource={step.skillSource} and the " +
                "loaded helper character has no Default Chain Skill authored on its CharacterStats.");
            onFinished(step.skipIfActorUnavailable);
            yield break;
        }

        bool started = step.helperChainAttackSequence != null
            ? allyHelperManager.TryStartChainAttackHelperToTarget(
                step.helperChainAttackSequence,
                helperSkillDef,
                runtime.targetAnchor != null ? runtime.targetAnchor : runtime.targetTransform,
                step.helperHideOnComplete,
                helperContinueMode,
                helperContinueNormalizedTime)
            : allyHelperManager.TrySummonAllyHelper(
                helperSkillDef,
                step.helperHideOnComplete);

        if (!started)
        {
            // "The helper would not start" is an availability problem (not summoned, busy, no
            // loadout), so it answers to the same flag the other actor roles use rather than
            // hard-failing the sequence on its own.
            Log(runtime.sequenceDef, $"Helper step '{step.RuntimeId}' failed to start.");
            onFinished(step.skipIfActorUnavailable);
            yield break;
        }

        if (step.helperChainAttackSequence == null || helperContinueMode == ChainStepContinueMode.OnStepComplete)
        {
            while (allyHelperManager.IsHelperBusy)
            {
                if (HasExceededDuration(runtime))
                {
                    LogTimeout(runtime, $"waiting for helper step '{step.RuntimeId}'");
                    onFinished(false);
                    yield break;
                }

                yield return null;
            }

            onFinished(allyHelperManager.LastExecutionSucceeded);
            yield break;
        }

        int executionId = allyHelperManager.ActiveChainAttackExecutionId;
        if (executionId <= 0)
        {
            while (allyHelperManager.IsHelperBusy)
            {
                if (HasExceededDuration(runtime))
                {
                    LogTimeout(runtime, $"waiting for helper step '{step.RuntimeId}'");
                    onFinished(false);
                    yield break;
                }

                yield return null;
            }

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

            if (HasExceededDuration(runtime))
            {
                LogTimeout(runtime, $"waiting for helper step '{step.RuntimeId}'");
                onFinished(false);
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
        bool timedOut = false;

        while (helperManager != null &&
               !helperManager.TryGetCompletedChainAttackExecutionResult(executionId, out success))
        {
            if (HasExceededDuration(runtime, SequenceCleanupGraceSeconds))
            {
                LogTimeout(runtime, $"waiting for tracked helper step '{step.RuntimeId}'");
                timedOut = true;
                break;
            }

            yield return null;
        }

        if (runtime != null && runtime.pendingTrackedStepCompletions > 0)
            runtime.pendingTrackedStepCompletions--;

        if (runtime == null)
            yield break;

        if (helperManager == null || timedOut)
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

    /// <summary>
    /// Picks the skill a helper step casts. The helper path used to hand <c>step.skillDef</c> straight
    /// through and never look at <see cref="ChainStepSkillSource"/>, so ActorDefault silently meant
    /// "cast nothing" on a helper step, and an explicit skill was cast by whichever character happened
    /// to be loaded into the helper rig. An explicit skill still wins; ActorDefault, or an empty
    /// explicit slot, now falls back to the helper character's own Default Chain Skill.
    /// </summary>
    SkillGemDefinition ResolveHelperStepSkill(ChainAttackStepDef step)
    {
        if (step == null)
            return null;

        if (!step.UsesActorDefaultSkill && step.skillDef != null)
            return step.skillDef;

        SkillGemDefinition actorDefault = allyHelperManager != null
            ? allyHelperManager.ResolvedHelperChainAttackSkill
            : null;

        return actorDefault != null ? actorDefault : step.skillDef;
    }

    static ChainStepContinueMode ResolveHelperContinueMode(
        ChainAttackStepDef step,
        SkillGemDefinition resolvedSkillDef)
    {
        if (step == null)
            return ChainStepContinueMode.OnStepComplete;

        if (step.UsesStepContinueOverride)
            return step.continueMode;

        return resolvedSkillDef != null && resolvedSkillDef.payload != null
            ? resolvedSkillDef.payload.GetChainContinueMode()
            : ChainStepContinueMode.OnStepComplete;
    }

    static float ResolveHelperContinueNormalizedTime(
        ChainAttackStepDef step,
        SkillGemDefinition resolvedSkillDef,
        ChainStepContinueMode continueMode)
    {
        if (continueMode == ChainStepContinueMode.OnAttackCastMoment)
        {
            return resolvedSkillDef != null
                ? resolvedSkillDef.GetCastPointNormalized()
                : step != null ? step.ClampedContinueNormalizedTime : 1f;
        }

        if (continueMode == ChainStepContinueMode.OnAttackNormalizedTime)
        {
            if (step != null && step.UsesStepContinueOverride)
                return step.ClampedContinueNormalizedTime;

            return resolvedSkillDef != null && resolvedSkillDef.payload != null
                ? resolvedSkillDef.payload.GetChainContinueNormalizedTime()
                : 1f;
        }

        return 1f;
    }

    /// <summary>
    /// The sequence's hard wall clock. Every wait in the coordinator is measured against this so a
    /// step that never reports back cannot pin IsSequenceActive on forever.
    /// </summary>
    bool HasExceededDuration(ActiveChainRuntime runtime, float extraGraceSeconds = 0f)
    {
        if (runtime == null || runtime.sequenceDef == null)
            return true;

        float budget = runtime.sequenceDef.maxSequenceDurationSeconds + Mathf.Max(0f, extraGraceSeconds);
        return WorldNow - runtime.startedAt > budget;
    }

    void LogTimeout(ActiveChainRuntime runtime, string what)
    {
        if (runtime == null || runtime.sequenceDef == null)
            return;

        Debug.LogWarning(
            $"[ChainAttackCoordinator] Sequence '{runtime.sequenceDef.RuntimeId}' timed out {what}. " +
            "Ending the sequence so it cannot block future chains.",
            this);
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
