using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InteractionIndicatorView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private Image progressRing;
    [SerializeField] private TMP_Text keyLabel;
    [SerializeField] private TMP_Text actionLabel;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float transitionDuration = 0.12f;
    [SerializeField, Range(0.5f, 1f)] private float hiddenScale = 0.9f;
    [SerializeField, Min(0f)] private float completionPulseDuration = 0.16f;
    [SerializeField, Min(1f)] private float completionPulseScale = 1.12f;

    Coroutine transitionRoutine;
    Coroutine completionRoutine;
    bool requestedVisible;
    bool hideAfterCompletion;

    void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        HideImmediate();
    }

    public void Show(string bindingLabel, string interactionLabel)
    {
        requestedVisible = true;
        hideAfterCompletion = false;

        SetBindingLabel(bindingLabel);
        SetInteractionLabel(interactionLabel);

        if (completionRoutine != null)
        {
            StopCoroutine(completionRoutine);
            completionRoutine = null;
        }

        gameObject.SetActive(true);
        StartTransition(visible: true);
    }

    public void Hide()
    {
        requestedVisible = false;
        if (completionRoutine != null)
        {
            hideAfterCompletion = true;
            return;
        }

        StartTransition(visible: false);
    }

    public void HideImmediate()
    {
        requestedVisible = false;
        hideAfterCompletion = false;

        StopRunningAnimations();
        SetProgress(0f);

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        if (visualRoot != null)
            visualRoot.localScale = Vector3.one * hiddenScale;

        gameObject.SetActive(false);
    }

    public void SetProgress(float normalizedProgress)
    {
        if (progressRing != null)
            progressRing.fillAmount = Mathf.Clamp01(normalizedProgress);
    }

    public void SetBindingLabel(string value)
    {
        if (keyLabel != null)
            keyLabel.text = string.IsNullOrWhiteSpace(value) ? "F" : value;
    }

    public void SetInteractionLabel(string value)
    {
        if (actionLabel != null)
            actionLabel.text = value ?? string.Empty;
    }

    public void PlayCompletion()
    {
        gameObject.SetActive(true);
        hideAfterCompletion = !requestedVisible;

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }
        if (completionRoutine != null)
            StopCoroutine(completionRoutine);

        completionRoutine = StartCoroutine(CompletionRoutine());
    }

    void StartTransition(bool visible)
    {
        if (completionRoutine != null)
            return;

        if (!gameObject.activeInHierarchy)
        {
            if (canvasGroup != null)
                canvasGroup.alpha = visible ? 1f : 0f;
            if (visualRoot != null)
                visualRoot.localScale = Vector3.one * (visible ? 1f : hiddenScale);
            if (!visible)
                gameObject.SetActive(false);
            return;
        }

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(TransitionRoutine(visible));
    }

    IEnumerator TransitionRoutine(bool visible)
    {
        float duration = Mathf.Max(0.001f, transitionDuration);
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : (visible ? 0f : 1f);
        float endAlpha = visible ? 1f : 0f;
        float startScale = visualRoot != null ? visualRoot.localScale.x : 1f;
        float endScale = visible ? 1f : hiddenScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - t) * (1f - t);

            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
            if (visualRoot != null)
                visualRoot.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, eased);

            yield return null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = endAlpha;
        if (visualRoot != null)
            visualRoot.localScale = Vector3.one * endScale;

        transitionRoutine = null;
        if (!visible && !requestedVisible)
            gameObject.SetActive(false);
    }

    IEnumerator CompletionRoutine()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
        SetProgress(1f);

        float duration = Mathf.Max(0.001f, completionPulseDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(t * Mathf.PI);
            if (visualRoot != null)
                visualRoot.localScale = Vector3.one * Mathf.Lerp(1f, completionPulseScale, pulse);
            yield return null;
        }

        if (visualRoot != null)
            visualRoot.localScale = Vector3.one;
        SetProgress(0f);
        completionRoutine = null;

        if (hideAfterCompletion || !requestedVisible)
            StartTransition(visible: false);
    }

    void StopRunningAnimations()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        if (completionRoutine != null)
        {
            StopCoroutine(completionRoutine);
            completionRoutine = null;
        }
    }
}
