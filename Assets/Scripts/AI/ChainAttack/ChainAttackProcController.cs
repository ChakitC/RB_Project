using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DefaultExecutionOrder(-106)]
public sealed class ChainAttackProcController : MonoBehaviour
{
    [SerializeField] private PlayerContext playerContext;
    [SerializeField] private CombatEventBus combatEventBus;
    [SerializeField] private ChainAttackCoordinator chainAttackCoordinator;
    [FormerlySerializedAs("procDefinitions")]
    [SerializeField] private SkillChainDef[] skillChainDefinitions;
    [SerializeField, Min(0.1f)] private float attackIdLockTtlSeconds = 10f;
    [SerializeField] private bool logProcController;

    readonly Dictionary<SkillChainDef, float> _nextReadyTimeByDef = new();
    readonly Dictionary<string, float> _attackIdLocks = new();
    readonly List<SkillChainDef> _resolvedSkillChainDefinitions = new();
    readonly HashSet<SkillChainDef> _resolvedSkillChainSet = new();

    StaggerMeter _pendingChainReadyMeter;
    GameObject _pendingChainReadyTarget;
    SkillTargetHandle _pendingChainReadyTargetHandle = SkillTargetHandle.None;
    SkillChainDef _pendingChainReadyDef;
    bool _subscribed;
    CombatEventBus _subscribedCombatEventBus;
    ChainAttackCoordinator _subscribedCoordinator;

    int _nextIntroRequestId = 900000;
    int _pendingIntroId;
    SkillChainDef _pendingIntroDef;
    SkillTargetHandle _pendingIntroTargetHandle = SkillTargetHandle.None;
    CharacterAnimBrain _animBrain;
    CharacterAnimBrain _subscribedIntroBrain;
    CharacterAnimDriver _animDriver;
    CutsceneSkillPresenter _cutscenePresenter;
    bool _introGateActive;

    float WorldNow => TimeSlowManager.Instance.WorldTime;
    public bool IsSequenceActive => chainAttackCoordinator != null && chainAttackCoordinator.IsSequenceActive;

    void Awake()
    {
        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();

        playerContext?.ResolveReferences();

        if (combatEventBus == null)
            combatEventBus = playerContext != null ? playerContext.CombatEventBus : null;
        if (combatEventBus == null)
            combatEventBus = GetComponent<CombatEventBus>();

        if (chainAttackCoordinator == null && playerContext != null)
            chainAttackCoordinator = playerContext.chainAttackCoordinator;

        if (chainAttackCoordinator == null)
            chainAttackCoordinator = GetComponent<ChainAttackCoordinator>();

        ResolveAnimBrainAndPresenter();
    }

    void OnEnable()
    {
        Subscribe();
    }

    void OnDisable()
    {
        if (_introGateActive)
        {
            if (_cutscenePresenter != null)
                _cutscenePresenter.EndChainIntro(_pendingIntroId);
            ClearIntroGate();
        }

        Unsubscribe();
        _attackIdLocks.Clear();

        // Dropping the reference here used to strand the target: it stays ChainReady with its chain
        // execution flag set, which freezes its own countdown. Hand the break back instead.
        AbortPendingChainReady(refundReason: "owner disabled mid-chain");
    }

    public bool TryTriggerSequence(SkillChainDef chainDef)
    {
        return TryStartManualSequence(chainDef);
    }

    public bool TryStartManualSequence(SkillChainDef chainDef)
    {
        if (chainDef == null || chainAttackCoordinator == null)
            return false;

        if (!CanStartManualSequence(chainDef))
            return false;

        bool started = chainAttackCoordinator.TryStartSequence(chainDef.chainSequence);
        if (started)
            StampCooldown(chainDef);

        return started;
    }

    public bool CanStartManualSequence(SkillChainDef chainDef)
    {
        return CanStartManualSequence(chainDef, null);
    }

