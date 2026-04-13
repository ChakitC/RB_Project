using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class SkillHitboxSequenceRuntime : MonoBehaviour
{
    sealed class StepRuntimeState
    {
        public string EventKey;
        public PrefabHitboxSkillPayloadDef.HitboxStep Definition;
        public readonly List<SkillHitboxGroup> Groups = new List<SkillHitboxGroup>();
        public readonly HashSet<int> HitTargetIds = new HashSet<int>();
        public bool IsActive;
    }

    [Header("Authoring")]
    [SerializeField] private SkillHitboxGroup[] groups = Array.Empty<SkillHitboxGroup>();

    readonly Dictionary<string, StepRuntimeState> _stepsByEventKey =
        new Dictionary<string, StepRuntimeState>(StringComparer.OrdinalIgnoreCase);

    readonly Dictionary<SkillHitboxGroup, int> _groupActivationCounts =
        new Dictionary<SkillHitboxGroup, int>();

    readonly List<StepRuntimeState> _activeSteps = new List<StepRuntimeState>();
    readonly HashSet<int> _skillHitTargetIds = new HashSet<int>();
    readonly HashSet<int> _ownedColliderIds = new HashSet<int>();
    readonly HashSet<int> _sweepColliderIds = new HashSet<int>();
    readonly Collider[] _overlapBuffer = new Collider[64];

    Rigidbody _rigidbody;
    SkillCastContext _context;
    PrefabHitboxSkillPayloadDef _payload;
    CharacterAnimBrain _animBrain;
    CombatEventBus _combatEventBus;
    StatusEffectController _statusEffectController;
    Transform _anchor;
    Transform _casterRoot;
    GameObject _sourceObject;
    Quaternion _localRotationOffset;
    string _sourceId;
    string _attackId;
    ulong _chainId;
    int _requestId;
    float _expireAt;
    bool _initialized;
    bool _isShuttingDown;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.isKinematic = true;
        _rigidbody.useGravity = false;

        CacheGroups();
        DeactivateAllGroupsImmediate();
    }

    void OnDisable()
    {
        Unsubscribe();
        DeactivateAllGroupsImmediate();
    }

    void OnDestroy()
    {
        Unsubscribe();
        DeactivateAllGroupsImmediate();
    }

    void Update()
    {
        if (!_initialized)
            return;

        if (_payload != null && _payload.FollowAnchor)
            UpdatePoseFromAnchor();

        if (Time.time >= _expireAt)
        {
            ShutdownAndDestroy();
            return;
        }

        if (_animBrain == null || _requestId <= 0)
        {
            ShutdownAndDestroy();
            return;
        }

        if (!_animBrain.TryGetActiveSkillNormalizedTime(_requestId, out _))
        {
            ShutdownAndDestroy();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        ProcessContact(other);
    }

    void OnTriggerStay(Collider other)
    {
        ProcessContact(other);
    }

    public void Initialize(SkillCastContext context, PrefabHitboxSkillPayloadDef payload)
    {
        _context = context;
        _payload = payload;
        _animBrain = context != null ? context.AnimBrain : null;
        _requestId = context != null ? context.RequestId : 0;
        _expireAt = Time.time + (payload != null ? payload.MaxSequenceLifetime : 1f);

        CacheGroups();
        BuildStepLookup();
        ResolveContextState();

        if (_stepsByEventKey.Count == 0)
        {
            Debug.LogWarning("[SkillHitboxSequenceRuntime] No valid hitbox steps were configured for this payload.", this);
            ShutdownAndDestroy();
            return;
        }

        UpdatePoseFromAnchor(forceResolve: true);
        Subscribe();
        _initialized = true;
    }

    void Subscribe()
    {
        if (_animBrain == null)
            return;

        _animBrain.SkillTimelineEventRaised += OnSkillTimelineEventRaised;
        _animBrain.SkillCastInterrupted += OnSkillCastInterrupted;
    }

    void Unsubscribe()
    {
        if (_animBrain == null)
            return;

        _animBrain.SkillTimelineEventRaised -= OnSkillTimelineEventRaised;
        _animBrain.SkillCastInterrupted -= OnSkillCastInterrupted;
    }

    void ResolveContextState()
    {
        _sourceObject = _context != null ? _context.CasterObject : null;
        _casterRoot = _context != null ? _context.CasterRoot : null;
        _anchor = _payload != null ? _payload.ResolveAnchor(_context) : null;
        _localRotationOffset = Quaternion.Euler(_payload != null ? _payload.LocalEulerOffset : Vector3.zero);

        if (_sourceObject != null)
        {
            _combatEventBus = _sourceObject.GetComponent<CombatEventBus>();
            _statusEffectController = _sourceObject.GetComponent<StatusEffectController>();
        }
        else
        {
            _combatEventBus = null;
            _statusEffectController = null;
        }

        _sourceId = _context != null && _context.SkillDef != null
            ? $"skill:{_context.SkillDef.name}"
            : "skill:prefab_hitbox";
        _attackId = _combatEventBus != null ? _combatEventBus.CreateAttackId($"{_sourceId}:hitbox") : null;
        _chainId = CombatEventBus.NextChainId();
    }

    void BuildStepLookup()
    {
        _stepsByEventKey.Clear();
        _groupActivationCounts.Clear();
        _activeSteps.Clear();
        _skillHitTargetIds.Clear();
        _ownedColliderIds.Clear();

        Dictionary<string, SkillHitboxGroup> groupLookup =
            new Dictionary<string, SkillHitboxGroup>(StringComparer.OrdinalIgnoreCase);

        if (groups != null)
        {
            for (int i = 0; i < groups.Length; i++)
            {
                SkillHitboxGroup group = groups[i];
                if (group == null)
                    continue;

                group.Initialize();
                group.CollectOwnedColliderIds(_ownedColliderIds);

                if (_groupActivationCounts.ContainsKey(group))
                    continue;

                _groupActivationCounts.Add(group, 0);

                string key = group.GroupKey;
                if (!groupLookup.ContainsKey(key))
                    groupLookup.Add(key, group);
            }
        }

        IReadOnlyList<PrefabHitboxSkillPayloadDef.HitboxStep> configuredSteps =
            _payload != null ? _payload.Steps : null;
        if (configuredSteps == null)
            return;

        for (int i = 0; i < configuredSteps.Count; i++)
        {
            PrefabHitboxSkillPayloadDef.HitboxStep step = configuredSteps[i];
            if (step == null)
                continue;

            string eventKey = step.EventKey;
            if (!PrefabHitboxSkillPayloadDef.IsValidEventKey(eventKey))
            {
                Debug.LogWarning($"[SkillHitboxSequenceRuntime] Ignoring invalid event key '{eventKey}'.", this);
                continue;
            }

            if (_stepsByEventKey.ContainsKey(eventKey))
            {
                Debug.LogWarning($"[SkillHitboxSequenceRuntime] Duplicate event key '{eventKey}' detected. Keeping the first step only.", this);
                continue;
            }

            StepRuntimeState state = new StepRuntimeState
            {
                EventKey = eventKey,
                Definition = step,
            };

            IReadOnlyList<string> groupKeys = step.GroupKeys;
            for (int groupIndex = 0; groupIndex < groupKeys.Count; groupIndex++)
            {
                string groupKey = groupKeys[groupIndex];
                if (string.IsNullOrWhiteSpace(groupKey))
                    continue;

                if (!groupLookup.TryGetValue(groupKey.Trim(), out SkillHitboxGroup resolvedGroup))
                {
                    Debug.LogWarning(
                        $"[SkillHitboxSequenceRuntime] Event '{eventKey}' references missing hitbox group '{groupKey}'.",
                        this);
                    continue;
                }

                if (!state.Groups.Contains(resolvedGroup))
                    state.Groups.Add(resolvedGroup);
            }

            if (state.Groups.Count == 0)
            {
                Debug.LogWarning(
                    $"[SkillHitboxSequenceRuntime] Event '{eventKey}' has no valid hitbox groups and will be ignored.",
                    this);
                continue;
            }

            _stepsByEventKey.Add(eventKey, state);
        }
    }

    void CacheGroups()
    {
        if (groups == null || groups.Length == 0)
            groups = GetComponentsInChildren<SkillHitboxGroup>(true);
    }

    void UpdatePoseFromAnchor(bool forceResolve = false)
    {
        if (_payload == null)
            return;

        if (forceResolve || _anchor == null)
            _anchor = _payload.ResolveAnchor(_context);

        Transform fallback = _context != null
            ? (_context.CastOrigin != null ? _context.CastOrigin : _context.CasterRoot)
            : null;

        Transform basis = _anchor != null ? _anchor : fallback;
        Vector3 basisPosition = basis != null ? basis.position : (_context != null ? _context.CastPosition : transform.position);
        Quaternion basisRotation = basis != null ? basis.rotation : Quaternion.identity;

        transform.SetPositionAndRotation(
            basisPosition + basisRotation * _payload.LocalPositionOffset,
            basisRotation * _localRotationOffset);
    }

    void OnSkillTimelineEventRaised(int requestId, string eventName)
    {
        if (!_initialized || requestId != _requestId || string.IsNullOrWhiteSpace(eventName))
            return;

        if (!PrefabHitboxSkillPayloadDef.TrySplitTimelineEventName(eventName, out string eventKey, out bool isOn))
            return;

        if (!_stepsByEventKey.TryGetValue(eventKey, out StepRuntimeState step))
            return;

        if (isOn)
            ActivateStep(step);
        else
            DeactivateStep(step);
    }

    void OnSkillCastInterrupted(int requestId)
    {
        if (requestId != _requestId)
            return;

        ShutdownAndDestroy();
    }

    void ActivateStep(StepRuntimeState step)
    {
        if (step == null)
            return;

        if (!step.IsActive)
        {
            step.IsActive = true;
            _activeSteps.Add(step);
        }

        if (step.Definition.ClearHitCacheOnEnter)
            step.HitTargetIds.Clear();

        for (int i = 0; i < step.Groups.Count; i++)
            SetGroupActive(step.Groups[i], true);

        _sweepColliderIds.Clear();
        for (int i = 0; i < step.Groups.Count; i++)
        {
            SkillHitboxGroup group = step.Groups[i];
            if (group == null)
                continue;

            group.SampleContacts(_overlapBuffer, _sweepColliderIds, _payload.TargetMask, _payload.QueryTriggers, ProcessContact);
        }
    }

    void DeactivateStep(StepRuntimeState step)
    {
        if (step == null || !step.IsActive)
            return;

        step.IsActive = false;
        _activeSteps.Remove(step);

        for (int i = 0; i < step.Groups.Count; i++)
            SetGroupActive(step.Groups[i], false);
    }

    void SetGroupActive(SkillHitboxGroup group, bool active)
    {
        if (group == null)
            return;

        _groupActivationCounts.TryGetValue(group, out int currentCount);

        if (active)
        {
            currentCount++;
            _groupActivationCounts[group] = currentCount;
            if (currentCount == 1)
                group.SetActive(true);
            return;
        }

        currentCount = Mathf.Max(0, currentCount - 1);
        _groupActivationCounts[group] = currentCount;
        if (currentCount == 0)
            group.SetActive(false);
    }

    void ProcessContact(Collider other)
    {
        if (!_initialized || _activeSteps.Count == 0 || other == null)
            return;

        if (_ownedColliderIds.Contains(other.GetInstanceID()))
            return;

        if (!IsTargetLayerAllowed(other))
            return;

        if (MeleeController.IsCombatOnlyHitbox(other))
            return;

        Transform otherRoot = other.transform.root;
        if (_casterRoot != null && otherRoot == _casterRoot)
            return;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null || !target.IsAlive)
            return;

        int targetKey = GetTargetKey(target);
        Vector3 hitPoint = ResolveHitPoint(other);

        for (int i = 0; i < _activeSteps.Count; i++)
        {
            StepRuntimeState step = _activeSteps[i];
            if (step == null || !step.IsActive)
                continue;

            if (!TryRegisterHit(step, targetKey))
                continue;

            float finalDamage = CalculateFinalDamage(step, target);
            if (finalDamage <= 0f)
            {
                UnregisterHit(step, targetKey);
                continue;
            }

            KnockbackData knockback = BuildKnockback(step, hitPoint);
            bool applied = ApplyResolvedDamage(target, finalDamage, hitPoint, knockback);
            if (!applied)
                UnregisterHit(step, targetKey);
        }
    }

    bool TryRegisterHit(StepRuntimeState step, int targetKey)
    {
        switch (step.Definition.HitPolicy)
        {
            case PrefabHitboxSkillPayloadDef.HitPolicy.OncePerSkill:
                return _skillHitTargetIds.Add(targetKey);

            case PrefabHitboxSkillPayloadDef.HitPolicy.OncePerStep:
            default:
                return step.HitTargetIds.Add(targetKey);
        }
    }

    void UnregisterHit(StepRuntimeState step, int targetKey)
    {
        switch (step.Definition.HitPolicy)
        {
            case PrefabHitboxSkillPayloadDef.HitPolicy.OncePerSkill:
                _skillHitTargetIds.Remove(targetKey);
                break;

            case PrefabHitboxSkillPayloadDef.HitPolicy.OncePerStep:
            default:
                step.HitTargetIds.Remove(targetKey);
                break;
        }
    }

    float CalculateFinalDamage(StepRuntimeState step, IDamageable target)
    {
        FinalSkillStats skillStats = _context != null ? _context.SkillStats : null;
        float baseDamage = skillStats != null ? skillStats.damage : 0f;
        float scaledDamage = Mathf.Max(0f, baseDamage * Mathf.Max(0f, step.Definition.DamageMultiplier));
        float critChance = skillStats != null ? skillStats.critChance : 0f;
        float critMultiplier = skillStats != null ? skillStats.critMultiplier : 1f;
        float armor = target is IHasArmor armorHolder ? armorHolder.Armor : 0f;

        return DamageCalculator.CalculateFinalDamage(
            WeaponType.Melee,
            0f,
            scaledDamage,
            critChance,
            critMultiplier,
            armor);
    }

    KnockbackData BuildKnockback(StepRuntimeState step, Vector3 hitPoint)
    {
        if (step == null || !step.Definition.OverrideKnockback)
            return default;

        if (step.Definition.KnockbackDistance <= 0f || step.Definition.KnockbackDuration <= 0f)
            return default;

        Vector3 origin = _context != null ? _context.CastPosition : transform.position;
        return KnockbackData.FromOrigin(
            origin,
            hitPoint,
            step.Definition.KnockbackDistance,
            step.Definition.KnockbackDuration,
            step.Definition.KnockbackReaction,
            step.Definition.KnockbackInterruptsActions,
            step.Definition.KnockbackProgressCurve);
    }

    bool ApplyResolvedDamage(IDamageable target, float finalDamage, Vector3 hitPoint, KnockbackData knockback)
    {
        if (target == null || finalDamage <= 0f || !target.IsAlive)
            return false;

        if (_payload != null && _payload.ShowDamageNumbers && VfxSpawner.Instance != null)
            VfxSpawner.Instance.SpawnDamageNumber(hitPoint, finalDamage);

        bool wasAliveBeforeDamage = target.IsAlive;
        GameObject attacker = _sourceObject != null ? _sourceObject : gameObject;
        var damageContext = new DamageContext(
            finalDamage,
            attacker,
            _sourceId,
            _attackId,
            _chainId == 0 ? CombatEventBus.NextChainId() : _chainId,
            1,
            PassiveEventOrigin.External,
            knockback: knockback);

        target.TakeDamage(in damageContext);
        NotifyOwnerCombatTriggers(target, finalDamage, wasAliveBeforeDamage);
        return true;
    }

    void NotifyOwnerCombatTriggers(IDamageable target, float finalDamage, bool wasAliveBeforeDamage)
    {
        if (target == null || !wasAliveBeforeDamage)
            return;

        Component targetComponent = target as Component;
        GameObject targetObject = targetComponent != null ? targetComponent.gameObject : null;

        _statusEffectController?.NotifyTrigger(EffectTriggerType.OnHit, targetObject);

        if (_combatEventBus != null)
        {
            PassiveEventContext hitContext = CreateOwnerEventContext(PassiveEventType.Hit, targetObject, finalDamage);
            _combatEventBus.Publish(hitContext);
        }

        if (!target.IsAlive)
        {
            _statusEffectController?.NotifyTrigger(EffectTriggerType.OnKill, targetObject);

            if (_combatEventBus != null)
            {
                PassiveEventContext killContext = CreateOwnerEventContext(PassiveEventType.Kill, targetObject, finalDamage);
                _combatEventBus.Publish(killContext);
            }
        }
    }

    PassiveEventContext CreateOwnerEventContext(PassiveEventType type, GameObject targetObject, float value)
    {
        GameObject source = _sourceObject != null ? _sourceObject : gameObject;

        if (_chainId != 0)
        {
            var parent = new PassiveEventContext(
                PassiveEventType.None,
                source,
                source,
                targetObject,
                _sourceId,
                _attackId,
                value,
                Time.timeAsDouble,
                _chainId,
                0,
                PassiveEventOrigin.External,
                null,
                null);

            return _combatEventBus.CreateChildContext(
                parent,
                type,
                source,
                targetObject,
                _sourceId,
                _attackId,
                value,
                PassiveEventOrigin.External);
        }

        return _combatEventBus.CreateExternalContext(
            type,
            source,
            targetObject,
            _sourceId,
            _attackId,
            value,
            PassiveEventOrigin.External);
    }

    bool IsTargetLayerAllowed(Collider other)
    {
        return other != null &&
               _payload != null &&
               ((1 << other.gameObject.layer) & _payload.TargetMask.value) != 0;
    }

    int GetTargetKey(IDamageable target)
    {
        if (target is Component component && component.transform != null)
            return component.transform.root.GetInstanceID();

        return target.GetHashCode();
    }

    Vector3 ResolveHitPoint(Collider other)
    {
        Vector3 point = other.ClosestPoint(transform.position);
        if (point.sqrMagnitude > 0.0001f)
            return point;

        return other.bounds.center;
    }

    void ShutdownAndDestroy()
    {
        if (_isShuttingDown)
            return;

        _isShuttingDown = true;
        Unsubscribe();
        DeactivateAllGroupsImmediate();
        Destroy(gameObject);
    }

    void DeactivateAllGroupsImmediate()
    {
        _activeSteps.Clear();
        _skillHitTargetIds.Clear();

        if (groups == null)
            return;

        for (int i = 0; i < groups.Length; i++)
        {
            SkillHitboxGroup group = groups[i];
            if (group == null)
                continue;

            group.SetActive(false);
            _groupActivationCounts[group] = 0;
        }

        foreach (StepRuntimeState step in _stepsByEventKey.Values)
        {
            step.IsActive = false;
            step.HitTargetIds.Clear();
        }
    }
}
