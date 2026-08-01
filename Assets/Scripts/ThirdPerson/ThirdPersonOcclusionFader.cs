using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(10200)]
[DisallowMultipleComponent]
public sealed class ThirdPersonOcclusionFader : MonoBehaviour
{
    const string AllyLayerName = "Ally";
    const int MaxCompanionOcclusionHits = 32;

    static readonly int DitheringId = Shader.PropertyToID("_Dithering");

    [SerializeField, Min(0.01f)] private float fadeSpeed = 8f;

    readonly Dictionary<CharacteContext, float> fadeAmounts = new();
    readonly Dictionary<CharacteContext, Renderer[]> rendererCache = new();
    readonly Dictionary<Renderer, MaterialPropertyBlock> originalPropertyBlocks = new();
    readonly HashSet<Renderer> unsupportedRenderers = new();
    readonly HashSet<CharacteContext> desiredFades = new();
    readonly List<CharacteContext> fadeActors = new();
    readonly List<Material> materialBuffer = new();
    readonly Collider[] companionOcclusionHits =
        new Collider[MaxCompanionOcclusionHits];
    MaterialPropertyBlock propertyBlock;
    readonly ThirdPersonCharacterProfile fallbackProfile =
        ThirdPersonCharacterProfile.CreateDefault();

    Camera gameplayCamera;
    GameplayCameraController cameraController;
    PlayerContext playerContext;
    int companionLayerMask;
    float nextActorRefresh;

