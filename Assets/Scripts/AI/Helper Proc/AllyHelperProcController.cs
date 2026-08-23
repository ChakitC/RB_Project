using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-109)]
public sealed class AllyHelperProcController : MonoBehaviour
{
    [SerializeField] private PlayerContext playerContext;
    [SerializeField] private CombatEventBus combatEventBus;
    [SerializeField] private AllyHelperManager allyHelperManager;
    [SerializeField, Min(0.1f)] private float attackIdLockTtlSeconds = 10f;
    [SerializeField] private bool logProcController;

    readonly Dictionary<SkillHelperDef, float> _nextReadyTimeByDef = new();
    readonly Dictionary<string, float> _attackIdLocks = new();
    readonly List<SkillHelperDef> _resolvedHelperDefinitions = new();
    readonly HashSet<SkillHelperDef> _resolvedHelperDefinitionSet = new();

    readonly List<HealthSystem> _watchedHealth = new();
    readonly HashSet<SkillHelperDef> _queuedThresholdDefs = new();

    FieldAllyManager _fieldAllyManager;
    Coroutine _queueDrain;
    Coroutine _chargeWakeup;

    /// <summary>Manager we last subscribed to, so a late-resolved reference still gets hooked up.</summary>
    AllyHelperManager _loadoutSubscribedManager;

    /// <summary>Skill manager the cached proc list was built from. Null until the first build.</summary>
    CharacterSkillManager _builtFromSkillManager;

    /// <summary>Skill manager we last subscribed to for proc variant switches.</summary>
    CharacterSkillManager _procLoadoutSubscribedSkillManager;
    bool _helperDefinitionsDirty = true;

    bool _subscribed;
    bool _partySubscribed;

    /// <summary>How often a queued threshold request re-checks whether the helper has freed up.</summary>
    const float QueueDrainInterval = 0.25f;
    float WorldNow => TimeSlowManager.Instance.WorldTime;

    void Awake()
    {
        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();

        playerContext?.ResolveReferences();

        if (combatEventBus == null)
            combatEventBus = playerContext != null ? playerContext.CombatEventBus : null;
        if (combatEventBus == null)
            combatEventBus = GetComponent<CombatEventBus>();

        if (allyHelperManager == null && playerContext != null)
            allyHelperManager = playerContext.allyHelper;
    }

    void OnEnable()
    {
        _helperDefinitionsDirty = true;
        Subscribe();
        SubscribeParty();
        SubscribeHelperLoadout();
        EvaluatePartyHealthTriggers();
    }

    void OnDisable()
    {
        Unsubscribe();
        UnsubscribeParty();
        SubscribeHelperLoadout(null);
        SubscribeProcLoadout(null);
        StopQueueDrain();
        StopChargeWakeup();
        _queuedThresholdDefs.Clear();
        _attackIdLocks.Clear();
    }

    public bool TryTriggerHelper(SkillHelperDef helperDef)
    {
        if (helperDef == null || allyHelperManager == null)
            return false;

        if (!CanStartHelper(helperDef))
            return false;

        bool started = TryStartHelperExecution(helperDef);

        if (started)
            StampCooldown(helperDef);

        return started;
    }

    void Subscribe()
    {
        if (_subscribed || combatEventBus == null)
            return;

        combatEventBus.EventPublished += OnCombatEventPublished;
        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (!_subscribed || combatEventBus == null)
            return;

        combatEventBus.EventPublished -= OnCombatEventPublished;
        _subscribed = false;
    }

    void SubscribeHelperLoadout()
    {
        if (allyHelperManager == null && playerContext != null)
            allyHelperManager = playerContext.allyHelper;

        SubscribeHelperLoadout(allyHelperManager);
    }

    void SubscribeHelperLoadout(AllyHelperManager manager)
    {
        if (_loadoutSubscribedManager == manager)
            return;

        if (_loadoutSubscribedManager != null)
            _loadoutSubscribedManager.HelperLoadoutChanged -= OnHelperLoadoutChanged;

        _loadoutSubscribedManager = manager;

        if (_loadoutSubscribedManager != null)
            _loadoutSubscribedManager.HelperLoadoutChanged += OnHelperLoadoutChanged;
    }

    void OnHelperLoadoutChanged()
    {
        _helperDefinitionsDirty = true;
    }