    /// <summary>
    /// Validates against a target the caller already resolved, instead of resolving one again.
    /// The ChainReady press must use this: its own lookup deliberately accepts an off-screen
    /// ChainReady enemy, and a second lookup here would reject exactly that target and refuse a
    /// press the player was told they could make.
    /// </summary>
    public bool CanStartManualSequence(SkillChainDef chainDef, Transform explicitTargetTransform)
    {
        if (chainDef == null || chainAttackCoordinator == null)
            return false;

        if (!CanStartSequence(chainDef))
            return false;

        return chainAttackCoordinator.CanStartSequence(chainDef.chainSequence, explicitTargetTransform);
    }

    public bool IsSequenceCooldownActive(SkillChainDef chainDef, out float remainingSeconds)
    {
        remainingSeconds = 0f;

        if (chainDef == null)
            return false;

        if (!_nextReadyTimeByDef.TryGetValue(chainDef, out float readyAt))
            return false;

        remainingSeconds = readyAt - WorldNow;
        if (remainingSeconds <= 0f)
        {
            _nextReadyTimeByDef.Remove(chainDef);
            remainingSeconds = 0f;
            return false;
        }

        return true;
    }

    // The two subscriptions are independent and must stay that way. Auto-proc needs the event bus;
    // closing out a ChainReady chain needs the coordinator. Gating both on the bus being present
    // meant a rig without a CombatEventBus could start a manual chain and then never receive
    // SequenceFinished, leaving the target ChainReady forever.
    void Subscribe()
    {
        if (_subscribed)
            return;

        if (combatEventBus != null)
        {
            combatEventBus.EventPublished += OnCombatEventPublished;
            _subscribedCombatEventBus = combatEventBus;
        }

        if (chainAttackCoordinator != null)
        {
            chainAttackCoordinator.SequenceFinished += OnSequenceFinished;
            _subscribedCoordinator = chainAttackCoordinator;
        }

        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (!_subscribed)
            return;

        // Detach from whatever was actually subscribed, not from whatever the fields resolve to now.
        if (_subscribedCombatEventBus != null)
            _subscribedCombatEventBus.EventPublished -= OnCombatEventPublished;
        if (_subscribedCoordinator != null)
            _subscribedCoordinator.SequenceFinished -= OnSequenceFinished;

        _subscribedCombatEventBus = null;
        _subscribedCoordinator = null;
        _subscribed = false;
    }

    void OnCombatEventPublished(PassiveEventContext context)
    {
        CleanupExpiredAttackIdLocks();

        BuildRuntimeChainDefinitions();
        if (_resolvedSkillChainDefinitions.Count == 0)
            return;

        for (int i = 0; i < _resolvedSkillChainDefinitions.Count; i++)
        {
            SkillChainDef chainDef = _resolvedSkillChainDefinitions[i];
            if (!CanProc(chainDef, context))
                continue;

            if (!RollProc(chainDef))
                continue;

            bool started = chainAttackCoordinator.TryStartSequence(chainDef.chainSequence, context);
            if (!started)
            {
                Log(chainDef, $"Proc matched for '{chainDef.RuntimeId}' but chain attack execution failed.");
                continue;
            }

            StampCooldown(chainDef);
            StampAttackIdLock(chainDef, context.AttackId);
            Log(chainDef, $"Proc succeeded for '{chainDef.RuntimeId}' from event '{context.Type}'.");
        }
    }

    void BuildRuntimeChainDefinitions()
    {
        _resolvedSkillChainDefinitions.Clear();
        _resolvedSkillChainSet.Clear();

        playerContext?.ResolveReferences();
        AddUniqueDefinitions(skillChainDefinitions);
    }

