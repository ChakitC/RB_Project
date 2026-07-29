using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ThirdPersonReticleView : MonoBehaviour
{
    const float MinimumGapPixels = 8f;
    const float MaximumGapPixels = 90f;

    RectTransform reticleRoot;
    RectTransform[] arms;
    Image centerDot;
    RectTransform hitMarkerRoot;
    Image blockedMarker;
    float hitMarkerUntil;

    PlayerContext playerContext;
    ThirdPersonAimController aimController;
    CombatEventBus subscribedBus;

    public static ThirdPersonReticleView EnsureExists()
    {
        ThirdPersonReticleView existing = FindAnyObjectByType<ThirdPersonReticleView>();
        if (existing != null)
            return existing;

        GameObject root = new("Third Person Reticle");
        DontDestroyOnLoad(root);
        return root.AddComponent<ThirdPersonReticleView>();
    }

    void Awake()
    {
        BuildUi();
    }

    void OnDisable()
    {
        SubscribeToBus(null);
    }

    void Update()
    {
        ResolveReferences();
        UpdateReticle();
    }

    void BuildUi()
    {
        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        reticleRoot = CreateRect("Reticle", transform, Vector2.zero, new Vector2(160f, 160f));
        arms = new RectTransform[4];
        arms[0] = CreateGraphic("Top", reticleRoot, new Vector2(2f, 10f));
        arms[1] = CreateGraphic("Bottom", reticleRoot, new Vector2(2f, 10f));
        arms[2] = CreateGraphic("Left", reticleRoot, new Vector2(10f, 2f));
        arms[3] = CreateGraphic("Right", reticleRoot, new Vector2(10f, 2f));

        centerDot = CreateGraphic("Dot", reticleRoot, new Vector2(3f, 3f)).GetComponent<Image>();
        hitMarkerRoot = CreateRect(
            "Hit Marker",
            reticleRoot,
            Vector2.zero,
            new Vector2(32f, 32f));
        Vector2[] markerPositions =
        {
            new(-6f, 6f),
            new(6f, 6f),
            new(-6f, -6f),
            new(6f, -6f)
        };
        float[] markerAngles = { -45f, 45f, 45f, -45f };
        for (int i = 0; i < markerPositions.Length; i++)
        {
            RectTransform marker = CreateGraphic(
                $"Hit Marker Arm {i + 1}",
                hitMarkerRoot,
                new Vector2(2f, 8f));
            marker.anchoredPosition = markerPositions[i];
            marker.localRotation = Quaternion.Euler(0f, 0f, markerAngles[i]);
        }
        hitMarkerRoot.gameObject.SetActive(false);

        blockedMarker = CreateGraphic("Muzzle Blocked", reticleRoot, new Vector2(8f, 8f)).GetComponent<Image>();
        blockedMarker.color = new Color(1f, 0.38f, 0.08f, 0f);
    }

    void UpdateReticle()
    {
        if (reticleRoot == null)
            return;

        bool visible = GameplayCameraController.Instance == null ||
                       GameplayCameraController.Instance.GameplayInputEnabled;
        reticleRoot.gameObject.SetActive(visible);
        if (!visible)
            return;

        WeaponSystem weapon = playerContext != null ? playerContext.WeaponSystem : null;
        float spread = weapon != null ? weapon.CurrentSpreadDegrees : 0f;
        float gap = SpreadDegreesToPixels(spread);

        arms[0].anchoredPosition = new Vector2(0f, gap);
        arms[1].anchoredPosition = new Vector2(0f, -gap);
        arms[2].anchoredPosition = new Vector2(-gap, 0f);
        arms[3].anchoredPosition = new Vector2(gap, 0f);

        bool muzzleBlocked = aimController != null && aimController.IsMuzzleBlocked;
        Color normalColor = muzzleBlocked
            ? new Color(1f, 0.38f, 0.08f, 0.95f)
            : weapon != null && weapon.IsAiming
                ? new Color(0.65f, 0.92f, 1f, 0.95f)
                : new Color(1f, 1f, 1f, 0.9f);

        for (int i = 0; i < arms.Length; i++)
            arms[i].GetComponent<Image>().color = normalColor;
        centerDot.color = normalColor;

        blockedMarker.color = new Color(1f, 0.38f, 0.08f, muzzleBlocked ? 0.95f : 0f);
        hitMarkerRoot.gameObject.SetActive(Time.unscaledTime < hitMarkerUntil);
    }

    float SpreadDegreesToPixels(float spreadDegrees)
    {
        Camera gameplayCamera = Camera.main;
        float fov = gameplayCamera != null ? gameplayCamera.fieldOfView : 60f;
        float pixels = Mathf.Tan(spreadDegrees * Mathf.Deg2Rad) /
                       Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) *
                       Screen.height * 0.5f;
        return Mathf.Clamp(MinimumGapPixels + pixels, MinimumGapPixels, MaximumGapPixels);
    }

    void ResolveReferences()
    {
        if (playerContext == null)
            playerContext = PlayerContext.Instance;

        if (playerContext != null && aimController == null)
            aimController = playerContext.GetComponent<ThirdPersonAimController>();

        CombatEventBus nextBus = playerContext != null ? playerContext.CombatEventBus : null;
        SubscribeToBus(nextBus);
    }

    void SubscribeToBus(CombatEventBus nextBus)
    {
        if (subscribedBus == nextBus)
            return;

        if (subscribedBus != null)
            subscribedBus.EventPublished -= OnCombatEvent;

        subscribedBus = nextBus;
        if (subscribedBus != null)
            subscribedBus.EventPublished += OnCombatEvent;
    }

    void OnCombatEvent(PassiveEventContext context)
    {
        if (context.Type == PassiveEventType.Hit && context.Target != null)
            hitMarkerUntil = Time.unscaledTime + 0.12f;
    }

    static RectTransform CreateGraphic(string objectName, Transform parent, Vector2 size)
    {
        RectTransform rect = CreateRect(objectName, parent, Vector2.zero, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        return rect;
    }

    static RectTransform CreateRect(string objectName, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject child = new(objectName, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
