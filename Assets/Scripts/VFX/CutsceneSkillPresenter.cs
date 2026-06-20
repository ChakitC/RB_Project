using System.Collections;
using System.Collections.Generic;
using Animancer;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CutsceneSkillPresenter : MonoBehaviour
{
    [Header("Actor")]
    [SerializeField] private CharacteContext _ctx;

    [Header("Camera")]
    [SerializeField] private CameraF _mainFollowCamera;
    [SerializeField] private Camera _cutsceneCamera;           // legacy — no longer toggled by runtime
    [SerializeField] private AnimancerComponent _cutsceneCamAnimancer; // legacy — no longer used for camera animation

    [Header("Cutscene Character")]
    [SerializeField] private AnimancerComponent _cutsceneCharAnimancer; // legacy — character anim now driven by CharacterAnimBrain

    [Header("Overlay")]
    [SerializeField] private int _sortingOrder = 31999;
    [SerializeField] private Color _barColor = new Color(0f, 0f, 0f, 1f);

    CutsceneDef _pendingDef;
    int _pendingReqId;
    bool _cutsceneActive;

    Coroutine _overlayRoutine;

    Canvas _overlayCanvas;
    CanvasGroup _overlayGroup;
    RectTransform _topBar;
    RectTransform _bottomBar;

    AnimancerComponent _cameraHolderAnimancer;
    AnimationVfxPresenter _cutsceneVfxPresenter;
    AnimationVfxSessionToken _cutsceneVfxToken;
    bool _hasCutsceneVfxSession;

    CharacterSkillManager _skillManager;
    CharacterAnimBrain _animBrain;

    // Unity lifecycle

    void Awake()
    {
        ResolveReferences();
        EnsureOverlay();
    }

    void OnEnable()
    {
        ResolveReferences();

        if (_skillManager != null)
        {
            _skillManager.CastStarted   += HandleCastStarted;
            _skillManager.CastCancelled += HandleCastCancelled;
        }

        if (_animBrain != null)
        {
            _animBrain.SkillTimelineEventRaised   += HandleTimelineEvent;
            _animBrain.SkillAnimationVfxCueRaised += HandleVfxCue;
        }
    }

    void OnDisable()
    {
        if (_skillManager != null)
        {
            _skillManager.CastStarted   -= HandleCastStarted;
            _skillManager.CastCancelled -= HandleCastCancelled;
        }

        if (_animBrain != null)
        {
            _animBrain.SkillTimelineEventRaised   -= HandleTimelineEvent;
            _animBrain.SkillAnimationVfxCueRaised -= HandleVfxCue;
        }

        if (_cutsceneActive)
            ForceEndCutscene();
    }

    void LateUpdate()
    {
        if (_cutsceneActive && _pendingDef != null)
            UpdateBarHeights(_pendingDef.barThickness);
    }

    // Event handlers

    void HandleCastStarted(ActiveSkillCastInfo castInfo)
    {
        if (!castInfo.SkillDef.IsCutsceneSkill)
            return;

        _pendingDef   = castInfo.SkillDef.CutsceneDef;
        _pendingReqId = castInfo.RequestId;
    }

    void HandleCastCancelled(ActiveSkillCastInfo castInfo, SkillCastCancelReason reason)
    {
        if (castInfo.RequestId != _pendingReqId)
            return;

        _pendingDef   = null;
        _pendingReqId = 0;

        if (_cutsceneActive)
            EndCutscene(fadeOutDuration: 0.05f);
    }

    void HandleTimelineEvent(int requestId, CombatTimelineEventName eventName)
    {
        if (requestId != _pendingReqId || _pendingDef == null)
            return;

        if (eventName == CombatTimelineEventName.CutsceneSkillStart)
            StartCutscene(_pendingDef);
        else if (eventName == CombatTimelineEventName.CutsceneSkillEnd)
            EndCutscene(_pendingDef.fadeOutDuration);
    }

    void HandleVfxCue(CharacterAnimBrain.SkillAnimationVfxCueSignal signal)
    {
        if (signal.RequestId != _pendingReqId || _pendingDef == null)
            return;
        if (signal.Phase != CharacterAnimBrain.SkillAnimationVfxPhase.Cutscene)
            return;
        if (!_hasCutsceneVfxSession || _cutsceneVfxPresenter == null)
            return;

        _cutsceneVfxPresenter.HandleCue(_cutsceneVfxToken, signal.CueIndex);
    }

    // Cutscene control

    void StartCutscene(CutsceneDef def)
    {
        if (_cutsceneActive)
            ForceEndCutscene();

        _cutsceneActive = true;

        TimeSlowManager.Instance.StartSlow(def.worldSlowScale, float.MaxValue);

        if (_mainFollowCamera != null)
            _mainFollowCamera.enabled = false;

        // Character animation is driven by CharacterAnimBrain on the gameplay LocoLayer.
        // Drive only camera animation and VFX from here.
        PlayCameraAnimation(def.cameraCutsceneClip);
        StartCutsceneVfxSession(def);

        if (_overlayRoutine != null)
            StopCoroutine(_overlayRoutine);
        _overlayRoutine = StartCoroutine(FadeOverlay(0f, 1f, def.fadeInDuration));
    }

    void EndCutscene(float fadeOutDuration)
    {
        if (!_cutsceneActive)
            return;

        _cutsceneActive = false;
        _pendingDef     = null;
        _pendingReqId   = 0;

        TimeSlowManager.Instance.StartSlow(2f, 0f);

        if (_mainFollowCamera != null)
            _mainFollowCamera.enabled = true;

        StopCameraAnimation();
        EndCutsceneVfxSession();

        float currentAlpha = _overlayGroup != null ? _overlayGroup.alpha : 0f;
        if (_overlayRoutine != null)
            StopCoroutine(_overlayRoutine);
        _overlayRoutine = StartCoroutine(FadeOverlay(currentAlpha, 0f, fadeOutDuration));
    }

    void ForceEndCutscene()
    {
        _cutsceneActive = false;
        _pendingDef     = null;
        _pendingReqId   = 0;

        TimeSlowManager.Instance.StartSlow(2f, 0f);

        if (_mainFollowCamera != null)
            _mainFollowCamera.enabled = true;

        StopCameraAnimation();
        EndCutsceneVfxSession();

        if (_overlayRoutine != null)
        {
            StopCoroutine(_overlayRoutine);
            _overlayRoutine = null;
        }

        if (_overlayGroup != null)
            _overlayGroup.alpha = 0f;
    }

    // Camera animation

    void PlayCameraAnimation(AnimationClip clip)
    {
        if (clip == null || _mainFollowCamera == null) return;

        if (_cameraHolderAnimancer == null)
            _cameraHolderAnimancer = _mainFollowCamera.GetComponentInChildren<AnimancerComponent>(true);

        if (_cameraHolderAnimancer == null)
        {
            Debug.LogWarning("[CutsceneSkillPresenter] No AnimancerComponent found under CameraHolder — camera animation skipped.", this);
            return;
        }

        _cameraHolderAnimancer.Animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        _cameraHolderAnimancer.Play(clip);
    }

    void StopCameraAnimation()
    {
        if (_cameraHolderAnimancer == null) return;
        if (_cameraHolderAnimancer.Animator != null)
            _cameraHolderAnimancer.Animator.updateMode = AnimatorUpdateMode.Normal;
        _cameraHolderAnimancer.Stop();
    }

    // Cutscene VFX session

    void StartCutsceneVfxSession(CutsceneDef def)
    {
        if (def.cutsceneVfxEvents == null || def.cutsceneVfxEvents.Count == 0)
            return;

        if (_cutsceneVfxPresenter == null)
            _cutsceneVfxPresenter = GetComponent<AnimationVfxPresenter>();
        if (_cutsceneVfxPresenter == null)
            _cutsceneVfxPresenter = gameObject.AddComponent<AnimationVfxPresenter>();

        EndCutsceneVfxSession();

        _cutsceneVfxToken = _cutsceneVfxPresenter.BeginSession(
            new CutsceneVfxCueSource(def.cutsceneVfxEvents),
            BuildCutsceneAnchorContext());
        _hasCutsceneVfxSession = true;
    }

    void EndCutsceneVfxSession()
    {
        if (!_hasCutsceneVfxSession) return;
        _hasCutsceneVfxSession = false;
        _cutsceneVfxPresenter?.EndSession(_cutsceneVfxToken);
        _cutsceneVfxToken = default;
    }

    // Overlay (black bars)

    IEnumerator FadeOverlay(float from, float to, float duration)
    {
        if (_overlayGroup == null)
            yield break;

        _overlayGroup.alpha = from;

        float safeDuration = Mathf.Max(duration, 0.001f);
        float elapsed = 0f;
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _overlayGroup.alpha = Mathf.Lerp(from, to, elapsed / safeDuration);
            yield return null;
        }

        _overlayGroup.alpha = to;
        _overlayRoutine = null;
    }

    void UpdateBarHeights(float thickness)
    {
        float height = Screen.height * Mathf.Clamp(thickness, 0f, 0.45f);
        if (_topBar != null)
            _topBar.sizeDelta    = new Vector2(0f, height);
        if (_bottomBar != null)
            _bottomBar.sizeDelta = new Vector2(0f, height);
    }

    void EnsureOverlay()
    {
        if (_overlayCanvas != null)
            return;

        GameObject canvasObject = new GameObject("CutsceneSkillOverlay",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        _overlayCanvas = canvasObject.GetComponent<Canvas>();
        _overlayCanvas.renderMode      = RenderMode.ScreenSpaceOverlay;
        _overlayCanvas.overrideSorting = true;
        _overlayCanvas.sortingOrder    = _sortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode            = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor            = 1f;
        scaler.referencePixelsPerUnit = 100f;

        _overlayGroup = canvasObject.GetComponent<CanvasGroup>();
        _overlayGroup.alpha          = 0f;
        _overlayGroup.interactable   = false;
        _overlayGroup.blocksRaycasts = false;

        _topBar    = CreateBar("TopBar",    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        _bottomBar = CreateBar("BottomBar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
    }

    RectTransform CreateBar(string barName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        GameObject barObject = new GameObject(barName, typeof(RectTransform), typeof(Image));
        barObject.transform.SetParent(_overlayCanvas.transform, false);

        Image img = barObject.GetComponent<Image>();
        img.color         = _barColor;
        img.raycastTarget = false;

        RectTransform rect = barObject.GetComponent<RectTransform>();
        rect.anchorMin        = anchorMin;
        rect.anchorMax        = anchorMax;
        rect.pivot            = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta        = Vector2.zero;

        return rect;
    }

    // Helpers

    void ResolveReferences()
    {
        if (!_ctx)
        {
            TryGetComponent(out _ctx);
            if (!_ctx)
                _ctx = GetComponentInParent<CharacteContext>();
        }

        _ctx?.ResolveReferences();

        if (_ctx != null)
        {
            _skillManager = _ctx.SkillManager;
            _animBrain = _ctx.AnimBrain;
        }

        if (!_skillManager)
            TryGetComponent(out _skillManager);
        if (!_skillManager)
            _skillManager = GetComponentInChildren<CharacterSkillManager>(true);
        if (!_skillManager)
            _skillManager = GetComponentInParent<CharacterSkillManager>();

        if (!_animBrain)
            TryGetComponent(out _animBrain);
        if (!_animBrain)
            _animBrain = GetComponentInChildren<CharacterAnimBrain>(true);
        if (!_animBrain)
            _animBrain = GetComponentInParent<CharacterAnimBrain>();

        if (!_mainFollowCamera)
            _mainFollowCamera = ResolveMainFollowCamera();
    }

    CameraF ResolveMainFollowCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.TryGetComponent(out CameraF followCamera))
            return followCamera;
        if (mainCamera != null)
        {
            followCamera = mainCamera.GetComponentInParent<CameraF>(true);
            if (followCamera != null)
                return followCamera;
        }

        CameraF fallback = null;
        CameraF[] candidates = FindObjectsByType<CameraF>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
        {
            Camera candidateCamera = candidates[i] != null ? candidates[i].GetComponent<Camera>() : null;
            if (candidateCamera == null && candidates[i] != null)
                candidateCamera = candidates[i].GetComponentInChildren<Camera>(true);
            if (candidateCamera == null)
                continue;

            if (candidateCamera.CompareTag("MainCamera"))
                return candidates[i];

            if (fallback == null)
                fallback = candidates[i];
        }

        return fallback;
    }

    AnimationVfxAnchorContext BuildCutsceneAnchorContext()
    {
        if (_ctx == null) return default;
        Transform root = _ctx.transform;
        SkillUserSystem skillUser = _ctx.EnegySystem;
        Animator animator = root.GetComponentInChildren<Animator>(true);
        return new AnimationVfxAnchorContext(
            root,
            skillUser != null ? skillUser.CastOrigin : root,
            skillUser != null ? skillUser.AimTransform : root,
            animator);
    }

    // Nested types

    sealed class CutsceneVfxCueSource : IAnimationVfxCueSource
    {
        readonly List<AnimationVfxCue> _cues;

        public CutsceneVfxCueSource(List<AnimationVfxCue> cues) => _cues = cues;

        public int CueCount => _cues != null ? _cues.Count : 0;

        public IAnimationVfxCue GetCue(int index) =>
            _cues != null && index >= 0 && index < _cues.Count ? _cues[index] : null;
    }
}
