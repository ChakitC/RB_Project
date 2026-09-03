using System;
using UnityEngine;
using UnityEngine.Rendering;
using ZLZ.AnimeShader;

/// <summary>
/// Gameplay-facing presentation controller for "is this character visible right now".
///
/// It owns the character's dither alpha and nothing else: gameplay systems (helper summon,
/// chain-attack teleport, interruption snap, cutscenes) ask for a visibility change in
/// gameplay words and this component animates <c>ZLZ_CharacterVFX.Dither</c> to match.
///
/// The dither value is driven with <c>Dither.SetInstant()</c> rather than
/// <c>Dither.Hide()</c> / <c>Dither.Show()</c> on purpose:
/// <c>Show()</c> is a no-op after <c>SetInstant(1)</c> (the FX block is back in Idle),
/// <c>Hide()</c> parks in the settings asset's Loop phase, neither reports completion, and
/// neither can run on unscaled time. Owning the interpolation here gives completion events,
/// cancellation, unscaled time, and transitions that continue from the current value.
///
/// The controller never disables its own GameObject. Sequence owners subscribe to
/// <see cref="Disappeared"/> and deactivate the actor themselves.
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterVisibilityController : MonoBehaviour
{
    const float HiddenAlpha = 1f;
    const float VisibleAlpha = 0f;
    const float Epsilon = 0.01f;

    // How many LateUpdates keep re-pushing the desired dither value after a bind / re-activation.
    const int ReapplyFrames = 2;

    enum TransitionKind
    {
        None,
        Appear,
        Disappear
    }

    [Header("References")]
    [SerializeField, Tooltip("Leave empty to resolve from the character visual model at runtime.")]
    private ZLZ_CharacterVFX characterVfx;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float appearDuration = 0.18f;
    [SerializeField, Min(0f)] private float disappearDuration = 0.18f;
    [SerializeField] private AnimationCurve appearCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve disappearCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Tooltip("Drive transitions with unscaled time so they still run during hit-lag, world slow, and dialogue pauses.")]
    private bool useUnscaledTime = true;

    [Header("Shadows")]
    [SerializeField, Tooltip("Force Shadow Casting Mode off on the bound visual while fully hidden. Only needed if a hidden character still leaves a shadow.")]
    private bool disableShadowsWhileHidden;

    GameObject _boundVisual;
    Renderer[] _shadowRenderers;
    ShadowCastingMode[] _shadowModes;
    bool _shadowsSuppressed;

    float _currentAlpha = VisibleAlpha;
    float _startAlpha = VisibleAlpha;
    float _targetAlpha = VisibleAlpha;
    float _elapsed;
    float _duration;
    AnimationCurve _activeCurve;
    TransitionKind _transition = TransitionKind.None;

    bool _alphaDirty = true;
    int _reapplyFrames;
    bool _missingVfxWarned;
    bool _ditherDisabledWarned;

    /// <summary>Raised when an <see cref="Appear"/> finishes, including when deactivation cuts it short.</summary>
    public event Action Appeared;

    /// <summary>Raised when a <see cref="Disappear"/> finishes. Sequence owners deactivate the actor from here.</summary>
    public event Action Disappeared;

    public bool IsHidden => _currentAlpha >= HiddenAlpha - Epsilon;

    public bool IsTransitioning => _transition != TransitionKind.None;

    internal bool IsDisappearing => _transition == TransitionKind.Disappear;
    internal bool UsesUnscaledTime => useUnscaledTime;

    internal bool ShouldBeginAutoHide(
        float elapsed,
        float remainingDuration,
        float playbackDuration)
    {
        return ShouldBeginAutoHideForFade(elapsed, remainingDuration, playbackDuration, fadeDuration: -1f);
    }

    /// <summary>
    /// Same gate, but for a caller supplying its own fade length. Named apart from the pair above
    /// because a four-float overload would collide with the static helper.
    /// </summary>
    internal bool ShouldBeginAutoHideForFade(
        float elapsed,
        float remainingDuration,
        float playbackDuration,
        float fadeDuration)
    {
        float resolvedFade = fadeDuration >= 0f ? fadeDuration : disappearDuration;

        float minimumVisibleDuration = Mathf.Max(0.05f, appearDuration);
        if (float.IsFinite(playbackDuration) && playbackDuration > 0f)
            minimumVisibleDuration = Mathf.Min(minimumVisibleDuration, playbackDuration * 0.5f);

        // A fade sized from the window it has to fit in must not then be blocked by the
        // minimum-visible gate, or a short window loses its fade entirely — which is the case the
        // caller sized it for.
        if (fadeDuration >= 0f)
            minimumVisibleDuration = Mathf.Min(minimumVisibleDuration, Mathf.Max(0f, remainingDuration));

        return ShouldBeginAutoHide(
            elapsed,
            minimumVisibleDuration,
            remainingDuration,
            resolvedFade);
    }

    internal static bool ShouldBeginAutoHide(
        float elapsed,
        float minimumVisibleDuration,
        float remainingDuration,
        float disappearDuration)
    {
        const float triggerEpsilon = 0.01f;
        return elapsed + triggerEpsilon >= minimumVisibleDuration &&
               remainingDuration <= Mathf.Max(0f, disappearDuration) + triggerEpsilon;
    }

    void Awake()
    {
        if (characterVfx == null)
            characterVfx = GetComponentInChildren<ZLZ_CharacterVFX>(true);
    }

    void OnEnable()
    {
        // ZLZ_CharacterVFX resets _DitherAlpha to 0 (visible) in Awake and OnDisable, so the
        // desired state has to be written back every time the actor comes back. The write is
        // repeated from LateUpdate as well, because the VFX component may not have run Awake
        // yet this frame - LateUpdate still lands before the frame renders, so nothing flashes.
        _alphaDirty = true;
        _reapplyFrames = ReapplyFrames;
        ApplyAlpha();
    }

    void OnDisable()
    {
        // Finish a transition that deactivation cut short so whoever is waiting on the
        // completion event is never stranded.
        if (_transition == TransitionKind.None)
            return;

        TransitionKind finished = _transition;
        _transition = TransitionKind.None;
        _currentAlpha = _targetAlpha;
        RaiseCompleted(finished);
    }

    void LateUpdate()
    {
        if (_transition != TransitionKind.None)
        {
            _elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float progress = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
            float eased = _activeCurve != null ? _activeCurve.Evaluate(progress) : progress;

            SetAlpha(Mathf.LerpUnclamped(_startAlpha, _targetAlpha, eased));

            if (progress >= 1f)
            {
                SetAlpha(_targetAlpha);
                TransitionKind finished = _transition;
                _transition = TransitionKind.None;
                ApplyAlpha();
                RaiseCompleted(finished);
                return;
            }
        }

        if (_reapplyFrames > 0)
        {
            // The VFX component may only have built its material instances in Awake after our
            // write landed (a model instantiated under an inactive root, a re-activated actor).
            // Re-push for a couple of frames so the desired state is never silently swallowed.
            _reapplyFrames--;
            ApplyAlpha();
            return;
        }

        if (_alphaDirty)
            ApplyAlpha();
    }

    /// <summary>
    /// Points the controller at a freshly built character model. Called by
    /// <see cref="CharacterVisualController"/> after a model build, an existing-model bind, a
    /// form override, and a form restore, so the controller never keeps writing to renderers or
    /// material instances that belong to a destroyed model.
    /// </summary>
    public void BindVisual(GameObject visualRoot)
    {
        _boundVisual = visualRoot;
        _shadowRenderers = null;
        _shadowModes = null;
        _shadowsSuppressed = false;
        _missingVfxWarned = false;
        _ditherDisabledWarned = false;

        characterVfx = visualRoot != null
            ? visualRoot.GetComponentInChildren<ZLZ_CharacterVFX>(true)
            : null;

        if (characterVfx == null)
            characterVfx = GetComponentInChildren<ZLZ_CharacterVFX>(true);

        // A rebuilt model starts visible; push the state gameplay currently expects.
        _alphaDirty = true;
        _reapplyFrames = ReapplyFrames;
        ApplyAlpha();
    }

    /// <summary>
    /// Re-scans the bound model for renderers that appeared or disappeared underneath it — a weapon
    /// being mounted, swapped, or removed — so the body and whatever it is holding keep fading as one
    /// object. The model itself is unchanged, so this is not a rebind: call it only when the
    /// hierarchy under the model actually changes, never per frame.
    ///
    /// <c>ZLZ_CharacterVFX.RefreshRenderers</c> rebuilds its material instances and resets the dither
    /// to visible, so the desired alpha has to be written back in the same call or a hidden character
    /// pops for a frame.
    /// </summary>
    public void RefreshVisualRenderers()
    {
        if (characterVfx == null)
        {
            WarnMissingVfxOnce();
            return;
        }

        characterVfx.RefreshRenderers();

        // The renderer set just changed, so the cached shadow-casting modes describe the old one.
        _shadowRenderers = null;
        _shadowModes = null;
        _shadowsSuppressed = false;

        _alphaDirty = true;
        _reapplyFrames = ReapplyFrames;
        ApplyAlpha();
    }

    public void SetHiddenImmediate()
    {
        CancelTransition();
        SetAlpha(HiddenAlpha);
        ApplyAlpha();
    }

    public void SetVisibleImmediate()
    {
        CancelTransition();
        SetAlpha(VisibleAlpha);
        ApplyAlpha();
    }

    /// <summary>Hides instantly before a position warp so the actor is never seen sliding.</summary>
    public void ConcealForTeleport() => SetHiddenImmediate();

    /// <summary>Shows instantly after a position warp has landed.</summary>
    public void RevealAfterTeleport() => SetVisibleImmediate();

    /// <summary>Fades in from the current dither value and raises <see cref="Appeared"/>.</summary>
    public void Appear() => BeginTransition(TransitionKind.Appear, VisibleAlpha, appearDuration, appearCurve);

    /// <summary>Fades out from the current dither value and raises <see cref="Disappeared"/>.</summary>
    public void Disappear() => BeginTransition(TransitionKind.Disappear, HiddenAlpha, disappearDuration, disappearCurve);

    /// <summary>
    /// Fades out over an explicit duration instead of the serialized one. Used where the time
    /// available is dictated by something else — a warp fade has to be finished by the frame the
    /// actor is teleported, and that budget comes from the animation, not from this component.
    /// </summary>
    public void Disappear(float duration) =>
        BeginTransition(TransitionKind.Disappear, HiddenAlpha, Mathf.Max(0f, duration), disappearCurve);

    /// <summary>The authored fade-out length, for callers that scale their own timing against it.</summary>
    public float DisappearDuration => disappearDuration;

    /// <summary>Stops any running transition and holds the current value. No completion event is raised.</summary>
    public void CancelTransition()
    {
        _transition = TransitionKind.None;
    }

    void BeginTransition(TransitionKind kind, float target, float duration, AnimationCurve curve)
    {
        // Continue from wherever the previous transition got to instead of snapping to an endpoint.
        _startAlpha = _currentAlpha;
        _targetAlpha = target;
        _duration = Mathf.Max(0f, duration);
        _activeCurve = curve;
        _elapsed = 0f;

        if (_duration <= 0f || !isActiveAndEnabled)
        {
            _transition = TransitionKind.None;
            SetAlpha(target);
            ApplyAlpha();
            RaiseCompleted(kind);
            return;
        }

        _transition = kind;
        SetAlpha(_startAlpha);
    }

    void RaiseCompleted(TransitionKind kind)
    {
        switch (kind)
        {
            case TransitionKind.Appear:
                Appeared?.Invoke();
                break;
            case TransitionKind.Disappear:
                Disappeared?.Invoke();
                break;
        }
    }

    void SetAlpha(float value)
    {
        float clamped = Mathf.Clamp01(value);
        if (_alphaDirty || !Mathf.Approximately(clamped, _currentAlpha))
        {
            _currentAlpha = clamped;
            _alphaDirty = true;
        }
    }

    void ApplyAlpha()
    {
        _alphaDirty = false;

        if (characterVfx == null)
        {
            WarnMissingVfxOnce();
            return;
        }

        if (!characterVfx.Dither.Enabled)
        {
            // SetInstant is gated on ZLZ_DitherFX.CanDrive, so a disabled Dither silently
            // swallows every write and the character would stay visible while gameplay
            // believes it is hidden.
            WarnDitherDisabledOnce();
            return;
        }

        characterVfx.Dither.SetInstant(_currentAlpha);
        ApplyShadowSuppression(_currentAlpha >= HiddenAlpha - Epsilon);
    }

    void ApplyShadowSuppression(bool hidden)
    {
        if (!disableShadowsWhileHidden || _shadowsSuppressed == hidden)
            return;

        CacheShadowRenderers();
        if (_shadowRenderers == null)
            return;

        for (int i = 0; i < _shadowRenderers.Length; i++)
        {
            Renderer renderer = _shadowRenderers[i];
            if (renderer == null)
                continue;

            renderer.shadowCastingMode = hidden ? ShadowCastingMode.Off : _shadowModes[i];
        }

        _shadowsSuppressed = hidden;
    }

    void CacheShadowRenderers()
    {
        if (_shadowRenderers != null)
            return;

        GameObject source = _boundVisual != null ? _boundVisual
            : characterVfx != null ? characterVfx.gameObject
            : gameObject;

        _shadowRenderers = source.GetComponentsInChildren<Renderer>(true);
        _shadowModes = new ShadowCastingMode[_shadowRenderers.Length];
        for (int i = 0; i < _shadowRenderers.Length; i++)
            _shadowModes[i] = _shadowRenderers[i] != null
                ? _shadowRenderers[i].shadowCastingMode
                : ShadowCastingMode.Off;
    }

    void WarnMissingVfxOnce()
    {
        if (_missingVfxWarned)
            return;

        _missingVfxWarned = true;
        Debug.LogWarning(
            "[CharacterVisibilityController] No ZLZ_CharacterVFX on the bound visual - visibility changes will not render.",
            this);
    }

    void WarnDitherDisabledOnce()
    {
        if (_ditherDisabledWarned)
            return;

        _ditherDisabledWarned = true;
        Debug.LogWarning(
            "[CharacterVisibilityController] ZLZ_CharacterVFX has Dither disabled - enable it in the Character Dashboard or visibility changes are ignored.",
            characterVfx);
    }
}