    void AddUniqueDefinitions(SkillChainDef[] definitions)
    {
        if (definitions == null)
            return;

        for (int i = 0; i < definitions.Length; i++)
        {
            SkillChainDef definition = definitions[i];
            if (definition == null || !_resolvedSkillChainSet.Add(definition))
                continue;

            _resolvedSkillChainDefinitions.Add(definition);
        }
    }
    /// <summary>
    /// True while an intro cutscene is playing ahead of a ChainReady chain. The coordinator is not
    /// busy yet during that window, so callers have to consult this to avoid starting a second
    /// chain on top of the one already committed.
    /// </summary>
    public bool IsChainReadyIntroActive => _introGateActive;

    /// <summary>
    /// Raised when a ChainReady chain that was already paid for could not be carried through — the
    /// intro was interrupted, the sequence refused to start after it, or the owner was disabled.
    /// The cooldown is cleared before this fires; the listener owns refunding the command points.
    /// </summary>
    public event System.Action<SkillChainDef> ChainReadyChainAborted;

    public bool TryStartChainReadyManualSequence(
        SkillChainDef chainDef,
        GameObject target,
        Transform targetTransform,
        StaggerMeter meter)
    {
        if (chainDef == null || chainAttackCoordinator == null || meter == null)
            return false;

        SkillTargetHandle targetHandle = ChainAttackTargetingUtility.CreateTargetHandle(targetTransform);
        if (targetHandle == null || !targetHandle.TryResolveAliveTarget(out Transform resolvedTarget, out _) ||
            resolvedTarget != targetTransform)
        {
            return false;
        }

        // A chain is already committed and waiting on its intro. Starting another one here would
        // overwrite the pending intro state and strand the first target's meter.
        if (_introGateActive || _pendingChainReadyMeter != null)
            return false;

        if (chainDef.enableChainReadyIntroCutscene &&
            ResolveIntroCutsceneDef() != null &&
            TryStartIntroCutscene(chainDef, target, targetHandle, meter))
        {
            return true;
        }

        if (!chainAttackCoordinator.TryStartSequence(chainDef.chainSequence, targetHandle))
            return false;

        StampCooldown(chainDef);
        _pendingChainReadyDef = chainDef;
        _pendingChainReadyTarget = target;
        _pendingChainReadyTargetHandle = targetHandle;
        _pendingChainReadyMeter = meter;
        meter.BeginChainExecution();
        return true;
    }

    CutsceneDef ResolveIntroCutsceneDef()
    {
        if (playerContext == null || playerContext.baseStats == null)
            return null;
        return playerContext.baseStats.HasIntroChainCutscene
            ? playerContext.baseStats.introChainCutscene.cutscene
            : null;
    }

    bool TryStartIntroCutscene(
        SkillChainDef chainDef,
        GameObject target,
        SkillTargetHandle targetHandle,
        StaggerMeter meter)
    {
        ResolveAnimBrainAndPresenter();
        if (_animBrain == null || _animDriver == null) return false;

        CutsceneDef introDef = ResolveIntroCutsceneDef();
        if (introDef == null) return false;

        int introId = ++_nextIntroRequestId;

        bool stageStarted = _cutscenePresenter != null &&
            _cutscenePresenter.TryBeginChainIntro(introDef, introId);
        if (!stageStarted)
            return false;

        if (!_animDriver.TryPlayChainCutscene(introId, introDef))
        {
            _cutscenePresenter.EndChainIntro(introId);
            return false;
        }

        // The cooldown is stamped up front so the intro window cannot be spammed, but it is cleared
        // again (and the command points refunded) if the chain never actually runs — see
        // AbortPendingChainReady.
        StampCooldown(chainDef);
        _pendingChainReadyDef = chainDef;
        _pendingChainReadyTarget = target;
        _pendingChainReadyTargetHandle = targetHandle;
        _pendingChainReadyMeter = meter;
        meter.BeginChainExecution();

        _pendingIntroId = introId;
        _pendingIntroDef = chainDef;
        _pendingIntroTargetHandle = targetHandle;
        _introGateActive = true;
        _subscribedIntroBrain = _animBrain;
        _subscribedIntroBrain.ChainPlaybackCompleted += OnIntroChainPlaybackCompleted;
        _subscribedIntroBrain.ChainPlaybackInterrupted += OnIntroChainPlaybackInterrupted;
        return true;
    }