    void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        int allyLayer = LayerMask.NameToLayer(AllyLayerName);
        companionLayerMask = allyLayer >= 0 ? 1 << allyLayer : 0;
        gameplayCamera = GetComponent<Camera>();
        if (gameplayCamera == null)
            gameplayCamera = GetComponentInChildren<Camera>(true);
    }

    void LateUpdate()
    {
        cameraController = GameplayCameraController.Instance;
        if (cameraController == null || !cameraController.isActiveAndEnabled)
        {
            ReleaseAllFades();
            nextActorRefresh = 0f;
            return;
        }

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
        ReleaseAllFades();
    }

    void OnDisable()
    {
        ReleaseAllFades();
    }

    void RefreshActors()
    {
        playerContext = PlayerContext.Instance;
        if (Time.unscaledTime < nextActorRefresh)
            return;

        nextActorRefresh = Time.unscaledTime + 0.5f;
        if (playerContext == null)
        {
            fadeActors.Clear();
            return;
        }

        fadeActors.Clear();
        AddFadeActor(playerContext);

        CharacteContext[] contexts = FindObjectsByType<CharacteContext>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < contexts.Length; i++)
        {
            CharacteContext context = contexts[i];
            if (context == null ||
                context == playerContext ||
                context.TargetIdentity != AITargetIdentity.Companion)
            {
                continue;
            }

            AddFadeActor(context);
        }
    }

    void AddFadeActor(CharacteContext actor)
    {
        fadeActors.Add(actor);
        rendererCache[actor] =
            actor.GetComponentsInChildren<Renderer>(true);
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

        ResolveCompanionOcclusionFades(profile);
    }

    void ResolveCompanionOcclusionFades(
        ThirdPersonCharacterProfile profile)
    {
        if (companionLayerMask == 0)
            return;

        float fadeRadius = cameraController.CompanionFadeRadius;
        if (fadeRadius <= 0f)
            return;

        Vector3 cameraPosition = gameplayCamera.transform.position;
        Vector3 playerPivot =
            playerContext.transform.TransformPoint(profile.pivotOffset);
        int hitCount = Physics.OverlapCapsuleNonAlloc(
            cameraPosition,
            playerPivot,
            fadeRadius,
            companionOcclusionHits,
            companionLayerMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = companionOcclusionHits[i];
            if (hit == null)
                continue;

            CharacteContext actor =
                hit.GetComponentInParent<CharacteContext>();
            if (actor != null &&
                actor.TargetIdentity == AITargetIdentity.Companion)
            {
                if (!fadeActors.Contains(actor))
                    AddFadeActor(actor);

                desiredFades.Add(actor);
            }
        }

        if (playerContext.WeaponSystem != null &&
            playerContext.WeaponSystem.IsAiming)
        {
            ResolveCompanionVisualFades(
                cameraPosition,
                playerPivot,
                fadeRadius);
        }
    }

    void ResolveCompanionVisualFades(
        Vector3 cameraPosition,
        Vector3 playerPivot,
        float fadeRadius)
    {
        Vector3 cameraToPivot = playerPivot - cameraPosition;
        float corridorLength = cameraToPivot.magnitude;
        if (corridorLength <= 0.001f)
            return;

        Ray corridorRay = new(
            cameraPosition,
            cameraToPivot / corridorLength);
        float fadeRadiusSquared = fadeRadius * fadeRadius;

        for (int i = 0; i < fadeActors.Count; i++)
        {
            CharacteContext actor = fadeActors[i];
            if (actor == null ||
                actor == playerContext ||
                actor.TargetIdentity != AITargetIdentity.Companion ||
                desiredFades.Contains(actor))
            {
                continue;
            }

            if (!rendererCache.TryGetValue(
                    actor,
                    out Renderer[] renderers))
            {
                renderers =
                    actor.GetComponentsInChildren<Renderer>(true);
                rendererCache[actor] = renderers;
            }

            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null ||
                    !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy ||
                    (renderer is not MeshRenderer &&
                     renderer is not SkinnedMeshRenderer))
                {
                    continue;
                }

                Bounds visualBounds = renderer.bounds;
                if (visualBounds.SqrDistance(cameraPosition) <=
                    fadeRadiusSquared)
                {
                    desiredFades.Add(actor);
                    break;
                }

                visualBounds.Expand(fadeRadius * 2f);
                if (visualBounds.IntersectRay(
                        corridorRay,
                        out float hitDistance) &&
                    hitDistance <= corridorLength)
                {
                    desiredFades.Add(actor);
                    break;
                }
            }
        }
    }

    void ApplyFades()
    {
        for (int i = 0; i < fadeActors.Count; i++)
        {
            CharacteContext actor = fadeActors[i];
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

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        if (!rendererCache.TryGetValue(actor, out Renderer[] renderers))
        {
            renderers = actor.GetComponentsInChildren<Renderer>(true);
            rendererCache[actor] = renderers;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (value <= 0f)
            {
                RestorePropertyBlock(renderer);
                continue;
            }

            if (unsupportedRenderers.Contains(renderer))
                continue;

            if (!TryResolveMaterialDithering(
                    renderer,
                    out float materialValue))
            {
                unsupportedRenderers.Add(renderer);
                continue;
            }

            if (!originalPropertyBlocks.TryGetValue(
                    renderer,
                    out MaterialPropertyBlock originalPropertyBlock))
            {
                originalPropertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(originalPropertyBlock);
                originalPropertyBlocks[renderer] = originalPropertyBlock;
            }

            if (originalPropertyBlock.HasFloat(DitheringId))
            {
                materialValue = Mathf.Max(
                    materialValue,
                    originalPropertyBlock.GetFloat(DitheringId));
            }

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(
                DitheringId,
                Mathf.Max(materialValue, value));
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    void ReleaseAllFades()
    {
        foreach (KeyValuePair<Renderer, MaterialPropertyBlock> pair in originalPropertyBlocks)
        {
            if (pair.Key != null)
                pair.Key.SetPropertyBlock(pair.Value);
        }

        originalPropertyBlocks.Clear();
        fadeAmounts.Clear();
        desiredFades.Clear();
    }

    void RestorePropertyBlock(Renderer renderer)
    {
        if (!originalPropertyBlocks.TryGetValue(
                renderer,
                out MaterialPropertyBlock originalPropertyBlock))
        {
            return;
        }

        renderer.SetPropertyBlock(originalPropertyBlock);
        originalPropertyBlocks.Remove(renderer);
    }

    bool TryResolveMaterialDithering(
        Renderer renderer,
        out float value)
    {
        value = 0f;
        bool supported = false;
        materialBuffer.Clear();
        renderer.GetSharedMaterials(materialBuffer);
        for (int i = 0; i < materialBuffer.Count; i++)
        {
            Material material = materialBuffer[i];
            if (material != null && material.HasFloat(DitheringId))
            {
                supported = true;
                value = Mathf.Max(value, material.GetFloat(DitheringId));
            }
        }

        return supported;
    }
}
