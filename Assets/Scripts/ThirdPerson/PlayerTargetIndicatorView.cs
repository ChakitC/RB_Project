using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerTargetIndicatorView : MonoBehaviour
{
    RectTransform canvasRect;
    RectTransform indicator;
    PlayerContext player;
    PlayerTargetingController targeting;

    public static PlayerTargetIndicatorView EnsureExists()
    {
        PlayerTargetIndicatorView existing = FindFirstObjectByType<PlayerTargetIndicatorView>(
            FindObjectsInactive.Include);
        if (existing != null)
        {
            if (!existing.gameObject.activeSelf)
                existing.gameObject.SetActive(true);
            return existing;
        }

        GameObject root = new GameObject("Player Target Indicator", typeof(RectTransform));
        DontDestroyOnLoad(root);
        return root.AddComponent<PlayerTargetIndicatorView>();
    }

    void Awake()
    {
        BuildUi();
    }

    void OnEnable()
    {
        ResolveBinding();
    }

    void OnDisable()
    {
        BindTargeting(null);
        SetVisible(false);
    }

    void Update()
    {
        if (player != PlayerContext.Instance || targeting == null)
            ResolveBinding();
    }

    void BuildUi()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 510;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRect = transform as RectTransform;
        GameObject arrow = new GameObject("Target Arrow", typeof(RectTransform), typeof(Text));
        indicator = arrow.GetComponent<RectTransform>();
        indicator.SetParent(transform, false);
        indicator.anchorMin = indicator.anchorMax = new Vector2(0.5f, 0.5f);
        indicator.pivot = new Vector2(0.5f, 0f);
        indicator.sizeDelta = new Vector2(48f, 48f);

        Text text = arrow.GetComponent<Text>();
        text.raycastTarget = false;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 36;
        text.fontStyle = FontStyle.Bold;
        text.color = new Color(1f, 0.78f, 0.1f, 1f);
        text.text = "▼";
        SetVisible(false);
    }

    void ResolveBinding()
    {
        player = PlayerContext.Instance;
        player?.ResolveReferences();
        BindTargeting(player != null ? player.Targeting : null);
    }

    void BindTargeting(PlayerTargetingController next)
    {
        if (targeting == next)
            return;

        if (targeting != null)
        {
            targeting.TargetChanged -= OnTargetChanged;
            targeting.SelectionCommitted -= OnSelectionCommitted;
        }

        targeting = next;
        if (targeting != null)
        {
            targeting.TargetChanged += OnTargetChanged;
            targeting.SelectionCommitted += OnSelectionCommitted;
        }

        RefreshPosition();
    }

    void OnTargetChanged(CharacteContext previous, CharacteContext current)
    {
        RefreshPosition();
    }

    void OnSelectionCommitted()
    {
        RefreshPosition();
    }

    void RefreshPosition()
    {
        if (targeting == null || canvasRect == null ||
            !targeting.TryGetIndicatorScreenPoint(out Vector2 screenPoint) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPoint,
                null,
                out Vector2 localPoint))
        {
            SetVisible(false);
            return;
        }

        indicator.anchoredPosition = localPoint;
        SetVisible(true);
    }

    void SetVisible(bool visible)
    {
        if (indicator != null && indicator.gameObject.activeSelf != visible)
            indicator.gameObject.SetActive(visible);
    }
}