    void OnIntroChainPlaybackCompleted(int id)
    {
        if (!_introGateActive || id != _pendingIntroId) return;

        SkillChainDef def = _pendingIntroDef;
        SkillTargetHandle target = _pendingIntroTargetHandle;
        ClearIntroGate();

        if (_cutscenePresenter != null)
            _cutscenePresenter.EndChainIntro(id);

        StartChainAfterIntro(def, target);
    }

    void OnIntroChainPlaybackInterrupted(int id)
    {
        if (!_introGateActive || id != _pendingIntroId) return;
        ClearIntroGate();

        if (_cutscenePresenter != null)
            _cutscenePresenter.EndChainIntro(id);

        AbortPendingChainReady(refundReason: "intro cutscene interrupted");
    }

    void StartChainAfterIntro(SkillChainDef def, SkillTargetHandle target)
    {
        if (def == null || _pendingChainReadyTarget == null || target == null ||
            !target.TryResolveAliveTarget(out _, out _))
        {
            AbortPendingChainReady(refundReason: "chain target was lost during the intro cutscene");
            return;
        }

        if (!chainAttackCoordinator.TryStartSequence(def.chainSequence, target))
            AbortPendingChainReady(refundReason: "sequence refused to start after the intro cutscene");
    }

    void ClearIntroGate()
    {
        _introGateActive = false;
        _pendingIntroId = 0;
        _pendingIntroDef = null;
        _pendingIntroTargetHandle = SkillTargetHandle.None;

        // Unsubscribe from whichever Brain was subscribed, not whichever is resolved now.
        if (_subscribedIntroBrain != null)
        {
            _subscribedIntroBrain.ChainPlaybackCompleted -= OnIntroChainPlaybackCompleted;
            _subscribedIntroBrain.ChainPlaybackInterrupted -= OnIntroChainPlaybackInterrupted;
            _subscribedIntroBrain = null;
        }
    }

    /// <summary>
    /// Ends a ChainReady chain that was paid for but never ran. The target is handed its ordinary
    /// Stagger, the cooldown this attempt stamped is cleared, and listeners get the chance to refund
    /// the command points — otherwise a cutscene interruption costs the player CP for nothing.
    /// </summary>
    void AbortPendingChainReady(string refundReason)
    {
        StaggerMeter meter = _pendingChainReadyMeter;
        SkillTargetHandle targetHandle = _pendingChainReadyTargetHandle;
        SkillChainDef abortedDef = _pendingChainReadyDef;
        _pendingChainReadyMeter = null;
        _pendingChainReadyTarget = null;
        _pendingChainReadyTargetHandle = SkillTargetHandle.None;
        _pendingChainReadyDef = null;

        if (meter != null && targetHandle != null &&
            targetHandle.TryResolveAliveTarget(out _, out _) && meter.IsChainReady)
            meter.CompleteChainReadyAndEnterStagger();

        if (abortedDef == null)
            return;

        _nextReadyTimeByDef.Remove(abortedDef);
        Log(abortedDef, $"ChainReady chain '{abortedDef.RuntimeId}' aborted: {refundReason}.");
        ChainReadyChainAborted?.Invoke(abortedDef);
    }

