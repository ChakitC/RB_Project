using System.Collections;
using System.Collections.Generic;
using Animancer;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CutsceneSkillPresenter : MonoBehaviour
{
    const string CutsceneCameraName = "CutsceneCamera";
    const string CutsceneCameraRigName = "CutsceneCameraRig";
    const string CutsceneCharacterName = "CutsceneCharacter";

    [Header("Actor")]
    [SerializeField] private CharacteContext _ctx;

    [Header("Camera")]
    [SerializeField] private CameraF _mainFollowCamera;
    [SerializeField] private Camera _cutsceneCamera;
    [SerializeField] private AnimancerComponent _cutsceneCamAnimancer;

    [Header("Cutscene Character")]
    [SerializeField] private AnimancerComponent _cutsceneCharAnimancer;

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

    readonly List<GameObject> _spawnedCutsceneVfx = new List<GameObject>();
    readonly Dictionary<string, List<GameObject>> _cutsceneVfxLoops =
        new Dictionary<string, List<GameObject>>(System.StringComparer.Ordinal);

    CharacterSkillManager _skillManager;
    CharacterAnimBrain _animBrain;

    static int _cutsceneLayer = -1;

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
            _animBrain.SkillTimelineEventRaised += HandleTimelineEvent;
    }

    void OnDisable()
    {
        if (_skillManager != null)
        {
            _skillManager.CastStarted   -= HandleCastStarted;
            _skillManager.CastCancelled -= HandleCastCancelled;
        }

        if (_animBrain != null)
            _animBrain.SkillTimelineEventRaised -= HandleTimelineEvent;

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

    // Cutscene control

    void StartCutscene(CutsceneDef def)
    {
        if (_cutsceneActive)
            ForceEndCutscene();

        _cutsceneActive = true;

        TimeSlowManager.Instance.StartSlow(def.worldSlowScale, float.MaxValue);

        if (_mainFollowCamera != null)
            _mainFollowCamera.enabled = false;

        if (_cutsceneCamera != null)
            _cutsceneCamera.enabled = true;

        if (_cutsceneCharAnimancer != null && def.characterCutsceneClip != null && def.characterCutsceneClip.IsValid)
        {
            _cutsceneCharAnimancer.Animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            AnimancerState state = _cutsceneCharAnimancer.Play(def.characterCutsceneClip);
            if (def.cutsceneVfxEvents != null && def.cutsceneVfxEvents.Count > 0)
            {
                AnimationVfxAnchorContext context = BuildCutsceneAnchorContext();
                var cueSource = new CutsceneVfxCueSource(def.cutsceneVfxEvents);
                var runtimeEvents = new AnimancerEvent.Sequence(def.characterCutsceneClip.Events);
                AnimationVfxEventBinder.Bind(runtimeEvents,
                    cueIndex => SpawnCutsceneVfxForCue(cueSource, cueIndex, context));
                state.SharedEvents = runtimeEvents;
            }
        }

        if (_cutsceneCamAnimancer != null && def.cameraCutsceneClip != null)
        {
            if (_cutsceneCharAnimancer != null)
            {
                Transform charRoot = _cutsceneCharAnimancer.transform;
                _cutsceneCamAnimancer.transform.SetPositionAndRotation(
                    charRoot.position, charRoot.rotation);
            }
            _cutsceneCamAnimancer.Animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            _cutsceneCamAnimancer.Play(def.cameraCutsceneClip);
        }

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

        if (_cutsceneCamera != null)
            _cutsceneCamera.enabled = false;

        ResetAnimancerUpdateModes();
        StopAndClearVfx();

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

        if (_cutsceneCamera != null)
            _cutsceneCamera.enabled = false;

        ResetAnimancerUpdateModes();
        StopAndClearVfx();

        if (_overlayRoutine != null)
        {
            StopCoroutine(_overlayRoutine);
            _overlayRoutine = null;
        }

        if (_overlayGroup != null)
            _overlayGroup.alpha = 0f;
    }

    void ResetAnimancerUpdateModes()
    {
        if (_cutsceneCharAnimancer != null && _cutsceneCharAnimancer.Animator != null)
            _cutsceneCharAnimancer.Animator.updateMode = AnimatorUpdateMode.Normal;
        if (_cutsceneCamAnimancer != null && _cutsceneCamAnimancer.Animator != null)
            _cutsceneCamAnimancer.Animator.updateMode = AnimatorUpdateMode.Normal;
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

    // VFX spawning

    void SpawnCutsceneVfxForCue(CutsceneVfxCueSource source, int cueIndex, AnimationVfxAnchorContext context)
    {
        for (int i = 0; i < source.CueCount; i++)
        {
            IAnimationVfxCue cue = source.GetCue(i);
            if (cue != null && cue.CueIndex == cueIndex)
                SpawnCutsceneVfxEntry(cue, context);
        }
    }

    void SpawnCutsceneVfxEntry(IAnimationVfxCue cue, AnimationVfxAnchorContext context)
    {
        if (cue.Action == AnimationVfxAction.StopLoop)
        {
            string stopKey = cue.LoopKey?.Trim();
            if (!string.IsNullOrEmpty(stopKey) &&
                _cutsceneVfxLoops.TryGetValue(stopKey, out List<GameObject> loopInstances))
            {
                _cutsceneVfxLoops.Remove(stopKey);
                for (int i = 0; i < loopInstances.Count; i++)
                {
                    _spawnedCutsceneVfx.Remove(loopInstances[i]);
                    if (loopInstances[i] != null)
                        Destroy(loopInstances[i]);
                }
            }
            return;
        }

        if (cue.Prefab == null)
            return;

        Transform anchor = AnimationVfxAnchorResolver.Resolve(context, cue);
        AnimationVfxAnchorResolver.ResolvePose(anchor, cue, out Vector3 pos, out Quaternion rot);

        GameObject spawned = Instantiate(cue.Prefab, pos, rot);
        spawned.transform.localScale = Vector3.Scale(spawned.transform.localScale, cue.LocalScale);

        if (cue.AnchorMode == AnimationVfxAnchorMode.FollowAnchor && anchor != null)
        {
            if (cue.Anchor == AnimationVfxAnchor.GenericBone)
            {
                AnimationVfxFollowAnchor follower = spawned.GetComponent<AnimationVfxFollowAnchor>();
                if (follower == null)
                    follower = spawned.AddComponent<AnimationVfxFollowAnchor>();
                follower.Configure(anchor, cue.LocalPosition, Quaternion.Euler(cue.LocalEulerAngles));
            }
            else
            {
                spawned.transform.SetParent(anchor, true);
            }
        }

        int layer = GetCutsceneLayer();
        if (layer >= 0)
            SetLayerRecursive(spawned, layer);

        AnimationVfxPresenter.PlayAnimClip(spawned, cue.AnimClip);

        _spawnedCutsceneVfx.Add(spawned);

        if (cue.Action == AnimationVfxAction.StartLoop && !string.IsNullOrWhiteSpace(cue.LoopKey))
        {
            string loopKey = cue.LoopKey.Trim();
            if (!_cutsceneVfxLoops.TryGetValue(loopKey, out List<GameObject> loopInstances))
            {
                loopInstances = new List<GameObject>();
                _cutsceneVfxLoops.Add(loopKey, loopInstances);
            }
            loopInstances.Add(spawned);
        }
    }

    void StopAndClearVfx()
    {
        for (int i = 0; i < _spawnedCutsceneVfx.Count; i++)
        {
            if (_spawnedCutsceneVfx[i] != null)
                Destroy(_spawnedCutsceneVfx[i]);
        }
        _spawnedCutsceneVfx.Clear();
        _cutsceneVfxLoops.Clear();
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

        ResolveSceneReferences();
    }

    void ResolveSceneReferences()
    {
        if (!_mainFollowCamera)
            _mainFollowCamera = ResolveMainFollowCamera();

        if (!_cutsceneCamera)
            _cutsceneCamera = ResolveCutsceneCameraFromRig();
        if (!_cutsceneCamera)
            _cutsceneCamera = ResolveCutsceneCamera();

        if (!_cutsceneCamAnimancer)
            _cutsceneCamAnimancer = ResolveCutsceneCameraAnimancer();

        if (!_cutsceneCharAnimancer)
            _cutsceneCharAnimancer = ResolveNamedAnimancer(CutsceneCharacterName);
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

            if (!IsCutsceneOnlyCamera(candidateCamera) && fallback == null)
                fallback = candidates[i];
        }

        return fallback;
    }

    Camera ResolveCutsceneCameraFromRig()
    {
        return _cutsceneCamAnimancer != null
            ? _cutsceneCamAnimancer.GetComponentInChildren<Camera>(true)
            : null;
    }

    Camera ResolveCutsceneCamera()
    {
        Camera fallback = null;
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null)
                continue;

            if (string.Equals(camera.name, CutsceneCameraName, System.StringComparison.Ordinal))
                return camera;

            if (IsCutsceneOnlyCamera(camera) && fallback == null)
                fallback = camera;
        }

        return fallback;
    }

    AnimancerComponent ResolveCutsceneCameraAnimancer()
    {
        if (_cutsceneCamera != null)
        {
            AnimancerComponent parentAnimancer = _cutsceneCamera.GetComponentInParent<AnimancerComponent>(true);
            if (parentAnimancer != null)
                return parentAnimancer;
        }

        return ResolveNamedAnimancer(CutsceneCameraRigName);
    }

    AnimancerComponent ResolveNamedAnimancer(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        AnimancerComponent[] candidates = FindObjectsByType<AnimancerComponent>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
        {
            AnimancerComponent candidate = candidates[i];
            if (candidate != null && string.Equals(candidate.name, objectName, System.StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }

    static bool IsCutsceneOnlyCamera(Camera camera)
    {
        if (camera == null)
            return false;

        int layer = GetCutsceneLayer();
        if (layer < 0)
            return false;

        int cutsceneMask = 1 << layer;
        return (camera.cullingMask & cutsceneMask) != 0 &&
               (camera.cullingMask & ~cutsceneMask) == 0;
    }

    AnimationVfxAnchorContext BuildCutsceneAnchorContext()
    {
        if (_cutsceneCharAnimancer == null)
            return default;
        Transform root = _cutsceneCharAnimancer.transform;
        Animator animator = root.GetComponentInChildren<Animator>(true);
        return new AnimationVfxAnchorContext(root, root, root, animator);
    }

    static int GetCutsceneLayer()
    {
        if (_cutsceneLayer < 0)
            _cutsceneLayer = LayerMask.NameToLayer("Cutscene");
        return _cutsceneLayer;
    }

    static void SetLayerRecursive(GameObject root, int layer)
    {
        root.layer = layer;
        Transform t = root.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursive(t.GetChild(i).gameObject, layer);
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
