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

    bool _subscribed;

    void Awake()
    {
        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();

        if (combatEventBus == null)
            combatEventBus = GetComponent<CombatEventBus>();

        if (chainAttackCoordinator == null && playerContext != null)
            chainAttackCoordinator = playerContext.chainAttackCoordinator;

        if (chainAttackCoordinator == null)
            chainAttackCoordinator = GetComponent<ChainAttackCoordinator>();
    }

    void OnEnable()
    {
        Subscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
        _attackIdLocks.Clear();
    }

    public bool TryTriggerSequence(SkillChainDef chainDef)
    {
        if (chainDef == null || chainAttackCoordinator == null)
            return false;

        if (!CanStartSequence(chainDef))
            return false;

        bool started = chainAttackCoordinator.TryStartSequence(chainDef.chainSequence);
        if (started)
            StampCooldown(chainDef);

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

    void OnCombatEventPublished(PassiveEventContext context)
    {
        CleanupExpiredAttackIdLocks();

        if (skillChainDefinitions == null || skillChainDefinitions.Length == 0)
            return;

        for (int i = 0; i < skillChainDefinitions.Length; i++)
        {
            SkillChainDef chainDef = skillChainDefinitions[i];
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
        if (chainDef == null)
            return false;

        if (!_nextReadyTimeByDef.TryGetValue(chainDef, out float readyAt))
            return true;

        if (Time.time >= readyAt)
        {
            _nextReadyTimeByDef.Remove(chainDef);
            return true;
        }

        return false;
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

        _nextReadyTimeByDef[chainDef] = Time.time + cooldown;
    }

    bool IsAttackIdLocked(SkillChainDef chainDef, string attackId)
    {
        if (chainDef == null || !chainDef.oncePerAttackId || string.IsNullOrWhiteSpace(attackId))
            return false;

        string key = BuildAttackIdLockKey(chainDef, attackId);
        if (!_attackIdLocks.TryGetValue(key, out float expiresAt))
            return false;

        if (Time.time <= expiresAt)
            return true;

        _attackIdLocks.Remove(key);
        return false;
    }

    void StampAttackIdLock(SkillChainDef chainDef, string attackId)
    {
        if (chainDef == null || !chainDef.oncePerAttackId || string.IsNullOrWhiteSpace(attackId))
            return;

        _attackIdLocks[BuildAttackIdLockKey(chainDef, attackId)] =
            Time.time + Mathf.Max(attackIdLockTtlSeconds, chainDef.internalCooldownSeconds);
    }

    void CleanupExpiredAttackIdLocks()
    {
        if (_attackIdLocks.Count == 0)
            return;

        float now = Time.time;
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
