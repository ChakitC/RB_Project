using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class NpcPresentationController : MonoBehaviour
{
    enum PresentationPhase
    {
        Closed,
        Opening,
        Open,
        Closing
    }

    const float FadeToBlackDuration = 0.25f;
    const float FadeFromBlackDuration = 0.3f;
    const int PresentationCameraPriority = 1000;

    static NpcPresentationController instance;

    CinemachineCamera presentationCamera;
    CanvasGroup fadeCanvasGroup;
    PresentationPhase phase;
    GameObject activeUiSection;
    Action hideUi;
    Coroutine transitionRoutine;

    PlayerContext lockedPlayer;
    PlayerInput lockedPlayerInput;
    bool playerInputWasEnabled;
    PlayerMovementCC lockedMovement;
    bool movementWasEnabled;
    CharacterKnockbackMotor lockedKnockbackMotor;
    bool knockbackMotorWasEnabled;
    CharacterVerticalMotor lockedVerticalMotor;
    int gravitySuspendToken;
    CinemachineBrain[] presentationBrains;
    CinemachineBrain.LensModeOverrideSettings[] brainLensModeSnapshots;
    Camera[] brainOutputCameras;
    bool[] outputCameraWasPhysical;
    Renderer[] lockedPlayerRenderers;
    bool[] playerRendererWasEnabled;
    PlayerUIContext playerUiContext;
    bool playerUiWasActive;
    InteractionIndicatorPresenter interactionIndicator;
    bool interactionIndicatorWasEnabled;

    RectTransform activeUiRect;
    RectTransformSnapshot uiRectSnapshot;
    bool hasUiRectSnapshot;

    public static bool IsActive => instance != null && instance.phase != PresentationPhase.Closed;
    public static bool IsTransitioning => instance != null &&
        (instance.phase == PresentationPhase.Opening || instance.phase == PresentationPhase.Closing);

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildRuntimePresentationObjects();
    }

    void Update()
    {
        if (phase != PresentationPhase.Open)
            return;

        bool keyboardCancel = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool gamepadCancel = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
        if (keyboardCancel || gamepadCancel)
            BeginClose();
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;

        RestoreBrainLensModes();
        RestoreUiLayout();
        RestoreHud();
        RestorePlayerControl();
    }

    public static bool TryOpen(
        NpcPresentationTarget target,
        GameObject uiSection,
        Action showUi,
        Action hideUiAction)
    {
        if (target == null || uiSection == null || showUi == null || hideUiAction == null)
            return false;

        NpcPresentationController controller = EnsureInstance();
        if (controller.phase != PresentationPhase.Closed)
            return false;

        controller.transitionRoutine = controller.StartCoroutine(
            controller.OpenRoutine(target, uiSection, showUi, hideUiAction));
        return true;
    }

    public static bool TryClose(GameObject uiSection)
    {
        if (instance == null || instance.phase == PresentationPhase.Closed)
            return false;
        if (uiSection != null && instance.activeUiSection != uiSection)
            return false;

        instance.BeginClose();
        return true;
    }

    public static void RequestCloseActive()
    {
        if (instance != null)
            instance.BeginClose();
    }

    static NpcPresentationController EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<NpcPresentationController>(FindObjectsInactive.Include);
        if (instance != null)
            return instance;

        GameObject controllerObject = new("NPC Presentation Controller");
        return controllerObject.AddComponent<NpcPresentationController>();
    }

    IEnumerator OpenRoutine(
        NpcPresentationTarget target,
        GameObject uiSection,
        Action showUi,
        Action hideUiAction)
    {
        phase = PresentationPhase.Opening;
        activeUiSection = uiSection;
        hideUi = hideUiAction;

        LockPlayerControl();
        HideHud();
        yield return FadeTo(1f, FadeToBlackDuration);

        if (target == null || uiSection == null)
        {
            RestoreHud();
            RestorePlayerControl();
            yield return FadeTo(0f, FadeFromBlackDuration);
            phase = PresentationPhase.Closed;
            ClearActivePresentation();
            yield break;
        }

        ConfigurePresentationCamera(target);
        ApplyRightUiLayout(uiSection, target);
        showUi.Invoke();

        yield return null;
        yield return FadeTo(0f, FadeFromBlackDuration);

        phase = PresentationPhase.Open;
        transitionRoutine = null;
    }

    void BeginClose()
    {
        if (phase != PresentationPhase.Open)
            return;

        transitionRoutine = StartCoroutine(CloseRoutine());
    }

    IEnumerator CloseRoutine()
    {
        phase = PresentationPhase.Closing;
        yield return FadeTo(1f, FadeToBlackDuration);

        hideUi?.Invoke();
        if (presentationCamera != null)
            presentationCamera.gameObject.SetActive(false);
        RestoreBrainLensModes();
        ResetActiveCinemachineBrains();
        RestoreUiLayout();
        RestoreHud();

        yield return null;
        yield return FadeTo(0f, FadeFromBlackDuration);

        RestorePlayerControl();
        phase = PresentationPhase.Closed;
        ClearActivePresentation();
        transitionRoutine = null;
    }

    void BuildRuntimePresentationObjects()
    {
        GameObject cameraObject = new("NPC Presentation Camera");
        cameraObject.transform.SetParent(transform, false);
        presentationCamera = cameraObject.AddComponent<CinemachineCamera>();
        presentationCamera.Priority = PresentationCameraPriority;
        cameraObject.SetActive(false);

        GameObject canvasObject = new(
            "NPC Presentation Fade",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        fadeCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.interactable = false;
        fadeCanvasGroup.blocksRaycasts = false;

        GameObject imageObject = new("Black", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.SetParent(canvasObject.transform, false);
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
    }

    void ConfigurePresentationCamera(NpcPresentationTarget target)
    {
        target.GetCameraPose(out Vector3 position, out Quaternion rotation);
        presentationCamera.transform.SetPositionAndRotation(position, rotation);

        LensSettings lens = presentationCamera.Lens;
        lens.FieldOfView = target.FieldOfView;
        lens.ModeOverride = LensSettings.OverrideModes.Perspective;
        presentationCamera.Lens = lens;
        presentationCamera.Priority = PresentationCameraPriority;
        EnableBrainLensModeOverrides();
        presentationCamera.gameObject.SetActive(true);
        ResetActiveCinemachineBrains();
    }

    void EnableBrainLensModeOverrides()
    {
        int count = CinemachineBrain.ActiveBrainCount;
        presentationBrains = new CinemachineBrain[count];
        brainLensModeSnapshots = new CinemachineBrain.LensModeOverrideSettings[count];
        brainOutputCameras = new Camera[count];
        outputCameraWasPhysical = new bool[count];

        for (int i = 0; i < count; i++)
        {
            CinemachineBrain brain = CinemachineBrain.GetActiveBrain(i);
            presentationBrains[i] = brain;
            if (brain == null)
                continue;

            brainLensModeSnapshots[i] = brain.LensModeOverride;
            CinemachineBrain.LensModeOverrideSettings settings = brain.LensModeOverride;
            settings.Enabled = true;
            brain.LensModeOverride = settings;

            Camera outputCamera = brain.OutputCamera;
            brainOutputCameras[i] = outputCamera;
            outputCameraWasPhysical[i] = outputCamera != null && outputCamera.usePhysicalProperties;
        }
    }

    void RestoreBrainLensModes()
    {
        if (presentationBrains == null || brainLensModeSnapshots == null)
            return;

        int count = Mathf.Min(presentationBrains.Length, brainLensModeSnapshots.Length);
        for (int i = 0; i < count; i++)
        {
            CinemachineBrain brain = presentationBrains[i];
            if (brain != null)
                brain.LensModeOverride = brainLensModeSnapshots[i];

            if (brainOutputCameras != null && outputCameraWasPhysical != null &&
                i < brainOutputCameras.Length && i < outputCameraWasPhysical.Length &&
                brainOutputCameras[i] != null)
            {
                brainOutputCameras[i].usePhysicalProperties = outputCameraWasPhysical[i];
            }
        }

        presentationBrains = null;
        brainLensModeSnapshots = null;
        brainOutputCameras = null;
        outputCameraWasPhysical = null;
    }

    static void ResetActiveCinemachineBrains()
    {
        for (int i = 0; i < CinemachineBrain.ActiveBrainCount; i++)
            CinemachineBrain.GetActiveBrain(i)?.ResetState();
    }

    void LockPlayerControl()
    {
        lockedPlayer = PlayerContext.Instance;
        if (lockedPlayer == null)
            return;

        lockedPlayer.ResolveReferences();
        lockedPlayer.moveInput = Vector2.zero;
        lockedPlayer.lookInput = Vector2.zero;
        lockedPlayer.WeaponSystem?.OnAim(false);
        lockedPlayer.stateHub?.RequestCanceledFire();
        lockedPlayer.DashSystem?.CancelDash();

        lockedMovement = lockedPlayer.movement;
        if (lockedMovement != null)
        {
            movementWasEnabled = lockedMovement.enabled;
            lockedMovement.enabled = false;
        }

        lockedKnockbackMotor = lockedPlayer.KnockbackMotor;
        if (lockedKnockbackMotor != null)
        {
            knockbackMotorWasEnabled = lockedKnockbackMotor.enabled;
            lockedKnockbackMotor.enabled = false;
        }

        lockedVerticalMotor = lockedPlayer.VerticalMotor;
        if (lockedVerticalMotor != null)
            gravitySuspendToken = lockedVerticalMotor.AcquireGravitySuspendToken();

        HidePlayerVisuals();

        lockedPlayerInput = lockedPlayer.GetComponentInChildren<PlayerInput>(true);
        if (lockedPlayerInput != null)
        {
            playerInputWasEnabled = lockedPlayerInput.enabled;
            lockedPlayerInput.enabled = false;
        }
    }

    void RestorePlayerControl()
    {
        RestorePlayerVisuals();

        if (lockedVerticalMotor != null && gravitySuspendToken != 0)
            lockedVerticalMotor.ReleaseGravitySuspendToken(gravitySuspendToken);

        if (lockedKnockbackMotor != null)
            lockedKnockbackMotor.enabled = knockbackMotorWasEnabled;

        if (lockedMovement != null)
            lockedMovement.enabled = movementWasEnabled;

        if (lockedPlayerInput != null)
            lockedPlayerInput.enabled = playerInputWasEnabled;

        if (lockedPlayer != null)
        {
            lockedPlayer.moveInput = Vector2.zero;
            lockedPlayer.lookInput = Vector2.zero;
        }

        lockedPlayerInput = null;
        lockedPlayer = null;
        playerInputWasEnabled = false;
        lockedMovement = null;
        movementWasEnabled = false;
        lockedKnockbackMotor = null;
        knockbackMotorWasEnabled = false;
        lockedVerticalMotor = null;
        gravitySuspendToken = 0;
    }

    void HidePlayerVisuals()
    {
        lockedPlayerRenderers = lockedPlayer.GetComponentsInChildren<Renderer>(true);
        playerRendererWasEnabled = new bool[lockedPlayerRenderers.Length];

        for (int i = 0; i < lockedPlayerRenderers.Length; i++)
        {
            Renderer playerRenderer = lockedPlayerRenderers[i];
            if (playerRenderer == null)
                continue;

            playerRendererWasEnabled[i] = playerRenderer.enabled;
            playerRenderer.enabled = false;
        }
    }

    void RestorePlayerVisuals()
    {
        if (lockedPlayerRenderers == null || playerRendererWasEnabled == null)
            return;

        int count = Mathf.Min(lockedPlayerRenderers.Length, playerRendererWasEnabled.Length);
        for (int i = 0; i < count; i++)
        {
            Renderer playerRenderer = lockedPlayerRenderers[i];
            if (playerRenderer != null)
                playerRenderer.enabled = playerRendererWasEnabled[i];
        }

        lockedPlayerRenderers = null;
        playerRendererWasEnabled = null;
    }

    void HideHud()
    {
        playerUiContext = lockedPlayer != null ? lockedPlayer.playerUIContext : null;
        if (playerUiContext != null)
        {
            playerUiWasActive = playerUiContext.gameObject.activeSelf;
            playerUiContext.gameObject.SetActive(false);
        }

        interactionIndicator = FindFirstObjectByType<InteractionIndicatorPresenter>(FindObjectsInactive.Include);
        if (interactionIndicator != null)
        {
            interactionIndicatorWasEnabled = interactionIndicator.enabled;
            interactionIndicator.enabled = false;
        }
    }

    void RestoreHud()
    {
        if (playerUiContext != null)
            playerUiContext.gameObject.SetActive(playerUiWasActive);
        if (interactionIndicator != null)
            interactionIndicator.enabled = interactionIndicatorWasEnabled;

        playerUiContext = null;
        playerUiWasActive = false;
        interactionIndicator = null;
        interactionIndicatorWasEnabled = false;
    }

    void ApplyRightUiLayout(GameObject uiSection, NpcPresentationTarget target)
    {
        activeUiRect = uiSection.transform as RectTransform;
        if (activeUiRect == null)
            return;

        uiRectSnapshot = new RectTransformSnapshot(activeUiRect);
        hasUiRectSnapshot = true;

        Canvas canvas = uiSection.GetComponentInParent<Canvas>(true);
        float scaleFactor = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
        Rect safeArea = Screen.safeArea;
        float margin = target.UiMargin;
        float safeWidth = safeArea.width / scaleFactor;
        float safeHeight = safeArea.height / scaleFactor;

        activeUiRect.anchorMin = new Vector2(1f, 0.5f);
        activeUiRect.anchorMax = new Vector2(1f, 0.5f);
        activeUiRect.pivot = new Vector2(1f, 0.5f);
        activeUiRect.anchoredPosition = new Vector2(
            -((Screen.width - safeArea.xMax) / scaleFactor + margin),
            (safeArea.center.y - Screen.height * 0.5f) / scaleFactor);
        activeUiRect.sizeDelta = new Vector2(
            Mathf.Max(480f, safeWidth * target.UiWidthRatio - margin * 2f),
            Mathf.Min(uiRectSnapshot.SizeDelta.y, Mathf.Max(320f, safeHeight - margin * 2f)));
    }

    void RestoreUiLayout()
    {
        if (hasUiRectSnapshot && activeUiRect != null)
            uiRectSnapshot.Apply(activeUiRect);

        activeUiRect = null;
        hasUiRectSnapshot = false;
    }

    IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (fadeCanvasGroup == null)
            yield break;

        fadeCanvasGroup.blocksRaycasts = true;
        float startAlpha = fadeCanvasGroup.alpha;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.001f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        fadeCanvasGroup.alpha = targetAlpha;
        fadeCanvasGroup.blocksRaycasts = targetAlpha > 0.01f;
    }

    void ClearActivePresentation()
    {
        activeUiSection = null;
        hideUi = null;
    }

    readonly struct RectTransformSnapshot
    {
        public readonly Vector2 AnchorMin;
        public readonly Vector2 AnchorMax;
        public readonly Vector2 AnchoredPosition;
        public readonly Vector2 SizeDelta;
        public readonly Vector2 Pivot;

        public RectTransformSnapshot(RectTransform rect)
        {
            AnchorMin = rect.anchorMin;
            AnchorMax = rect.anchorMax;
            AnchoredPosition = rect.anchoredPosition;
            SizeDelta = rect.sizeDelta;
            Pivot = rect.pivot;
        }

        public void Apply(RectTransform rect)
        {
            rect.anchorMin = AnchorMin;
            rect.anchorMax = AnchorMax;
            rect.anchoredPosition = AnchoredPosition;
            rect.sizeDelta = SizeDelta;
            rect.pivot = Pivot;
        }
    }
}
