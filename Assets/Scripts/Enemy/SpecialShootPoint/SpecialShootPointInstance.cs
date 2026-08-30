using UnityEngine;

/// <summary>
/// One pooled Special Shoot Point.
///
/// Owns local concerns only: its collider and hittable state, its own HP, the hit zone the selected
/// anchor authored, and its ring/hit/break presentation. It never applies enemy health damage,
/// stagger, a Mini Stun, ChainReady, or Behavior Tree state — it reports to its owning
/// <see cref="SpecialShootPointController"/> and lets the controller own the round.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpecialShootPointInstance : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Sphere collider the player must hit. Disabled during Telegraph and after the point resolves.")]
    [SerializeField] private SphereCollider hitCollider;

    [Tooltip("Scaled by the anchor's VFX scale. Faded out when the point resolves.")]
    [SerializeField] private Transform presentationRoot;

    [Tooltip("Renderer carrying the ring/crack material. Optional.")]
    [SerializeField] private Renderer ringRenderer;

    [Header("Material Properties")]
    [SerializeField] private string fillProperty = "_Fill";
    [SerializeField] private string flashProperty = "_Flash";
    [SerializeField] private string alphaProperty = "_Alpha";

    [Header("Feedback")]
    [Tooltip("Seconds a hit flash stays lit.")]
    [Min(0f)][SerializeField] private float hitFlashSeconds = 0.08f;

    [Tooltip("Flash cycles per second while the round is in its last-second warning.")]
    [Min(0f)][SerializeField] private float warningFlashHz = 6f;

    SpecialShootPointController _owner;
    SpecialShootPointProfileSO _profile;
    MaterialPropertyBlock _block;
    int _fillId;
    int _flashId;
    int _alphaId;
    bool _propertyIdsResolved;

    float _maxHealth = 1f;
    float _health;
    bool _hittable;
    bool _bound;
    bool _warningActive;
    float _hitFlashRemaining;
    float _fadeRemaining;
    float _fadeDuration;
    bool _fading;
    float _flashPhase;

    /// <summary>The controller that pooled this point. Null while the point sits idle in the pool.</summary>
    public SpecialShootPointController Owner => _owner;

    /// <summary>Hit zone authored on the anchor this point was bound to.</summary>
    public CharacterHitZone HitZone { get; private set; } = CharacterHitZone.Torso;

    /// <summary>True while the point still has HP left in the current round.</summary>
    public bool IsAlive => _bound && _health > 0f;

    /// <summary>
    /// True while the point still belongs to a round — including the fade after it broke or timed
    /// out. Goes false only on <see cref="ReturnToPool"/>, which is how the controller knows it may
    /// stop ticking this point.
    /// </summary>
    public bool IsBound => _bound;

    /// <summary>True only in the Active phase: Telegraph, broken, and resolved points refuse damage.</summary>
    public bool IsHittable => _bound && _hittable && _health > 0f;

    public float Health01 => _maxHealth > 0f ? Mathf.Clamp01(_health / _maxHealth) : 0f;

    public Vector3 WorldPosition => hitCollider != null ? hitCollider.bounds.center : transform.position;

    void Awake()
    {
        ResolveRefs();
        SetColliderEnabled(false);
    }

    void OnDisable()
    {
        // A disabled point must never stay resolvable, whatever tore it down.
        SpecialShootPointRegistry.Unregister(hitCollider);
        _hittable = false;
    }

    void OnDestroy()
    {
        SpecialShootPointRegistry.Unregister(hitCollider);
    }

    // ----- Round lifecycle -----

    /// <summary>
    /// Attaches this pooled point to a selected anchor and resets it to full HP. The collider stays
    /// disabled: Telegraph shows the point without letting it absorb a shot.
    /// </summary>
    public void Bind(
        SpecialShootPointController owner,
        SpecialShootPointProfileSO profile,
        SpecialShootPointAnchor anchor,
        float maxHealth)
    {
        ResolveRefs();

        _owner = owner;
        _profile = profile;
        HitZone = anchor != null ? anchor.hitZone : CharacterHitZone.Torso;
        _maxHealth = Mathf.Max(0.01f, maxHealth);
        _health = _maxHealth;
        _bound = true;
        _warningActive = false;
        _hitFlashRemaining = 0f;
        _fading = false;
        _fadeRemaining = 0f;
        _flashPhase = 0f;

        Transform anchorTransform = anchor != null ? anchor.anchor : null;
        transform.SetParent(anchorTransform, false);
        transform.localPosition = anchor != null ? anchor.localPosition : Vector3.zero;
        transform.localRotation = anchor != null ? anchor.LocalRotation : Quaternion.identity;
        transform.localScale = Vector3.one;

        if (presentationRoot != null && presentationRoot != transform)
        {
            float scale = anchor != null ? Mathf.Max(0.01f, anchor.vfxScale) : 1f;
            presentationRoot.localScale = Vector3.one * scale;
        }

        if (profile != null)
        {
            int layer = Mathf.Clamp(profile.pointColliderLayer, 0, 31);
            gameObject.layer = layer;
            if (hitCollider != null)
                hitCollider.gameObject.layer = layer;
        }

        if (hitCollider != null)
        {
            hitCollider.radius = anchor != null ? Mathf.Max(0.01f, anchor.colliderRadius) : 0.25f;
            hitCollider.isTrigger = true;
        }

        gameObject.SetActive(true);
        SetColliderEnabled(false);
        ApplyMaterialState(1f, 0f, 1f);
    }

    /// <summary>Enables or disables the point collider and its registry entry in one step.</summary>
    public void SetHittable(bool hittable)
    {
        if (!_bound)
            hittable = false;

        _hittable = hittable && _health > 0f;
        SetColliderEnabled(_hittable);
    }

    /// <summary>Drives the shared last-second warning. The HUD owns the countdown; each point owns its flash.</summary>
    public void SetWarningActive(bool active)
    {
        _warningActive = active;
    }

    /// <summary>
    /// Applies one accepted direct-hit result to this point.
    /// </summary>
    /// <returns>True when this hit destroyed the point.</returns>
    public bool ApplyPointDamage(float amount)
    {
        if (!IsHittable || amount <= 0f)
            return false;

        _health = Mathf.Max(0f, _health - amount);
        _hitFlashRemaining = hitFlashSeconds;

        PlaySfx(_profile != null ? _profile.pointHitSfx : null);

        if (_health > 0f)
            return false;

        // Collider off immediately: a piercing projectile in the same frame must not hit a dead point.
        _hittable = false;
        SetColliderEnabled(false);
        return true;
    }

    /// <summary>Success feedback: break VFX/audio, then fade out.</summary>
    public void PlayBreak()
    {
        _hittable = false;
        SetColliderEnabled(false);
        SpawnResolveVfx(_profile != null ? _profile.breakVfxPrefab : null);
        PlaySfx(_profile != null ? _profile.pointBreakSfx : null);
        BeginFade();
    }

    /// <summary>
    /// Timeout feedback. Deliberately not the break effect: the player must be able to tell a failed
    /// round from a completed one without reading the HUD.
    /// </summary>
    public void PlayTimeoutExtinguish()
    {
        _hittable = false;
        SetColliderEnabled(false);
        SpawnResolveVfx(_profile != null ? _profile.timeoutVfxPrefab : null);
        BeginFade();
    }

    /// <summary>Silent teardown for death, cinematic, or disable. No success or failure feedback.</summary>
    public void CancelSilently()
    {
        _hittable = false;
        SetColliderEnabled(false);
        ReturnToPool();
    }

    /// <summary>Detaches the point and parks it under its pool root with no live collider.</summary>
    public void ReturnToPool()
    {
        SpecialShootPointRegistry.Unregister(hitCollider);

        _bound = false;
        _hittable = false;
        _fading = false;
        _warningActive = false;
        _health = 0f;
        _profile = null;

        if (_owner != null && _owner.PoolRoot != null)
            transform.SetParent(_owner.PoolRoot, false);

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        gameObject.SetActive(false);
    }

    // ----- Presentation -----

    /// <summary>
    /// Driven by the controller rather than by <c>Update</c>: the round already ticks on the owner's
    /// gameplay clock, and a point must never advance its fade on a different time base than the
    /// challenge it belongs to.
    /// </summary>
    public void TickPresentation(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        if (_hitFlashRemaining > 0f)
            _hitFlashRemaining = Mathf.Max(0f, _hitFlashRemaining - deltaTime);

        if (_fading)
        {
            _fadeRemaining = Mathf.Max(0f, _fadeRemaining - deltaTime);
            float alpha = _fadeDuration > 0f ? _fadeRemaining / _fadeDuration : 0f;
            ApplyMaterialState(Health01, 0f, alpha);

            if (_fadeRemaining <= 0f)
                ReturnToPool();

            return;
        }

        if (!_bound)
            return;

        float flash = _hitFlashRemaining > 0f ? 1f : 0f;

        if (flash <= 0f && _warningActive && warningFlashHz > 0f)
        {
            _flashPhase += deltaTime * warningFlashHz;
            flash = Mathf.PingPong(_flashPhase, 1f);
        }

        ApplyMaterialState(Health01, flash, 1f);
    }

    void BeginFade()
    {
        _fadeDuration = _profile != null ? Mathf.Max(0f, _profile.resolveFadeSeconds) : 0.25f;

        if (_fadeDuration <= 0f)
        {
            ReturnToPool();
            return;
        }

        _fading = true;
        _fadeRemaining = _fadeDuration;
    }

    void SetColliderEnabled(bool colliderEnabled)
    {
        if (hitCollider == null)
            return;

        hitCollider.enabled = colliderEnabled;

        if (colliderEnabled)
            SpecialShootPointRegistry.Register(hitCollider, this);
        else
            SpecialShootPointRegistry.Unregister(hitCollider);
    }

    void ApplyMaterialState(float fill01, float flash01, float alpha01)
    {
        if (ringRenderer == null)
            return;

        EnsurePropertyIds();

        _block ??= new MaterialPropertyBlock();
        ringRenderer.GetPropertyBlock(_block);

        if (_fillId != 0) _block.SetFloat(_fillId, fill01);
        if (_flashId != 0) _block.SetFloat(_flashId, flash01);
        if (_alphaId != 0) _block.SetFloat(_alphaId, alpha01);

        ringRenderer.SetPropertyBlock(_block);
    }

    void EnsurePropertyIds()
    {
        if (_propertyIdsResolved)
            return;

        _fillId = string.IsNullOrEmpty(fillProperty) ? 0 : Shader.PropertyToID(fillProperty);
        _flashId = string.IsNullOrEmpty(flashProperty) ? 0 : Shader.PropertyToID(flashProperty);
        _alphaId = string.IsNullOrEmpty(alphaProperty) ? 0 : Shader.PropertyToID(alphaProperty);
        _propertyIdsResolved = true;
    }

    void SpawnResolveVfx(GameObject prefab)
    {
        if (prefab == null || VfxSpawner.Instance == null)
            return;

        VfxSpawner.Instance.SpawnVfx(prefab, WorldPosition, Vector3.up, 1f, 1f);
    }

    void PlaySfx(AudioClip clip)
    {
        if (clip == null)
            return;

        AudioSource.PlayClipAtPoint(clip, WorldPosition);
    }

    void ResolveRefs()
    {
        if (hitCollider == null)
            hitCollider = GetComponentInChildren<SphereCollider>(true);

        if (presentationRoot == null)
            presentationRoot = transform;

        if (ringRenderer == null)
            ringRenderer = GetComponentInChildren<Renderer>(true);
    }
}
