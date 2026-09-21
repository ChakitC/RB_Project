using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class SkillHitboxSequenceRuntime : MonoBehaviour
{
    sealed class StepRuntimeState
    {
        public int StepIndex;
        public string StepLabel;
        public PrefabHitboxSkillPayloadDef.HitboxStep Definition;
        public readonly List<SkillHitboxGroup> Groups = new List<SkillHitboxGroup>();
        public readonly HashSet<int> HitTargetIds = new HashSet<int>();
        public bool IsActive;
        public bool HasSpawnedImpactThisActivation;
    }

    [Header("Authoring")]
    [SerializeField] private SkillHitboxGroup[] groups = Array.Empty<SkillHitboxGroup>();

    readonly Dictionary<SkillHitboxGroup, int> _groupActivationCounts =
        new Dictionary<SkillHitboxGroup, int>();

    readonly List<StepRuntimeState> _steps = new List<StepRuntimeState>();
    readonly List<StepRuntimeState> _activeSteps = new List<StepRuntimeState>();
    readonly HashSet<int> _skillHitTargetIds = new HashSet<int>();
    readonly HashSet<int> _ownedColliderIds = new HashSet<int>();
    readonly HashSet<int> _sweepColliderIds = new HashSet<int>();
    readonly Collider[] _overlapBuffer = new Collider[64];

    bool _keepForReuse;
    LayerMask TargetMask => IsBasicMelee && _context.CasterContext?.MeleeController != null
        ? _context.CasterContext.MeleeController.TargetMask : _payload.TargetMask;
    PrefabHitboxSkillPayloadDef _builtPayload;

    public bool TryPrepare(SkillCastContext context, PrefabHitboxSkillPayloadDef payload, out string error)
    {
        error = null;
        // Resolve every bone before mutating an existing cached layout.
        var anchors = new Transform[payload.HitboxLayout.Groups.Count];
        for (int i = 0; i < anchors.Length; i++)
        {
            var data = payload.HitboxLayout.Groups[i];
            if (!SkillHitboxGroup.TryResolveAnchor(data.Anchor, data.AnchorPath, transform,
                    context.CasterContext, out anchors[i]))
            {
                error = $"Hitbox group '{data.GroupKey}' cannot resolve {data.Anchor} path '{data.AnchorPath}'.";
                return false;
            }
        }
        bool rebuild = _builtPayload != payload || groups.Length != anchors.Length;
        for (int i = 0; i < groups.Length; i++) if (groups[i] == null) rebuild = true;
        if (rebuild)
        {
            DestroyOwnedGroups();
            if (!SkillHitboxRuntimeBuilder.TryBuild(transform, payload.HitboxLayout,
                    gameObject.layer, out groups, out error)) return false;
            _builtPayload = payload;
        }
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i].SetActive(false);
            groups[i].transform.SetParent(anchors[i], false);
            groups[i].transform.localPosition = Vector3.zero;
            groups[i].transform.localRotation = Quaternion.identity;
            groups[i].transform.localScale = Vector3.one;
        }
        return true;
    }

    void DestroyOwnedGroups()
    {
        foreach (var group in groups)
        {
            if (group == null) continue;
            group.SetActive(false);
            if (Application.isPlaying) Destroy(group.gameObject);
            else DestroyImmediate(group.gameObject);
        }
        groups = Array.Empty<SkillHitboxGroup>();
    }

    bool IsBasicMelee => _context != null && _context.IsBasicMelee;
    public void KeepForReuse() => _keepForReuse = true;

    Rigidbody _rigidbody;
    SkillCastContext _context;
    PrefabHitboxSkillPayloadDef _payload;
    CharacterAnimBrain _animBrain;
    CombatEventBus _combatEventBus;
    StatusEffectController _statusEffectController;
    Transform _anchor;
    Transform _casterRoot;
    GameObject _sourceObject;
    CombatAttributionSnapshot _attribution;
    Quaternion _localRotationOffset;
    string _damageSourceId;
    string _attackId;
    ulong _chainId;
    int _depth;
    ComboExecutionProvenance _comboProvenance;
    int _requestId;
    int _casterLife;
    int _nextSequentialStepIndex;
    float _expireAt;
    bool _initialized;
    bool _isShuttingDown;
    DefensiveBlockAttack _defensiveBlock;
    readonly HashSet<int> _suppressedBlockSteps = new();
    public int ActiveStepIndex => _activeSequentialStep != null && _activeSequentialStep.IsActive ? _activeSequentialStep.StepIndex : -1;

    public bool TryGetActiveBounds(out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        if (_activeSequentialStep == null || !_activeSequentialStep.IsActive) return false;
        if (_payload.FollowAnchor) UpdatePoseFromAnchor();
        foreach (var group in _activeSequentialStep.Groups)
            foreach (var collider in group.Colliders)
            {
                if (collider == null || !collider.enabled) continue;
                if (!found) { bounds = collider.bounds; found = true; }
                else bounds.Encapsulate(collider.bounds);
            }
        return found;
    }

    public bool StopExecution(int requestId)
    {
        if (!_initialized || _isShuttingDown || requestId != _requestId) return false;
        ShutdownAndDestroy();
        return true;
    }
    public bool SuppressSteps(int requestId, int casterLife, int[] steps)
    {
        if (!_initialized || _isShuttingDown || requestId != _requestId || casterLife != _casterLife || steps == null) return false;
        foreach (int index in steps)
        {
            if (index < 0) continue;
            _suppressedBlockSteps.Add(index);
            foreach (var step in _steps)
                if (step.StepIndex == index && step.IsActive) DeactivateStep(step);
        }
        return true;
    }
    StepRuntimeState _activeSequentialStep;

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
        _initialized = false;
        Unsubscribe();
        DeactivateAllGroupsImmediate();
    }

    void OnDestroy()
    {
        Unsubscribe();
        DeactivateAllGroupsImmediate();
        DestroyOwnedGroups();
    }

    void Update()
    {
        if (!_initialized)
            return;

        if (_payload != null && _payload.FollowAnchor)
            UpdatePoseFromAnchor();

        if ((_context?.CasterContext != null && _context.CasterContext.LifeGeneration != _casterLife) ||
            Time.time >= _expireAt)
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

    void LateUpdate()
    {
        if (!_initialized || _isShuttingDown) return;
        foreach (var group in groups)
            if (group == null) { ShutdownAndDestroy(); return; }
        if (_payload.FollowAnchor) UpdatePoseFromAnchor();
        _sweepColliderIds.Clear();
        for (int i = 0; i < groups.Length && _initialized; i++)
            groups[i].SampleContacts(_overlapBuffer, _sweepColliderIds, TargetMask, _payload.QueryTriggers, ProcessContact);
    }

    void OnTriggerEnter(Collider other)
    {
        ProcessContact(other);
    }

    void OnTriggerStay(Collider other)
    {
        ProcessContact(other);
    }

    public void AssignGroups(SkillHitboxGroup[] assignedGroups)
    {
        groups = assignedGroups ?? Array.Empty<SkillHitboxGroup>();
    }

    public void Initialize(SkillCastContext context, PrefabHitboxSkillPayloadDef payload)
    {
        _initialized = false;
        Unsubscribe();
        DeactivateAllGroupsImmediate();
        _isShuttingDown = false;
        _suppressedBlockSteps.Clear();
        _context = context;
        _payload = payload;
        _animBrain = context != null ? context.AnimBrain : null;
        _requestId = context != null ? context.RequestId : 0;
        _casterLife = context?.CasterContext != null ? context.CasterContext.LifeGeneration : 0;
        _expireAt = IsBasicMelee ? float.PositiveInfinity : Time.time + (payload != null ? payload.MaxSequenceLifetime : 1f);

        CacheGroups();
        BuildStepLookup();
        ResolveContextState();

        if (_steps.Count == 0)
        {
            Debug.LogWarning("[SkillHitboxSequenceRuntime] No valid hitbox steps were configured for this payload.", this);
            ShutdownAndDestroy();
            return;
        }

        UpdatePoseFromAnchor(forceResolve: true);
        Subscribe();
        _initialized = true;
        _defensiveBlock = context?.CasterContext != null ? context.CasterContext.DefensiveBlockAttack : null;
        _defensiveBlock?.Bind(this, context);
    }

    void Subscribe()
    {
        if (_animBrain == null)
            return;

        _animBrain.SkillTimelineEventRaised += OnSkillTimelineEventRaised;
        _animBrain.PlaybackEvent += OnPlaybackEvent;
        _animBrain.SkillCastInterrupted += OnSkillCastInterrupted;
    }

    void Unsubscribe()
    {
        if (_animBrain == null)
            return;

        _animBrain.SkillTimelineEventRaised -= OnSkillTimelineEventRaised;
        _animBrain.PlaybackEvent -= OnPlaybackEvent;
        _animBrain.SkillCastInterrupted -= OnSkillCastInterrupted;
    }

    void ResolveContextState()
    {
        _sourceObject = IsBasicMelee && _context.CasterContext != null
            ? _context.CasterContext.gameObject : (_context != null ? _context.CasterObject : null);
        _attribution = CombatAttributionSnapshot.FromPhysicalActor(_sourceObject);
        _casterRoot = _context != null ? _context.CasterRoot : null;
        _anchor = _payload != null ? _payload.ResolveAnchor(_context) : null;
        _localRotationOffset = Quaternion.Euler(_payload != null ? _payload.LocalEulerOffset : Vector3.zero);

        if (_sourceObject != null)
        {
            // Through the caster's context, not a one-direction GetComponent: on several prefabs
            // the bus lives on a child branch, and missing it silently drops every OnHit/OnKill
            // this skill would have raised.
            _combatEventBus = _context != null && _context.CasterEventBus != null
                ? _context.CasterEventBus
                : CharacterContextModuleLookup.ResolveCombatEventBus(
                    _sourceObject, _context != null ? _context.CasterContext : null);
            _statusEffectController = _context != null && _context.CasterStatusEffects != null
                ? _context.CasterStatusEffects
                : CharacterContextModuleLookup.ResolveStatusEffects(
                    _sourceObject, _context != null ? _context.CasterContext : null);
            if (_attribution.HasCredit)
            {
                _sourceObject = _attribution.PhysicalActor != null
                    ? _attribution.PhysicalActor
                    : _sourceObject;
                _combatEventBus = _attribution.CreditedEventBus;
                _statusEffectController = _attribution.CreditedStatusOwner;
            }
        }
        else
        {
            _combatEventBus = null;
            _statusEffectController = null;
        }

        _damageSourceId = _context != null && _context.SkillDef != null
            ? $"skill:{_context.SkillDef.name}"
            : "skill:prefab_hitbox";
        _attackId = _combatEventBus != null ? _combatEventBus.CreateAttackId($"{_damageSourceId}:hitbox") : null;
        _chainId = _context != null && _context.CombatChainId != 0
            ? _context.CombatChainId
            : CombatEventBus.NextChainId();
        _depth = _context != null ? _context.CombatDepth : 0;
        _comboProvenance = _context != null ? _context.ComboProvenance : default;
    }

    void BuildStepLookup()
    {
        _steps.Clear();
        _groupActivationCounts.Clear();
        _activeSteps.Clear();
        _skillHitTargetIds.Clear();
        _ownedColliderIds.Clear();
        _nextSequentialStepIndex = 0;
        _activeSequentialStep = null;

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

        if (_payload == null || !_payload.HasHitboxTimelineEvents)
        {
            Debug.LogWarning(
                "[SkillHitboxSequenceRuntime] Payload is missing valid hitbox start/end timeline events.",
                this);
            return;
        }

        for (int i = 0; i < configuredSteps.Count; i++)
        {
            PrefabHitboxSkillPayloadDef.HitboxStep step = configuredSteps[i];
            if (step == null)
                continue;

            StepRuntimeState state = new StepRuntimeState
            {
                StepIndex = i,
                StepLabel = $"Step {i + 1}",
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
                        $"[SkillHitboxSequenceRuntime] Step '{state.StepLabel}' references missing hitbox group '{groupKey}'.",
                        this);
                    continue;
                }

                if (!state.Groups.Contains(resolvedGroup))
                    state.Groups.Add(resolvedGroup);
            }

            if (state.Groups.Count == 0)
            {
                Debug.LogWarning(
                    $"[SkillHitboxSequenceRuntime] Step '{state.StepLabel}' has no valid hitbox groups and will be ignored.",
                    this);
                continue;
            }

            _steps.Add(state);
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

    void OnSkillTimelineEventRaised(int requestId, CombatTimelineEventName eventName)
    {
        if (!_initialized ||
            requestId != _requestId ||
            !PrefabHitboxSkillPayloadDef.IsValidTimelineEvent(eventName))
        {
            return;
        }

        if (_payload != null && eventName == _payload.HitboxStartEventName)
        {
            ActivateNextSequentialStep();
            return;
        }

        if (_payload != null && eventName == _payload.HitboxEndEventName)
        {
            DeactivateCurrentSequentialStep();
            return;
        }
    }

    void ActivateNextSequentialStep()
    {
        if (_activeSequentialStep != null && _activeSequentialStep.IsActive)
            return;

        if (_nextSequentialStepIndex < 0 || _nextSequentialStepIndex >= _steps.Count)
            return;

        StepRuntimeState step = _steps[_nextSequentialStepIndex];
        _nextSequentialStepIndex++;
        _activeSequentialStep = step;
        ActivateStep(step);
    }

    void DeactivateCurrentSequentialStep()
    {
        if (_activeSequentialStep == null)
            return;

        StepRuntimeState step = _activeSequentialStep;
        _activeSequentialStep = null;

        if (step.IsActive)
            DeactivateStep(step);
    }

    void OnPlaybackEvent(CharacterAnimBrain.PlaybackSignal signal)
    {
        if (signal.RequestId == _requestId &&
            (signal.Phase == CharacterAnimBrain.PlaybackPhase.Completed ||
             signal.Phase == CharacterAnimBrain.PlaybackPhase.Interrupted))
            ShutdownAndDestroy();
    }

    void OnSkillCastInterrupted(int requestId)
    {
        if (requestId != _requestId)
            return;

        ShutdownAndDestroy();
    }

    void ActivateStep(StepRuntimeState step)
    {
        if (step == null || _suppressedBlockSteps.Contains(step.StepIndex))
            return;

        if (IsBasicMelee)
        {
            var actor = _context.CasterContext;
            var weapons = actor != null ? actor.WeaponSystem : null;
            string instanceId = weapons?.CurrentWeaponInstance?.instanceId;
            var weapon = weapons != null ? weapons.CurrentWeapon : actor != null ? actor.currentWeapon : null;
            _damageSourceId = !string.IsNullOrWhiteSpace(instanceId)
                ? $"weapon:{instanceId}:melee" : $"melee:{(weapon != null ? weapon.name : "unarmed")}";
            _attackId = _combatEventBus != null ? _combatEventBus.CreateAttackId($"{_damageSourceId}:melee") : null;
            _chainId = _combatEventBus != null ? CombatEventBus.NextChainId() : 0;
        }
        bool wasInactive = !step.IsActive;
        if (wasInactive)
        {
            step.IsActive = true;
            step.HasSpawnedImpactThisActivation = false;
            _activeSteps.Add(step);
            TrySpawnStepStartVfx(step);
        }

        if (step.Definition.ClearHitCacheOnEnter)
            step.HitTargetIds.Clear();

        for (int i = 0; i < step.Groups.Count; i++)
            SetGroupActive(step.Groups[i], true);

        _sweepColliderIds.Clear();
        if (_defensiveBlock != null && _defensiveBlock.TryIntercept(this)) return;
        for (int i = 0; i < step.Groups.Count; i++)
        {
            SkillHitboxGroup group = step.Groups[i];
            if (group == null)
                continue;

            group.SampleContacts(_overlapBuffer, _sweepColliderIds, TargetMask, _payload.QueryTriggers, ProcessContact);
        }
    }

    void DeactivateStep(StepRuntimeState step)
    {
        if (step == null || !step.IsActive)
            return;

        step.IsActive = false;
        step.HasSpawnedImpactThisActivation = false;
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
        if (_isShuttingDown) return;
        if (_defensiveBlock != null && _defensiveBlock.TryIntercept(this)) return;
        if (!_initialized || _activeSteps.Count == 0 || !CanDamageCollider(other))
            return;

        IDamageable target = DamageableResolver.ResolveFrom(other);
        if (target == null || !target.IsAlive || (IsBasicMelee && ReferenceEquals(target, _context.CasterContext?.HealthSystem)))
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

            float finalDamage = CalculateFinalDamage(step, target, other, out bool wasCritical);
            if (finalDamage <= 0f)
            {
                if (!IsBasicMelee) UnregisterHit(step, targetKey);
                continue;
            }

            KnockbackData knockback = BuildKnockback(step, hitPoint);
            DamageResult result = ApplyResolvedDamage(step, target, finalDamage, hitPoint, knockback, wasCritical);
            if (!result.Applied)
            {
                if (!IsBasicMelee && !result.WasPrevented)
                    UnregisterHit(step, targetKey);

                continue;
            }

            TrySpawnImpactVfx(step, hitPoint);
        }
    }

    bool BelongsToCaster(Transform other)
    {
        if (_casterRoot == null || other == null)
            return false;

        if (other == _casterRoot || other.IsChildOf(_casterRoot))
            return true;

        CharacteContext otherContext = other.GetComponentInParent<CharacteContext>();
        if (otherContext != null && otherContext.transform == _casterRoot)
            return true;

        return other.root == _casterRoot;
    }

    // Shared by normal contact processing and the defensive contact-order probe.
    public bool CanDamageCollider(Collider other) => other != null && other.enabled &&
        other.gameObject.activeInHierarchy && !_ownedColliderIds.Contains(other.GetInstanceID()) &&
        IsTargetLayerAllowed(other) && !MeleeController.IsCombatOnlyHitbox(other) && !BelongsToCaster(other.transform);

    void TrySpawnStepStartVfx(StepRuntimeState step)
    {
        if (step == null || step.Definition == null || VfxSpawner.Instance == null)
            return;

        PrefabHitboxSkillPayloadDef.StepStartVfxSettings settings = step.Definition.StepStartVfx;
        if (settings == null || !settings.IsEnabled)
            return;

        VfxSpawner.Instance.SpawnVfx(
            settings.Prefab,
            transform.position,
            ResolveSequenceForward(),
            scale: settings.Scale);
    }

    void TrySpawnImpactVfx(StepRuntimeState step, Vector3 hitPoint)
    {
        if (step == null || step.Definition == null || VfxSpawner.Instance == null)
            return;

        PrefabHitboxSkillPayloadDef.ImpactVfxSettings settings = step.Definition.ImpactVfx;
        if (settings == null || !settings.IsEnabled || !CanSpawnImpactForStep(step, settings.SpawnPolicy))
            return;

        VfxSpawner.Instance.SpawnVfx(
            settings.Prefab,
            hitPoint,
            ResolveImpactNormal(hitPoint),
            1f,
            settings.Scale);

        if (settings.SpawnPolicy == PrefabHitboxSkillPayloadDef.ImpactSpawnPolicy.FirstHitPerStep)
            step.HasSpawnedImpactThisActivation = true;
    }

    bool CanSpawnImpactForStep(
        StepRuntimeState step,
        PrefabHitboxSkillPayloadDef.ImpactSpawnPolicy spawnPolicy)
    {
        if (step == null)
            return false;

        switch (spawnPolicy)
        {
            case PrefabHitboxSkillPayloadDef.ImpactSpawnPolicy.EveryHit:
                return true;

            case PrefabHitboxSkillPayloadDef.ImpactSpawnPolicy.FirstHitPerStep:
                return !step.HasSpawnedImpactThisActivation;

            case PrefabHitboxSkillPayloadDef.ImpactSpawnPolicy.Disabled:
            default:
                return false;
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

    float CalculateFinalDamage(StepRuntimeState step, IDamageable target, Collider other, out bool wasCritical)
    {
        FinalSkillStats skillStats = _context != null ? _context.SkillStats : null;
        float baseDamage = skillStats != null ? skillStats.damage : 0f;
        float scaledDamage = Mathf.Max(0f, baseDamage * Mathf.Max(0f, step.Definition.DamageMultiplier));
        float critChance = skillStats != null ? skillStats.critChance : 0f;
        float critMultiplier = skillStats != null ? skillStats.critMultiplier : 1f;
        float armor = target is IHasArmor armorHolder ? armorHolder.Armor : 0f;

        float distance = 0f;
        if (IsBasicMelee)
        {
            var actor = _context.CasterContext;
            var weapons = actor != null ? actor.WeaponSystem : null;
            var weapon = weapons != null ? weapons.CurrentWeapon : actor != null ? actor.currentWeapon : null;
            var stats = actor != null ? actor.StatsHub : null;
            scaledDamage = (stats != null ? stats.GetSkillBaseDamage() : 0f) * step.Definition.DamageMultiplier;
            critChance = stats != null ? stats.GetCritRatePercent(weapon) : 0f;
            critMultiplier = stats != null ? stats.GetCritMultiplier(weapon) : 1f;
            distance = other != null ? Vector3.Distance(transform.position, other.ClosestPoint(transform.position)) : 0f;
        }
        DamageCalculationResult calculation = DamageCalculator.CalculateDamage(
            WeaponType.Melee,
            distance,
            scaledDamage,
            critChance,
            critMultiplier,
            armor);
        wasCritical = calculation.WasCritical;
        return calculation.Damage;
    }

    KnockbackData BuildKnockback(StepRuntimeState step, Vector3 hitPoint)
    {
        if (step == null || step.Definition == null || !step.Definition.OverrideKnockback)
            return default;

        Vector3 origin = ResolveCurrentImpactOrigin();
        KnockbackSettings settings = step.Definition.ToKnockbackSettings();
        KnockbackBuildContext context = new KnockbackBuildContext(
            origin,
            hitPoint,
            ResolveSequenceForward());

        return KnockbackFactory.TryBuild(in settings, in context, out KnockbackData knockback)
            ? knockback
            : default;
    }

    DamageResult ApplyResolvedDamage(StepRuntimeState step, IDamageable target, float finalDamage, Vector3 hitPoint, KnockbackData knockback, bool wasCritical)
    {
        if (target == null || finalDamage <= 0f || !target.IsAlive)
            return default;

        bool wasAliveBeforeDamage = target.IsAlive;
        GameObject attacker = _sourceObject != null ? _sourceObject : gameObject;
        var damageContext = new DamageContext(
            finalDamage,
            attacker,
            _damageSourceId,
            _attackId,
            _chainId == 0 ? CombatEventBus.NextChainId() : _chainId,
            IsBasicMelee ? 0 : _depth + 1,
            PassiveEventOrigin.External,
            knockback: knockback,
            stagger: BuildStaggerPayload(step),
            attribution: _attribution);

        DamageResult result = target.TakeDamage(in damageContext);
        if (!result.Applied)
            return result;

        var victim = target is HealthSystem health ? health.CTX :
            (target as Component)?.GetComponentInParent<CharacteContext>();
        _defensiveBlock?.NotifyDamageApplied(this, _requestId, _casterLife, victim, step.StepIndex);

        if (_payload != null && _payload.ShowDamageNumbers && VfxSpawner.Instance != null)
            VfxSpawner.Instance.SpawnDamageNumber(hitPoint, result.AppliedDamage, target);

        NotifyOwnerCombatTriggers(target, result, wasAliveBeforeDamage, wasCritical);
        return result;
    }

    StaggerPayload BuildStaggerPayload(StepRuntimeState step)
    {
        float staggerPower = 0f;
        if (step != null && step.Definition != null && _context != null && _context.SkillStats != null)
        {
            staggerPower = _context.SkillStats.staggerPower * Mathf.Max(0f, step.Definition.DamageMultiplier);
        }

        if (IsBasicMelee)
        {
            staggerPower = _context.SkillDef != null ? _context.SkillDef.baseStaggerPower : 0f;
            if (staggerPower <= 0f && _context.CasterContext?.StatsHub != null)
                staggerPower = _context.CasterContext.StatsHub.GetSkillBaseDamage() * 0.5f;
        }
        return new StaggerPayload(staggerPower, 1f, _damageSourceId);
    }

    void NotifyOwnerCombatTriggers(IDamageable target, in DamageResult result, bool wasAliveBeforeDamage, bool wasCritical)
    {
        if (target == null || !wasAliveBeforeDamage)
            return;

        Component targetComponent = target as Component;
        GameObject targetObject = targetComponent != null ? targetComponent.gameObject : null;

        _statusEffectController?.NotifyTrigger(EffectTriggerType.OnHit, targetObject);

        if (_combatEventBus != null)
        {
            PassiveEventContext hitContext = CreateOwnerEventContext(PassiveEventType.Hit, targetObject, result.AppliedDamage, result, wasCritical);
            _combatEventBus.Publish(hitContext);
        }

        if (result.Killed)
        {
            _statusEffectController?.NotifyTrigger(EffectTriggerType.OnKill, targetObject);

            if (_combatEventBus != null)
            {
                PassiveEventContext killContext = CreateOwnerEventContext(PassiveEventType.Kill, targetObject, result.AppliedDamage, result, wasCritical);
                _combatEventBus.Publish(killContext);
            }
        }
    }

    PassiveEventContext CreateOwnerEventContext(PassiveEventType type, GameObject targetObject, float value, in DamageResult result, bool wasCritical)
    {
        GameObject source = _sourceObject != null ? _sourceObject : gameObject;
        var metadata = new CombatEventMetadata(
            result.RequestedDamage, result.ResolvedDamage, result.AppliedDamage,
            result.HealthBeforeHit, result.MaxHealth, wasCritical,
            staggerApplied: result.StaggerApplied, enteredChainReady: result.EnteredChainReady,
            sourceKind: IsBasicMelee ? CombatSourceKind.Melee : CombatSourceKind.Skill);

        if (_chainId != 0)
        {
            var parent = new PassiveEventContext(
                PassiveEventType.None,
                source,
                source,
                targetObject,
                _damageSourceId,
                _attackId,
                value,
                Time.timeAsDouble,
                _chainId,
                _depth,
                PassiveEventOrigin.External,
                null,
                null,
                metadata,
                CombatEventBus.NextFactId(),
                _comboProvenance);

            return _combatEventBus.CreateChildContext(
                parent,
                type,
                source,
                targetObject,
                _damageSourceId,
                _attackId,
                value,
                PassiveEventOrigin.External,
                metadata: metadata,
                actor: _attribution.CreditedActor);
        }

        return _combatEventBus.CreateExternalContext(
            type,
            source,
            targetObject,
            _damageSourceId,
            _attackId,
            value,
            PassiveEventOrigin.External,
            metadata: metadata,
            actor: _attribution.CreditedActor);
    }

    bool IsTargetLayerAllowed(Collider other)
    {
        return other != null &&
               _payload != null &&
               ((1 << other.gameObject.layer) & TargetMask.value) != 0;
    }

    int GetTargetKey(IDamageable target)
    {
        if (target is Component component && component.transform != null)
            return component.GetInstanceID();

        return target.GetHashCode();
    }

    Vector3 ResolveHitPoint(Collider other)
    {
        Vector3 point = other.ClosestPoint(transform.position);
        if (point.sqrMagnitude > 0.0001f)
            return point;

        return other.bounds.center;
    }

    Vector3 ResolveSequenceForward()
    {
        if (IsBasicMelee) return transform.forward;
        if (_context != null && _context.AimDirection.sqrMagnitude > 0.0001f)
        {
            Vector3 planarAim = Vector3.ProjectOnPlane(_context.AimDirection, Vector3.up);
            if (planarAim.sqrMagnitude > 0.0001f)
                return planarAim.normalized;
        }

        Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (planarForward.sqrMagnitude > 0.0001f)
            return planarForward.normalized;

        return Vector3.forward;
    }

    Vector3 ResolveImpactNormal(Vector3 hitPoint)
    {
        Vector3 origin = ResolveCurrentImpactOrigin();
        Vector3 normal = hitPoint - origin;
        if (normal.sqrMagnitude > 0.0001f)
            return normal.normalized;

        return ResolveSequenceForward();
    }

    Vector3 ResolveCurrentImpactOrigin()
    {
        Vector3 origin = transform.position;
        if (float.IsNaN(origin.x) || float.IsNaN(origin.y) || float.IsNaN(origin.z) ||
            float.IsInfinity(origin.x) || float.IsInfinity(origin.y) || float.IsInfinity(origin.z))
        {
            return _context != null ? _context.CastPosition : Vector3.zero;
        }

        return origin;
    }

    void ShutdownAndDestroy()
    {
        if (_isShuttingDown)
            return;

        _isShuttingDown = true;
        _initialized = false;
        Unsubscribe();
        DeactivateAllGroupsImmediate();
        _defensiveBlock = null;
        if (!_keepForReuse)
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }

    void DeactivateAllGroupsImmediate()
    {
        _activeSteps.Clear();
        _skillHitTargetIds.Clear();
        _nextSequentialStepIndex = 0;
        _activeSequentialStep = null;

        if (groups != null)
        {
            for (int i = 0; i < groups.Length; i++)
            {
                SkillHitboxGroup group = groups[i];
                if (group == null)
                    continue;

                group.SetActive(false);
                _groupActivationCounts[group] = 0;
            }
        }

        for (int i = 0; i < _steps.Count; i++)
        {
            StepRuntimeState step = _steps[i];
            step.IsActive = false;
            step.HasSpawnedImpactThisActivation = false;
            step.HitTargetIds.Clear();
        }
    }
}

static class SkillHitboxRuntimeBuilder
{
    public static bool TryBuild(
        Transform root,
        SkillHitboxLayoutData hitboxLayout,
        int layer,
        out SkillHitboxGroup[] builtGroups,
        out string errorMessage)
    {
        builtGroups = Array.Empty<SkillHitboxGroup>();
        errorMessage = null;

        if (root == null)
        {
            errorMessage = "Runtime root is missing.";
            return false;
        }

        if (hitboxLayout == null)
        {
            errorMessage = "Inline hitbox layout is missing.";
            return false;
        }

        IReadOnlyList<SkillHitboxLayoutData.HitBoxGroupData> sourceGroups = hitboxLayout.Groups;
        if (sourceGroups == null || sourceGroups.Count == 0)
        {
            errorMessage = "Inline hitbox layout has no groups.";
            return false;
        }

        List<SkillHitboxGroup> createdGroups = new List<SkillHitboxGroup>(sourceGroups.Count);
        List<GameObject> createdObjects = new List<GameObject>();

        try
        {
            for (int i = 0; i < sourceGroups.Count; i++)
            {
                SkillHitboxLayoutData.HitBoxGroupData sourceGroup = sourceGroups[i];
                if (sourceGroup == null)
                {
                    errorMessage = $"Inline hitbox layout has a null group at index {i}.";
                    Cleanup(createdObjects);
                    return false;
                }

                string groupKey = sourceGroup.GroupKey;
                if (string.IsNullOrWhiteSpace(groupKey))
                {
                    errorMessage = "Inline hitbox layout has a group with an empty key.";
                    Cleanup(createdObjects);
                    return false;
                }

                List<SkillHitboxLayoutData.HitBoxShapeData> sourceShapes = sourceGroup.Shapes;
                if (sourceShapes == null || sourceShapes.Count == 0)
                {
                    errorMessage = $"Inline hitbox group '{groupKey}' has no shapes.";
                    Cleanup(createdObjects);
                    return false;
                }

                GameObject groupObject = new GameObject(groupKey);
                createdObjects.Add(groupObject);
                groupObject.layer = layer;
                groupObject.transform.SetParent(root, false);

                SkillHitboxGroup runtimeGroup = groupObject.AddComponent<SkillHitboxGroup>();
                List<Collider> groupColliders = new List<Collider>(sourceShapes.Count);

                for (int shapeIndex = 0; shapeIndex < sourceShapes.Count; shapeIndex++)
                {
                    SkillHitboxLayoutData.HitBoxShapeData sourceShape = sourceShapes[shapeIndex];
                    if (sourceShape == null)
                    {
                        errorMessage = $"Inline hitbox group '{groupKey}' has a null shape.";
                        Cleanup(createdObjects);
                        return false;
                    }

                    GameObject shapeObject = new GameObject(sourceShape.ShapeName);
                    createdObjects.Add(shapeObject);
                    shapeObject.layer = layer;
                    shapeObject.transform.SetParent(groupObject.transform, false);
                    shapeObject.transform.localPosition = sourceShape.LocalPosition;
                    shapeObject.transform.localRotation = Quaternion.Euler(sourceShape.LocalEulerAngles);
                    shapeObject.transform.localScale = sourceShape.LocalScale;

                    if (!TryAddCollider(shapeObject, sourceShape, out Collider runtimeCollider, out errorMessage))
                    {
                        Cleanup(createdObjects);
                        return false;
                    }

                    runtimeCollider.isTrigger = true;
                    runtimeCollider.enabled = false;
                    groupColliders.Add(runtimeCollider);
                }

                runtimeGroup.Configure(groupKey, groupColliders);
                runtimeGroup.ConfigureAnchor(sourceGroup.Anchor, sourceGroup.AnchorPath);
                createdGroups.Add(runtimeGroup);
            }

            builtGroups = createdGroups.ToArray();
            return true;
        }
        catch (Exception ex)
        {
            Cleanup(createdObjects);
            errorMessage = $"Failed to build inline hitbox runtime: {ex.Message}";
            return false;
        }
    }

    static bool TryAddCollider(
        GameObject shapeObject,
        SkillHitboxLayoutData.HitBoxShapeData sourceShape,
        out Collider runtimeCollider,
        out string errorMessage)
    {
        runtimeCollider = null;
        errorMessage = null;

        if (shapeObject == null || sourceShape == null)
        {
            errorMessage = "Cannot build a hitbox shape from null input.";
            return false;
        }

        switch (sourceShape.Type)
        {
            case SkillHitboxLayoutData.HitBoxType.Box:
                BoxCollider box = shapeObject.AddComponent<BoxCollider>();
                box.center = sourceShape.Center;
                box.size = sourceShape.Size;
                runtimeCollider = box;
                return true;

            case SkillHitboxLayoutData.HitBoxType.Capsule:
                CapsuleCollider capsule = shapeObject.AddComponent<CapsuleCollider>();
                capsule.center = sourceShape.Center;
                capsule.radius = sourceShape.Radius;
                capsule.height = sourceShape.Height;
                capsule.direction = sourceShape.Direction;
                runtimeCollider = capsule;
                return true;

            case SkillHitboxLayoutData.HitBoxType.Sphere:
                SphereCollider sphere = shapeObject.AddComponent<SphereCollider>();
                sphere.center = sourceShape.Center;
                sphere.radius = sourceShape.Radius;
                runtimeCollider = sphere;
                return true;

            default:
                errorMessage = $"Unsupported hitbox shape type '{sourceShape.Type}'.";
                return false;
        }
    }

    static void Cleanup(List<GameObject> createdObjects)
    {
        if (createdObjects == null)
            return;

        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            GameObject createdObject = createdObjects[i];
            if (createdObject == null)
                continue;

            if (Application.isPlaying) UnityEngine.Object.Destroy(createdObject);
            else UnityEngine.Object.DestroyImmediate(createdObject);
        }
    }
}
