using System;
using Unity.Cinemachine;
using UnityEngine;


public class CameraManager : MonoBehaviour
{
  public BasementContext bct;
  public CinemachineCamera StadeSelectCamera;
  public CinemachineCamera CharacterSelectCamera;
  [Header("Accessory presentation (baked from the reference FBX)")]
  [SerializeField] private CinemachineCamera accessoryCamera;
  [SerializeField] private CinemachineBrain presentationBrain;
  [SerializeField] private Vector3 accessoryCameraOffset;
  [SerializeField] private Vector3 accessoryCameraEuler;
  [SerializeField, Min(0f)] private float accessoryBlendDuration = 0.8f;

  Transform accessoryTarget;
  bool accessorySession;
  bool accessoryReturning;
  float accessoryTransitionUntil;
  PrioritySettings savedAccessoryPriority;
  CinemachineBrain.LensModeOverrideSettings savedLensMode;
  Rect savedViewport;
  Rect returnStartViewport;
  LensSettings savedCharacterLens;
  LensSettings savedOutputLens;
  bool savedIgnoreTimeScale;
  bool convertedCharacterLens;
  public Rect AccessoryViewport { get; private set; } = new Rect(0, 0, 1, 1);
  public bool IsAccessoryTransitioning => accessorySession &&
      (Time.unscaledTime < accessoryTransitionUntil || (presentationBrain != null && presentationBrain.IsBlending));

  public float GetAccessoryRevealProgress(float duration)
  {
    if (!accessorySession || accessoryReturning || presentationBrain == null) return 0f;
    var blend = presentationBrain.ActiveBlend;
    if (blend != null && blend.CamB == accessoryCamera)
    {
      float fadeTime = Mathf.Min(Mathf.Max(0f, duration), blend.Duration);
      return fadeTime > 0f
          ? Mathf.Clamp01((blend.TimeInBlend - (blend.Duration - fadeTime)) / fadeTime)
          : (blend.IsComplete ? 1f : 0f);
    }
    return presentationBrain.ActiveVirtualCamera == accessoryCamera ? 1f : 0f;
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
    if (accessorySession) return;
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
    if (accessorySession) return;
    if (_inMapUI)
    {
      CharacterSelectCamera.Priority = 20;
      StadeSelectCamera.Priority = 10;
      bct.uiBaseMentManager.FadeIn();
      Debug.Log("FadeinToolbar");
      _inMapUI = false;
    }
  }
  public bool BeginAccessoryView(Transform target)
  {
    if (accessorySession || _inMapUI || target == null || accessoryCamera == null ||
        presentationBrain == null || presentationBrain.OutputCamera == null) return false;
    accessorySession = true;
    accessoryReturning = false;
    accessoryTarget = target;
    savedAccessoryPriority = accessoryCamera.Priority;
    savedLensMode = presentationBrain.LensModeOverride;
    savedViewport = presentationBrain.OutputCamera.rect;
    AccessoryViewport = savedViewport;
    savedOutputLens = LensSettings.FromCamera(presentationBrain.OutputCamera);
    savedCharacterLens = CharacterSelectCamera.Lens;
    savedIgnoreTimeScale = presentationBrain.IgnoreTimeScale;
    presentationBrain.IgnoreTimeScale = true;
    // Lens mode and gate fit are discrete in Cinemachine (they switch at 50%).
    // Use an equivalent physical source lens throughout the blend to avoid a projection pop.
    convertedCharacterLens = !savedCharacterLens.IsPhysicalCamera && accessoryCamera.Lens.IsPhysicalCamera;
    UpdateCharacterLens();
    var mode = savedLensMode;
    mode.Enabled = true;
    presentationBrain.LensModeOverride = mode;
    PositionAccessoryCamera();
    accessoryCamera.Priority = Mathf.Max(CharacterSelectCamera.Priority.Value, StadeSelectCamera.Priority.Value) + 10;
    accessoryTransitionUntil = Time.unscaledTime + accessoryBlendDuration;
    return true;
  }

  void Update()
  {
    if (!accessorySession) return;
    if (accessoryReturning)
    {
      if (!IsAccessoryTransitioning) RestoreAccessoryCamera();
      else UpdateViewport();
      return;
    }
    if (accessoryTarget == null) { EndAccessoryView(); return; }
    PositionAccessoryCamera();
    UpdateViewport();
  }

  void PositionAccessoryCamera()
  {
    // Use the presentation root's orientation, not the FBX marker's import-axis rotation.
    accessoryCamera.transform.SetPositionAndRotation(
        accessoryTarget.position + accessoryTarget.rotation * accessoryCameraOffset,
        accessoryTarget.rotation * Quaternion.Euler(accessoryCameraEuler));
  }

  void UpdateViewport()
  {
    float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
    const float referenceAspect = 16f / 9f;
    float width = Mathf.Min(1f, referenceAspect / aspect);
    float height = Mathf.Min(1f, aspect / referenceAspect);
    Rect target = new Rect((1 - width) * 0.5f, (1 - height) * 0.5f, width, height);
    var blend = presentationBrain.ActiveBlend;
    float t = blend != null ? blend.BlendWeight : (IsAccessoryTransitioning ? 0f : 1f);
    Rect from = accessoryReturning ? returnStartViewport : savedViewport;
    Rect to = accessoryReturning ? savedViewport : target;
    AccessoryViewport = new Rect(Vector2.Lerp(from.position, to.position, t), Vector2.Lerp(from.size, to.size, t));
    presentationBrain.OutputCamera.rect = AccessoryViewport;
    UpdateCharacterLens();
  }

  void UpdateCharacterLens()
  {
    if (!convertedCharacterLens) return;
    var lens = savedCharacterLens;
    lens.ModeOverride = LensSettings.OverrideModes.Physical;
    lens.PhysicalProperties = accessoryCamera.Lens.PhysicalProperties;
    // Match the original perspective vertical FOV, including while the viewport fits to 16:9.
    float aspect = presentationBrain.OutputCamera.aspect;
    float sensorAspect = lens.PhysicalProperties.SensorSize.x / lens.PhysicalProperties.SensorSize.y;
    float scale = lens.PhysicalProperties.GateFit == Camera.GateFitMode.Horizontal ? aspect / sensorAspect : 1f;
    lens.FieldOfView = 2f * Mathf.Atan(Mathf.Tan(savedCharacterLens.FieldOfView * Mathf.Deg2Rad * 0.5f) * scale) * Mathf.Rad2Deg;
    CharacterSelectCamera.Lens = lens;
  }

  public void EndAccessoryView(bool immediate = false)
  {
    if (!accessorySession) return;
    if (!accessoryReturning)
    {
      accessoryReturning = true;
      returnStartViewport = presentationBrain != null && presentationBrain.OutputCamera != null
          ? presentationBrain.OutputCamera.rect : AccessoryViewport;
      if (accessoryCamera != null) accessoryCamera.Priority = savedAccessoryPriority;
      accessoryTransitionUntil = Time.unscaledTime + accessoryBlendDuration;
    }
    if (immediate) RestoreAccessoryCamera();
  }

  void RestoreAccessoryCamera()
  {
    if (accessoryCamera != null) accessoryCamera.Priority = savedAccessoryPriority;
    if (convertedCharacterLens && CharacterSelectCamera != null) CharacterSelectCamera.Lens = savedCharacterLens;
    if (presentationBrain != null)
    {
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
    AccessoryViewport = savedViewport;
    convertedCharacterLens = false;
    accessoryTarget = null;
    accessorySession = accessoryReturning = false;
  }

  void OnDisable()
  {
    if (accessorySession) RestoreAccessoryCamera();
  }
}
