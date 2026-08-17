using UnityEngine;

/// <summary>
/// Presentation for a barrier. Lives on the barrier's <c>Presentation</c> child and knows nothing
/// about gameplay: it only reacts to <see cref="BarrierRuntime.Damaged"/> and
/// <see cref="BarrierRuntime.Ended"/>.
///
/// When the barrier ends, its gameplay collider is disabled and the runtime root is destroyed the
/// same frame. This presenter detaches itself first so the shield can finish its break or fade
/// after the gameplay object is gone, then cleans itself up — no orphan is left behind.
/// </summary>
[DisallowMultipleComponent]
public sealed class BarrierVfxPresenter : MonoBehaviour
{
    [Header("Hit Pulse")]
    [SerializeField, Min(0f)]
    [Tooltip("How long a hit pulse takes to settle back, in world seconds.")]
    private float hitPulseSeconds = 0.18f;

    [SerializeField, Min(1f)]
    [Tooltip("Peak scale multiplier of a hit pulse. Purely visual — gameplay radius never changes.")]
    private float hitPulseScale = 1.08f;

    [Header("Break")]
    [SerializeField, Min(0f)]
    private float breakSeconds = 0.35f;

    [SerializeField, Min(1f)]
    private float breakScale = 1.25f;

    [Header("Fade (expired / anchor lost)")]
    [SerializeField, Min(0f)]
    private float fadeSeconds = 0.5f;

    BarrierRuntime barrier;
    ParticleSystem[] particleSystems;
    Vector3 baseScale = Vector3.one;

    float pulseRemaining;
    float endRemaining;
    float endDuration;
    float endStartScale = 1f;
    float endTargetScale = 1f;
    bool ending;
    bool baseScaleCaptured;

    void Awake()
    {
        Bind(GetComponentInParent<BarrierRuntime>());
    }

    /// <summary>
    /// Attaches this presenter to a barrier. Called automatically from <c>Awake</c> with the
    /// barrier found up the hierarchy; exposed so callers (and tests) can wire it explicitly.
    /// </summary>
    public void Bind(BarrierRuntime runtime)
    {
        Unsubscribe();
        barrier = runtime;
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        Subscribe();
    }

    void Start()
    {
        // Deferred to Start: BarrierRuntime.Initialize resizes this transform to the barrier
        // radius after Awake, so capturing in Awake would pin the prefab's scale instead.
        EnsureBaseScale();
    }

    void EnsureBaseScale()
    {
        if (baseScaleCaptured)
            return;

        baseScale = transform.localScale;
        baseScaleCaptured = true;
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    void Subscribe()
    {
        if (barrier == null)
            return;

        barrier.Damaged += HandleDamaged;
        barrier.Ended += HandleEnded;
    }

    void Unsubscribe()
    {
        if (barrier == null)
            return;

        barrier.Damaged -= HandleDamaged;
        barrier.Ended -= HandleEnded;
    }

    void HandleDamaged(BarrierDamagedEventData data)
    {
        if (ending)
            return;

        EnsureBaseScale();
        pulseRemaining = hitPulseSeconds;
    }

    void HandleEnded(BarrierBrokenEventData data)
    {
        if (ending)
            return;

        EnsureBaseScale();
        ending = true;
        pulseRemaining = 0f;
        Unsubscribe();

        // The runtime root is destroyed as soon as this event returns, so step out of it first or
        // the shield would vanish with no visible ending.
        transform.SetParent(null, worldPositionStays: true);

        if (data.Reason == BarrierEndReason.Broken)
        {
            endDuration = breakSeconds;
            endTargetScale = breakScale;
            StopEmission();
        }
        else
        {
            // Expired or anchor lost: a softer collapse rather than a shatter.
            endDuration = fadeSeconds;
            endTargetScale = 0f;
            StopEmission();
        }

        endStartScale = 1f;
        endRemaining = endDuration;

        if (endRemaining <= 0f)
            Destroy(gameObject);
    }

    void StopEmission()
    {
        if (particleSystems == null)
            return;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem system = particleSystems[i];
            if (system != null)
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    void Update()
    {
        // World time, so barrier feedback slows with the same clock as the barrier itself.
        float worldDeltaTime = TimeSlowManager.Instance.WorldDeltaTime;

        if (ending)
        {
            TickEnding(worldDeltaTime);
            return;
        }

        if (pulseRemaining <= 0f)
            return;

        pulseRemaining -= worldDeltaTime;
        if (pulseRemaining <= 0f)
        {
            pulseRemaining = 0f;
            transform.localScale = baseScale;
            return;
        }

        // Single hump: 0 -> 1 -> 0 across the pulse.
        float progress01 = 1f - pulseRemaining / Mathf.Max(0.0001f, hitPulseSeconds);
        float hump = Mathf.Sin(progress01 * Mathf.PI);
        transform.localScale = baseScale * Mathf.LerpUnclamped(1f, hitPulseScale, hump);
    }

    void TickEnding(float worldDeltaTime)
    {
        endRemaining -= worldDeltaTime;
        if (endRemaining <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        float progress01 = 1f - endRemaining / Mathf.Max(0.0001f, endDuration);
        transform.localScale = baseScale * Mathf.LerpUnclamped(endStartScale, endTargetScale, progress01);
    }
}