    void OnCombatEventPublished(PassiveEventContext context)
    {
        CleanupExpiredAttackIdLocks();

        BuildRuntimeHelperDefinitions();
        if (_resolvedHelperDefinitions.Count == 0)
            return;

        for (int i = 0; i < _resolvedHelperDefinitions.Count; i++)
        {
            SkillHelperDef helperDef = _resolvedHelperDefinitions[i];
            if (!CanProc(helperDef, context))
                continue;

            if (!RollProc(helperDef))
                continue;

            bool started = TryStartHelperExecution(helperDef);

            if (!started)
            {
                Log(helperDef, $"Proc matched for '{helperDef.RuntimeId}' but helper execution failed.");
                continue;
            }

            StampCooldown(helperDef);
            StampAttackIdLock(helperDef, context.AttackId);
            Log(helperDef, $"Proc succeeded for '{helperDef.RuntimeId}' from event '{context.Type}'.");
        }
    }

    /// <summary>
    /// Rebuilds the proc list from the runtime helper's own character asset, and nothing else.
    ///
    /// Ally_Helper.prefab and every party-slot rig are shared, so a proc authored on one of them
    /// would fire for whoever happened to be loaded into it. The single source is the helper's
    /// <c>ctx.baseStats.helperProcSlots</c>, reached through its CharacterSkillManager, and only the
    /// variant selected in each slot.
    ///
    /// Cached between character swaps: this runs on every combat event, and the answer only
    /// changes when the helper rig is loaded with a different character.
    /// </summary>
    void BuildRuntimeHelperDefinitions()
    {
        if (allyHelperManager == null && playerContext != null)
        {
            playerContext.ResolveReferences();
            allyHelperManager = playerContext.allyHelper;
        }

        SubscribeHelperLoadout(allyHelperManager);

        CharacterSkillManager helperSkillManager =
            allyHelperManager != null ? allyHelperManager.HelperSkillManager : null;

        SubscribeProcLoadout(helperSkillManager);

        if (!_helperDefinitionsDirty && ReferenceEquals(helperSkillManager, _builtFromSkillManager))
            return;

        _helperDefinitionsDirty = false;
        _builtFromSkillManager = helperSkillManager;

        _resolvedHelperDefinitions.Clear();
        _resolvedHelperDefinitionSet.Clear();

        helperSkillManager?.AppendConfiguredHelperChainDefinitions(
            _resolvedHelperDefinitions,
            _resolvedHelperDefinitionSet);

        DropQueuedRequestsForUnequippedProcs();
    }

    void SubscribeProcLoadout(CharacterSkillManager skillManager)
    {
        if (_procLoadoutSubscribedSkillManager == skillManager)
            return;

        if (_procLoadoutSubscribedSkillManager != null)
            _procLoadoutSubscribedSkillManager.HelperProcLoadoutChanged -= OnHelperLoadoutChanged;

        _procLoadoutSubscribedSkillManager = skillManager;

        if (_procLoadoutSubscribedSkillManager != null)
            _procLoadoutSubscribedSkillManager.HelperProcLoadoutChanged += OnHelperLoadoutChanged;
    }

    /// <summary>
    /// Forgets threshold requests held for a variant that is no longer equipped.
    ///
    /// Internal cooldowns are deliberately left in <see cref="_nextReadyTimeByDef"/>: they are
    /// keyed by definition, so switching a variant out and back must not clear the wait.
    /// </summary>
    void DropQueuedRequestsForUnequippedProcs()
    {
        if (_queuedThresholdDefs.Count == 0)
            return;

        _queuedThresholdDefs.RemoveWhere(def => def == null || !_resolvedHelperDefinitionSet.Contains(def));
    }

    // ---------------------------------------------------------------------------------------
    // Party health threshold trigger
    //
    // Everything here is event-driven. Health changes and charge readiness are both things the
    // game already announces, so there is no reason for an assist that fires roughly twice a
    // minute to cost anything on a frame where nothing happened.
    // ---------------------------------------------------------------------------------------

    void SubscribeParty()
    {
        if (_partySubscribed)
            return;

        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();

        playerContext?.ResolveReferences();
        _fieldAllyManager = playerContext != null ? playerContext.fieldAllyManager : null;

        if (_fieldAllyManager == null)
            return;

        _fieldAllyManager.MemberRegistered += OnPartyMemberRegistered;
        _fieldAllyManager.MemberUnregistered += OnPartyMemberUnregistered;
        _partySubscribed = true;

        RebuildHealthSubscriptions();
    }

    void UnsubscribeParty()
    {
        if (_fieldAllyManager != null && _partySubscribed)
        {
            _fieldAllyManager.MemberRegistered -= OnPartyMemberRegistered;
            _fieldAllyManager.MemberUnregistered -= OnPartyMemberUnregistered;
        }

        _partySubscribed = false;
        ClearHealthSubscriptions();
    }

