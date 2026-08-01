using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public sealed class CameraOcclusionCutoutFader : MonoBehaviour, IPartySpawnedReceiver
{
    static readonly int FadeId = Shader.PropertyToID("_Fade");
    static readonly int CutoutCenterId = Shader.PropertyToID("_CutoutCenter");
    static readonly int CutoutRadiusId = Shader.PropertyToID("_CutoutRadius");
    static readonly int CutoutSoftnessId = Shader.PropertyToID("_CutoutSoftness");
    static readonly int CutoutStrengthId = Shader.PropertyToID("_CutoutStrength");
    static readonly int CutoutVerticalInfluenceId = Shader.PropertyToID("_CutoutVerticalInfluence");
    static readonly int OcclusionInteriorBlackStrengthId = Shader.PropertyToID("_OcclusionInteriorBlackStrength");

    [Header("References")]
    [SerializeField] private Camera sourceCamera;
    [SerializeField] private Transform target;
    [SerializeField] private bool previewInEditMode = true;

    [Header("Occlusion")]
    [SerializeField] private LayerMask occluderLayers = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private float sphereRadius = 0.25f;
    [SerializeField] private int maxHits = 32;
    [SerializeField] private bool fadeOnlyNearestOccluders = true;
    [SerializeField] private float nearestDepthTolerance = 0.75f;
    [SerializeField] private bool fadeContinuousOccluderChain = true;
    [SerializeField] private float occluderChainGap = 1.5f;
    [SerializeField] private bool fadeWholeOccluderGroup = true;
    [SerializeField] private Transform occluderGroupContainer;
    [SerializeField] private string occluderGroupContainerName = "Terrain";
    [SerializeField] private bool useRendererBoundsFallback = true;
    [SerializeField] private float rendererBoundsPadding = 0.25f;
    [SerializeField] private float rendererCacheRefreshInterval = 1f;

    [Header("Cutout")]
    [SerializeField] private float fadedValue;
    [SerializeField] private float cutoutRadius = 4f;
    [SerializeField] private float cutoutSoftness = 1.5f;
    [SerializeField, Range(0f, 1f)] private float cutoutVerticalInfluence = 1f;
    [SerializeField, Range(0f, 1f)] private float occlusionInteriorBlackStrength = 0.2f;
    [SerializeField] private float fadeSpeed = 8f;
    [SerializeField] private Vector3 cutoutCenterOffset;

    [Header("Debug")]
    [SerializeField] private bool drawOccluderDebug;
    [SerializeField] private bool drawOccluderDebugOnlyWhenSelected = true;
    [SerializeField] private Color debugCastColor = new Color(1f, 0.82f, 0.1f, 0.8f);
    [SerializeField] private Color debugCandidateColor = new Color(0.15f, 0.65f, 1f, 0.9f);
    [SerializeField] private Color debugActiveCandidateColor = new Color(0.25f, 1f, 0.35f, 0.95f);

    readonly Dictionary<Renderer, OccluderState> states = new Dictionary<Renderer, OccluderState>();
    readonly List<OccluderCandidate> candidates = new List<OccluderCandidate>();
    readonly List<Renderer> groupRenderers = new List<Renderer>();
    readonly List<Renderer> removals = new List<Renderer>();

    RaycastHit[] hits;
    MaterialPropertyBlock propertyBlock;
    Renderer[] rendererFallbackCandidates;
    float nextRendererCacheRefreshTime;

    void Awake()
    {
        Initialize();
    }

    void OnEnable()
    {
        Initialize();
    }

    public void PrepareParty(PartyRuntime party)
    {
        target = party?.Player != null ? party.Player.transform : null;
    }

    public void PartySpawned(PartyRuntime party)
    {
        target = party?.Player != null ? party.Player.transform : null;
    }

    void OnValidate()
    {
        sphereRadius = Mathf.Max(0f, sphereRadius);
        maxHits = Mathf.Max(1, maxHits);
        nearestDepthTolerance = Mathf.Max(0f, nearestDepthTolerance);
        occluderChainGap = Mathf.Max(0f, occluderChainGap);
        rendererBoundsPadding = Mathf.Max(0f, rendererBoundsPadding);
        rendererCacheRefreshInterval = Mathf.Max(0.1f, rendererCacheRefreshInterval);
        cutoutRadius = Mathf.Max(0f, cutoutRadius);
        cutoutSoftness = Mathf.Max(0.01f, cutoutSoftness);
        cutoutVerticalInfluence = Mathf.Clamp01(cutoutVerticalInfluence);
        occlusionInteriorBlackStrength = Mathf.Clamp01(occlusionInteriorBlackStrength);
        fadeSpeed = Mathf.Max(0f, fadeSpeed);
    }

    void LateUpdate()
    {
        if (!Application.IsPlaying(gameObject) && !previewInEditMode)
        {
            FadeOutTrackedRenderers();
            return;
        }

        if (sourceCamera == null || target == null)
        {
            FadeOutTrackedRenderers();
            return;
        }

        MarkRenderersUntargeted();
        FindBlockingRenderers();
        ApplyRendererStates();
    }

    void OnDisable()
    {
        foreach (KeyValuePair<Renderer, OccluderState> pair in states)
            ApplyCutout(pair.Key, 0f, pair.Value.CurrentCenter);

        states.Clear();
    }

    void OnDrawGizmos()
    {
        if (!drawOccluderDebugOnlyWhenSelected)
            DrawOccluderDebugGizmos();
    }

    void OnDrawGizmosSelected()
    {
        if (drawOccluderDebugOnlyWhenSelected)
            DrawOccluderDebugGizmos();
    }

    void MarkRenderersUntargeted()
    {
        foreach (OccluderState state in states.Values)
            state.TargetStrength = 0f;
    }

    void Initialize()
    {
        if (sourceCamera == null)
            sourceCamera = GetComponent<Camera>();

        int desiredHitCount = Mathf.Max(1, maxHits);
        if (hits == null || hits.Length != desiredHitCount)
            hits = new RaycastHit[desiredHitCount];

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();
    }

    void FindBlockingRenderers()
    {
        Vector3 origin = sourceCamera.transform.position;
        Vector3 targetPosition = target.position;
        Vector3 delta = targetPosition - origin;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
            return;

        Vector3 direction = delta / distance;
        candidates.Clear();

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            Mathf.Max(0.001f, sphereRadius),
            direction,
            hits,
            distance,
            occluderLayers,
            triggerInteraction);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null)
                continue;

            Renderer renderer = ResolveRenderer(hitCollider);
            if (renderer == null || !UsesCutoutShader(renderer))
                continue;

            AddCandidate(renderer, hits[i].point, hits[i].distance);
        }

        if (useRendererBoundsFallback)
            FindBlockingRenderersByBounds(origin, direction, distance);

        MarkCandidateOccluders();
    }

    void FindBlockingRenderersByBounds(Vector3 origin, Vector3 direction, float distance)
    {
        RefreshRendererFallbackCandidatesIfNeeded();

        if (rendererFallbackCandidates == null)
            return;

        Ray ray = new Ray(origin, direction);
        for (int i = 0; i < rendererFallbackCandidates.Length; i++)
        {
            Renderer renderer = rendererFallbackCandidates[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (!IsLayerIncluded(renderer.gameObject.layer) || !UsesCutoutShader(renderer))
                continue;

            Bounds expandedBounds = renderer.bounds;
            expandedBounds.Expand(Mathf.Max(0f, sphereRadius + rendererBoundsPadding) * 2f);
            if (!expandedBounds.IntersectRay(ray, out float hitDistance) || hitDistance > distance)
                continue;

            AddCandidate(renderer, ray.GetPoint(hitDistance), hitDistance);
        }
    }

    void AddCandidate(Renderer renderer, Vector3 cutoutCenter, float distance)
    {
        Transform groupRoot = ResolveOccluderGroupRoot(renderer);
        for (int i = 0; i < candidates.Count; i++)
        {
            OccluderCandidate candidate = candidates[i];
            if (candidate.GroupRoot != groupRoot)
                continue;

            if (distance < candidate.Distance)
            {
                candidate.Distance = distance;
                candidate.CutoutCenter = cutoutCenter;
            }

            return;
        }

        candidates.Add(new OccluderCandidate
        {
            Renderer = renderer,
            GroupRoot = groupRoot,
            CutoutCenter = cutoutCenter,
            Distance = distance
        });
    }

    void MarkCandidateOccluders()
    {
        if (candidates.Count == 0)
            return;

        if (!fadeOnlyNearestOccluders)
        {
            for (int i = 0; i < candidates.Count; i++)
                MarkOccluder(candidates[i]);

            return;
        }

        if (fadeContinuousOccluderChain)
        {
            MarkContinuousOccluderChain();
            return;
        }

        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < candidates.Count; i++)
            nearestDistance = Mathf.Min(nearestDistance, candidates[i].Distance);

        float maxAllowedDistance = nearestDistance + Mathf.Max(0f, nearestDepthTolerance);
        for (int i = 0; i < candidates.Count; i++)
        {
            OccluderCandidate candidate = candidates[i];
            if (candidate.Distance <= maxAllowedDistance)
                MarkOccluder(candidate);
        }
    }

    void MarkContinuousOccluderChain()
    {
        SortCandidatesByDistance();

        float maxGap = Mathf.Max(0f, occluderChainGap);
        bool hasPreviousDistance = false;
        float previousDistance = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            OccluderCandidate candidate = candidates[i];
            if (candidate == null)
                continue;

            if (hasPreviousDistance && candidate.Distance - previousDistance > maxGap)
                break;

            MarkOccluder(candidate);
            previousDistance = candidate.Distance;
            hasPreviousDistance = true;
        }
    }

    void MarkOccluder(OccluderCandidate candidate)
    {
        if (candidate == null)
            return;

        if (fadeWholeOccluderGroup && candidate.GroupRoot != null)
        {
            MarkOccluderGroup(candidate.GroupRoot, candidate.CutoutCenter);
            return;
        }

        MarkOccluder(candidate.Renderer, candidate.CutoutCenter);
    }

    void MarkOccluderGroup(Transform groupRoot, Vector3 cutoutCenter)
    {
        if (groupRoot == null)
            return;

        groupRenderers.Clear();
        groupRoot.GetComponentsInChildren(false, groupRenderers);

        for (int i = 0; i < groupRenderers.Count; i++)
        {
            Renderer renderer = groupRenderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (UsesCutoutShader(renderer))
                MarkOccluder(renderer, cutoutCenter);
        }
    }

    void MarkOccluder(Renderer renderer, Vector3 cutoutCenter)
    {
        if (!states.TryGetValue(renderer, out OccluderState state))
        {
            state = new OccluderState
            {
                CurrentCenter = cutoutCenter,
                TargetCenter = cutoutCenter
            };
            states.Add(renderer, state);
        }

        state.TargetStrength = 1f;
        state.TargetCenter = cutoutCenter;
    }

    void RefreshRendererFallbackCandidatesIfNeeded()
    {
        float refreshInterval = Mathf.Max(0.1f, rendererCacheRefreshInterval);
        if (rendererFallbackCandidates != null && Time.realtimeSinceStartup < nextRendererCacheRefreshTime)
            return;

        rendererFallbackCandidates = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        nextRendererCacheRefreshTime = Time.realtimeSinceStartup + refreshInterval;
    }

    bool IsLayerIncluded(int layer)
    {
        return (occluderLayers.value & (1 << layer)) != 0;
    }

    Renderer ResolveRenderer(Collider hitCollider)
    {
        Renderer renderer = hitCollider.GetComponent<Renderer>();
        if (renderer != null)
            return renderer;

        return hitCollider.GetComponentInParent<Renderer>();
    }

    Transform ResolveOccluderGroupRoot(Renderer renderer)
    {
        if (!fadeWholeOccluderGroup || renderer == null)
            return renderer != null ? renderer.transform : null;

        Transform rendererTransform = renderer.transform;
        if (occluderGroupContainer != null)
        {
            Transform directChild = ResolveDirectChildUnderContainer(rendererTransform, occluderGroupContainer);
            if (directChild != null)
                return directChild;
        }

        if (!string.IsNullOrWhiteSpace(occluderGroupContainerName))
        {
            Transform current = rendererTransform;
            while (current != null && current.parent != null)
            {
                if (current.parent.name == occluderGroupContainerName)
                    return current;

                current = current.parent;
            }
        }

        return rendererTransform;
    }

    static Transform ResolveDirectChildUnderContainer(Transform child, Transform container)
    {
        if (child == null || container == null || child == container)
            return null;

        Transform current = child;
        while (current != null && current.parent != null)
        {
            if (current.parent == container)
                return current;

            current = current.parent;
        }

        return null;
    }

    bool UsesCutoutShader(Renderer renderer)
    {
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material != null && material.HasProperty(CutoutCenterId))
                return true;
        }

        return false;
    }

    void ApplyRendererStates()
    {
        removals.Clear();

        float deltaTime = Application.IsPlaying(gameObject) ? Time.deltaTime : 1f;
        float step = Mathf.Max(0f, fadeSpeed) * deltaTime;
        foreach (KeyValuePair<Renderer, OccluderState> pair in states)
        {
            Renderer renderer = pair.Key;
            OccluderState state = pair.Value;
            if (renderer == null)
            {
                removals.Add(renderer);
                continue;
            }

            state.CurrentStrength = Mathf.MoveTowards(state.CurrentStrength, state.TargetStrength, step);
            state.CurrentCenter = Vector3.Lerp(state.CurrentCenter, state.TargetCenter, Mathf.Clamp01(step));
            ApplyCutout(renderer, state.CurrentStrength, state.CurrentCenter);

            if (state.CurrentStrength <= 0f && state.TargetStrength <= 0f)
                removals.Add(renderer);
        }

        for (int i = 0; i < removals.Count; i++)
            states.Remove(removals[i]);
    }

    void FadeOutTrackedRenderers()
    {
        MarkRenderersUntargeted();
        ApplyRendererStates();
    }

    void ApplyCutout(Renderer renderer, float strength, Vector3 center)
    {
        if (renderer == null)
            return;

        float clampedStrength = Mathf.Clamp01(strength);
        float effectiveFade = Mathf.Lerp(1f, Mathf.Clamp01(fadedValue), clampedStrength);
        center += cutoutCenterOffset;

        renderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(FadeId, effectiveFade);
        propertyBlock.SetVector(CutoutCenterId, center);
        propertyBlock.SetFloat(CutoutRadiusId, Mathf.Max(0f, cutoutRadius));
        propertyBlock.SetFloat(CutoutSoftnessId, Mathf.Max(0.01f, cutoutSoftness));
        propertyBlock.SetFloat(CutoutStrengthId, clampedStrength);
        propertyBlock.SetFloat(CutoutVerticalInfluenceId, Mathf.Clamp01(cutoutVerticalInfluence));
        propertyBlock.SetFloat(OcclusionInteriorBlackStrengthId, Mathf.Clamp01(occlusionInteriorBlackStrength));
        renderer.SetPropertyBlock(propertyBlock);
    }

    void DrawOccluderDebugGizmos()
    {
        if (!drawOccluderDebug)
            return;

        Camera debugCamera = sourceCamera != null ? sourceCamera : GetComponent<Camera>();
        if (debugCamera == null || target == null)
            return;

        Vector3 origin = debugCamera.transform.position;
        Vector3 targetPosition = target.position;
        Vector3 delta = targetPosition - origin;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
            return;

        float radius = Mathf.Max(0.001f, sphereRadius);
        Gizmos.color = debugCastColor;
        Gizmos.DrawLine(origin, targetPosition);

        const int castSphereSamples = 8;
        for (int i = 0; i <= castSphereSamples; i++)
        {
            float t = i / (float)castSphereSamples;
            Gizmos.DrawWireSphere(Vector3.Lerp(origin, targetPosition, t), radius);
        }

        float nearestDistance = ResolveNearestCandidateDistance();
        float maxActiveDistance = nearestDistance + Mathf.Max(0f, nearestDepthTolerance);
        if (fadeOnlyNearestOccluders && fadeContinuousOccluderChain)
            SortCandidatesByDistance();

        float markerRadius = Mathf.Max(0.15f, radius * 0.5f);
        for (int i = 0; i < candidates.Count; i++)
        {
            OccluderCandidate candidate = candidates[i];
            if (candidate == null)
                continue;

            bool isActive = IsCandidateActiveForDebug(candidate, nearestDistance, maxActiveDistance);

            Gizmos.color = isActive ? debugActiveCandidateColor : debugCandidateColor;
            Gizmos.DrawWireSphere(candidate.CutoutCenter, markerRadius);

            if (candidate.GroupRoot != null)
                Gizmos.DrawLine(candidate.CutoutCenter, candidate.GroupRoot.position);
        }
    }

    float ResolveNearestCandidateDistance()
    {
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < candidates.Count; i++)
        {
            OccluderCandidate candidate = candidates[i];
            if (candidate != null)
                nearestDistance = Mathf.Min(nearestDistance, candidate.Distance);
        }

        return nearestDistance;
    }

    bool IsCandidateActiveForDebug(
        OccluderCandidate candidate,
        float nearestDistance,
        float maxNearestDistance)
    {
        if (candidate == null)
            return false;

        if (!fadeOnlyNearestOccluders)
            return true;

        if (!fadeContinuousOccluderChain)
            return !float.IsPositiveInfinity(nearestDistance) && candidate.Distance <= maxNearestDistance;

        float maxGap = Mathf.Max(0f, occluderChainGap);
        bool hasPreviousDistance = false;
        float previousDistance = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            OccluderCandidate current = candidates[i];
            if (current == null)
                continue;

            if (hasPreviousDistance && current.Distance - previousDistance > maxGap)
                return false;

            if (current == candidate)
                return true;

            previousDistance = current.Distance;
            hasPreviousDistance = true;
        }

        return false;
    }

    void SortCandidatesByDistance()
    {
        candidates.Sort(CompareCandidatesByDistance);
    }

    static int CompareCandidatesByDistance(OccluderCandidate left, OccluderCandidate right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;

        return left.Distance.CompareTo(right.Distance);
    }

    sealed class OccluderState
    {
        public float CurrentStrength;
        public float TargetStrength;
        public Vector3 CurrentCenter;
        public Vector3 TargetCenter;
    }

    sealed class OccluderCandidate
    {
        public Renderer Renderer;
        public Transform GroupRoot;
        public Vector3 CutoutCenter;
        public float Distance;
    }
}
