using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The shared Special Shoot Point HUD.
///
/// One countdown for the whole round, not one timer per point: the challenge has a single active
/// window, and per-point timers would read as several independent deadlines. Per-point HP is shown
/// on the points themselves as a ring, never as a number.
///
/// Binds through the controller's static round events so a single HUD instance can serve whichever
/// enemy opens a round, without holding a reference to every enemy in the scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpecialShootPointHudView : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Shown while a round is running, hidden otherwise.")]
    [SerializeField] private GameObject root;

    [Tooltip("Filled image driven by the remaining active time, 1 at the start of the window.")]
    [SerializeField] private Image countdownFill;

    [Tooltip("Optional numeric seconds remaining.")]
    [SerializeField] private TMP_Text countdownLabel;

    [Tooltip("Optional 'points left' readout.")]
    [SerializeField] private TMP_Text pointsRemainingLabel;

    [Header("Warning")]
    [Tooltip("Fill colour for the normal part of the window.")]
    [SerializeField] private Color normalColor = Color.white;

    [Tooltip("Fill colour once the round enters its last-second warning.")]
    [SerializeField] private Color warningColor = new(1f, 0.35f, 0.2f, 1f);

    SpecialShootPointController _bound;
    bool _warningActive;
    int _lastPointsShown = -1;

    void OnEnable()
    {
        SpecialShootPointController.AnyRoundStarted += OnAnyRoundStarted;
        SpecialShootPointController.AnyRoundResolved += OnAnyRoundResolved;
        Hide();
    }

    void OnDisable()
    {
        SpecialShootPointController.AnyRoundStarted -= OnAnyRoundStarted;
        SpecialShootPointController.AnyRoundResolved -= OnAnyRoundResolved;
        Unbind();
    }

    void OnAnyRoundStarted(SpecialShootPointController controller)
    {
        if (controller == null)
            return;

        // The newest round wins. Two enemies opening rounds at once is possible but not a case the
        // locked design asks the HUD to arbitrate.
        Bind(controller);
    }

    void OnAnyRoundResolved(SpecialShootPointController controller, SpecialShootPointOutcome outcome)
    {
        if (controller != _bound)
            return;

        Unbind();
        Hide();
    }

    void Bind(SpecialShootPointController controller)
    {
        Unbind();

        _bound = controller;
        _bound.ActiveTimeChanged += OnActiveTimeChanged;
        _bound.PointsRemainingChanged += OnPointsRemainingChanged;

        _warningActive = false;
        _lastPointsShown = -1;

        Show();
        OnPointsRemainingChanged(controller.PointsRemaining, controller.PointsRemaining);
        OnActiveTimeChanged(controller.ActiveDuration, controller.ActiveDuration);
    }

    void Unbind()
    {
        if (_bound == null)
            return;

        _bound.ActiveTimeChanged -= OnActiveTimeChanged;
        _bound.PointsRemainingChanged -= OnPointsRemainingChanged;
        _bound = null;
    }

    void OnActiveTimeChanged(float remaining, float total)
    {
        if (countdownFill != null)
            countdownFill.fillAmount = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;

        if (countdownLabel != null)
            countdownLabel.text = remaining.ToString("0.0");

        float threshold = _bound != null && _bound.Profile != null
            ? _bound.Profile.lastSecondWarningThreshold
            : 1f;

        bool warning = remaining <= threshold;
        if (warning == _warningActive)
            return;

        // Colour is only written on the transition. A per-frame material write on a HUD element that
        // usually is not changing is exactly the kind of unconditional UI write that costs frames.
        _warningActive = warning;
        if (countdownFill != null)
            countdownFill.color = warning ? warningColor : normalColor;
    }

    void OnPointsRemainingChanged(int remaining, int total)
    {
        if (pointsRemainingLabel == null || remaining == _lastPointsShown)
            return;

        _lastPointsShown = remaining;
        pointsRemainingLabel.text = remaining.ToString();
    }

    void Show()
    {
        if (root != null && !root.activeSelf)
            root.SetActive(true);
    }

    void Hide()
    {
        if (root != null && root.activeSelf)
            root.SetActive(false);
    }
}