    void OnPartyMemberRegistered(ChainActorRole role, FieldAllyMember member)
    {
        RebuildHealthSubscriptions();
        EvaluatePartyHealthTriggers();
    }

    void OnPartyMemberUnregistered(ChainActorRole role)
    {
        RebuildHealthSubscriptions();
        EvaluatePartyHealthTriggers();
    }

    void RebuildHealthSubscriptions()
    {
        ClearHealthSubscriptions();

        if (_fieldAllyManager == null)
            return;

        foreach (FieldAllyMember member in _fieldAllyManager.RegisteredMembers)
        {
            HealthSystem health = ResolveMemberHealth(member);
            if (health == null || _watchedHealth.Contains(health))
                continue;

            health.HealthChanged += OnWatchedHealthChanged;
            _watchedHealth.Add(health);
        }
    }

    void ClearHealthSubscriptions()
    {
        for (int i = 0; i < _watchedHealth.Count; i++)
        {
            HealthSystem health = _watchedHealth[i];
            if (health != null)
                health.HealthChanged -= OnWatchedHealthChanged;
        }

        _watchedHealth.Clear();
    }

    void OnWatchedHealthChanged(float current, float max)
    {
        EvaluatePartyHealthTriggers();
    }

    static HealthSystem ResolveMemberHealth(FieldAllyMember member)
    {
        CharacteContext context = member != null ? member.ActorContext : null;
        if (context == null)
            return null;

        context.ResolveReferences();
        return context.HealthSystem;
    }

    void EvaluatePartyHealthTriggers()
    {
        if (!isActiveAndEnabled || allyHelperManager == null)
            return;

        BuildRuntimeHelperDefinitions();
        if (_resolvedHelperDefinitions.Count == 0)
            return;

        for (int i = 0; i < _resolvedHelperDefinitions.Count; i++)
        {
            SkillHelperDef helperDef = _resolvedHelperDefinitions[i];
            if (helperDef == null || !helperDef.IsPartyHealthTrigger)
                continue;

            EvaluatePartyHealthTrigger(helperDef);
        }
    }

    void EvaluatePartyHealthTrigger(SkillHelperDef helperDef)
    {
        if (!helperDef.HasExecutionConfigured)
            return;

        // Nobody to help means nothing to queue either: a request held for a target who is no
        // longer hurt would fire the moment the helper freed up, long after the danger passed.
        if (!TrySelectLowestHealthTarget(helperDef, out CharacteContext target))
        {
            _queuedThresholdDefs.Remove(helperDef);
            return;
        }

        if (!CanStartHelper(helperDef))
        {
            // Busy is temporary and worth waiting out; anything else (owner down, no execution
            // skill) is not going to resolve on its own.
            if (helperDef.queueWhileHelperBusy && allyHelperManager.IsHelperBusy)
            {
                _queuedThresholdDefs.Add(helperDef);
                StartQueueDrain();
            }

            return;
        }

        if (!IsThresholdChargeReady(helperDef, out float remaining))
        {
            // The charge pool already knows exactly when it will be ready, so wake up once then
            // instead of asking every frame.
            ScheduleChargeWakeup(remaining);
            return;
        }

        _queuedThresholdDefs.Remove(helperDef);

        bool started = allyHelperManager.TrySummonAllyHelperToTarget(
            helperDef.executionSkill,
            target,
            helperDef.hideHelperOnSkillComplete,
            SkillCastCostPolicy.IgnoreEnergyRespectCharge);

        if (!started)
        {
            Log(helperDef, $"Party-health trigger for '{helperDef.RuntimeId}' could not start the helper.");
            return;
        }

        // No cooldown stamp here on purpose. The cooldown belongs to the skill's charge pool and
        // starts when the cast transaction commits at its cast point - stamping a second timer
        // here would put the assist on two different clocks.
        Log(helperDef, $"Party-health trigger fired for '{helperDef.RuntimeId}'.");
    }

