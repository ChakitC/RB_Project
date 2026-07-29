using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ThirdPersonOcclusionFader : MonoBehaviour
{
    static readonly int DitheringId = Shader.PropertyToID("_Dithering");

    [SerializeField, Min(0.01f)] private float fadeSpeed = 8f;
    [SerializeField, Min(0.01f)] private float allyProbeRadius = 0.2f;
    [SerializeField, Min(0.1f)] private float allyProbeDistance = 30f;

    readonly Dictionary<CharacteContext, float> fadeAmounts = new();
    readonly Dictionary<CharacteContext, Renderer[]> rendererCache = new();
    readonly Dictionary<Renderer, float> authoredDithering = new();
    readonly HashSet<Renderer> unsupportedRenderers = new();
    readonly HashSet<CharacteContext> desiredFades = new();
    readonly RaycastHit[] probeHits = new RaycastHit[32];
    readonly MaterialPropertyBlock propertyBlock = new();
    readonly ThirdPersonCharacterProfile fallbackProfile =
        ThirdPersonCharacterProfile.CreateDefault();

    Camera gameplayCamera;
    PlayerContext playerContext;
    float nextActorRefresh;
    CharacteContext[] friendlyActors = System.Array.Empty<CharacteContext>();

    void Awake()
    {
        gameplayCamera = GetComponent<Camera>();
        if (gameplayCamera == null)
            gameplayCamera = GetComponentInChildren<Camera>(true);
    }

    void LateUpdate()
    {
        if (gameplayCamera == null)
            gameplayCamera = Camera.main;
        if (gameplayCamera == null)
            return;

        RefreshActors();
        ResolveDesiredFades();
        ApplyFades();
    }

    void OnDestroy()
    {
        foreach (KeyValuePair<CharacteContext, float> pair in fadeAmounts)
            ApplyDithering(pair.Key, 0f);
    }

    void RefreshActors()
    {
        playerContext = PlayerContext.Instance;
        if (Time.unscaledTime < nextActorRefresh)
            return;

        nextActorRefresh = Time.unscaledTime + 0.5f;
        CharacteContext[] allActors = FindObjectsByType<CharacteContext>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        List<CharacteContext> friendlies = new();
        for (int i = 0; i < allActors.Length; i++)
        {
            CharacteContext actor = allActors[i];
            if (actor != null &&
                (actor.TargetIdentity == AITargetIdentity.Player ||
                 actor.TargetIdentity == AITargetIdentity.Companion))
            {
                friendlies.Add(actor);
                rendererCache[actor] =
                    actor.GetComponentsInChildren<Renderer>(true);
            }
        }

        friendlyActors = friendlies.ToArray();
    }

    void ResolveDesiredFades()
    {
        desiredFades.Clear();
        if (playerContext == null)
            return;

        ThirdPersonCharacterProfile profile =
            playerContext.baseStats != null &&
            playerContext.baseStats.thirdPersonProfile != null
                ? playerContext.baseStats.thirdPersonProfile
                : fallbackProfile;
        float cameraDistance = Vector3.Distance(
            gameplayCamera.transform.position,
            playerContext.transform.position + profile.pivotOffset);
        if (cameraDistance < profile.fadeStartDistance)
            desiredFades.Add(playerContext);

        ProbeFriendlies(
            new Ray(gameplayCamera.transform.position, gameplayCamera.transform.forward),
            allyProbeDistance);

        Vector3 pivot = playerContext.transform.TransformPoint(profile.pivotOffset);
        Vector3 pivotToCamera = gameplayCamera.transform.position - pivot;
        if (pivotToCamera.sqrMagnitude > 0.001f)
            ProbeFriendlies(new Ray(pivot, pivotToCamera.normalized), pivotToCamera.magnitude);
    }

    void ProbeFriendlies(Ray ray, float distance)
    {
        int hitCount = Physics.SphereCastNonAlloc(
            ray,
            allyProbeRadius,
            probeHits,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = probeHits[i].collider;
            CharacteContext actor = collider != null
                ? collider.GetComponentInParent<CharacteContext>()
                : null;
            if (actor != null &&
                actor != playerContext &&
                actor.TargetIdentity == AITargetIdentity.Companion)
            {
                desiredFades.Add(actor);
            }
        }
    }

    void ApplyFades()
    {
        for (int i = 0; i < friendlyActors.Length; i++)
        {
            CharacteContext actor = friendlyActors[i];
            if (actor == null)
                continue;

            float current = fadeAmounts.TryGetValue(actor, out float value) ? value : 0f;
            float target = desiredFades.Contains(actor) ? 1f : 0f;

            if (actor == playerContext && target > 0f)
            {
                ThirdPersonCharacterProfile profile =
                    actor.baseStats != null &&
                    actor.baseStats.thirdPersonProfile != null
                        ? actor.baseStats.thirdPersonProfile
                        : fallbackProfile;
                float distance = Vector3.Distance(
                    gameplayCamera.transform.position,
                    actor.transform.TransformPoint(profile.pivotOffset));
                target = Mathf.InverseLerp(
                    profile.fadeStartDistance,
                    profile.fadeFullyHiddenDistance,
                    distance);
            }

            current = Mathf.MoveTowards(
                current,
                target,
                fadeSpeed * Time.unscaledDeltaTime);
            fadeAmounts[actor] = current;
            ApplyDithering(actor, current);
        }
    }

    void ApplyDithering(CharacteContext actor, float value)
    {
        if (actor == null)
            return;

        if (!rendererCache.TryGetValue(actor, out Renderer[] renderers))
        {
            renderers = actor.GetComponentsInChildren<Renderer>(true);
            rendererCache[actor] = renderers;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || unsupportedRenderers.Contains(renderer))
                continue;

            if (!authoredDithering.TryGetValue(
                    renderer,
                    out float authoredValue))
            {
                if (!TryResolveMaterialDithering(
                        renderer,
                        out authoredValue))
                {
                    unsupportedRenderers.Add(renderer);
                    continue;
                }

                authoredDithering[renderer] = authoredValue;
            }

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(
                DitheringId,
                Mathf.Max(authoredValue, value));
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    static bool TryResolveMaterialDithering(
        Renderer renderer,
        out float value)
    {
        value = 0f;
        bool supported = false;
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material != null && material.HasFloat(DitheringId))
            {
                supported = true;
                value = Mathf.Max(value, material.GetFloat(DitheringId));
            }
        }

        return supported;
    }
}
