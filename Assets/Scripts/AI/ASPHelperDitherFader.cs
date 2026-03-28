using System;
using System.Collections;
using ASP;
using Animancer;
using UnityEngine;

public sealed class ASPHelperDitherFader : MonoBehaviour
{
    const float HiddenDithering = 1f;
    const float VisibleDithering = 0f;
    const float FadeTriggerEpsilon = 0.01f;

    [Header("References")]
    [SerializeField] private ASPCharacterPanel characterPanel;
    [SerializeField] private AnimancerComponent animancer;

    [Header("Fade")]
    [SerializeField] private int animancerLayerIndex;
    [SerializeField] private float fadeInDuration = 0.18f;
    [SerializeField] private float fadeOutDuration = 0.18f;
    [SerializeField] private float fadeOutLeadTime = 0.18f;
    [SerializeField] private float ditheringSize = 6f;
    [SerializeField] private bool useUnscaledTime;
    [SerializeField] private bool warnIfAlphaClipDisabled = true;

    Coroutine fadeRoutine;
    Coroutine monitorRoutine;
    int lifecycleToken;
    float currentDithering = HiddenDithering;
    bool isHidden = true;
    bool alphaClipWarningIssued;

    void Awake()
    {
        InitializeReferences();
        ApplyDitheringSize();
    }
    
    void OnDisable()
    {
        StopFadeRoutine();
        StopMonitorRoutine(true);
    }
    
    public void SetHiddenImmediate()
    {
        InitializeReferences();

        StopFadeRoutine();
        StopMonitorRoutine(true);

        ApplyDitheringSize();
        ApplyDithering(HiddenDithering);
    }

    public void BeginAnimationLifecycle(bool hideOnAnimationComplete)
    {
        InitializeReferences();
        if (characterPanel == null)
            return;

        ApplyDitheringSize();
        WarnIfAlphaClipIsDisabled();

        StopFadeRoutine();
        StopMonitorRoutine(true);

        StartFade(VisibleDithering, fadeInDuration, deactivateAfterFade: false);

        if (!hideOnAnimationComplete || !gameObject.activeInHierarchy)
            return;

        int token = ++lifecycleToken;
        monitorRoutine = StartCoroutine(MonitorAnimationNearEnd(token));
    }

    public void FinalizeAfterAnimation()
    {
        if (!gameObject.activeSelf)
            return;

        StopMonitorRoutine(true);

        if (isHidden)
        {
            gameObject.SetActive(false);
            return;
        }

        StartFade(HiddenDithering, fadeOutDuration, deactivateAfterFade: true);
    }

    public void FadeOutThenDeactivate()
    {
        if (!gameObject.activeSelf)
            return;

        StopMonitorRoutine(true);

        if (isHidden)
        {
            gameObject.SetActive(false);
            return;
        }

        StartFade(HiddenDithering, fadeOutDuration, deactivateAfterFade: true);
    }

    IEnumerator MonitorAnimationNearEnd(int token)
    {
        AnimancerState watchedState = null;

        for (int i = 0; i < 4 && watchedState == null; i++)
        {
            watchedState = TryGetCurrentState();
            if (watchedState != null)
                break;

            yield return null;
        }

        if (watchedState == null)
        {
            monitorRoutine = null;
            yield break;
        }

        while (token == lifecycleToken && gameObject.activeInHierarchy)
        {
            AnimancerState currentState = TryGetCurrentState();
            if (currentState == null || !ReferenceEquals(currentState, watchedState))
                break;

            if (!currentState.IsPlaying)
                break;

            float remainingDuration = currentState.RemainingDuration;
            if (float.IsFinite(remainingDuration) &&
                remainingDuration <= GetFadeOutTriggerTime() + FadeTriggerEpsilon)
            {
                StartFade(HiddenDithering, fadeOutDuration, deactivateAfterFade: false);
                break;
            }

            yield return null;
        }

        if (token == lifecycleToken)
            monitorRoutine = null;
    }

    void StartFade(float targetDithering, float duration, bool deactivateAfterFade)
    {
        StopFadeRoutine();

        if (!gameObject.activeInHierarchy || duration <= 0f)
        {
            ApplyDithering(targetDithering);

            if (deactivateAfterFade && gameObject.activeSelf)
                gameObject.SetActive(false);

            return;
        }

        fadeRoutine = StartCoroutine(FadeRoutine(targetDithering, duration, deactivateAfterFade));
    }

    IEnumerator FadeRoutine(float targetDithering, float duration, bool deactivateAfterFade)
    {
        float startDithering = currentDithering;
        float time = 0f;
        duration = Mathf.Max(0.0001f, duration);

        while (time < duration)
        {
            time += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float progress = Mathf.Clamp01(time / duration);
            float value = Mathf.Lerp(startDithering, targetDithering, progress);
            ApplyDithering(value);
            yield return null;
        }

        ApplyDithering(targetDithering);
        fadeRoutine = null;

        if (deactivateAfterFade && gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    void ApplyDithering(float value)
    {
        if (characterPanel == null)
            return;

        currentDithering = Mathf.Clamp01(value);
        characterPanel.SetDitheringValueToAllMaterials(currentDithering);
        isHidden = currentDithering >= HiddenDithering - FadeTriggerEpsilon;
    }

    void ApplyDitheringSize()
    {
        if (characterPanel == null)
            return;

        characterPanel.SetDitheringSizeValueToAllMaterials(Mathf.Max(1f, ditheringSize));
    }

    float GetFadeOutTriggerTime()
    {
        float fadeDuration = Mathf.Max(0f, fadeOutDuration);
        float leadTime = Mathf.Max(0f, fadeOutLeadTime);
        return Mathf.Max(fadeDuration, leadTime);
    }

    AnimancerState TryGetCurrentState()
    {
        InitializeReferences();
        if (animancer == null)
            return null;

        if (animancerLayerIndex < 0 || animancerLayerIndex >= animancer.Layers.Count)
            return null;

        return animancer.Layers[animancerLayerIndex].CurrentState;
    }

    void InitializeReferences()
    {
        if (characterPanel == null)
            characterPanel = GetComponent<ASPCharacterPanel>() ?? GetComponentInChildren<ASPCharacterPanel>(true);

        if (animancer == null)
            animancer = GetComponent<AnimancerComponent>() ?? GetComponentInChildren<AnimancerComponent>(true);
    }

    void StopFadeRoutine()
    {
        if (fadeRoutine == null)
            return;

        StopCoroutine(fadeRoutine);
        fadeRoutine = null;
    }

    void StopMonitorRoutine(bool invalidateLifecycle)
    {
        if (invalidateLifecycle)
            lifecycleToken++;

        if (monitorRoutine == null)
            return;

        StopCoroutine(monitorRoutine);
        monitorRoutine = null;
    }

    void WarnIfAlphaClipIsDisabled()
    {
        if (!warnIfAlphaClipDisabled || alphaClipWarningIssued)
            return;

        bool foundAspMaterial = false;

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null || material.shader == null)
                    continue;

                string shaderName = material.shader.name;
                if (shaderName != "ASP/Character" && shaderName != "ASP/Eye")
                    continue;

                foundAspMaterial = true;

                if (material.HasFloat("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f)
                    continue;

                alphaClipWarningIssued = true;
                Debug.LogWarning(
                    "[ASPHelperDitherFader] ASP dithering fade needs Alpha Clip enabled on the helper materials.",
                    this);
                return;
            }
        }

        alphaClipWarningIssued = foundAspMaterial;
    }
}
