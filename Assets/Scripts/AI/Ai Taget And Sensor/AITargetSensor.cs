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
    [SerializeField] private string requiredTag = "";
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

    readonly Dictionary<int, ThreatEntry> threatEntries = new Dictionary<int, ThreatEntry>();
    readonly HashSet<int> candidateInstanceIds = new HashSet<int>();

    Collider[] overlapBuffer;
    HealthSystem healthSystem;
    Transform tauntTarget;
    float tauntUntilTime = float.NegativeInfinity;
    float nextScanTime;

    public AITargetingProfileDef TargetingProfile => targetingProfile;
    public Transform CurrentTarget => IsCurrentTargetRetained() ? currentTarget : null;
    public Transform VisibleTarget => IsTrackedTargetStillValid(visibleTarget) ? visibleTarget : null;
    public Transform LastSeenTarget => IsTrackedTargetStillValid(lastSeenTarget) ? lastSeenTarget : null;
    public Vector3 LastSeenPosition => lastSeenPosition;
    public bool HasLineOfSight => hasLineOfSight;
    public float GracePeriod => GetGracePeriodValue();
    public float TargetDistance => targetDistance;
    public float VisibleTargetDistance => visibleTargetDistance;
    public float LastSeenTime => lastSeenTime;
    public float TimeSinceLastSeen => WorldNow - lastSeenTime;
    float WorldNow => TimeSlowManager.Instance.WorldTime;

    public bool HasLiveTarget => CurrentTarget != null;
    public bool HasVisibleTarget => VisibleTarget != null;
    public bool HasAnyTarget => HasLiveTarget || IsWithinGracePeriod();

    public event Action<Transform, Transform> OnTargetChanged;
    public event Action<Transform, Transform> OnVisibleTargetChanged;

    void Awake()
    {
        if (sensorOrigin == null)
            sensorOrigin = transform;

        if (forwardReference == null)
            forwardReference = transform;

        maxHits = Mathf.Max(1, maxHits);
        overlapBuffer = new Collider[maxHits];
        healthSystem = GetComponent<HealthSystem>();
        ScheduleNextRetargetEvaluation();
    }

    void OnEnable()
    {
        AITargetInfo.GlobalTargetStateChanged += HandleGlobalTargetStateChanged;

        if (healthSystem == null)
            healthSystem = GetComponent<HealthSystem>();

        if (healthSystem != null)
            healthSystem.DamageTaken += OnDamageTaken;
    }

    void OnDisable()
    {
        AITargetInfo.GlobalTargetStateChanged -= HandleGlobalTargetStateChanged;

        if (healthSystem != null)
            healthSystem.DamageTaken -= OnDamageTaken;

        SetVisibleTarget(null, 0f);
    }

    void Update()
    {
        if (RefreshTrackedTargetValidity())
            nextScanTime = WorldNow;

        if (WorldNow < nextScanTime)
            return;

        nextScanTime = WorldNow + Mathf.Max(0.01f, scanInterval);
        Scan(false);
    }

    public void ForceScan()
    {
        RefreshTrackedTargetValidity();
        Scan(true);
        nextScanTime = WorldNow + Mathf.Max(0.01f, scanInterval);
    }

    public void ClearTargetMemory()
    {
        SetVisibleTarget(null, 0f);
        SetCurrentTarget(null);
        if (!ShouldKeepLastSeenTarget())
            lastSeenTarget = null;

        lastSeenPosition = Vector3.zero;
        hasLineOfSight = false;
        targetDistance = 0f;
        lastSeenTime = float.NegativeInfinity;
        currentTargetScore = float.NegativeInfinity;
        visibleBestScore = float.NegativeInfinity;
        currentTargetAcquiredTime = float.NegativeInfinity;
        tauntTarget = null;
        tauntUntilTime = float.NegativeInfinity;
        ScheduleNextRetargetEvaluation();
    }

    public void ClearThreatMemory()
    {
        threatEntries.Clear();
    }

    public void SetTargetingProfile(AITargetingProfileDef profile, bool forceImmediateScan = true)
    {
        targetingProfile = profile;
        if (!forceImmediateScan)
            return;

        nextScanTime = WorldNow;
        ScheduleNextRetargetEvaluation();
    }

    void HandleGlobalTargetStateChanged(AITargetInfo targetInfo)
    {
        if (targetInfo == null || !ReferencesTrackedTarget(targetInfo.transform))
            return;

        ForceScan();
    }

    bool ReferencesTrackedTarget(Transform changedTarget)
    {
        if (changedTarget == null)
            return false;

        return currentTarget == changedTarget || lastSeenTarget == changedTarget;
    }

    public void RegisterThreat(GameObject source, float amount)
    {
        if (source == null || amount <= 0f)
            return;

        RegisterThreat(source.transform, amount);
    }

    public void RegisterThreat(Transform source, float amount)
    {
        if (source == null || amount <= 0f)
            return;

        if (!TryResolveTrackedTarget(source, out Transform targetRoot, out IAITargetable targetable))
            return;

        RegisterThreatInternal(targetRoot, targetable, amount);
    }

    public bool ApplyTaunt(Transform target, float duration)
    {
        if (target == null || duration <= 0f)
            return false;

        if (!TryResolveTrackedTarget(target, out Transform targetRoot, out IAITargetable targetable))
            return false;

        if (!IsTrackedTargetStillValid(targetRoot))
            return false;

        tauntTarget = targetRoot;
        tauntUntilTime = WorldNow + duration;
        RegisterThreatInternal(targetRoot, targetable, GetMaxThreatPerTargetValue());
        ForceScan();
        return true;
    }

    void Scan(bool ignoreRetargetTimers)
    {
        Vector3 origin = GetOriginPosition();
        int count = Physics.OverlapSphereNonAlloc(
            origin,
            radius,
            overlapBuffer,
            targetLayers,
            triggerInteraction);

        candidateInstanceIds.Clear();
        UpdateTauntState();

        Candidate bestVisible = default(Candidate);
        Candidate currentVisible = default(Candidate);
        bool hasBestVisible = false;
        bool hasCurrentVisible = false;

        for (int i = 0; i < count; i++)
        {
            Collider hit = overlapBuffer[i];
            if (hit == null)
                continue;

            if (!TryResolveTarget(hit, out Transform targetRoot, out IAITargetable targetable))
                continue;

            if (targetRoot == null || !candidateInstanceIds.Add(targetRoot.GetInstanceID()))
                continue;

            if (!TryBuildCandidate(hit, targetRoot, targetable, origin, out Candidate candidate))
                continue;

            if (currentTarget != null && candidate.TargetRoot == currentTarget)
            {
                currentVisible = candidate;
                hasCurrentVisible = true;
            }

            if (!hasBestVisible || candidate.Score > bestVisible.Score)
            {
                bestVisible = candidate;
                hasBestVisible = true;
            }
        }

        visibleBestScore = hasBestVisible ? bestVisible.Score : float.NegativeInfinity;
        if (hasBestVisible)
            SetVisibleTarget(bestVisible.TargetRoot, bestVisible.Distance);
        else
            SetVisibleTarget(null, 0f);

        if (hasBestVisible)
        {
            if (currentTarget == null)
            {
                ApplyVisibleTarget(bestVisible);
                return;
            }

            if (currentTarget == bestVisible.TargetRoot)
            {
                ApplyVisibleTarget(bestVisible);
                if (ignoreRetargetTimers || WorldNow >= nextRetargetEvaluationTime)
                    ScheduleNextRetargetEvaluation();

                return;
            }

            if (hasCurrentVisible)
            {
                ApplyVisibleTarget(currentVisible);
                if (ShouldSwitchVisibleTarget(currentVisible, bestVisible, ignoreRetargetTimers))
                    ApplyVisibleTarget(bestVisible);

                return;
            }

            hasLineOfSight = false;
            currentTargetScore = float.NegativeInfinity;
            targetDistance = Vector3.Distance(origin, lastSeenPosition);

            if (ShouldSwitchFromLostTarget(ignoreRetargetTimers))
                ApplyVisibleTarget(bestVisible);

            return;
        }

        hasLineOfSight = false;
        currentTargetScore = float.NegativeInfinity;

        if (IsCurrentTargetRetained())
        {
            targetDistance = Vector3.Distance(origin, lastSeenPosition);
            return;
        }

        SetCurrentTarget(null);

        if (IsWithinGracePeriod())
        {
            targetDistance = Vector3.Distance(origin, lastSeenPosition);
        }
        else
        {
            if (!ShouldKeepLastSeenTarget())
                lastSeenTarget = null;

            targetDistance = 0f;
        }
    }

    void ApplyVisibleTarget(Candidate candidate)
    {
        SetCurrentTarget(candidate.TargetRoot);
        lastSeenTarget = candidate.TargetRoot;
        lastSeenPosition = candidate.TargetPoint;
        hasLineOfSight = candidate.HasLineOfSight;
        targetDistance = candidate.Distance;
        lastSeenTime = WorldNow;
        currentTargetScore = candidate.Score;
    }

    bool TryBuildCandidate(
        Collider hit,
        Transform targetRoot,
        IAITargetable targetable,
        Vector3 origin,
        out Candidate candidate)
    {
        candidate = default(Candidate);

        if (!IsValidTarget(hit, targetRoot, targetable))
            return false;

        Vector3 targetPoint = GetTargetPoint(hit, targetRoot, targetable);
        Vector3 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;

        if (useFieldOfView && fieldOfView < 360f && !IsInsideFOVXZ(toTarget))
            return false;

        bool lineOfSight = !requireLineOfSight || CheckLineOfSight(origin, targetPoint);
        if (requireLineOfSight && !lineOfSight)
            return false;

        candidate = new Candidate
        {
            TargetRoot = targetRoot,
            TargetPoint = targetPoint,
            Distance = distance,
            HasLineOfSight = lineOfSight,
            Score = EvaluateCandidateScore(targetRoot, targetable, distance, lineOfSight)
        };
        return true;
    }

    float EvaluateCandidateScore(
        Transform targetRoot,
        IAITargetable targetable,
        float distance,
        bool lineOfSight)
    {
        float score = 0f;

        if (targetable != null)
            score += targetable.BaseTargetPriority;

        score += GetIdentityPriorityBonus(targetable);
        score += GetCombatRolePriorityBonus(targetable);
        score += GetDistanceScore(distance);
        score += GetThreatScore(targetRoot);

        if (currentTarget != null && currentTarget == targetRoot)
            score += GetCurrentTargetStickinessValue();

        if (!lineOfSight)
            score -= GetLineOfSightPenaltyValue();

        if (IsTauntedTarget(targetRoot))
            score += GetTauntScoreBonusValue();

        return score;
    }

    float GetIdentityPriorityBonus(IAITargetable targetable)
    {
        if (targetable == null)
            return 0f;

        if (targetingProfile != null)
            return targetingProfile.GetIdentityPriorityBonus(targetable.TargetIdentity);

        switch (targetable.TargetIdentity)
        {
            case AITargetIdentity.Player:
                return playerPriorityBonus;
            case AITargetIdentity.Companion:
                return companionPriorityBonus;
            default:
                return 0f;
        }
    }

    float GetCombatRolePriorityBonus(IAITargetable targetable)
    {
        if (targetable == null)
            return 0f;

        if (targetingProfile != null)
            return targetingProfile.GetCombatRolePriorityBonus(targetable.CombatRole);

        switch (targetable.CombatRole)
        {
            case CharacterCombatRole.Tank:
                return tankPriorityBonus;
            case CharacterCombatRole.Healer:
                return healerPriorityBonus;
            case CharacterCombatRole.Support:
                return supportPriorityBonus;
            case CharacterCombatRole.Sniper:
                return sniperPriorityBonus;
            case CharacterCombatRole.Assault:
                return assaultPriorityBonus;
            default:
                return 0f;
        }
    }

    float GetDistanceScore(float distance)
    {
        if (radius <= 0.01f)
            return 0f;

        float normalized = 1f - Mathf.Clamp01(distance / radius);
        return normalized * GetDistanceScoreWeightValue();
    }

    float GetThreatScore(Transform targetRoot)
    {
        if (!UseThreatMemoryEnabled() || targetRoot == null)
            return 0f;

        int instanceId = targetRoot.GetInstanceID();
        if (!threatEntries.TryGetValue(instanceId, out ThreatEntry entry))
            return 0f;

        UpdateThreatEntry(entry);
        if (entry.Threat <= 0.001f)
        {
            threatEntries.Remove(instanceId);
            return 0f;
        }

        return entry.Threat * GetThreatScoreWeightValue();
    }

    bool ShouldSwitchVisibleTarget(Candidate currentVisible, Candidate bestVisible, bool ignoreRetargetTimers)
    {
        if (!DoesCandidateBeatCurrent(currentVisible.Score, bestVisible.Score))
        {
            if (WorldNow >= nextRetargetEvaluationTime)
                ScheduleNextRetargetEvaluation();

            return false;
        }

        if (ignoreRetargetTimers)
            return true;

        if (WorldNow - currentTargetAcquiredTime < GetMinTargetLockDurationValue())
            return false;

        if (WorldNow < nextRetargetEvaluationTime)
            return false;

        ScheduleNextRetargetEvaluation();
        return true;
    }

    bool ShouldSwitchFromLostTarget(bool ignoreRetargetTimers)
    {
        if (ignoreRetargetTimers)
            return true;

        if (WorldNow - lastSeenTime < GetLostTargetHoldDurationValue())
            return false;

        ScheduleNextRetargetEvaluation();
        return true;
    }

    bool DoesCandidateBeatCurrent(float currentScoreValue, float challengerScore)
    {
        float ratioThreshold = Mathf.Abs(currentScoreValue) * GetSwitchScoreThresholdRatioValue();
        float threshold = Mathf.Max(GetSwitchScoreThresholdValue(), ratioThreshold);
        return challengerScore > currentScoreValue + threshold;
    }

    void ScheduleNextRetargetEvaluation()
    {
        Vector2 intervalRange = GetRetargetIntervalRangeValue();
        float minInterval = Mathf.Max(0.05f, intervalRange.x);
        float maxInterval = Mathf.Max(minInterval, intervalRange.y);
        nextRetargetEvaluationTime = WorldNow + UnityEngine.Random.Range(minInterval, maxInterval);
    }

    void SetCurrentTarget(Transform newTarget)
    {
        if (currentTarget == newTarget)
            return;

        Transform old = currentTarget;
        currentTarget = newTarget;

        if (newTarget != null)
        {
            currentTargetAcquiredTime = WorldNow;
            ScheduleNextRetargetEvaluation();
        }
        else
        {
            currentTargetAcquiredTime = float.NegativeInfinity;
            nextRetargetEvaluationTime = WorldNow;
        }

        OnTargetChanged?.Invoke(old, newTarget);
    }

    void SetVisibleTarget(Transform newTarget, float distance)
    {
        if (visibleTarget == newTarget)
        {
            visibleTargetDistance = newTarget != null ? distance : 0f;
            return;
        }

        Transform old = visibleTarget;
        visibleTarget = newTarget;
        visibleTargetDistance = newTarget != null ? distance : 0f;
        OnVisibleTargetChanged?.Invoke(old, newTarget);
    }

    bool TryResolveTarget(Collider hit, out Transform targetRoot, out IAITargetable targetable)
    {
        targetRoot = null;
        targetable = null;

        if (hit == null)
            return false;

        if (TryResolveCharacterTarget(hit.transform, out targetRoot, out targetable))
            return true;

        targetable = FindTargetable(hit.transform);
        if (targetable is Component targetComponent)
        {
            targetRoot = targetComponent.transform;
            return true;
        }

        if (hit.attachedRigidbody != null)
        {
            Transform rigidbodyRoot = hit.attachedRigidbody.transform;
            if (TryResolveCharacterTarget(rigidbodyRoot, out targetRoot, out targetable))
                return true;

            targetRoot = rigidbodyRoot;
            return true;
        }

        targetRoot = hit.transform.root != null ? hit.transform.root : hit.transform;
        return targetRoot != null;
    }

    bool IsValidTarget(Collider hit, Transform targetRoot, IAITargetable targetable)
    {
        if (targetRoot == null)
            return false;

        if (targetRoot == transform || targetRoot.root == transform.root)
            return false;

        if (!string.IsNullOrEmpty(requiredTag))
        {
            bool rootMatch = targetRoot.CompareTag(requiredTag);
            bool hitMatch = hit.CompareTag(requiredTag);
            if (!rootMatch && !hitMatch)
                return false;
        }

        if (targetable != null && !IsTargetAllowedByTargetable(targetable))
            return false;

        return IsTargetAllowedByLifeState(targetRoot);
    }

    bool RefreshTrackedTargetValidity()
    {
        bool invalidated = false;
        UpdateTauntState();

        if (currentTarget != null && !IsTrackedTargetStillValid(currentTarget))
        {
            SetCurrentTarget(null);
            hasLineOfSight = false;
            targetDistance = 0f;
            currentTargetScore = float.NegativeInfinity;
            invalidated = true;
        }

        if (visibleTarget != null && !IsTrackedTargetStillValid(visibleTarget))
        {
            SetVisibleTarget(null, 0f);
            invalidated = true;
        }

        if (lastSeenTarget != null && !IsTrackedTargetStillValid(lastSeenTarget))
        {
            lastSeenTarget = null;
            lastSeenPosition = Vector3.zero;
            lastSeenTime = float.NegativeInfinity;
            hasLineOfSight = false;
            targetDistance = 0f;
            currentTargetScore = float.NegativeInfinity;
            invalidated = true;
        }

        PruneThreatEntries();
        return invalidated;
    }

    bool IsTrackedTargetStillValid(Transform target)
    {
        if (!TryResolveTrackedTarget(target, out Transform targetRoot, out IAITargetable targetable))
            return false;

        if (targetRoot == transform || targetRoot.root == transform.root)
            return false;

        if (targetable != null && !IsTargetAllowedByTargetable(targetable))
            return false;

        return IsTargetAllowedByLifeState(targetRoot);
    }

    bool TryResolveTrackedTarget(Transform target, out Transform targetRoot, out IAITargetable targetable)
    {
        targetRoot = null;
        targetable = null;

        if (target == null)
            return false;

        if (TryResolveCharacterTarget(target, out targetRoot, out targetable))
            return true;

        targetable = FindTargetable(target);
        if (targetable is Component targetComponent)
        {
            targetRoot = targetComponent.transform;
            return true;
        }

        Rigidbody targetRigidbody = target.GetComponentInParent<Rigidbody>();
        if (targetRigidbody != null)
        {
            Transform rigidbodyRoot = targetRigidbody.transform;
            if (TryResolveCharacterTarget(rigidbodyRoot, out targetRoot, out targetable))
                return true;

            targetRoot = rigidbodyRoot;
            return true;
        }

        targetRoot = target.root != null ? target.root : target;
        return targetRoot != null;
    }

    bool TryResolveCharacterTarget(Transform start, out Transform targetRoot, out IAITargetable targetable)
    {
        targetRoot = null;
        targetable = null;

        if (start == null)
            return false;

        CharacteContext targetContext = start.GetComponentInParent<CharacteContext>();
        if (targetContext == null)
            return false;

        if (targetContext.transform == transform || targetContext.transform.root == transform.root)
            return false;

        AITargetInfo targetInfo = GetCachedTargetInfo(targetContext);
        if (targetInfo != null)
        {
            targetRoot = targetInfo.transform;
            targetable = targetInfo;
            return true;
        }

        targetRoot = targetContext.transform;
        return true;
    }

    AITargetInfo GetCachedTargetInfo(CharacteContext targetContext)
    {
        if (targetContext == null)
            return null;

        if (targetContext.TargetInfo != null)
            return targetContext.TargetInfo;

        targetContext.TargetInfo = targetContext.GetComponent<AITargetInfo>();
        if (targetContext.TargetInfo == null)
            targetContext.TargetInfo = targetContext.GetComponentInChildren<AITargetInfo>(true);

        return targetContext.TargetInfo;
    }

    bool IsTargetAllowedByTargetable(IAITargetable targetable)
    {
        if (targetable == null)
            return true;

        if (requireAliveIfAvailable && !targetable.IsAlive)
            return false;

        if (!targetable.IsTargetable)
            return false;

        if (useTeamFilter && targetable.TeamId == ownerTeamId)
            return false;

        return true;
    }

    bool IsTargetAllowedByLifeState(Transform targetRoot)
    {
        if (targetRoot == null)
            return false;

        CharacteContext targetContext = targetRoot.GetComponentInParent<CharacteContext>();
        if (targetContext == null || targetContext.stateHub == null)
            return true;

        return targetContext.stateHub.IsAlive && !targetContext.stateHub.Isdown;
    }

    IAITargetable FindTargetable(Transform start)
    {
        if (start == null)
            return null;

        MonoBehaviour[] all = start.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is IAITargetable targetable)
                return targetable;
        }

        return null;
    }

    Vector3 GetTargetPoint(Collider hit, Transform targetRoot, IAITargetable targetable)
    {
        if (targetable != null && targetable.AimPoint != null)
            return targetable.AimPoint.position;

        if (hit != null)
            return hit.bounds.center;

        return targetRoot.position + Vector3.up * fallbackTargetHeightOffset;
    }

    Vector3 GetOriginPosition()
    {
        Transform origin = sensorOrigin != null ? sensorOrigin : transform;
        return origin.position + Vector3.up * originHeightOffset;
    }

    bool IsInsideFOVXZ(Vector3 toTarget)
    {
        Vector3 forward = forwardReference != null ? forwardReference.forward : transform.forward;
        Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        Vector3 flatToTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);

        if (flatForward.sqrMagnitude < 0.0001f || flatToTarget.sqrMagnitude < 0.0001f)
            return true;

        return Vector3.Angle(flatForward, flatToTarget) <= fieldOfView * 0.5f;
    }

    bool CheckLineOfSight(Vector3 origin, Vector3 targetPoint)
    {
        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
            return true;

        return !Physics.Raycast(
            origin,
            direction / distance,
            distance,
            obstacleLayers,
            triggerInteraction);
    }

    bool IsWithinGracePeriod()
    {
        if (lastSeenTarget != null && !IsTrackedTargetStillValid(lastSeenTarget))
            return false;

        return WorldNow - lastSeenTime <= GetGracePeriodValue();
    }

    bool IsCurrentTargetRetained()
    {
        if (currentTarget == null)
            return false;

        if (!IsTrackedTargetStillValid(currentTarget))
            return false;

        if (hasLineOfSight)
            return true;

        return WorldNow - lastSeenTime <= GetGracePeriodValue();
    }

    void OnDamageTaken(float amount, GameObject attacker)
    {
        if (attacker == null || amount <= 0f)
            return;

        RegisterThreat(attacker, amount * GetDamageThreatMultiplierValue());
    }

    void RegisterThreatInternal(Transform targetRoot, IAITargetable targetable, float amount)
    {
        if (!UseThreatMemoryEnabled() || targetRoot == null || amount <= 0f)
            return;

        int instanceId = targetRoot.GetInstanceID();
        if (!threatEntries.TryGetValue(instanceId, out ThreatEntry entry))
        {
            entry = new ThreatEntry();
            entry.Target = targetRoot;
            entry.Threat = 0f;
            entry.LastUpdateTime = WorldNow;
            threatEntries.Add(instanceId, entry);
        }

        UpdateThreatEntry(entry);

        float multiplier = targetable != null ? Mathf.Max(0f, targetable.ThreatMultiplier) : 1f;
        entry.Target = targetRoot;
        entry.Threat = Mathf.Clamp(entry.Threat + amount * multiplier, 0f, GetMaxThreatPerTargetValue());
        entry.LastUpdateTime = WorldNow;
    }

    void UpdateThreatEntry(ThreatEntry entry)
    {
        if (entry == null)
            return;

        float elapsed = Mathf.Max(0f, WorldNow - entry.LastUpdateTime);
        if (elapsed <= 0f)
            return;

        entry.Threat = Mathf.Max(0f, entry.Threat - GetThreatDecayPerSecondValue() * elapsed);
        entry.LastUpdateTime = WorldNow;
    }

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

    void PruneThreatEntries()
    {
        if (threatEntries.Count == 0)
            return;

        List<int> removeKeys = null;
        foreach (KeyValuePair<int, ThreatEntry> pair in threatEntries)
        {
            ThreatEntry entry = pair.Value;
            if (entry == null)
            {
                if (removeKeys == null)
                    removeKeys = new List<int>();

                removeKeys.Add(pair.Key);
                continue;
            }

            UpdateThreatEntry(entry);
            if (entry.Target == null || entry.Threat <= 0.001f || !IsTrackedTargetStillValid(entry.Target))
            {
                if (removeKeys == null)
                    removeKeys = new List<int>();

                removeKeys.Add(pair.Key);
            }
        }

        if (removeKeys == null)
            return;

        for (int i = 0; i < removeKeys.Count; i++)
            threatEntries.Remove(removeKeys[i]);
    }

    bool IsTauntedTarget(Transform target)
    {
        return tauntTarget != null &&
               WorldNow <= tauntUntilTime &&
               target != null &&
               tauntTarget == target;
    }

    void UpdateTauntState()
    {
        if (tauntTarget == null)
            return;

        if (WorldNow <= tauntUntilTime && IsTrackedTargetStillValid(tauntTarget))
            return;

        tauntTarget = null;
        tauntUntilTime = float.NegativeInfinity;
    }

    struct Candidate
    {
        public Transform TargetRoot;
        public Vector3 TargetPoint;
        public float Distance;
        public bool HasLineOfSight;
        public float Score;
    }

    sealed class ThreatEntry
    {
        public Transform Target;
        public float Threat;
        public float LastUpdateTime;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 origin = (sensorOrigin != null ? sensorOrigin.position : transform.position) + Vector3.up * originHeightOffset;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, radius);

        Transform trackedTarget = CurrentTarget;
        if (trackedTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin, trackedTarget.position);
        }
        else if (IsWithinGracePeriod())
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(lastSeenPosition, 0.15f);
            Gizmos.DrawLine(origin, lastSeenPosition);
        }
    }
#endif
}
