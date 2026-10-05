using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AITargetSensor : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform sensorOrigin;
    [SerializeField] private Transform forwardReference;

    [Header("Behavior Profile")]
    [Tooltip("Optional targeting policy asset. When assigned, the local policy fields below are used only as fallback.")]
    [SerializeField] private AITargetingProfileDef targetingProfile;

    [Header("Scan")]
    [SerializeField] private float radius = 15f;
    [SerializeField] private float scanInterval = 0.1f;
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField] private LayerMask obstacleLayers = 0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private int maxHits = 64;

    [Header("Filter")]
    [SerializeField] private bool useFieldOfView = false;
    [SerializeField, Range(0f, 360f)] private float fieldOfView = 180f;
    [SerializeField] private bool requireLineOfSight = true;

    [Header("Team / Alive")]
    [SerializeField] private bool useTeamFilter = false;
    [SerializeField] private int ownerTeamId = 0;
    [SerializeField] private bool requireAliveIfAvailable = true;

    [Header("Memory")]
    [SerializeField] private float gracePeriod = 1.25f;
    [SerializeField] private bool keepLastSeenTarget = true;

    [Header("Scoring")]
    [SerializeField, Min(0f)] private float distanceScoreWeight = 12f;
    [SerializeField] private float playerPriorityBonus = 16f;
    [SerializeField] private float companionPriorityBonus = 0f;
    [SerializeField] private float tankPriorityBonus = 6f;
    [SerializeField] private float healerPriorityBonus = 9f;
    [SerializeField] private float supportPriorityBonus = 6f;
    [SerializeField] private float sniperPriorityBonus = 7f;
    [SerializeField] private float assaultPriorityBonus = 0f;
    [SerializeField, Min(0f)] private float currentTargetStickiness = 5f;
    [SerializeField, Min(0f)] private float lineOfSightPenalty = 6f;

    [Header("Retarget")]
    [SerializeField] private Vector2 retargetIntervalRange = new Vector2(0.75f, 1.1f);
    [SerializeField, Min(0f)] private float minTargetLockDuration = 0.9f;
    [SerializeField, Min(0f)] private float switchScoreThreshold = 2.5f;
    [SerializeField, Range(0f, 1f)] private float switchScoreThresholdRatio = 0.2f;
    [SerializeField, Min(0f)] private float lostTargetHoldDuration = 0.35f;

    [Header("Threat")]
    [SerializeField] private bool useThreatMemory = true;
    [SerializeField, Min(0f)] private float damageThreatMultiplier = 1f;
    [SerializeField, Min(0f)] private float threatScoreWeight = 1f;
    [SerializeField, Min(0f)] private float threatDecayPerSecond = 8f;
    [SerializeField, Min(0f)] private float maxThreatPerTarget = 100f;

    // Taunt ownership อยู่ที่ TauntSkillPayloadDef.tauntStatus — sensor ไม่ถือ Taunt Def เอง และ derive
    // taunt state จาก StatusEffectInstance ที่ติด tag StatusEffectTags.Taunt (ดู UpdateTauntState)
    [Header("Taunt")]
    [SerializeField, Min(0f)] private float tauntScoreBonus = 1000f;

    [Header("Offsets")]
    [SerializeField] private float originHeightOffset = 0.5f;
    [SerializeField] private float fallbackTargetHeightOffset = 0.6f;

    [Header("Debug Runtime")]
    [SerializeField] private Transform currentTarget;
    [SerializeField] private Transform visibleTarget;
    [SerializeField] private Transform lastSeenTarget;
    [SerializeField] private Vector3 lastSeenPosition;
    [SerializeField] private bool hasLineOfSight;
    [SerializeField] private float targetDistance;
    [SerializeField] private float visibleTargetDistance;
    [SerializeField] private float lastSeenTime = float.NegativeInfinity;
    [SerializeField] private float currentTargetScore = float.NegativeInfinity;
    [SerializeField] private float visibleBestScore = float.NegativeInfinity;
    [SerializeField] private float currentTargetAcquiredTime = float.NegativeInfinity;
    [SerializeField] private float nextRetargetEvaluationTime;

    readonly AITargetPerception perception = new AITargetPerception();
    readonly AITargetSelector selector = new AITargetSelector();
    readonly AITargetMemory memory = new AITargetMemory();
    AITargetSnapshot snapshot = AITargetSnapshot.Empty;
    CharacteContext ownerContext;
    HealthSystem healthSystem;
    StatusEffectController statusEffects;
    AIAimTargetDriver aimTargetDriver;
    float clock;
    float lastClockSample;
    float nextScanTime;
    bool scanning;
    bool pendingScan;
    bool pendingClear;
    int publicationVersion;

    public AITargetingProfileDef TargetingProfile => targetingProfile;
    /// <summary>Pure committed read for Behavior Designer conditional reevaluation.</summary>
    public AITargetSnapshot ReadSnapshot => snapshot;
    public float ObservationClock => clock;
    readonly RaycastHit[] hypotheticalSightHits = new RaycastHit[64];

    /// <summary>Checks geometry at a proposed actor position without scanning or updating memory.</summary>
    public bool HasLineOfSightFrom(Vector3 actorPosition, AITargetHandle target, Vector3 perceivedAim)
    {
        if (!target.IsLive || ownerContext == null) return false;
        Vector3 offset = sensorOrigin != null
            ? sensorOrigin.position - ownerContext.transform.position : Vector3.zero;
        return ThirdPersonTargetingUtility.HasLineOfSightNonAlloc(
            actorPosition + offset + Vector3.up * originHeightOffset, perceivedAim,
            target.Context.transform, obstacleLayers, hypotheticalSightHits, triggerInteraction, ownerContext.transform);
    }
    public AITargetSnapshot Snapshot
    {
        get
        {
            if (!scanning && isActiveAndEnabled &&
                ((snapshot.Target.InstanceId != 0 && !IsEligible(snapshot.Target)) ||
                 (snapshot.Visible.InstanceId != 0 && !IsEligible(snapshot.Visible)) ||
                 (snapshot.LastSeen.InstanceId != 0 && !IsEligible(snapshot.LastSeen))))
                RequestScan(false);
            return snapshot;
        }
    }
    public CharacteContext CurrentTargetContext => Snapshot.CurrentTargetContext;
    public Transform CurrentTarget => Snapshot.CurrentTarget;
    public Transform VisibleTarget => Snapshot.VisibleTarget;
    public Transform LastSeenTarget => Snapshot.LastSeenTarget;
    public Vector3 LastSeenPosition => Snapshot.LastSeenPosition;
    public bool HasLineOfSight => Snapshot.HasLineOfSight;
    public float GracePeriod => GetGracePeriodValue();
    public float TargetDistance => Snapshot.TargetDistance;
    public float VisibleTargetDistance => Snapshot.VisibleTargetDistance;
    public float LastSeenTime => Snapshot.LastSeenTime;
    public float TimeSinceLastSeen => clock - Snapshot.LastSeenTime;
    public bool HasLiveTarget => Snapshot.HasLiveTarget;
    public bool HasVisibleTarget => Snapshot.VisibleTarget != null;
    public bool HasAnyTarget => Snapshot.HasAnyTarget;
    public event Action<Transform, Transform> OnTargetChanged;
    public event Action<Transform, Transform> OnVisibleTargetChanged;

    public static AITargetSensor ResolveFor(Component module)
    {
        CharacteContext ctx = CharacterContextModuleLookup.ResolveContext(module);
        if (ctx == null) return null;
        // Read the common binding first; never call ResolveReferences here (sensor Awake can
        // precede context Awake). Bootstrap discovery remains ownership-scoped and unambiguous.
        if (ctx.AITargetSensor != null)
            return CharacterContextModuleLookup.ResolveContext(ctx.AITargetSensor) == ctx ? ctx.AITargetSensor : null;
        AITargetSensor found = null;
        foreach (var candidate in ctx.GetComponentsInChildren<AITargetSensor>(true))
        {
            if (CharacterContextModuleLookup.ResolveContext(candidate) != ctx) continue;
            if (found != null) return null;
            found = candidate;
        }
        return found;
    }

    void Awake()
    {
        ResolveReferences();
        if (sensorOrigin == null) sensorOrigin = transform;
        if (forwardReference == null) forwardReference = transform;
    }

    void OnEnable()
    {
        ResolveReferences();
        AITargetInfo.GlobalTargetStateChanged += HandleGlobalTargetStateChanged;
        CharacterContextRegistry.ContextUnregistered += HandleContextUnregistered;
        if (healthSystem != null) healthSystem.DamageTaken += OnDamageTaken;
        lastClockSample = Time.unscaledTime;
        nextScanTime = clock;
        pendingScan = true;
    }

    void OnDisable()
    {
        AITargetInfo.GlobalTargetStateChanged -= HandleGlobalTargetStateChanged;
        CharacterContextRegistry.ContextUnregistered -= HandleContextUnregistered;
        if (healthSystem != null) healthSystem.DamageTaken -= OnDamageTaken;
        memory.ClearTargets();
        memory.ClearThreat();
        pendingScan = false;
        Publish(AITargetSnapshot.Empty);
    }

    void ResolveReferences()
    {
        ownerContext = CharacterContextModuleLookup.ResolveContext(this);
        ownerContext?.ResolveReferences();
        healthSystem = ownerContext != null ? ownerContext.HealthSystem : null;
        statusEffects = ownerContext != null ? ownerContext.StatusEffects : null;
    }

    void UpdateClock()
    {
        float sample = Time.unscaledTime;
        float elapsed = Mathf.Max(0f, sample - lastClockSample);
        lastClockSample = sample;
        if (!Application.isPlaying || GlobalTimeScaleManager.Instance.IsPaused) return;
        clock += elapsed * (ownerContext != null && ownerContext.UsesWorldSlow
            ? TimeSlowManager.Instance.WorldTimeScale : 1f);
    }

    void Update()
    {
        UpdateClock();
        bool invalid = (memory.Current.InstanceId != 0 && !IsEligible(memory.Current)) ||
            (memory.LastSeen.InstanceId != 0 && !IsEligible(memory.LastSeen));
        // Taunt expiry/replacement must not wait for the normal perception interval.
        AITargetHandle taunt = ResolveTaunt();
        bool tauntChanged = taunt.InstanceId != 0
            ? snapshot.Reason != AITargetSelectionReason.Taunt || taunt != snapshot.Target
            : snapshot.Reason == AITargetSelectionReason.Taunt;
        if (invalid || pendingScan || tauntChanged || clock >= nextScanTime) RequestScan(false);
    }

    public void ForceScan()
    {
        UpdateClock();
        RequestScan(true);
    }

    void RequestScan(bool force)
    {
        if (!isActiveAndEnabled) return;
        if (scanning) { pendingScan = true; return; }
        scanning = true;
        try
        {
            // One deferred pass handles event callbacks; persistent requests continue next Update.
            for (int pass = 0; pass < 2; pass++)
            {
                pendingScan = false;
                if (pendingClear) { pendingClear = false; ClearMemoryCore(); break; }
                Scan(force || pass > 0);
                if (!pendingScan || !isActiveAndEnabled) break;
            }
            nextScanTime = clock + Mathf.Max(0.01f, scanInterval);
        }
        finally { scanning = false; }
    }

    public void ClearTargetMemory()
    {
        if (scanning) { pendingClear = true; pendingScan = true; return; }
        scanning = true;
        try { ClearMemoryCore(); }
        finally { scanning = false; }
    }

    void ClearMemoryCore()
    {
        memory.ClearTargets();
        statusEffects = ownerContext != null ? ownerContext.StatusEffects : null;
        statusEffects?.RemoveEffectsWithTag(StatusEffectTags.Taunt);
        Publish(AITargetSnapshot.Empty);
        nextScanTime = clock;
    }

    public void ClearThreatMemory() => memory.ClearThreat();
    public void SetTargetingProfile(AITargetingProfileDef profile, bool forceImmediateScan = true)
    {
        targetingProfile = profile;
        if (forceImmediateScan) ForceScan();
    }

    void HandleGlobalTargetStateChanged(AITargetInfo info)
    {
        if (info != null) RequestScan(false);
    }

    void HandleContextUnregistered(CharacteContext ctx)
    {
        memory.Forget(ctx);
        RequestScan(false);
    }

    int OwnerTeam => ownerContext != null && ownerContext.TargetInfo != null
        ? ownerContext.TargetInfo.TeamId : ownerTeamId;
    bool IsEligible(AITargetHandle target) => AITargetPerception.IsEligible(ownerContext, target,
        requireAliveIfAvailable, useTeamFilter, OwnerTeam);

    public void RegisterThreat(GameObject source, float amount)
    {
        if (source != null) RegisterThreat(source.transform, amount);
    }
    public void RegisterThreat(Transform source, float amount)
    {
        UpdateClock();
        AITargetHandle handle = new AITargetHandle(CharacterContextModuleLookup.ResolveContext(source));
        if (!UseThreatMemoryEnabled() || amount <= 0f || !IsEligible(handle)) return;
        AITargetInfo info = handle.Context.TargetInfo;
        memory.AddThreat(handle, amount * (info != null ? info.ThreatMultiplier : 1f), clock,
            GetThreatDecayPerSecondValue(), GetMaxThreatPerTargetValue());
    }
    void OnDamageTaken(float amount, GameObject attacker) => RegisterThreat(attacker, amount * GetDamageThreatMultiplierValue());
    public bool CanAcceptTaunt(Transform source) => IsEligible(new AITargetHandle(CharacterContextModuleLookup.ResolveContext(source)));
    public bool OnTauntApplied(Transform source)
    {
        if (!CanAcceptTaunt(source)) return false;
        RegisterThreat(source, GetMaxThreatPerTargetValue());
        ForceScan();
        if (aimTargetDriver == null && ownerContext != null)
            aimTargetDriver = ownerContext.GetComponentInChildren<AIAimTargetDriver>(true);
        aimTargetDriver?.ClearOverride();
        return true;
    }

    AITargetHandle ResolveTaunt()
    {
        statusEffects = ownerContext != null ? ownerContext.StatusEffects : null;
        if (statusEffects == null) return default;
        AITargetHandle best = default;
        ulong sequence = 0;
        var effects = statusEffects.ActiveEffects;
        for (int i = 0; i < effects.Count; i++)
        {
            StatusEffectInstance effect = effects[i];
            if (effect == null || effect.CurrentStacks <= 0 || effect.IsExpired() ||
                !StatusEffectTags.Has(effect.Definition, StatusEffectTags.Taunt) || effect.ApplicationSequence <= sequence)
                continue;
            AITargetHandle source = new AITargetHandle(effect.SourceContext, effect.SourceLifeGeneration);
            if (!IsEligible(source)) continue;
            best = source;
            sequence = effect.ApplicationSequence;
        }
        return best;
    }

    void Scan(bool force)
    {
        if (ownerContext == null || !ownerContext.isActiveAndEnabled)
        {
            memory.ClearTargets();
            memory.ClearThreat();
            Publish(AITargetSnapshot.Empty);
            return;
        }
        if (!IsEligible(memory.Current)) memory.Current = default;
        if (!IsEligible(memory.LastSeen)) memory.ClearLastSeen();
        memory.Prune(ownerContext, clock, GetThreatDecayPerSecondValue(), requireAliveIfAvailable, useTeamFilter, OwnerTeam);
        Vector3 origin = GetOriginPosition();
        var scanPolicy = new AITargetScanPolicy
        {
            Origin = origin, Forward = forwardReference != null ? forwardReference.forward : transform.forward,
            Radius = Mathf.Max(0f, radius), UseFov = useFieldOfView, Fov = fieldOfView, RequireLos = requireLineOfSight,
            RequireAlive = requireAliveIfAvailable, TeamFilter = useTeamFilter, OwnerTeam = OwnerTeam,
            TargetLayers = targetLayers, Obstacles = obstacleLayers, Triggers = triggerInteraction, FallbackHeight = fallbackTargetHeightOffset,
            DistanceWeight = GetDistanceScoreWeightValue(), Stickiness = GetCurrentTargetStickinessValue(), LosPenalty = GetLineOfSightPenaltyValue(),
            UseThreat = UseThreatMemoryEnabled(), ThreatDecay = GetThreatDecayPerSecondValue(), ThreatWeight = GetThreatScoreWeightValue(),
            PlayerBonus = targetingProfile != null ? targetingProfile.GetIdentityPriorityBonus(AITargetIdentity.Player) : playerPriorityBonus,
            CompanionBonus = targetingProfile != null ? targetingProfile.GetIdentityPriorityBonus(AITargetIdentity.Companion) : companionPriorityBonus,
            TankBonus = RoleBonus(CharacterCombatRole.Tank, tankPriorityBonus), HealerBonus = RoleBonus(CharacterCombatRole.Healer, healerPriorityBonus),
            SupportBonus = RoleBonus(CharacterCombatRole.Support, supportPriorityBonus), SniperBonus = RoleBonus(CharacterCombatRole.Sniper, sniperPriorityBonus),
            AssaultBonus = RoleBonus(CharacterCombatRole.Assault, assaultPriorityBonus)
        };
        perception.Collect(ownerContext, memory, selector, ResolveTaunt(), scanPolicy, clock,
            out AITargetCandidate best, out AITargetCandidate current, out AITargetCandidate taunted);
        Vector2 interval = GetRetargetIntervalRangeValue();
        float minimum = Mathf.Max(0.05f, interval.x);
        var policy = new AITargetSelectionPolicy
        {
            GracePeriod = GetGracePeriodValue(), MinLockDuration = GetMinTargetLockDurationValue(),
            SwitchThreshold = GetSwitchScoreThresholdValue(), SwitchRatio = GetSwitchScoreThresholdRatioValue(),
            LostHoldDuration = GetLostTargetHoldDurationValue(), KeepLastSeen = ShouldKeepLastSeenTarget(),
            EvaluationDelay = UnityEngine.Random.Range(minimum, Mathf.Max(minimum, interval.y))
        };
        AITargetCandidate selected = selector.Select(memory, best, current, taunted, policy, clock, force, out var reason);
        bool hasTarget = selected.Handle.InstanceId != 0;
        float distance = hasTarget ? Vector3.Distance(origin, selected.Point) : 0f;
        currentTargetScore = hasTarget ? selected.Score : float.NegativeInfinity;
        visibleBestScore = best.Handle.InstanceId != 0 ? best.Score : float.NegativeInfinity;
        Publish(new AITargetSnapshot(selected.Handle, best.Handle, memory.LastSeen, selected.Point,
            memory.LastSeenPosition, memory.LastSeenTime, distance, best.Distance,
            hasTarget && selected.HasLineOfSight, selected.IsVisible, reason));
    }

    float RoleBonus(CharacterCombatRole role, float fallback) => targetingProfile != null ? targetingProfile.GetCombatRolePriorityBonus(role) : fallback;

    void Publish(AITargetSnapshot next)
    {
        int version = ++publicationVersion;
        AITargetSnapshot old = snapshot;
        Transform oldTarget = currentTarget;
        Transform oldVisible = visibleTarget;
        snapshot = next;
        currentTarget = next.CurrentTarget;
        visibleTarget = next.VisibleTarget;
        lastSeenTarget = next.LastSeenTarget;
        lastSeenPosition = next.LastSeenPosition;
        lastSeenTime = next.LastSeenTime;
        hasLineOfSight = next.HasLineOfSight;
        targetDistance = next.TargetDistance;
        visibleTargetDistance = next.VisibleTargetDistance;
        currentTargetAcquiredTime = memory.AcquiredTime;
        nextRetargetEvaluationTime = memory.NextEvaluation;
        if (old.Target != next.Target) OnTargetChanged?.Invoke(oldTarget, currentTarget);
        if (version != publicationVersion) return; // A callback disabled this sensor and published an empty state.
        if (old.Visible != next.Visible) OnVisibleTargetChanged?.Invoke(oldVisible, visibleTarget);
    }

    Vector3 GetOriginPosition() => (sensorOrigin != null ? sensorOrigin.position : transform.position) + Vector3.up * originHeightOffset;

    public void CollectAuthoringIssues(List<string> issues)
    {
        CharacteContext ctx = CharacterContextModuleLookup.ResolveContext(this);
        if (ctx == null) issues.Add("AITargetSensor requires an owning CharacteContext.");
        if (requireLineOfSight && obstacleLayers.value == 0) issues.Add("Line of sight is enabled but obstacleLayers is empty.");
        if (targetLayers.value == 0) issues.Add("targetLayers is empty; no ordinary character targets can be discovered.");
        if (ctx != null)
        {
            if (ResolveFor(ctx) == null) issues.Add("Owning context has an ambiguous or foreign sensor binding.");
            int hostileLayers = ctx.TargetIdentity == AITargetIdentity.Enemy
                ? LayerMask.GetMask("Player", "Ally") : LayerMask.GetMask("Enemy");
            if ((targetLayers.value & hostileLayers) == 0)
                issues.Add("targetLayers excludes all authored opposing actor root layers.");
            string expectedLayer = ctx.TargetIdentity == AITargetIdentity.Enemy ? "Enemy" :
                ctx.TargetIdentity == AITargetIdentity.Player ? "Player" : "Ally";
            if (ctx.gameObject.layer != LayerMask.NameToLayer(expectedLayer))
                issues.Add("Actor root layer differs from the authored " + expectedLayer + " targeting layer.");
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        maxHits = Mathf.Max(1, maxHits); // Retained serialized compatibility; discovery has no hit cap.
        radius = Mathf.Max(0f, radius);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(GetOriginPosition(), radius);
        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(GetOriginPosition(), snapshot.AimPosition);
        }
    }
