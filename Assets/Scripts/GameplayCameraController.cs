using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class GameplayCameraController : MonoBehaviour, IPartySpawnedReceiver
{
    const string AllyLayerName = "Ally";

    public static GameplayCameraController Instance { get; private set; }

    [Header("Legacy Target")]
    public Transform taget;
    public float smooth = 0.08f;
    public Vector3 offset;

    [Header("Third Person")]
    [SerializeField] private LayerMask cameraCollisionMask = ~0;
    [SerializeField, Min(0f), Tooltip("Radius around the camera-to-player capsule that fades blocking companions.")]
    private float companionFadeRadius = 0.3f;
    [SerializeField] private float initialPitch = 12f;
    [SerializeField] private float aimBlendSpeed = 10f;
    [SerializeField] private float recoilRecoverySpeed = 11f;
    [SerializeField] private float combatAlignmentDuration = 0.3f;

    [Header("Camera Impulse")]
    [SerializeField] private float shakeImpulsePerMarker = 0.55f;
    [SerializeField, Range(0f, 1f)] private float aimShakeMultiplier = 0.45f;

    Camera gameplayCamera;
    CinemachineBrain brain;
    CinemachineCamera virtualCamera;
    CinemachineThirdPersonFollow thirdPersonFollow;
    CinemachineThirdPersonAim thirdPersonAim;
    CinemachineImpulseSource impulseSource;
    CinemachineImpulseListener impulseListener;
    Transform cameraTarget;

    PlayerContext playerContext;
    ThirdPersonCharacterProfile profile;
    UIMunuBar pauseMenu;

    float yaw;
    float pitch;
    float recoilPitch;
    float recoilYaw;
    float aimBlend;
    float nextReferenceRefreshTime;
    float combatAlignmentUntil;
    bool cursorWasLocked;
    bool runtimeRigReady;

    readonly List<CharacterAnimBrain> subscribedBrains = new();
    FieldAllyManager fieldAllyManager;
    AllyHelperManager allyHelperManager;

    public bool GameplayInputEnabled =>
        isActiveAndEnabled &&
        !CutsceneDirector.IsCinematicPlaying &&
        !IsBlockingUiOpen();

    public float PlanarYaw => yaw;
    public float CompanionFadeRadius => Mathf.Max(0f, companionFadeRadius);
    public Vector3 PlanarForward =>
        Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
    public bool HasCombatAlignment =>
        Time.unscaledTime < combatAlignmentUntil ||
        (playerContext != null &&
         playerContext.WeaponSystem != null &&
         playerContext.WeaponSystem.IsAiming);

    void Awake()
    {
        Instance = this;
        ResolveMainCamera();
        EnsureRuntimeRig();
        ThirdPersonReticleView.EnsureExists();
    }

    void OnEnable()
    {
        Instance = this;
        ResolveMainCamera();
        EnsureRuntimeRig();
        SetRigEnabled(true);
        RefreshGameplayReferences(force: true);
    }

    void OnDisable()
    {
        UnsubscribeAll();
        SetRigEnabled(false);
        SetCursorLocked(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (cameraTarget != null)
            Destroy(cameraTarget.gameObject);
    }

    void LateUpdate()
    {
        RefreshGameplayReferences(force: false);
        if (taget == null || cameraTarget == null || virtualCamera == null)
            return;

        bool inputEnabled = GameplayInputEnabled;
        SetCursorLocked(inputEnabled);
        TickLookInput(inputEnabled);
        TickRecoil();
        TickCameraTarget();
        TickCameraProperties();
    }

    public void NotifyShotFired(float pitchKick, float yawKick)
    {
        float stabilityMultiplier = 1f;
        if (playerContext != null && playerContext.WeaponSystem != null)
            stabilityMultiplier = 1f - Mathf.Clamp01(playerContext.WeaponSystem.stability * 0.01f);

        recoilPitch += Mathf.Max(0f, pitchKick) * stabilityMultiplier;
        recoilYaw += Random.Range(-Mathf.Abs(yawKick), Mathf.Abs(yawKick)) * stabilityMultiplier;
        combatAlignmentUntil = Time.unscaledTime + combatAlignmentDuration;
    }

    public void RequestCombatAlignment(float duration = -1f)
    {
        combatAlignmentUntil = Time.unscaledTime +
            (duration >= 0f ? duration : combatAlignmentDuration);
    }

    public void AddImpulse(float strength)
    {
        if (impulseSource == null)
            return;

        bool aiming = playerContext != null &&
                      playerContext.WeaponSystem != null &&
                      playerContext.WeaponSystem.IsAiming;
        float multiplier = aiming ? aimShakeMultiplier : 1f;
        impulseSource.GenerateImpulseWithVelocity(
            new Vector3(0.12f, -0.18f, -0.08f) * strength * multiplier);
    }

    public void SetSensitivity(float x, float y)
    {
        ThirdPersonCameraSettings.SensitivityX = x;
        ThirdPersonCameraSettings.SensitivityY = y;
    }

    public void SetInvertY(bool value)
    {
        ThirdPersonCameraSettings.InvertY = value;
    }

    public void SetFieldOfView(float value)
    {
        ThirdPersonCameraSettings.FieldOfView = value;
    }

    public void PrepareParty(PartyRuntime party)
    {
        BindPlayer(party?.Player);
    }

    public void PartySpawned(PartyRuntime party)
    {
        BindPlayer(party?.Player);
    }

    void ResolveMainCamera()
    {
        if (gameplayCamera == null)
        {
            Camera[] cameras = GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].CompareTag("MainCamera"))
                {
                    gameplayCamera = cameras[i];
                    break;
                }
            }

            if (gameplayCamera == null && cameras.Length > 0)
                gameplayCamera = cameras[0];
        }

        if (gameplayCamera == null)
            gameplayCamera = Camera.main;
    }

    void EnsureRuntimeRig()
    {
        if (runtimeRigReady || gameplayCamera == null)
            return;

        brain = gameplayCamera.GetComponent<CinemachineBrain>();
        if (brain == null)
            brain = gameplayCamera.gameObject.AddComponent<CinemachineBrain>();
        brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
        brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
        brain.IgnoreTimeScale = true;
        brain.DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Styles.EaseInOut,
            0.18f);

        GameObject targetObject = new("TPS Camera Target");
        targetObject.hideFlags = HideFlags.DontSave;
        cameraTarget = targetObject.transform;

        GameObject cameraObject = new("TPS Cinemachine Camera");
        cameraObject.transform.SetParent(transform, false);
        virtualCamera = cameraObject.AddComponent<CinemachineCamera>();
        virtualCamera.Follow = cameraTarget;

        thirdPersonFollow = cameraObject.AddComponent<CinemachineThirdPersonFollow>();
        thirdPersonAim = cameraObject.AddComponent<CinemachineThirdPersonAim>();
        thirdPersonAim.AimCollisionFilter = cameraCollisionMask;
        thirdPersonAim.AimDistance = 250f;
        thirdPersonAim.NoiseCancellation = false;

        impulseListener = cameraObject.AddComponent<CinemachineImpulseListener>();
        impulseListener.ChannelMask = 1;
        impulseListener.Gain = 1f;
        impulseListener.UseCameraSpace = true;

        impulseSource = cameraObject.AddComponent<CinemachineImpulseSource>();

        if (gameplayCamera.GetComponent<ThirdPersonOcclusionFader>() == null)
            gameplayCamera.gameObject.AddComponent<ThirdPersonOcclusionFader>();

        CameraOcclusionCutoutFader oldCutout =
            gameplayCamera.GetComponent<CameraOcclusionCutoutFader>();
        if (oldCutout != null)
            oldCutout.enabled = false;

        yaw = taget != null ? taget.eulerAngles.y : transform.eulerAngles.y;
        pitch = initialPitch;
        runtimeRigReady = true;
        SetRigEnabled(isActiveAndEnabled);
    }

    void SetRigEnabled(bool value)
    {
        if (virtualCamera != null)
            virtualCamera.enabled = value;
        if (brain != null)
            brain.enabled = value;

        if (value && virtualCamera != null)
        {
            virtualCamera.PreviousStateIsValid = false;
            virtualCamera.ForceCameraPosition(
                gameplayCamera != null ? gameplayCamera.transform.position : transform.position,
                gameplayCamera != null ? gameplayCamera.transform.rotation : transform.rotation);
        }
    }

    void RefreshGameplayReferences(bool force)
    {
        if (!force && Time.unscaledTime < nextReferenceRefreshTime)
            return;

        nextReferenceRefreshTime = Time.unscaledTime + 0.5f;
        PlayerContext nextPlayer = PlayerContext.Instance;
        if (nextPlayer == null)
            return;

        BindPlayer(nextPlayer);
    }

    void BindPlayer(PlayerContext nextPlayer)
    {
        if (nextPlayer == null)
            return;

        bool changedPlayer = playerContext != nextPlayer;
        playerContext = nextPlayer;
        playerContext.ResolveReferences();
        taget = playerContext.transform;
        profile = playerContext.baseStats != null &&
                  playerContext.baseStats.thirdPersonProfile != null
            ? playerContext.baseStats.thirdPersonProfile
            : ThirdPersonCharacterProfile.CreateDefault();

        if (changedPlayer)
        {
            yaw = playerContext.transform.eulerAngles.y;
            pitch = initialPitch;
            SubscribeAll();
        }

        if (pauseMenu == null)
            pauseMenu = FindAnyObjectByType<UIMunuBar>(FindObjectsInactive.Include);
    }

    void TickLookInput(bool inputEnabled)
    {
        if (playerContext == null)
            return;

        Vector2 lookDelta = playerContext.lookInput;
        playerContext.lookInput = Vector2.zero;
        if (!inputEnabled)
            return;

        float invert = ThirdPersonCameraSettings.InvertY ? -1f : 1f;
        yaw += lookDelta.x *
               ThirdPersonCameraSettings.SensitivityX *
               profile.yawSensitivityMultiplier;
        pitch += lookDelta.y *
                 ThirdPersonCameraSettings.SensitivityY *
                 profile.pitchSensitivityMultiplier *
                 invert;
        pitch = Mathf.Clamp(pitch, profile.minimumPitch, profile.maximumPitch);
    }

    void TickRecoil()
    {
        float recovery = 1f - Mathf.Exp(-recoilRecoverySpeed * Time.unscaledDeltaTime);
        recoilPitch = Mathf.Lerp(recoilPitch, 0f, recovery);
        recoilYaw = Mathf.Lerp(recoilYaw, 0f, recovery);
    }

    /// <summary>
    /// Re-aligns the camera behind the player after something teleported or re-oriented them.
    /// <see cref="yaw"/> is otherwise only sampled from the player when the player reference itself
    /// changes, so a room warp — which faces the party into the new room, and every room carries its
    /// own yaw — would leave the camera pointing the way the previous room faced.
    /// </summary>
    public void SnapYawToPlayer()
    {
        if (playerContext == null)
            return;

        yaw = playerContext.transform.eulerAngles.y;

        // Apply immediately so the corrected angle is not one frame late behind the warp.
        if (taget != null && cameraTarget != null && virtualCamera != null)
            TickCameraTarget();
    }

    void TickCameraTarget()
    {
        Vector3 pivotPosition = taget.TransformPoint(profile.pivotOffset);
        cameraTarget.SetPositionAndRotation(
            pivotPosition,
            Quaternion.Euler(-(pitch + recoilPitch), yaw + recoilYaw, 0f));

        virtualCamera.transform.rotation = cameraTarget.rotation;
    }

    void TickCameraProperties()
    {
        bool aiming = playerContext != null &&
                      playerContext.WeaponSystem != null &&
                      playerContext.WeaponSystem.IsAiming;
        float blendTarget = aiming ? 1f : 0f;
        aimBlend = Mathf.MoveTowards(
            aimBlend,
            blendTarget,
            aimBlendSpeed * Time.unscaledDeltaTime);

        thirdPersonFollow.Damping = profile.followDamping;
        thirdPersonFollow.ShoulderOffset = profile.shoulderOffset;
        thirdPersonFollow.VerticalArmLength = profile.verticalArmLength;
        thirdPersonFollow.CameraSide = profile.cameraSide;
        thirdPersonFollow.CameraDistance = Mathf.Lerp(
            profile.cameraDistance,
            profile.aimCameraDistance,
            aimBlend);
        thirdPersonFollow.AvoidObstacles = new CinemachineThirdPersonFollow.ObstacleSettings
        {
            Enabled = true,
            CollisionFilter = ResolveCameraObstacleMask(),
            IgnoreTag = "Player",
            CameraRadius = profile.collisionRadius,
            DampingIntoCollision = 0.03f,
            DampingFromCollision = 0.22f
        };

        float baseFov = ThirdPersonCameraSettings.FieldOfView;
        float aimFov = Mathf.Clamp(
            baseFov - (profile.freeLookFov - profile.shoulderAimFov),
            20f,
            baseFov);
        LensSettings lens = virtualCamera.Lens;
        lens.FieldOfView = Mathf.Lerp(baseFov, aimFov, aimBlend);
        virtualCamera.Lens = lens;

        thirdPersonAim.AimCollisionFilter = cameraCollisionMask;
    }

    LayerMask ResolveCameraObstacleMask()
    {
        LayerMask obstacleMask = cameraCollisionMask;
        int allyLayer = LayerMask.NameToLayer(AllyLayerName);
        if (allyLayer >= 0)
            obstacleMask.value &= ~(1 << allyLayer);

        return obstacleMask;
    }

    bool IsBlockingUiOpen()
    {
        if (playerContext != null && playerContext.playerUIContext != null)
        {
            PlayerUIContext ui = playerContext.playerUIContext;
            if (ui.inventoryUI != null && ui.inventoryUI.activeInHierarchy)
                return true;
            if (ui.activeSkillScreen != null && ui.activeSkillScreen.gameObject.activeInHierarchy)
                return true;
        }

        return pauseMenu != null &&
               pauseMenu.menuBar != null &&
               pauseMenu.menuBar.activeInHierarchy;
    }

    void SetCursorLocked(bool shouldLock)
    {
        if (cursorWasLocked == shouldLock)
            return;

        cursorWasLocked = shouldLock;
        Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !shouldLock;
    }

    void SubscribeAll()
    {
        UnsubscribeAll();
        if (playerContext == null)
            return;

        if (playerContext.AnimBrain != null)
            SubscribeBrain(playerContext.AnimBrain);

        fieldAllyManager = playerContext.fieldAllyManager;
        if (fieldAllyManager != null)
        {
            fieldAllyManager.MemberRegistered += OnAllyRegistered;
            fieldAllyManager.MemberUnregistered += OnAllyUnregistered;
            foreach (FieldAllyMember member in fieldAllyManager.RegisteredMembers)
            {
                if (member != null && member.AnimBrainRef != null)
                    SubscribeBrain(member.AnimBrainRef);
            }
        }

        allyHelperManager = playerContext.allyHelper;
        if (allyHelperManager != null)
        {
            allyHelperManager.HelperAnimBrainChanged += OnHelperAnimBrainChanged;
            if (allyHelperManager.HelperAnimBrain != null)
                SubscribeBrain(allyHelperManager.HelperAnimBrain);
        }
    }

    void UnsubscribeAll()
    {
        for (int i = subscribedBrains.Count - 1; i >= 0; i--)
        {
            if (subscribedBrains[i] != null)
                subscribedBrains[i].SkillTimelineEventRaised -= OnSkillTimelineEvent;
        }
        subscribedBrains.Clear();

        if (fieldAllyManager != null)
        {
            fieldAllyManager.MemberRegistered -= OnAllyRegistered;
            fieldAllyManager.MemberUnregistered -= OnAllyUnregistered;
            fieldAllyManager = null;
        }

        if (allyHelperManager != null)
        {
            allyHelperManager.HelperAnimBrainChanged -= OnHelperAnimBrainChanged;
            allyHelperManager = null;
        }
    }

    void SubscribeBrain(CharacterAnimBrain brainToSubscribe)
    {
        if (brainToSubscribe == null || subscribedBrains.Contains(brainToSubscribe))
            return;

        brainToSubscribe.SkillTimelineEventRaised += OnSkillTimelineEvent;
        subscribedBrains.Add(brainToSubscribe);
    }

    void UnsubscribeBrain(CharacterAnimBrain brainToUnsubscribe)
    {
        if (brainToUnsubscribe == null)
            return;

        brainToUnsubscribe.SkillTimelineEventRaised -= OnSkillTimelineEvent;
        subscribedBrains.Remove(brainToUnsubscribe);
    }

    void OnSkillTimelineEvent(int requestId, CombatTimelineEventName eventName)
    {
        if (eventName == CombatTimelineEventName.ShakeCamera)
            AddImpulse(shakeImpulsePerMarker);
    }

    void OnAllyRegistered(ChainActorRole role, FieldAllyMember member)
    {
        if (member != null && member.AnimBrainRef != null)
            SubscribeBrain(member.AnimBrainRef);
    }

    void OnAllyUnregistered(ChainActorRole role)
    {
        for (int i = subscribedBrains.Count - 1; i >= 0; i--)
        {
            if (subscribedBrains[i] == null)
                subscribedBrains.RemoveAt(i);
        }
    }

    void OnHelperAnimBrainChanged(CharacterAnimBrain previous, CharacterAnimBrain next)
    {
        UnsubscribeBrain(previous);
        SubscribeBrain(next);
    }
}