    void ResolveAnimBrainAndPresenter()
    {
        if (_animDriver == null && playerContext != null)
            _animDriver = playerContext.AnimDriver;
        if (_animDriver == null)
            _animDriver = GetComponentInChildren<CharacterAnimDriver>(true);

        // The event source must be the Brain the command actually reaches. Resolving the two
        // independently lets a prefab with a stale or duplicated component subscribe to one Brain
        // while commanding another, and then the completion callback never arrives and the
        // ChainReady gate hangs with the meter still held.
        _animBrain = _animDriver != null ? _animDriver.Brain : null;
        if (_animBrain == null && playerContext != null)
            _animBrain = playerContext.AnimBrain;
        if (_animBrain == null)
            _animBrain = GetComponentInChildren<CharacterAnimBrain>(true);

        if (_cutscenePresenter == null)
            _cutscenePresenter = GetComponentInChildren<CutsceneSkillPresenter>(true);
        if (_cutscenePresenter == null && playerContext != null)
            _cutscenePresenter = playerContext.GetComponentInChildren<CutsceneSkillPresenter>(true);
    }

    void OnSequenceFinished(ChainAttackSequenceDef seq, GameObject target, bool success)
    {
        if (_pendingChainReadyMeter == null)
            return;

        // The coordinator runs one sequence at a time, so the sequence that just finished owns this
        // pending transaction. Always release the transaction; a mismatch is diagnostic only and
        // must not strand every later ChainReady press.
        if (target != _pendingChainReadyTarget)
        {
            Debug.LogWarning(
                $"[ChainAttackProcController] Sequence '{seq?.RuntimeId}' finished on " +
                $"'{(target != null ? target.name : "<null>")}' but the ChainReady press locked " +
                $"'{(_pendingChainReadyTarget != null ? _pendingChainReadyTarget.name : "<null>")}'. " +
                "Releasing the pending ChainReady transaction anyway.",
                this);
        }

        StaggerMeter meter = _pendingChainReadyMeter;
        SkillTargetHandle targetHandle = _pendingChainReadyTargetHandle;
        _pendingChainReadyMeter = null;
        _pendingChainReadyTarget = null;
        _pendingChainReadyTargetHandle = SkillTargetHandle.None;
        _pendingChainReadyDef = null;

        if (meter != null && targetHandle != null &&
            targetHandle.TryResolveAliveTarget(out _, out _) && meter.IsChainReady)
            meter.CompleteChainReadyAndEnterStagger();
    }

    bool CanProc(SkillChainDef chainDef, PassiveEventContext context)
    {
        if (chainDef == null || chainAttackCoordinator == null)
            return false;

        if (chainDef.triggerEvent != context.Type)
            return false;

        if (!MatchesOriginFilter(chainDef.originFilter, context.Origin))
        {
            Log(chainDef, $"Blocked '{chainDef.RuntimeId}': origin '{context.Origin}' does not match filter '{chainDef.originFilter}'.");
            return false;
        }

        if (chainDef.requireTarget && context.Target == null)
        {
            Log(chainDef, $"Blocked '{chainDef.RuntimeId}': event '{context.Type}' has no target.");
            return false;
        }

        if (context.Target != null)
        {
            StaggerMeter targetMeter = context.Target.GetComponentInParent<StaggerMeter>();
            if (targetMeter == null)
                targetMeter = context.Target.GetComponentInChildren<StaggerMeter>();
            if (targetMeter != null && targetMeter.IsChainReady)
            {
                Log(chainDef, $"Blocked '{chainDef.RuntimeId}': target is ChainReady (reserved for manual chain).");
                return false;
            }
        }

        if (chainDef.requireAttackId && string.IsNullOrWhiteSpace(context.AttackId))
        {
            Log(chainDef, $"Blocked '{chainDef.RuntimeId}': event '{context.Type}' has no attackId.");
            return false;
        }

        if (chainDef.oncePerAttackId && IsAttackIdLocked(chainDef, context.AttackId))
        {
            Log(chainDef, $"Blocked '{chainDef.RuntimeId}': attackId '{context.AttackId}' is already consumed.");
            return false;
        }

        if (!IsCooldownReady(chainDef))
        {
            Log(chainDef, $"Blocked '{chainDef.RuntimeId}': internal cooldown is still active.");
            return false;
        }

        if (!CanStartSequence(chainDef))
        {
            Log(chainDef, $"Blocked '{chainDef.RuntimeId}': chain attack cannot start right now.");
            return false;
        }

        return true;
    }