#endif
    bool ShouldKeepLastSeenTarget()
    {
        return targetingProfile != null ? targetingProfile.KeepLastSeenTarget : keepLastSeenTarget;
    }

    float GetGracePeriodValue()
    {
        return targetingProfile != null ? targetingProfile.GracePeriod : Mathf.Max(0f, gracePeriod);
    }

    float GetDistanceScoreWeightValue()
    {
        return targetingProfile != null ? targetingProfile.DistanceScoreWeight : Mathf.Max(0f, distanceScoreWeight);
    }

    float GetCurrentTargetStickinessValue()
    {
        return targetingProfile != null ? targetingProfile.CurrentTargetStickiness : Mathf.Max(0f, currentTargetStickiness);
    }

    float GetLineOfSightPenaltyValue()
    {
        return targetingProfile != null ? targetingProfile.LineOfSightPenalty : Mathf.Max(0f, lineOfSightPenalty);
    }

    Vector2 GetRetargetIntervalRangeValue()
    {
        return targetingProfile != null ? targetingProfile.RetargetIntervalRange : retargetIntervalRange;
    }

    float GetMinTargetLockDurationValue()
    {
        return targetingProfile != null ? targetingProfile.MinTargetLockDuration : Mathf.Max(0f, minTargetLockDuration);
    }

    float GetSwitchScoreThresholdValue()
    {
        return targetingProfile != null ? targetingProfile.SwitchScoreThreshold : Mathf.Max(0f, switchScoreThreshold);
    }

    float GetSwitchScoreThresholdRatioValue()
    {
        return targetingProfile != null ? targetingProfile.SwitchScoreThresholdRatio : Mathf.Clamp01(switchScoreThresholdRatio);
    }

    float GetLostTargetHoldDurationValue()
    {
        return targetingProfile != null ? targetingProfile.LostTargetHoldDuration : Mathf.Max(0f, lostTargetHoldDuration);
    }

    bool UseThreatMemoryEnabled()
    {
        return targetingProfile != null ? targetingProfile.UseThreatMemory : useThreatMemory;
    }

    float GetDamageThreatMultiplierValue()
    {
        return targetingProfile != null ? targetingProfile.DamageThreatMultiplier : Mathf.Max(0f, damageThreatMultiplier);
    }

    float GetThreatScoreWeightValue()
    {
        return targetingProfile != null ? targetingProfile.ThreatScoreWeight : Mathf.Max(0f, threatScoreWeight);
    }

    float GetThreatDecayPerSecondValue()
    {
        return targetingProfile != null ? targetingProfile.ThreatDecayPerSecond : Mathf.Max(0f, threatDecayPerSecond);
    }

    float GetMaxThreatPerTargetValue()
    {
        return targetingProfile != null ? targetingProfile.MaxThreatPerTarget : Mathf.Max(0f, maxThreatPerTarget);
    }

    float GetTauntScoreBonusValue()
    {
        return targetingProfile != null ? targetingProfile.TauntScoreBonus : Mathf.Max(0f, tauntScoreBonus);
    }

}
