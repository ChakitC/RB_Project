using UnityEngine;
using TMPro;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ChainReadyPromptView : MonoBehaviour
{
    [SerializeField] private StaggerMeter staggerMeter;
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private Image countdownFill;

    bool subscribed;

    void OnEnable()
    {
        if (staggerMeter == null)
            staggerMeter = GetComponentInParent<StaggerMeter>();

        Subscribe();
        Refresh();
    }

    void OnDisable()
    {
        Unsubscribe();
        SetPromptVisible(false);
    }

    public void Bind(StaggerMeter meter)
    {
        if (staggerMeter == meter)
        {
            Refresh();
            return;
        }

        Unsubscribe();
        staggerMeter = meter;

        if (isActiveAndEnabled)
            Subscribe();

        Refresh();
    }

    void Subscribe()
    {
        if (subscribed || staggerMeter == null)
            return;

        staggerMeter.ChainReadyStarted += OnChainReadyStarted;
        staggerMeter.ChainReadyEnded += OnChainReadyEnded;
        staggerMeter.ChainReadyTimeChanged += OnChainReadyTimeChanged;
        subscribed = true;
    }

    void Unsubscribe()
    {
        if (!subscribed || staggerMeter == null)
            return;

        staggerMeter.ChainReadyStarted -= OnChainReadyStarted;
        staggerMeter.ChainReadyEnded -= OnChainReadyEnded;
        staggerMeter.ChainReadyTimeChanged -= OnChainReadyTimeChanged;
        subscribed = false;
    }

    void Refresh()
    {
        bool show = staggerMeter != null && staggerMeter.IsChainReady;
        SetPromptVisible(show);

        if (show)
        {
            OnChainReadyTimeChanged(
                staggerMeter.ChainReadyTimeRemaining,
                staggerMeter.ChainReadyDuration);
        }
    }

    void OnChainReadyStarted()
    {
        SetPromptVisible(true);
    }

    void OnChainReadyEnded()
    {
        SetPromptVisible(false);
    }

    void OnChainReadyTimeChanged(float remaining, float duration)
    {
        if (countdownText != null)
            countdownText.text = $"[F] CHAIN {remaining:0.0}s";

        if (countdownFill != null && duration > 0f)
            countdownFill.fillAmount = remaining / duration;
    }

    void SetPromptVisible(bool visible)
    {
        if (promptRoot != null && promptRoot.activeSelf != visible)
            promptRoot.SetActive(visible);
    }
}
