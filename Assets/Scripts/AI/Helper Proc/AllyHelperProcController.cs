using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-109)]
public sealed class AllyHelperProcController : MonoBehaviour
{
    [SerializeField] private PlayerContext playerContext;
    [SerializeField] private CombatEventBus combatEventBus;
    [SerializeField] private AllyHelperManager allyHelperManager;
    [SerializeField] private SkillHelperDef[] helperDefinitions;
    [SerializeField, Min(0.1f)] private float attackIdLockTtlSeconds = 10f;
    [SerializeField] private bool logProcController;

    readonly Dictionary<SkillHelperDef, float> _nextReadyTimeByDef = new();
    readonly Dictionary<string, float> _attackIdLocks = new();
    readonly List<SkillHelperDef> _resolvedHelperDefinitions = new();
    readonly HashSet<SkillHelperDef> _resolvedHelperDefinitionSet = new();

    bool _subscribed;
    float WorldNow => TimeSlowManager.Instance.WorldTime;

    void Awake()
    {
        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();

        if (combatEventBus == null)
            combatEventBus = GetComponent<CombatEventBus>();

        if (allyHelperManager == null && playerContext != null)
            allyHelperManager = playerContext.allyHelper;
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

    void BuildRuntimeHelperDefinitions()
    {
        _resolvedHelperDefinitions.Clear();
        _resolvedHelperDefinitionSet.Clear();

        playerContext?.ResolveReferences();
        if (allyHelperManager == null && playerContext != null)
            allyHelperManager = playerContext.allyHelper;

        AddUniqueDefinitions(helperDefinitions);
        AddUniqueDefinitions(allyHelperManager != null ? allyHelperManager.HelperSkillManager : null);
    }

    void AddUniqueDefinitions(SkillHelperDef[] definitions)
    {
        if (definitions == null)
            return;

        for (int i = 0; i < definitions.Length; i++)
        {
            SkillHelperDef definition = definitions[i];
            if (definition == null || !_resolvedHelperDefinitionSet.Add(definition))
                continue;

            _resolvedHelperDefinitions.Add(definition);
        }
    }

    void AddUniqueDefinitions(CharacterSkillManager skillManager)
    {
        skillManager?.AppendConfiguredHelperChainDefinitions(_resolvedHelperDefinitions, _resolvedHelperDefinitionSet);
    }

    bool CanProc(SkillHelperDef helperDef, PassiveEventContext context)
    {
        if (helperDef == null || allyHelperManager == null)
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
                helperDef.ClampedSkillLevel,
                helperDef.hideHelperOnSkillComplete);
        }

        return allyHelperManager.TrySummonAllyHelper(
            helperDef.executionSkill,
            helperDef.ClampedSkillLevel,
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
