using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ThirdPersonOcclusionFader : MonoBehaviour
{
    static readonly int DitheringId = Shader.PropertyToID("_Dithering");

    [SerializeField, Min(0.01f)] private float fadeSpeed = 8f;

    readonly Dictionary<CharacteContext, float> fadeAmounts = new();
    readonly Dictionary<CharacteContext, Renderer[]> rendererCache = new();
    readonly Dictionary<Renderer, MaterialPropertyBlock> originalPropertyBlocks = new();
    readonly HashSet<Renderer> unsupportedRenderers = new();
    readonly HashSet<CharacteContext> desiredFades = new();
    readonly List<Material> materialBuffer = new();
    MaterialPropertyBlock propertyBlock;
    readonly ThirdPersonCharacterProfile fallbackProfile =
        ThirdPersonCharacterProfile.CreateDefault();

    Camera gameplayCamera;
    PlayerContext playerContext;
    float nextActorRefresh;
    CharacteContext[] fadeActors = System.Array.Empty<CharacteContext>();

    void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
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
            fadeActors = System.Array.Empty<CharacteContext>();
            return;
        }

        fadeActors = new CharacteContext[] { playerContext };
        rendererCache[playerContext] =
            playerContext.GetComponentsInChildren<Renderer>(true);
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
    }

    void ApplyFades()
    {
        for (int i = 0; i < fadeActors.Length; i++)
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
