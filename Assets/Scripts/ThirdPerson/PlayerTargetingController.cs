using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerTargetingController : MonoBehaviour
{
    sealed class CandidateCache
    {
        public AITargetInfo TargetInfo;
        public HealthSystem Health;
        public CharacterController CharacterController;
        public Collider[] Colliders;
        public Renderer[] Renderers;
    }

    [SerializeField] private PlayerContext owner;
    [SerializeField, Min(0.1f)] private float maximumRange = 30f;
    [SerializeField, Range(0.01f, 0.5f)] private float acquireRadius = 0.15f;
    [SerializeField, Range(0.01f, 0.5f)] private float releaseRadius = 0.18f;
    [SerializeField, Range(0f, 0.25f)] private float switchAdvantage = 0.015f;
    [SerializeField, Min(0f)] private float switchDelay = 0.1f;
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private QueryTriggerInteraction obstacleTriggers = QueryTriggerInteraction.Ignore;
    [SerializeField] private float fallbackAimHeight = 0.9f;
    [SerializeField] private float indicatorClearance = 0.25f;

    readonly Dictionary<CharacteContext, CandidateCache> candidateCaches =
        new Dictionary<CharacteContext, CandidateCache>();
    readonly RaycastHit[] lineOfSightHits = new RaycastHit[64];

    SkillTargetHandle currentHandle = SkillTargetHandle.None;
    CharacteContext currentTarget;
    CharacteContext switchChallenger;
    int switchChallengerGeneration;
    float switchChallengerSince;
    int lastCommittedFrame = -1;

    public CharacteContext CurrentTarget => TryReadCommittedTarget(out CharacteContext target)
        ? target
        : null;

    public event Action<CharacteContext, CharacteContext> TargetChanged;
    public event Action SelectionCommitted;

    void Awake()
    {
        ResolveReferences();
    }

    void OnEnable()
    {
        ResolveReferences();
        CharacterContextRegistry.ContextUnregistered += OnContextUnregistered;
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
    }

    void OnDisable()
    {
        CharacterContextRegistry.ContextUnregistered -= OnContextUnregistered;
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        CommitTarget(null);
        SelectionCommitted?.Invoke();
    }

    void Update()
    {
        if (Camera.main != null || currentTarget == null)
            return;

        lastCommittedFrame = Time.frameCount;
        CommitTarget(null);
        SelectionCommitted?.Invoke();
    }

    void OnContextUnregistered(CharacteContext context)
    {
        candidateCaches.Remove(context);
        if (context == currentTarget)
            CommitTarget(null);
        if (context == switchChallenger)
            ResetSwitchChallenger();
    }

    public bool TryGetTarget(out CharacteContext target)
    {
        return TryReadCommittedTarget(out target);
    }

    public bool TryValidateForAction(
        CharacteContext target,
        float actionRange,
        bool requireLineOfSight = true)
    {
        if (!TryReadCommittedTarget(out CharacteContext committed) || committed != target)
            return false;

        Vector3 scoringPoint = ResolveScoringPoint(target);
        if (actionRange > 0f &&
            (scoringPoint - owner.transform.position).sqrMagnitude > actionRange * actionRange)
        {
            return false;
        }

        Camera camera = Camera.main;
        return !requireLineOfSight ||
               (camera != null && HasLineOfSight(camera.transform.position, scoringPoint, target));
    }

    public bool TryGetIndicatorScreenPoint(out Vector2 screenPoint)
    {
        screenPoint = default;
        if (!TryReadCommittedTarget(out CharacteContext target))
            return false;

        Camera camera = Camera.main;
        if (camera == null)
            return false;

        Vector3 viewport = camera.WorldToViewportPoint(ResolveOverheadPoint(target));
        if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
            return false;

        screenPoint = new Vector2(viewport.x * Screen.width, viewport.y * Screen.height);
        return true;
    }

    void OnCameraUpdated(CinemachineBrain brain)
    {
        Camera mainCamera = Camera.main;
        if (lastCommittedFrame == Time.frameCount)
            return;

        if (mainCamera == null)
        {
            lastCommittedFrame = Time.frameCount;
            CommitTarget(null);
            SelectionCommitted?.Invoke();
            return;
        }

        Camera camera = brain != null ? brain.OutputCamera : mainCamera;
        if (camera != mainCamera)
            return;

        lastCommittedFrame = Time.frameCount;
        TickSelection(camera);
        SelectionCommitted?.Invoke();
    }

    void TickSelection(Camera camera)
    {
        ResolveReferences();
        if (!CanSelectTargets())
        {
            CommitTarget(null);
            return;
        }

        bool hasCurrent = TryReadCommittedTarget(out CharacteContext validatedCurrent);
        float currentScore = float.PositiveInfinity;
        bool currentStillValid = hasCurrent &&
            TryEvaluateCandidate(validatedCurrent, camera, releaseRadius, out currentScore, out _);

        CharacteContext best = null;
        float bestScore = float.PositiveInfinity;
        float bestWorldDistance = float.PositiveInfinity;
        IReadOnlyList<CharacteContext> contexts = CharacterContextRegistry.ActiveContexts;

        for (int i = 0; i < contexts.Count; i++)
        {
            CharacteContext candidate = contexts[i];
            if (!TryEvaluateCandidate(candidate, camera, acquireRadius, out float score, out float worldDistance))
                continue;

            if (!IsBetterCandidate(
                    candidate,
                    score,
                    worldDistance,
                    best,
                    bestScore,
                    bestWorldDistance,
                    validatedCurrent))
            {
                continue;
            }

            best = candidate;
            bestScore = score;
            bestWorldDistance = worldDistance;
        }

        if (!currentStillValid)
        {
            CommitTarget(best);
            return;
        }

        if (best == null || best == validatedCurrent || bestScore + switchAdvantage >= currentScore)
        {
            ResetSwitchChallenger();
            return;
        }

        if (switchChallenger != best || switchChallengerGeneration != best.LifeGeneration)
        {
            switchChallenger = best;
            switchChallengerGeneration = best.LifeGeneration;
            switchChallengerSince = Time.unscaledTime;
            return;
        }

        if (Time.unscaledTime - switchChallengerSince >= switchDelay)
            CommitTarget(best);
    }

    bool TryEvaluateCandidate(
        CharacteContext candidate,
        Camera camera,
        float radius,
        out float score,
        out float worldDistance)
    {
        score = float.PositiveInfinity;
        worldDistance = float.PositiveInfinity;

        if (!IsBasicTargetValid(candidate))
            return false;

        Vector3 point = ResolveScoringPoint(candidate);
        worldDistance = Vector3.Distance(owner.transform.position, point);
        if (worldDistance > maximumRange)
            return false;

        if (!ThirdPersonTargetingUtility.TryGetReticleScore(camera, point, out score, radius))
            return false;

        return HasLineOfSight(camera.transform.position, point, candidate);
    }

    bool TryReadCommittedTarget(out CharacteContext target)
    {
        target = null;
        if (!CanSelectTargets() || currentTarget == null || currentHandle == null ||
            !currentHandle.TryResolveEffectTarget(out CharacteContext resolved) ||
            resolved != currentTarget || !IsBasicTargetValid(resolved))
        {
            return false;
        }

        target = resolved;
        return true;
    }

    bool IsBasicTargetValid(CharacteContext candidate)
    {
        if (candidate == null || candidate == owner || !candidate.isActiveAndEnabled ||
            !candidate.gameObject.activeInHierarchy || !CharacterFactionUtility.AreHostile(owner, candidate))
        {
            return false;
        }

        CandidateCache cache = ResolveCache(candidate);
        if (cache.Health == null || !cache.Health.IsAlive)
            return false;

        return cache.TargetInfo == null ||
               (cache.TargetInfo.IsAlive && cache.TargetInfo.IsTargetable);
    }

    bool CanSelectTargets()
    {
        if (owner == null || !owner.isActiveAndEnabled || !owner.gameObject.activeInHierarchy)
            return false;

        if (Camera.main == null)
            return false;

        if (owner.HealthSystem != null && !owner.HealthSystem.IsAlive)
            return false;

        if (owner.stateHub != null && (!owner.stateHub.IsAlive || owner.stateHub.Isdown))
            return false;

        GameplayCameraController cameraController = GameplayCameraController.Instance;
        return cameraController == null || cameraController.GameplayInputEnabled;
    }

    CandidateCache ResolveCache(CharacteContext target)
    {
        if (candidateCaches.TryGetValue(target, out CandidateCache cache))
            return cache;

        target.ResolveReferences();
        cache = new CandidateCache
        {
            TargetInfo = target.TargetInfo,
            Health = target.HealthSystem,
            CharacterController = target.cc != null
                ? target.cc
                : target.GetComponentInChildren<CharacterController>(true),
            Colliders = target.GetComponentsInChildren<Collider>(true),
            Renderers = target.GetComponentsInChildren<Renderer>(true),
        };
        candidateCaches[target] = cache;
        return cache;
    }

    Vector3 ResolveScoringPoint(CharacteContext target)
    {
        CandidateCache cache = ResolveCache(target);
        if (cache.TargetInfo != null && cache.TargetInfo.AimPoint != null)
            return cache.TargetInfo.AimPoint.position;

        if (TryResolveBounds(target, cache, out Bounds bounds))
            return bounds.center;

        return target.transform.position + Vector3.up * Mathf.Max(0f, fallbackAimHeight);
    }

    Vector3 ResolveOverheadPoint(CharacteContext target)
    {
        CandidateCache cache = ResolveCache(target);
        if (TryResolveBounds(target, cache, out Bounds bounds))
            return new Vector3(bounds.center.x, bounds.max.y + indicatorClearance, bounds.center.z);

        return target.transform.position +
               Vector3.up * (CharacterTargetHeightUtility.FallbackHeight + indicatorClearance);
    }

    static bool TryResolveBounds(CharacteContext target, CandidateCache cache, out Bounds bounds)
    {
        bounds = default;
        bool found = false;

        if (cache.CharacterController != null && cache.CharacterController.gameObject.activeInHierarchy)
        {
            bounds = cache.CharacterController.bounds;
            found = true;
        }

        Collider[] colliders = cache.Colliders;
        for (int i = 0; colliders != null && i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy)
                continue;

            if (!found)
            {
                bounds = collider.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        Renderer[] renderers = cache.Renderers;
        for (int i = 0; renderers != null && i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    bool HasLineOfSight(Vector3 origin, Vector3 targetPoint, CharacteContext target)
    {
        return ThirdPersonTargetingUtility.HasLineOfSightNonAlloc(
            origin,
            targetPoint,
            target.transform,
            obstacleMask,
            lineOfSightHits,
            obstacleTriggers,
            owner != null ? owner.transform : null);
    }

    static bool IsBetterCandidate(
        CharacteContext candidate,
        float score,
        float worldDistance,
        CharacteContext best,
        float bestScore,
        float bestWorldDistance,
        CharacteContext current,
        float epsilon = 0.00001f)
    {
        if (best == null)
            return true;
        if (score < bestScore - epsilon)
            return true;
        if (score > bestScore + epsilon)
            return false;
        if (candidate == current && best != current)
            return true;
        if (best == current)
            return false;
        if (worldDistance < bestWorldDistance - epsilon)
            return true;
        if (worldDistance > bestWorldDistance + epsilon)
            return false;

        int candidateKey = candidate.GetInstanceID();
        int bestKey = best.GetInstanceID();
        return candidateKey < bestKey ||
               (candidateKey == bestKey && candidate.LifeGeneration < best.LifeGeneration);
    }

    void CommitTarget(CharacteContext next)
    {
        CharacteContext previous = currentTarget;
        int previousGeneration = currentHandle != null ? currentHandle.OriginalLifeGeneration : 0;
        int nextGeneration = next != null ? next.LifeGeneration : 0;

        if (previous == next && previousGeneration == nextGeneration)
        {
            ResetSwitchChallenger();
            return;
        }

        currentTarget = next;
        currentHandle = SkillTargetHandle.For(next);
        ResetSwitchChallenger();
        TargetChanged?.Invoke(previous, next);
    }

    void ResetSwitchChallenger()
    {
        switchChallenger = null;
        switchChallengerGeneration = 0;
        switchChallengerSince = 0f;
    }

    void ResolveReferences()
    {
        if (owner == null)
            owner = GetComponent<PlayerContext>();
        if (owner == null)
            owner = GetComponentInParent<PlayerContext>();

        owner?.ResolveReferences();
    }
}
