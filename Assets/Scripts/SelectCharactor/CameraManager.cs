using Unity.Cinemachine;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    [System.Serializable]
    public struct MenuCameraBinding
    {
        public BasementMenuDefinitionSO menu;
        public CinemachineCamera camera;
    }
    [SerializeField] private MenuCameraBinding[] menuCameras = System.Array.Empty<MenuCameraBinding>();
    [Tooltip("Character-local point used to keep the character framed during menu camera blends. Ignores imported model scale.")]
    [SerializeField] private Vector3 menuBlendLookAtOffset = new Vector3(0, 1.1f, 0);
    readonly System.Collections.Generic.Dictionary<CinemachineCamera, BasementMenuBlendTarget> blendTargets = new();
    BasementMenuDefinitionSO activeMenu;
    LensSettings savedPresentationLens;
    bool appliedPreset;
    CinemachineBlenderSettings savedBlends;
    CinemachineBlenderSettings sessionBlends;
    public BasementContext bct;
    public CinemachineCamera StadeSelectCamera;
    public CinemachineCamera CharacterSelectCamera;
    [Header("Accessory presentation (baked from the reference FBX)")]
    [SerializeField] private CinemachineCamera accessoryCamera;
    [SerializeField] private CinemachineBrain presentationBrain;
    [SerializeField] private Vector3 accessoryCameraOffset;
    [SerializeField] private Vector3 accessoryCameraEuler;
    [SerializeField, Min(0f)] private float accessoryBlendDuration = 0.8f;
    [Header("Gun menu presentation (baked from the reference FBX)")]
    [SerializeField] private CinemachineCamera gunMenuCamera;
    [SerializeField] private Vector3 gunMenuCameraOffset;
    [SerializeField] private Vector3 gunMenuCameraEuler;
    [SerializeField, Min(0f)] private float gunMenuBlendDuration = 0.8f;

    enum PresentationView { None, Accessory, GunMenu, Menu }
    PresentationView activeView;
    Transform presentationTarget;
    CinemachineCamera activeCamera;
    Vector3 cameraOffset;
    Vector3 cameraEuler;
    float blendDuration;
    bool returning;
    float transitionUntil;
    PrioritySettings savedPriority;
    CinemachineBrain.LensModeOverrideSettings savedLensMode;
    Rect savedViewport;
    Rect transitionStartViewport;
    Rect returnStartViewport;
    Rect presentationViewport = new Rect(0, 0, 1, 1);
    LensSettings savedCharacterLens;
    LensSettings savedOutputLens;
    bool savedIgnoreTimeScale;
    bool convertedCharacterLens;
    readonly System.Collections.Generic.Dictionary<CinemachineCamera, (PrioritySettings priority, LensSettings lens)> menuCameraStates = new();
    bool HasPresentation => activeView != PresentationView.None;
    bool IsTransitioning => HasPresentation &&
        (Time.unscaledTime < transitionUntil || (presentationBrain != null && presentationBrain.IsBlending));
    public Rect AccessoryViewport => presentationViewport;
    public bool IsAccessoryTransitioning => activeView == PresentationView.Accessory && IsTransitioning;
    public Rect GunMenuViewport => presentationViewport;
    public bool IsGunMenuTransitioning => activeView == PresentationView.GunMenu && IsTransitioning;
    public bool IsGunMenuViewActive => activeView == PresentationView.GunMenu;
    public bool IsMenuTransitioning => IsTransitioning;
    public Rect MenuViewport => presentationViewport;
    public bool IsMenuViewActive(BasementMenuDefinitionSO menu) => HasPresentation && !returning && activeMenu == menu;
    public float GetMenuRevealProgress(float duration) => GetRevealProgress(duration);

    public void RegisterMenu(BasementMenuDefinitionSO menu, CinemachineCamera camera)
    {
        for (int i = 0; i < menuCameras.Length; i++)
            if (menuCameras[i].menu == menu)
            {
                menuCameras[i].camera = camera;
                return;
            }
        System.Array.Resize(ref menuCameras, menuCameras.Length + 1);
        menuCameras[menuCameras.Length - 1] = new MenuCameraBinding { menu = menu, camera = camera };
    }

    public bool BeginMenuView(BasementMenuDefinitionSO menu, Transform target, BasementMenuCameraPresetSO preset)
    {
        if (bct != null && bct.SupplyRoom != null && bct.SupplyRoom.IsOpen) return false;
        if (menu == null || preset == null) return false;
        CinemachineCamera camera = null;
        foreach (var binding in menuCameras)
            if (binding.menu == menu)
            {
                if (camera != null) { Debug.LogWarning("[Basement Menu] Duplicate camera binding.", this); return false; }
                camera = binding.camera;
            }
        var view = camera == accessoryCamera ? PresentationView.Accessory :
            camera == gunMenuCamera ? PresentationView.GunMenu : PresentationView.Menu;
        if (!BeginPresentation(view, target, camera, preset.position, preset.rotation.eulerAngles, menu.blendDuration, preset))
            return false;
        activeMenu = menu;
        return true;
    }

    public void EndMenuView(bool immediate = false) => EndPresentation(immediate);

    // Change the shot inside the same session; the formation snapshot stays intact.
    public bool SwitchMenuView(BasementMenuDefinitionSO menu, BasementMenuCameraPresetSO preset)
    {
        if (!HasPresentation || returning || !isActiveAndEnabled || menu == null || preset == null) return false;
        CinemachineCamera next = null;
        foreach (var binding in menuCameras)
            if (binding.menu == menu)
            {
                if (next != null) return false;
                next = binding.camera;
            }
        if (next == null || !next.isActiveAndEnabled || presentationTarget == null) return false;
        if (!menuCameraStates.ContainsKey(next)) menuCameraStates.Add(next, (next.Priority, next.Lens));
        string previousName = activeCamera.name;
        transitionStartViewport = presentationViewport;
        activeCamera.Priority = savedPriority;
        activeCamera = next;
        savedPriority = menuCameraStates[next].priority;
        activeCamera.Lens = preset.lens;
        activeMenu = menu;
        activeView = next == accessoryCamera ? PresentationView.Accessory :
            next == gunMenuCamera ? PresentationView.GunMenu : PresentationView.Menu;
        cameraOffset = preset.position;
        cameraEuler = preset.rotation.eulerAngles;
        blendDuration = menu.blendDuration;
        if (sessionBlends != null)
        {
            var rules = new System.Collections.Generic.List<CinemachineBlenderSettings.CustomBlend>(sessionBlends.CustomBlends);
            rules.RemoveAll(r => r.From == previousName && r.To == next.name);
            rules.Insert(0, new CinemachineBlenderSettings.CustomBlend {
                From = previousName, To = next.name,
                Blend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendDuration) });
            sessionBlends.CustomBlends = rules.ToArray();
        }
        PositionPresentationCamera();
        TrackMenuBlend(next);
        activeCamera.Priority = Mathf.Max(CharacterSelectCamera.Priority.Value, StadeSelectCamera.Priority.Value) + 10;
        transitionUntil = Time.unscaledTime + blendDuration;
        return true;
    }

    public float GetAccessoryRevealProgress(float duration) =>
        activeView == PresentationView.Accessory ? GetRevealProgress(duration) : 0f;
    public float GetGunMenuRevealProgress(float duration) =>
        activeView == PresentationView.GunMenu ? GetRevealProgress(duration) : 0f;

    float GetRevealProgress(float duration)
    {
        if (!HasPresentation || returning || presentationBrain == null) return 0f;
        var blend = presentationBrain.ActiveBlend;
        if (blend != null && blend.CamB == activeCamera)
        {
            float fadeTime = Mathf.Min(Mathf.Max(0f, duration), blend.Duration);
            return fadeTime > 0f
                ? Mathf.Clamp01((blend.TimeInBlend - (blend.Duration - fadeTime)) / fadeTime)
                : (blend.IsComplete ? 1f : 0f);
        }
        return presentationBrain.ActiveVirtualCamera == activeCamera ? 1f : 0f;
    }

    private bool _isCharacterCamActive = false;
    public bool _inMapUI = false;
    public bool CamareInMapselect => _isCharacterCamActive;

    private void Awake()
    {
        if (CharacterSelectCamera != null) CharacterSelectCamera.Priority = 20;
    }

    public void MobilizClick()
    {
        if (bct != null && bct.SupplyRoom != null && bct.SupplyRoom.IsOpen) return;
        if (HasPresentation) return;
        if (!_inMapUI)
        {
            CharacterSelectCamera.Priority = 10;
            StadeSelectCamera.Priority = 20;
            bct.uiBaseMentManager.FadeOut();
            Debug.Log("FadeOutToolbar");
            _inMapUI = true;
        }
        else
        {
            bct.sceneLoader.LoadGame();
            Debug.Log("Playgame");
        }
    }

    public void BackClick()
    {
        if (bct != null && bct.SupplyRoom != null && bct.SupplyRoom.IsOpen) return;
        if (HasPresentation) return;
        if (_inMapUI)
        {
            CharacterSelectCamera.Priority = 20;
            StadeSelectCamera.Priority = 10;
            bct.uiBaseMentManager.FadeIn();
            Debug.Log("FadeinToolbar");
            _inMapUI = false;
        }
    }

    public bool BeginAccessoryView(Transform target) => BeginLegacyView(target, accessoryCamera, PresentationView.Accessory,
        accessoryCameraOffset, accessoryCameraEuler, accessoryBlendDuration);
    public bool BeginGunMenuView(Transform target) => BeginLegacyView(target, gunMenuCamera, PresentationView.GunMenu,
        gunMenuCameraOffset, gunMenuCameraEuler, gunMenuBlendDuration);

    bool BeginLegacyView(Transform target, CinemachineCamera camera, PresentationView view, Vector3 offset, Vector3 euler, float duration)
    {
        foreach (var binding in menuCameras)
            if (binding.camera == camera && binding.menu != null)
                return BeginMenuView(binding.menu, target, binding.menu.defaultCamera);
        return BeginPresentation(view, target, camera, offset, euler, duration);
    }

    bool BeginPresentation(PresentationView view, Transform target, CinemachineCamera camera,
        Vector3 offset, Vector3 euler, float duration, BasementMenuCameraPresetSO preset = null)
    {
        if (!isActiveAndEnabled || HasPresentation || _inMapUI || target == null || camera == null ||
            !camera.isActiveAndEnabled || CharacterSelectCamera == null || StadeSelectCamera == null ||
            presentationBrain == null || presentationBrain.OutputCamera == null) return false;
        activeView = view;
        returning = false;
        presentationTarget = target;
        activeCamera = camera;
        cameraOffset = offset;
        cameraEuler = euler;
        blendDuration = duration;
        savedPriority = camera.Priority;
        savedLensMode = presentationBrain.LensModeOverride;
        savedViewport = presentationBrain.OutputCamera.rect;
        transitionStartViewport = savedViewport;
        presentationViewport = savedViewport;
        savedOutputLens = LensSettings.FromCamera(presentationBrain.OutputCamera);
        savedCharacterLens = CharacterSelectCamera.Lens;
        savedIgnoreTimeScale = presentationBrain.IgnoreTimeScale;
        presentationBrain.IgnoreTimeScale = true;
        savedPresentationLens = camera.Lens;
        menuCameraStates.Clear();
        menuCameraStates.Add(camera, (camera.Priority, camera.Lens));
        appliedPreset = preset != null;
        if (appliedPreset)
        {
            camera.Lens = preset.lens;
            InstallMenuBlends(camera, duration);
        }
        // Keep the same physical projection throughout the blend to avoid a midpoint pop.
        convertedCharacterLens = !savedCharacterLens.IsPhysicalCamera && camera.Lens.IsPhysicalCamera;
        UpdateCharacterLens();
        var mode = savedLensMode;
        mode.Enabled = true;
        presentationBrain.LensModeOverride = mode;
        PositionPresentationCamera();
        TrackMenuBlend(CharacterSelectCamera);
        TrackMenuBlend(camera);
        camera.Priority = Mathf.Max(CharacterSelectCamera.Priority.Value, StadeSelectCamera.Priority.Value) + 10;
        transitionUntil = Time.unscaledTime + duration;
        return true;
    }

    void InstallMenuBlends(CinemachineCamera camera, float duration)
    {
        savedBlends = presentationBrain.CustomBlends;
        sessionBlends = ScriptableObject.CreateInstance<CinemachineBlenderSettings>();
        sessionBlends.hideFlags = HideFlags.HideAndDontSave;
        var rules = new System.Collections.Generic.List<CinemachineBlenderSettings.CustomBlend>();
        var blend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, Mathf.Max(0, duration));
        rules.Add(new CinemachineBlenderSettings.CustomBlend { From = CharacterSelectCamera.name, To = camera.name, Blend = blend });
        rules.Add(new CinemachineBlenderSettings.CustomBlend { From = camera.name, To = CharacterSelectCamera.name, Blend = blend });
        if (savedBlends != null && savedBlends.CustomBlends != null) rules.AddRange(savedBlends.CustomBlends);
        sessionBlends.CustomBlends = rules.ToArray();
        presentationBrain.CustomBlends = sessionBlends;
    }

    void TrackMenuBlend(CinemachineCamera camera)
    {
        if (!blendTargets.TryGetValue(camera, out var extension))
        {
            extension = camera.gameObject.AddComponent<BasementMenuBlendTarget>();
            extension.hideFlags = HideFlags.HideAndDontSave;
            blendTargets.Add(camera, extension);
        }
        extension.Target = presentationTarget;
        extension.Offset = menuBlendLookAtOffset;
        extension.AuthoredOrientation = camera.transform.rotation;
    }

    void Update()
    {
        if (!HasPresentation) return;
        if (activeCamera == null || presentationBrain == null || presentationBrain.OutputCamera == null)
        {
            RestorePresentationCamera();
            return;
        }
        if (returning)
        {
            if (!IsTransitioning) RestorePresentationCamera();
            else UpdateViewport();
            return;
        }
        if (presentationTarget == null) { EndPresentation(); return; }
        PositionPresentationCamera();
        UpdateViewport();
    }

    void PositionPresentationCamera()
    {
        // FBX import-axis rotation and 100x marker scale are not actor transforms.
        activeCamera.transform.SetPositionAndRotation(
            presentationTarget.position + presentationTarget.rotation * cameraOffset,
            presentationTarget.rotation * Quaternion.Euler(cameraEuler));
        if (blendTargets.TryGetValue(activeCamera, out var extension))
            extension.AuthoredOrientation = activeCamera.transform.rotation;
    }

    void UpdateViewport()
    {
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        const float referenceAspect = 16f / 9f;
        float width = Mathf.Min(1f, referenceAspect / aspect);
        float height = Mathf.Min(1f, aspect / referenceAspect);
        Rect target = new Rect((1 - width) * 0.5f, (1 - height) * 0.5f, width, height);
        var blend = presentationBrain.ActiveBlend;
        float t = blend != null ? blend.BlendWeight : (IsTransitioning ? 0f : 1f);
        Rect from = returning ? returnStartViewport : transitionStartViewport;
        Rect to = returning ? savedViewport : target;
        presentationViewport = new Rect(Vector2.Lerp(from.position, to.position, t), Vector2.Lerp(from.size, to.size, t));
        presentationBrain.OutputCamera.rect = presentationViewport;
        UpdateCharacterLens();
    }

    void UpdateCharacterLens()
    {
        if (!convertedCharacterLens || CharacterSelectCamera == null) return;
        var lens = savedCharacterLens;
        lens.ModeOverride = LensSettings.OverrideModes.Physical;
        lens.PhysicalProperties = activeCamera.Lens.PhysicalProperties;
        float aspect = presentationBrain.OutputCamera.aspect;
        float sensorAspect = lens.PhysicalProperties.SensorSize.x / lens.PhysicalProperties.SensorSize.y;
        float scale = lens.PhysicalProperties.GateFit == Camera.GateFitMode.Horizontal ? aspect / sensorAspect : 1f;
        lens.FieldOfView = 2f * Mathf.Atan(Mathf.Tan(savedCharacterLens.FieldOfView * Mathf.Deg2Rad * 0.5f) * scale) * Mathf.Rad2Deg;
        CharacterSelectCamera.Lens = lens;
    }

    public void EndAccessoryView(bool immediate = false)
    {
        if (activeView == PresentationView.Accessory) EndPresentation(immediate);
    }
    public void EndGunMenuView(bool immediate = false)
    {
        if (activeView == PresentationView.GunMenu) EndPresentation(immediate);
    }

    void EndPresentation(bool immediate = false)
    {
        if (!HasPresentation) return;
        if (!returning)
        {
            returning = true;
            returnStartViewport = presentationBrain != null && presentationBrain.OutputCamera != null
                ? presentationBrain.OutputCamera.rect : presentationViewport;
            if (activeCamera != null) activeCamera.Priority = savedPriority;
            transitionUntil = Time.unscaledTime + blendDuration;
        }
        if (immediate) RestorePresentationCamera();
    }

    void RestorePresentationCamera()
    {
        foreach (var pair in blendTargets)
            if (pair.Value != null)
            {
                var extension = pair.Value;
                if (pair.Key != null) pair.Key.transform.rotation = extension.AuthoredOrientation;
                extension.Target = null;
                extension.enabled = false;
                if (Application.isPlaying) Destroy(extension);
                else DestroyImmediate(extension);
            }
        blendTargets.Clear();
        if (activeCamera != null) activeCamera.Priority = savedPriority;
        if (appliedPreset && activeCamera != null) activeCamera.Lens = savedPresentationLens;
        foreach (var state in menuCameraStates)
            if (state.Key != null) { state.Key.Priority = state.Value.priority; state.Key.Lens = state.Value.lens; }
        menuCameraStates.Clear();
        if (convertedCharacterLens && CharacterSelectCamera != null) CharacterSelectCamera.Lens = savedCharacterLens;
        if (presentationBrain != null)
        {
            if (sessionBlends != null) presentationBrain.CustomBlends = savedBlends;
            presentationBrain.LensModeOverride = savedLensMode;
            presentationBrain.IgnoreTimeScale = savedIgnoreTimeScale;
            if (presentationBrain.OutputCamera != null)
            {
                var output = presentationBrain.OutputCamera;
                output.rect = savedViewport;
                output.usePhysicalProperties = savedOutputLens.IsPhysicalCamera;
                output.sensorSize = savedOutputLens.PhysicalProperties.SensorSize;
                output.gateFit = savedOutputLens.PhysicalProperties.GateFit;
                output.lensShift = savedOutputLens.PhysicalProperties.LensShift;
                output.fieldOfView = savedCharacterLens.FieldOfView;
            }
        }
        if (sessionBlends != null)
        {
            if (Application.isPlaying) Destroy(sessionBlends);
            else DestroyImmediate(sessionBlends);
        }
        sessionBlends = savedBlends = null;
        appliedPreset = false;
        activeMenu = null;
        presentationViewport = savedViewport;
        convertedCharacterLens = false;
        presentationTarget = null;
        activeCamera = null;
        activeView = PresentationView.None;
        returning = false;
    }

    void OnDisable()
    {
        if (HasPresentation) RestorePresentationCamera();
    }
}