    /// <summary>
    /// Lowest health ratio at or below the threshold. Ties go to whoever is closer to the player,
    /// so repeated evaluations of the same situation always pick the same recipient.
    /// </summary>
    bool TrySelectLowestHealthTarget(SkillHelperDef helperDef, out CharacteContext target)
    {
        target = null;

        if (_fieldAllyManager == null)
            return false;

        float threshold = Mathf.Clamp01(helperDef.partyHealthThreshold);
        float bestRatio = float.MaxValue;
        float bestDistanceSqr = float.MaxValue;
        Vector3 playerPos = playerContext != null ? playerContext.transform.position : Vector3.zero;

        foreach (FieldAllyMember member in _fieldAllyManager.RegisteredMembers)
        {
            if (member == null || !helperDef.IsRoleEligible(member.ActorRole))
                continue;

            CharacteContext context = member.ActorContext;
            if (context == null || !context.isActiveAndEnabled)
                continue;

            HealthSystem health = ResolveMemberHealth(member);
            if (health == null || health.maximumHealth <= 0f)
                continue;

            // Down and dead are both excluded: an assist that healed a downed ally would bypass
            // whatever revive rules the game already has.
            if (!health.IsAlive || health.IsDown)
                continue;

            float ratio = health.currentHealth / health.maximumHealth;
            if (ratio > threshold)
                continue;

            float distanceSqr = (context.transform.position - playerPos).sqrMagnitude;

            bool better = ratio < bestRatio ||
                          (Mathf.Approximately(ratio, bestRatio) && distanceSqr < bestDistanceSqr);

            if (!better)
                continue;

            bestRatio = ratio;
            bestDistanceSqr = distanceSqr;
            target = context;
        }

        return target != null;
    }

    bool IsThresholdChargeReady(SkillHelperDef helperDef, out float remainingSeconds)
    {
        remainingSeconds = 0f;

        CharacterSkillManager skillManager = allyHelperManager.HelperSkillManager;
        if (skillManager == null)
            return false;

        if (!skillManager.TryGetExternalSkillChargeStatus(helperDef.executionSkill, out SkillChargeStatus status))
            return false;

        if (status.HasCharge)
            return true;

        remainingSeconds = status.NextChargeRemaining;
        return false;
    }

    void StartQueueDrain()
    {
        if (_queueDrain == null && isActiveAndEnabled)
            _queueDrain = StartCoroutine(DrainQueue());
    }

    void StopQueueDrain()
    {
        if (_queueDrain == null)
            return;

        StopCoroutine(_queueDrain);
        _queueDrain = null;
    }

    /// <summary>
    /// Re-checks queued requests while the helper is busy. This runs only while something is
    /// actually waiting, and there is no event for "the helper stopped being busy" to hook.
    /// A queued request is fully re-validated on release, never replayed blindly.
    /// </summary>
    IEnumerator DrainQueue()
    {
        var wait = new WaitForSeconds(QueueDrainInterval);

        while (_queuedThresholdDefs.Count > 0)
        {
            yield return wait;
            EvaluatePartyHealthTriggers();
        }

        _queueDrain = null;
    }

    void ScheduleChargeWakeup(float remainingSeconds)
    {
        if (_chargeWakeup != null || !isActiveAndEnabled)
            return;

        if (remainingSeconds <= 0f || !float.IsFinite(remainingSeconds))
            return;

        _chargeWakeup = StartCoroutine(WakeOnChargeReady(remainingSeconds));
    }

    void StopChargeWakeup()
    {
        if (_chargeWakeup == null)
            return;

        StopCoroutine(_chargeWakeup);
        _chargeWakeup = null;
    }

    IEnumerator WakeOnChargeReady(float remainingSeconds)
    {
        // Small overshoot so the pool has definitely ticked over by the time we look.
        yield return new WaitForSeconds(remainingSeconds + 0.05f);

        _chargeWakeup = null;

        // Re-queries rather than assuming: max charges and cooldown both come from stats that
        // could have changed while we waited, so "ready" has to be asked again, not remembered.
        EvaluatePartyHealthTriggers();
    }

    bool CanProc(SkillHelperDef helperDef, PassiveEventContext context)
    {
        if (helperDef == null || allyHelperManager == null)
            return false;

        // Threshold procs are driven by party health, not by the combat bus. Letting them also
        // match a bus event would fire them twice for the same situation.
        if (helperDef.IsPartyHealthTrigger)
            return false;

        if (helperDef.triggerEvent != context.Type)
            return false;

        if (!MatchesOriginFilter(helperDef.originFilter, context.Origin))
        {
            Log(helperDef, $"Blocked '{helperDef.RuntimeId}': origin '{context.Origin}' does not match filter '{helperDef.originFilter}'.");
            return false;
        }

        if (helperDef.requireTarget && context.Target == null)
        {
            Log(helperDef, $"Blocked '{helperDef.RuntimeId}': event '{context.Type}' has no target.");
            return false;
        }

        if (helperDef.requireAttackId && string.IsNullOrWhiteSpace(context.AttackId))
        {
            Log(helperDef, $"Blocked '{helperDef.RuntimeId}': event '{context.Type}' has no attackId.");
            return false;
        }

        if (helperDef.oncePerAttackId && IsAttackIdLocked(helperDef, context.AttackId))
        {
            Log(helperDef, $"Blocked '{helperDef.RuntimeId}': attackId '{context.AttackId}' is already consumed.");
            return false;
        }

        if (!IsCooldownReady(helperDef))
        {
            Log(helperDef, $"Blocked '{helperDef.RuntimeId}': internal cooldown is still active.");
            return false;
        }

        if (!CanStartHelper(helperDef))
        {
            Log(helperDef, $"Blocked '{helperDef.RuntimeId}': helper cannot start right now.");
            return false;
        }

        return true;
    }

