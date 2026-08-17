using System;
using UnityEngine;

/// <summary>
/// A spherical shell that swallows hostile projectiles until its HP or its lifetime runs out.
///
/// The barrier is not a character: it has no HealthSystem, no hit zones, and no HP bar. Feedback
/// is presentation-only (hit pulse, dissolve, break VFX) driven off <see cref="Damaged"/> and
/// <see cref="Ended"/>. Once broken it stays gone — recovering it needs a fresh cast.
/// </summary>
[DisallowMultipleComponent]
public sealed class BarrierRuntime : MonoBehaviour, IProjectileBarrier
{
    [Header("Setup")]
    [SerializeField, Tooltip("Trigger collider on the Barrier layer. Radius is driven at runtime.")]
    private SphereCollider barrierCollider;

    [SerializeField, Tooltip("Scaled to match the barrier radius. Purely visual.")]
    private Transform presentationRoot;

    [Header("Runtime (read-only)")]
    [SerializeField] private CharacteContext owner;
    [SerializeField] private BarrierAnchorMode anchorMode;
    [SerializeField] private float radius;
    [SerializeField] private float currentHealth;
    [SerializeField] private float maxHealth;
    [SerializeField] private float remainingLifetime;

    Transform anchor;
    SummonedEntityRuntime anchorSummon;
    bool active;
    bool initialized;

    public event Action<BarrierDamagedEventData> Damaged;
    public event Action<BarrierBrokenEventData> Ended;

    public CharacteContext Owner => owner;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float RemainingLifetime => remainingLifetime;
    public float Health01 => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;

    public bool IsBarrierActive => active;
    public float BarrierRadius => radius;
    public Vector3 BarrierCenter => transform.position;

    void Awake()
    {
        if (barrierCollider == null)
            barrierCollider = GetComponentInChildren<SphereCollider>(true);
    }

    public bool Initialize(BarrierSpawnRequest request)
    {
        if (request == null || request.Owner == null)
            return false;

        if (!(request.Radius > 0f) || !(request.Lifetime > 0f) || !(request.MaxHealth > 0f))
            return false;

        owner = request.Owner;
        anchorMode = request.AnchorMode;
        anchor = request.Anchor;
        anchorSummon = request.AnchorSummon;
        radius = request.Radius;
        maxHealth = request.MaxHealth;
        currentHealth = maxHealth;
        remainingLifetime = request.Lifetime;
        active = true;
        initialized = true;

        transform.position = anchor != null ? anchor.position : request.FallbackPosition;
        ApplyRadius();

        if (barrierCollider != null)
        {
            barrierCollider.isTrigger = true;
            barrierCollider.enabled = true;
        }

        BarrierRegistry.Register(this);
        return true;
    }

    void ApplyRadius()
    {
        if (barrierCollider != null)
        {
            // The collider may sit on a scaled child, so undo that scale to land on world radius.
            float colliderScale = MaxAbsAxis(barrierCollider.transform.lossyScale);
            barrierCollider.center = Vector3.zero;
            barrierCollider.radius = colliderScale > 0.0001f ? radius / colliderScale : radius;
        }

        if (presentationRoot != null)
        {
            float parentScale = presentationRoot.parent != null
                ? MaxAbsAxis(presentationRoot.parent.lossyScale)
                : 1f;
            float localDiameter = parentScale > 0.0001f ? radius * 2f / parentScale : radius * 2f;
            presentationRoot.localScale = Vector3.one * localDiameter;
        }
    }

    static float MaxAbsAxis(Vector3 scale)
    {
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }

    void Update()
    {
        // Lifetime shares the world clock with the summon it protects, so both slow together
        // under world-slow and both freeze on pause.
        TickLifetime(TimeSlowManager.Instance.WorldDeltaTime);
    }

    /// <summary>
    /// Advances anchor tracking and lifetime by one step. Split out of <c>Update</c> so the time
    /// source can be supplied by the caller instead of read from a singleton.
    /// </summary>
    public void TickLifetime(float worldDeltaTime)
    {
        if (!initialized || !active)
            return;

        if (!IsAnchorAlive())
        {
            End(BarrierEndReason.AnchorLost, transform.position, Vector3.up);
            return;
        }

        if (anchor != null)
            transform.position = anchor.position;

        remainingLifetime -= worldDeltaTime;
        if (remainingLifetime <= 0f)
            End(BarrierEndReason.Expired, transform.position, Vector3.up);
    }

    /// <summary>
    /// Liveness is decided per anchor mode rather than by guessing from which references happen
    /// to be null, so a cast-position barrier is never mistaken for one that lost its anchor.
    /// </summary>
    public bool IsAnchorAlive()
    {
        switch (anchorMode)
        {
            case BarrierAnchorMode.SpawnedEntitiesFromCurrentCast:
                return anchorSummon != null && anchorSummon.IsActive;

            case BarrierAnchorMode.CastPosition:
                // Pinned to a world position: nothing to lose. It ends on HP or lifetime only.
                return true;

            default:
                return IsCasterAlive();
        }
    }

    bool IsCasterAlive()
    {
        if (owner == null)
            return false;

        // Read health through the context hub, per the project's reference-resolution rules.
        HealthSystem health = owner.HealthSystem;
        if (health == null)
        {
            owner.ResolveReferences();
            health = owner.HealthSystem;
        }

        // A caster with no HealthSystem cannot be judged dead; keep the barrier rather than
        // silently cancelling it.
        return health == null || health.IsAlive;
    }

    public bool BlocksProjectileFrom(GameObject sourceActor)
    {
        if (!active || sourceActor == null || owner == null)
            return false;

        CharacteContext sourceContext = sourceActor.GetComponentInParent<CharacteContext>();
        if (sourceContext == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"[BarrierRuntime] Projectile from '{sourceActor.name}' has no CharacteContext; " +
                "faction is unknown so the barrier let it pass.",
                this);
#endif
            return false;
        }

        // Positive test: only a known opposing side is blocked. Unknown, Generic, Auto, and
        // Neutral shooters pass through instead of being swallowed by default.
        return BarrierFactionUtility.AreHostile(owner, sourceContext);
    }

    public void AbsorbProjectile(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!active)
            return;

        float applied = Mathf.Max(0f, damage);
        currentHealth -= applied;

        if (currentHealth > 0f)
        {
            Damaged?.Invoke(new BarrierDamagedEventData(
                this, applied, currentHealth, maxHealth, hitPoint, hitNormal));
            return;
        }

        // The breaking shot is fully consumed: no overflow damage reaches what is behind.
        currentHealth = 0f;
        End(BarrierEndReason.Broken, transform.position, hitPoint, hitNormal);
    }

    void End(BarrierEndReason reason, Vector3 position, Vector3 hitNormal)
    {
        End(reason, position, position, hitNormal);
    }

    void End(BarrierEndReason reason, Vector3 position, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!active)
            return;

        active = false;
        if (barrierCollider != null)
            barrierCollider.enabled = false;

        BarrierRegistry.Unregister(this);
        Ended?.Invoke(new BarrierBrokenEventData(this, reason, position, hitPoint, hitNormal));

        if (Application.isPlaying)
            Destroy(gameObject);
        else
            DestroyImmediate(gameObject);
    }

    void OnDisable()
    {
        BarrierRegistry.Unregister(this);
    }

    void OnDestroy()
    {
        active = false;
        BarrierRegistry.Unregister(this);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius > 0f ? radius : 1f);
    }
#endif
}