    bool CanStartSequence(SkillChainDef chainDef)
    {
        if (chainDef == null || !chainDef.HasExecutionConfigured)
            return false;

        if (chainDef.requireOwnerAlive &&
            playerContext != null &&
            playerContext.stateHub != null &&
            (!playerContext.stateHub.IsAlive || playerContext.stateHub.Isdown))
        {
            return false;
        }

        if (chainDef.blockWhileChainBusy && chainAttackCoordinator.IsSequenceActive)
            return false;

        return true;
    }

    bool RollProc(SkillChainDef chainDef)
    {
        float clampedChance = Mathf.Clamp01(chainDef.procChance);
        bool result = clampedChance > 0f && Random.value <= clampedChance;

        if (!result)
            Log(chainDef, $"Proc roll failed for '{chainDef.RuntimeId}' at chance {clampedChance:0.####}.");

        return result;
    }

    bool IsCooldownReady(SkillChainDef chainDef)
    {
        return chainDef != null && !IsSequenceCooldownActive(chainDef, out _);
    }

    void StampCooldown(SkillChainDef chainDef)
    {
        if (chainDef == null)
            return;

        float cooldown = Mathf.Max(0f, chainDef.internalCooldownSeconds);
        if (cooldown <= 0f)
        {
            _nextReadyTimeByDef.Remove(chainDef);
            return;
        }

        _nextReadyTimeByDef[chainDef] = WorldNow + cooldown;
    }

    bool IsAttackIdLocked(SkillChainDef chainDef, string attackId)
    {
        if (chainDef == null || !chainDef.oncePerAttackId || string.IsNullOrWhiteSpace(attackId))
            return false;

        string key = BuildAttackIdLockKey(chainDef, attackId);
        if (!_attackIdLocks.TryGetValue(key, out float expiresAt))
            return false;

        if (WorldNow <= expiresAt)
            return true;

        _attackIdLocks.Remove(key);
        return false;
    }

    void StampAttackIdLock(SkillChainDef chainDef, string attackId)
    {
        if (chainDef == null || !chainDef.oncePerAttackId || string.IsNullOrWhiteSpace(attackId))
            return;

        _attackIdLocks[BuildAttackIdLockKey(chainDef, attackId)] =
            WorldNow + Mathf.Max(attackIdLockTtlSeconds, chainDef.internalCooldownSeconds);
    }

    void CleanupExpiredAttackIdLocks()
    {
        if (_attackIdLocks.Count == 0)
            return;

        float now = WorldNow;
        List<string> expiredKeys = null;

        foreach (var pair in _attackIdLocks)
        {
            if (pair.Value > now)
                continue;

            expiredKeys ??= new List<string>();
            expiredKeys.Add(pair.Key);
        }

        if (expiredKeys == null)
            return;

        for (int i = 0; i < expiredKeys.Count; i++)
            _attackIdLocks.Remove(expiredKeys[i]);
    }

    static bool MatchesOriginFilter(PassiveOriginFilter filter, PassiveEventOrigin origin)
    {
        return filter switch
        {
            PassiveOriginFilter.ExternalOnly => origin == PassiveEventOrigin.External,
            PassiveOriginFilter.NonPassive => origin != PassiveEventOrigin.Passive,
            PassiveOriginFilter.PassiveOnly => origin == PassiveEventOrigin.Passive,
            PassiveOriginFilter.Any => true,
            _ => false,
        };
    }

    static string BuildAttackIdLockKey(SkillChainDef chainDef, string attackId)
    {
        return $"{chainDef.RuntimeId}:{attackId}";
    }

    void Log(SkillChainDef chainDef, string message)
    {
        if (!logProcController && (chainDef == null || !chainDef.debugLogging))
            return;

        Debug.Log($"[ChainAttackProcController] {message}", this);
    }
}
