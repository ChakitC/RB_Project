using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PerfectDashScreenFx : MonoBehaviour
{
    static PerfectDashScreenFx _instance;
    static bool _isQuitting;

    [Header("Canvas")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private int sortingOrder = 32000;
    [SerializeField] private bool persistAcrossScenes = true;

    [Header("Lines")]
    [SerializeField, Range(8, 96)] private int baseLineCount = 36;
    [SerializeField, Range(8, 128)] private int maxLineCount = 64;
    [SerializeField, Min(0f)] private float edgePadding = 28f;
    [SerializeField, Min(0f)] private float centerStopRadius = 210f;
    [SerializeField, Min(0f)] private float centerJitter = 70f;
    [SerializeField, Range(0f, 2f)] private float directionalBias = 1.25f;
    [SerializeField] private Vector2 lineLengthRange = new(145f, 260f);
    [SerializeField] private Vector2 lineThicknessRange = new(2f, 4.5f);
    [SerializeField] private Color flashColor = new(1f, 0.82f, 0.28f, 0.95f);
    [SerializeField] private Color slowTrailColor = new(0.42f, 0.86f, 1f, 0.62f);

    [Header("Timing")]
    [SerializeField, Min(0.001f)] private float flashInDuration = 0.035f;
    [SerializeField, Min(0.001f)] private float minActiveDuration = 0.28f;
    [SerializeField, Min(0.001f)] private float fadeOutDuration = 0.14f;
    [SerializeField] private AnimationCurve travelCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    readonly List<LineState> _lines = new();
    RectTransform _lineRoot;
    CanvasGroup _canvasGroup;
    Coroutine _playRoutine;

    public static PerfectDashScreenFx Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            if (_isQuitting)
                return null;

            _instance = FindAnyObjectByType<PerfectDashScreenFx>();
            if (_instance != null)
                return _instance;

            GameObject fxObject = new GameObject(nameof(PerfectDashScreenFx));
            _instance = fxObject.AddComponent<PerfectDashScreenFx>();
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        _instance = null;
        _isQuitting = false;
        Application.quitting -= HandleApplicationQuitting;
        Application.quitting += HandleApplicationQuitting;
    }

    static void HandleApplicationQuitting()
    {
        _isQuitting = true;
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);

        EnsureOverlay();
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    public void Play(Vector3 worldDashDirection, float slowDuration, float slowScale, float intensity = 1f)
    {
        if (!isActiveAndEnabled)
            return;

        EnsureOverlay();

        if (_playRoutine != null)
            StopCoroutine(_playRoutine);

        float slowStrength = Mathf.Clamp01(1f - slowScale);
        float clampedIntensity = Mathf.Clamp(intensity, 0.2f, 2f);
        int lineCount = Mathf.RoundToInt(baseLineCount * clampedIntensity * Mathf.Lerp(0.75f, 1.15f, slowStrength));
        lineCount = Mathf.Clamp(lineCount, 1, Mathf.Max(1, maxLineCount));

        EnsurePool(lineCount);
        ConfigureLines(lineCount, ResolveScreenDashDirection(worldDashDirection), slowStrength, clampedIntensity);

        float activeDuration = Mathf.Max(minActiveDuration, slowDuration);
        float alphaMultiplier = Mathf.Clamp01(Mathf.Lerp(0.5f, 1f, slowStrength) * clampedIntensity);
        _playRoutine = StartCoroutine(PlayRoutine(lineCount, activeDuration, alphaMultiplier));
    }

    public void Stop()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        HideAllLines();
    }

    void EnsureOverlay()
    {
        if (targetCanvas == null)
            targetCanvas = GetComponentInChildren<Canvas>(true);

        if (targetCanvas == null)
        {
            GameObject canvasObject = new GameObject("PerfectDashScreenFx Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            targetCanvas = canvasObject.GetComponent<Canvas>();
        }

        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        targetCanvas.overrideSorting = true;
        targetCanvas.sortingOrder = sortingOrder;
        targetCanvas.pixelPerfect = false;

        CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = targetCanvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        scaler.referencePixelsPerUnit = 100f;

        if (_lineRoot == null)
        {
            Transform existingRoot = targetCanvas.transform.Find("LineRoot");
            _lineRoot = existingRoot as RectTransform;
        }

        if (_lineRoot == null)
        {
            GameObject rootObject = new GameObject("LineRoot", typeof(RectTransform), typeof(CanvasGroup));
            rootObject.transform.SetParent(targetCanvas.transform, false);
            _lineRoot = rootObject.GetComponent<RectTransform>();
        }

        _lineRoot.anchorMin = Vector2.zero;
        _lineRoot.anchorMax = Vector2.one;
        _lineRoot.pivot = new Vector2(0.5f, 0.5f);
        _lineRoot.anchoredPosition = Vector2.zero;
        _lineRoot.sizeDelta = Vector2.zero;

        _canvasGroup = _lineRoot.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = _lineRoot.gameObject.AddComponent<CanvasGroup>();

        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    void EnsurePool(int requiredCount)
    {
        while (_lines.Count < requiredCount)
        {
            int index = _lines.Count;
            GameObject lineObject = new GameObject($"SpeedLine {index:00}", typeof(RectTransform), typeof(Image));
            lineObject.transform.SetParent(_lineRoot, false);

            RectTransform rect = lineObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image image = lineObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = Color.clear;

            lineObject.SetActive(false);
            _lines.Add(new LineState(rect, image));
        }
    }

    void ConfigureLines(int lineCount, Vector2 screenDashDirection, float slowStrength, float intensity)
    {
        Canvas.ForceUpdateCanvases();

        float width = Mathf.Max(_lineRoot.rect.width, Screen.width);
        float height = Mathf.Max(_lineRoot.rect.height, Screen.height);
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float lengthBoost = Mathf.Lerp(0.9f, 1.15f, slowStrength) * Mathf.Lerp(0.9f, 1.08f, Mathf.Clamp01(intensity - 0.2f));

        for (int i = 0; i < _lines.Count; i++)
        {
            bool active = i < lineCount;
            LineState line = _lines[i];
            line.Rect.gameObject.SetActive(active);
            if (!active)
                continue;

            Vector2 start = PickEdgePosition(halfWidth, halfHeight, screenDashDirection);
            Vector2 radial = start.sqrMagnitude > 0.0001f ? start.normalized : Vector2.up;
            Vector2 tangent = new Vector2(-radial.y, radial.x);
            float stopRadius = Random.Range(centerStopRadius * 0.78f, centerStopRadius * 1.18f);
            Vector2 end = radial * stopRadius + tangent * Random.Range(-centerJitter, centerJitter);
            Vector2 move = end - start;
            float angle = Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg;

            float length = Random.Range(lineLengthRange.x, lineLengthRange.y) * lengthBoost;
            float thickness = Random.Range(lineThicknessRange.x, lineThicknessRange.y);

            line.Start = start;
            line.End = end;
            line.Delay = Random.Range(0f, 0.055f);
            line.TravelDuration = Random.Range(0.72f, 1.08f);
            line.Alpha = Random.Range(0.72f, 1f);

            line.Rect.anchoredPosition = start;
            line.Rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            line.Rect.sizeDelta = new Vector2(Mathf.Max(1f, length), Mathf.Max(1f, thickness));
            line.Image.color = Color.clear;
        }
    }

    Vector2 PickEdgePosition(float halfWidth, float halfHeight, Vector2 screenDashDirection)
    {
        float leftWeight = 1f + Mathf.Max(0f, -screenDashDirection.x) * directionalBias;
        float rightWeight = 1f + Mathf.Max(0f, screenDashDirection.x) * directionalBias;
        float bottomWeight = 1f + Mathf.Max(0f, -screenDashDirection.y) * directionalBias;
        float topWeight = 1f + Mathf.Max(0f, screenDashDirection.y) * directionalBias;

        float roll = Random.value * (leftWeight + rightWeight + bottomWeight + topWeight);
        if (roll < leftWeight)
            return new Vector2(-halfWidth - edgePadding, Random.Range(-halfHeight, halfHeight));

        roll -= leftWeight;
        if (roll < rightWeight)
            return new Vector2(halfWidth + edgePadding, Random.Range(-halfHeight, halfHeight));

        roll -= rightWeight;
        if (roll < bottomWeight)
            return new Vector2(Random.Range(-halfWidth, halfWidth), -halfHeight - edgePadding);

        return new Vector2(Random.Range(-halfWidth, halfWidth), halfHeight + edgePadding);
    }

    Vector2 ResolveScreenDashDirection(Vector3 worldDashDirection)
    {
        worldDashDirection.y = 0f;
        if (worldDashDirection.sqrMagnitude <= 0.0001f)
            return Vector2.up;

        worldDashDirection.Normalize();
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return new Vector2(worldDashDirection.x, worldDashDirection.z).normalized;

        Vector3 cameraRight = mainCamera.transform.right;
        Vector3 cameraForward = mainCamera.transform.forward;
        cameraRight.y = 0f;
        cameraForward.y = 0f;

        if (cameraRight.sqrMagnitude > 0.0001f)
            cameraRight.Normalize();
        if (cameraForward.sqrMagnitude > 0.0001f)
            cameraForward.Normalize();

        Vector2 screenDirection = new(
            Vector3.Dot(worldDashDirection, cameraRight),
            Vector3.Dot(worldDashDirection, cameraForward));

        return screenDirection.sqrMagnitude > 0.0001f ? screenDirection.normalized : Vector2.up;
    }

    IEnumerator PlayRoutine(int lineCount, float activeDuration, float alphaMultiplier)
    {
        _canvasGroup.alpha = 1f;

        float elapsed = 0f;
        float totalDuration = activeDuration + fadeOutDuration;
        while (elapsed < totalDuration)
        {
            UpdateLines(lineCount, elapsed, activeDuration, alphaMultiplier);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        HideAllLines();
        _playRoutine = null;
    }

    void UpdateLines(int lineCount, float elapsed, float activeDuration, float alphaMultiplier)
    {
        float safeFlashDuration = Mathf.Max(0.001f, flashInDuration);
        float safeFadeDuration = Mathf.Max(0.001f, fadeOutDuration);
        float fadeOut = 1f - Mathf.Clamp01((elapsed - activeDuration) / safeFadeDuration);

        for (int i = 0; i < lineCount; i++)
        {
            LineState line = _lines[i];
            float localTime = elapsed - line.Delay;
            if (localTime < 0f)
            {
                line.Image.color = Color.clear;
                continue;
            }

            float travelDuration = Mathf.Max(0.001f, activeDuration * line.TravelDuration);
            float travelT = Mathf.Clamp01(localTime / travelDuration);
            float curveT = travelCurve != null ? travelCurve.Evaluate(travelT) : travelT;
            line.Rect.anchoredPosition = Vector2.LerpUnclamped(line.Start, line.End, curveT);

            Color color = Color.Lerp(flashColor, slowTrailColor, Mathf.Clamp01(localTime / activeDuration));
            float fadeIn = Mathf.Clamp01(localTime / safeFlashDuration);
            float travelFade = Mathf.Lerp(1f, 0.62f, travelT);
            color.a *= line.Alpha * alphaMultiplier * fadeIn * fadeOut * travelFade;
            line.Image.color = color;
        }
    }

    void HideAllLines()
    {
        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;

        for (int i = 0; i < _lines.Count; i++)
        {
            LineState line = _lines[i];
            if (line.Image != null)
                line.Image.color = Color.clear;
            if (line.Rect != null)
                line.Rect.gameObject.SetActive(false);
        }
    }

    sealed class LineState
    {
        public readonly RectTransform Rect;
        public readonly Image Image;
        public Vector2 Start;
        public Vector2 End;
        public float Delay;
        public float TravelDuration;
        public float Alpha;

        public LineState(RectTransform rect, Image image)
        {
            Rect = rect;
            Image = image;
        }
    }
}
