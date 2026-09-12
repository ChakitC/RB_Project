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

    [Header("Availability")]
    [Tooltip("ปิดเพื่อให้ป้ายโชว์ [F] เสมอโดยไม่สนว่ากดได้จริงไหม")]
    [SerializeField] private bool showAvailability = true;
    [SerializeField] private Color readyColor = Color.white;
    [SerializeField] private Color blockedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [Tooltip("ความถี่ในการถามว่ากดได้ไหม ไม่ถามทุกเฟรมเพราะป้ายนี้มีได้หลายอันพร้อมกัน")]
    [SerializeField, Min(0.05f)] private float availabilityPollInterval = 0.2f;

    bool subscribed;
    int lastShownTenths = -1;
    float lastShownFill = -1f;

    PartyCommandController partyCommand;
    PlayerInputHandler playerInput;
    bool ownerLookupDone;
    float nextAvailabilityPollAt;
    bool cachedBlocked;
    PartyCommandBlockReason cachedReason = PartyCommandBlockReason.None;
    float cachedMissingCommandPoints;
    string lastShownSuffix;

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
        InvalidateCachedDisplay();
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
        InvalidateCachedDisplay();
        SetPromptVisible(true);
    }

    /// <summary>Forces the next tick to repaint, so a re-shown prompt never keeps a stale label.</summary>
    void InvalidateCachedDisplay()
    {
        lastShownTenths = -1;
        lastShownFill = -1f;
        lastShownSuffix = null;
        nextAvailabilityPollAt = 0f;
    }

    /// <summary>
    /// Re-asks the player whether the press would land. Polled rather than event-driven because the
    /// answer moves with command point regeneration, and throttled because several enemies can be
    /// showing this prompt at once.
    /// </summary>
    void RefreshAvailability()
    {
        if (!showAvailability)
            return;

        float now = Time.unscaledTime;
        if (now < nextAvailabilityPollAt)
            return;

        nextAvailabilityPollAt = now + availabilityPollInterval;
        ResolveOwnersOnce();

        if (partyCommand == null || playerInput == null)
        {
            cachedBlocked = false;
            cachedReason = PartyCommandBlockReason.None;
            cachedMissingCommandPoints = 0f;
            return;
        }

        cachedBlocked = partyCommand.TryGetChainReadyPromptBlockReason(
            playerInput.ChainAttackDefinition,
            staggerMeter,
            out cachedReason,
            out cachedMissingCommandPoints);
    }

    /// <summary>
    /// The player is resolved once per prompt, on the first tick of a ChainReady window rather than
    /// on Awake: these prompts live on enemy prefabs that can spawn before the party exists.
    /// </summary>
    void ResolveOwnersOnce()
    {
        if (ownerLookupDone && partyCommand != null && playerInput != null)
            return;

        if (partyCommand == null)
            partyCommand = FindFirstObjectByType<PartyCommandController>();
        if (playerInput == null)
            playerInput = FindFirstObjectByType<PlayerInputHandler>();

        ownerLookupDone = partyCommand != null && playerInput != null;
    }

    string BuildAvailabilitySuffix()
    {
        if (!showAvailability || !cachedBlocked)
            return string.Empty;

        switch (cachedReason)
        {
            case PartyCommandBlockReason.NotEnoughCommandPoints:
                return $"  (need {Mathf.Ceil(cachedMissingCommandPoints):0} CP)";
            case PartyCommandBlockReason.Cooldown:
                return "  (cooldown)";
            case PartyCommandBlockReason.SequenceBusy:
                return "  (busy)";
            case PartyCommandBlockReason.OwnerUnavailable:
                return "  (down)";
            default:
                return "  (unavailable)";
        }
    }

    void OnChainReadyEnded()
    {
        SetPromptVisible(false);
    }

    /// <summary>
    /// Fires every frame the meter ticks, once per ChainReady enemy on screen. The label only shows
    /// tenths, so formatting a fresh string (and forcing a TMP mesh rebuild) on every one of those
    /// frames is pure waste — both writes are gated on the value the viewer can actually see
    /// changing.
    /// </summary>
    void OnChainReadyTimeChanged(float remaining, float duration)
    {
        RefreshAvailability();

        if (countdownText != null)
        {
            int tenths = Mathf.Max(0, Mathf.RoundToInt(remaining * 10f));
            string suffix = BuildAvailabilitySuffix();

            if (tenths != lastShownTenths || !string.Equals(suffix, lastShownSuffix))
            {
                lastShownTenths = tenths;
                lastShownSuffix = suffix;
                countdownText.text = $"[F] CHAIN {tenths * 0.1f:0.0}s{suffix}";
                countdownText.color = cachedBlocked ? blockedColor : readyColor;
            }
        }

        if (countdownFill != null && duration > 0f)
        {
            float fill = Mathf.Clamp01(remaining / duration);
            if (!Mathf.Approximately(fill, lastShownFill))
            {
                lastShownFill = fill;
                countdownFill.fillAmount = fill;
            }
        }
    }

    void SetPromptVisible(bool visible)
    {
        if (promptRoot != null && promptRoot.activeSelf != visible)
            promptRoot.SetActive(visible);
    }
}