    bool CanStartHelper(SkillHelperDef helperDef)
    {
        if (helperDef == null || !helperDef.HasExecutionConfigured)
            return false;

        if (helperDef.requireOwnerAlive &&
            playerContext != null &&
            playerContext.stateHub != null &&
            (!playerContext.stateHub.IsAlive || playerContext.stateHub.Isdown))
        {
            return false;
        }

        if (helperDef.blockWhileHelperBusy && allyHelperManager.IsHelperBusy)
            return false;

        if (helperDef.chainAttackSequence != null &&
            !allyHelperManager.HasChainAttackTarget(helperDef.chainAttackSequence))
        {
            return false;
        }

        return true;
    }

    bool TryStartHelperExecution(SkillHelperDef helperDef)
    {
        if (helperDef == null || allyHelperManager == null)
            return false;

        if (helperDef.chainAttackSequence != null)
        {
            return allyHelperManager.TryStartChainAttackHelper(
                helperDef.chainAttackSequence,
                helperDef.executionSkill,
                helperDef.hideHelperOnSkillComplete);
        }

        return allyHelperManager.TrySummonAllyHelper(
            helperDef.executionSkill,
            helperDef.hideHelperOnSkillComplete);
    }

    bool RollProc(SkillHelperDef helperDef)
    {
        float clampedChance = Mathf.Clamp01(helperDef.procChance);
        bool result = clampedChance > 0f && Random.value <= clampedChance;

        if (!result)
            Log(helperDef, $"Proc roll failed for '{helperDef.RuntimeId}' at chance {clampedChance:0.####}.");

        return result;
    }

    bool IsCooldownReady(SkillHelperDef helperDef)
    {
        if (helperDef == null)
            return false;

        if (!_nextReadyTimeByDef.TryGetValue(helperDef, out float readyAt))
            return true;

        if (WorldNow >= readyAt)
        {
            _nextReadyTimeByDef.Remove(helperDef);
            return true;
        }

        return false;
    }

    void StampCooldown(SkillHelperDef helperDef)
    {
        if (helperDef == null)
            return;

        float cooldown = Mathf.Max(0f, helperDef.internalCooldownSeconds);
        if (cooldown <= 0f)
        {
            _nextReadyTimeByDef.Remove(helperDef);
            return;
        }

        _nextReadyTimeByDef[helperDef] = WorldNow + cooldown;
    }

    bool IsAttackIdLocked(SkillHelperDef helperDef, string attackId)
    {
        if (helperDef == null || !helperDef.oncePerAttackId || string.IsNullOrWhiteSpace(attackId))
            return false;

        string key = BuildAttackIdLockKey(helperDef, attackId);
        if (!_attackIdLocks.TryGetValue(key, out float expiresAt))
            return false;

        if (WorldNow <= expiresAt)
            return true;

        _attackIdLocks.Remove(key);
        return false;
    }

    void StampAttackIdLock(SkillHelperDef helperDef, string attackId)
    {
        if (helperDef == null || !helperDef.oncePerAttackId || string.IsNullOrWhiteSpace(attackId))
            return;

        _attackIdLocks[BuildAttackIdLockKey(helperDef, attackId)] =
            WorldNow + Mathf.Max(attackIdLockTtlSeconds, helperDef.internalCooldownSeconds);
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

    static string BuildAttackIdLockKey(SkillHelperDef helperDef, string attackId)
    {
        return $"{helperDef.RuntimeId}:{attackId}";
    }

    void Log(SkillHelperDef helperDef, string message)
    {
        if (!logProcController && (helperDef == null || !helperDef.debugLogging))
            return;

        Debug.Log($"[AllyHelperProcController] {message}", this);
    }
}
