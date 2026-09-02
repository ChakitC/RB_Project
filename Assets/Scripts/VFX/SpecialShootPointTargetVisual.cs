using UnityEngine;

/// <summary>
/// The spin/pulse animation for a Special Shoot Point marker.
///
/// Facing the camera is left to <see cref="Billboard"/>, which already exists and runs late enough
/// to win against anything that moves the bone underneath. This only animates the marker's own
/// spin and breathing scale, so the two can be mixed and matched.
///
/// Runs on the owning actor's gameplay clock rather than <c>Time.deltaTime</c>: the round itself
/// stretches under world slow, and a marker still spinning at full speed during a slow reads as a
/// separate, unrelated effect.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpecialShootPointTargetVisual : MonoBehaviour
{
    [Header("Spin")]
    [Tooltip("Degrees per second around the marker's facing axis.")]
    [SerializeField] private float spinDegreesPerSecond = 28f;

    [Tooltip("Reverses the spin. Purely cosmetic variety between points.")]
    [SerializeField] private bool spinClockwise = true;

    [Header("Pulse")]
    [Tooltip("Scale multiplier at the top of the breathing cycle.")]
    [Min(1f)][SerializeField] private float pulseScale = 1.12f;

    [Tooltip("Breathing cycles per second.")]
    [Min(0f)][SerializeField] private float pulseHz = 1.15f;

    [Header("Break Burst")]
    [Tooltip("Scale the marker punches out to as it breaks.")]
    [Min(1f)][SerializeField] private float breakBurstScale = 1.4f;

    [Tooltip("Extra spin multiplier at the instant of the break, decaying across the burst.")]
    [Min(0f)][SerializeField] private float breakSpinMultiplier = 7f;

    [Header("Spawn")]
    [Tooltip("Seconds the marker takes to punch up to full size when it appears.")]
    [Min(0f)][SerializeField] private float appearSeconds = 0.18f;

    [Tooltip("Scale the marker starts from when it appears.")]
    [Min(0f)][SerializeField] private float appearFromScale = 0.35f;

    Transform _spinTarget;
    Vector3 _baseScale;
    float _phase;
    float _appearTime;
    float _burstRemaining;
    float _burstDuration;
    CharacteContext _ctx;
    bool _ctxResolved;

    void Awake()
    {
        _spinTarget = transform;
        _baseScale = transform.localScale;
    }

    void OnEnable()
    {
        // Pooled markers come back from the pool, so the punch-in has to restart every time rather
        // than only on first spawn.
        _appearTime = 0f;
        _burstRemaining = 0f;
        _burstDuration = 0f;
        _phase = Random.Range(0f, Mathf.PI * 2f);
        ApplyScale(appearSeconds > 0f ? appearFromScale : 1f);
    }

    void LateUpdate()
    {
        float dt = ResolveDeltaTime();
        if (dt <= 0f)
            return;

        // A break in progress owns the whole animation: the burst reads as the point coming apart,
        // and letting the idle pulse keep breathing underneath it muddies that read.
        // Latched on _burstDuration, not on time remaining: once the burst has finished the marker
        // must HOLD at its expanded size until the pool deactivates it. Falling back to the idle
        // pulse for the one frame in between popped it back to normal size right before it
        // vanished, which read as a flicker.
        bool bursting = _burstDuration > 0f;
        float burstT = 0f;
        if (bursting)
        {
            _burstRemaining = Mathf.Max(0f, _burstRemaining - dt);
            burstT = Mathf.Clamp01(1f - _burstRemaining / _burstDuration);
        }

        // Billboard runs at execution order 10200 and rewrites rotation wholesale, so the spin is
        // applied here as a local roll around the facing axis afterwards.
        if (Mathf.Abs(spinDegreesPerSecond) > 0.001f)
        {
            float dir = spinClockwise ? -1f : 1f;
            // The flourish is strongest at the instant of the break and decays out with it.
            float spinBoost = bursting ? 1f + breakSpinMultiplier * (1f - burstT) : 1f;
            _spinTarget.Rotate(0f, 0f, spinDegreesPerSecond * dir * spinBoost * dt, Space.Self);
        }

        float scale = 1f;

        if (bursting)
        {
            // Ease-out: most of the punch lands in the first frames, which is what makes it read as
            // a burst rather than a stretch.
            scale = Mathf.Lerp(1f, breakBurstScale, 1f - (1f - burstT) * (1f - burstT));
            ApplyScale(scale);
            return;
        }

        if (appearSeconds > 0f && _appearTime < appearSeconds)
        {
            _appearTime += dt;
            float t = Mathf.Clamp01(_appearTime / appearSeconds);
            // Slight overshoot so it snaps into place instead of easing in limply.
            scale = Mathf.LerpUnclamped(appearFromScale, 1f, 1f - Mathf.Pow(1f - t, 3f)) * (1f + 0.12f * Mathf.Sin(t * Mathf.PI));
        }
        else if (pulseHz > 0f)
        {
            _phase += dt * pulseHz * Mathf.PI * 2f;
            scale = Mathf.Lerp(1f, pulseScale, (Mathf.Sin(_phase) + 1f) * 0.5f);
        }

        ApplyScale(scale);
    }

    void ApplyScale(float multiplier)
    {
        transform.localScale = _baseScale * multiplier;
    }

    /// <summary>
    /// The owning character's clock. Falls back to unscaled-by-world-slow delta only when the
    /// marker is not parented under a character at all.
    /// </summary>
    float ResolveDeltaTime()
    {
        if (!_ctxResolved)
        {
            _ctx = GetComponentInParent<CharacteContext>();
            _ctxResolved = true;
        }

        if (_ctx == null || _ctx.UsesWorldSlow)
            return TimeSlowManager.Instance != null ? TimeSlowManager.Instance.WorldDeltaTime : Time.deltaTime;

        return Time.deltaTime;
    }

    /// <summary>Lets the pool re-capture the authored scale if an anchor changed the marker size.</summary>
    public void SetBaseScale(Vector3 scale)
    {
        _baseScale = scale;
    }

    /// <summary>
    /// Plays the break flourish: a quick punch outward with a spin kick, timed to land inside the
    /// same window as the alpha fade so the marker is gone the moment the motion finishes.
    /// </summary>
    public void PlayBreakBurst(float seconds)
    {
        _burstDuration = Mathf.Max(0.01f, seconds);
        _burstRemaining = _burstDuration;
        _appearTime = appearSeconds;   // cancel any punch-in still running
    }
}
