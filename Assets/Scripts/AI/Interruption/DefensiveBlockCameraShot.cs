using UnityEngine;
using Unity.Cinemachine;

// Scene-local presentation for the prototype's free camera. Gameplay/virtual cameras
// keep their own authority; this component never disables their drivers.
[DisallowMultipleComponent, RequireComponent(typeof(Camera)), DefaultExecutionOrder(1000)]
public sealed class DefensiveBlockCameraShot : MonoBehaviour
{
    [Header("Shot behind Player, facing the guard direction")]
    public Vector3 localPosition = new Vector3(2.65f, 1.55f, -1.91f);
    public Vector3 localEulerAngles = new Vector3(-0.35f, -35.39f, 10.49f);
    [Range(20f, 100f)] public float fieldOfView = 60f;
    [Header("Timing (unscaled seconds)")]
    [Min(0f)] public float blendInSeconds = 0.16f;
    [Min(0f)] public float holdAfterBlockSeconds = 0.12f;
    [Min(0f)] public float blendOutSeconds = 0.4f;
    [Header("World obstruction")]
    public LayerMask obstacleLayers = 1;
    [Min(0.01f)] public float cameraRadius = 0.15f;

    Camera output;
    CinemachineBrain brain;
    DefensiveBlockController defender;
    Transform protectedPlayer;
    bool observedArrival, hasSnapshot, returning;
    float elapsed, originalFov, startFov;
    Vector3 originalPosition, startPosition, anchorPosition;
    Quaternion originalRotation, startRotation, anchorRotation;
    public bool IsPlaying => hasSnapshot;

    void Awake()
    {
        output = GetComponent<Camera>();
        brain = GetComponent<CinemachineBrain>();
    }

    public void Bind(DefensiveBlockController actor)
    {
        Bind(actor, null);
    }

    public void Bind(DefensiveBlockController actor, Transform player)
    {
        RestoreImmediate();
        defender = actor;
        protectedPlayer = player;
        observedArrival = false;
    }

    bool CanOwnCamera() => output != null &&
        !CutsceneDirector.IsCinematicPlaying && !NpcPresentationController.IsActive &&
        (GameplayCameraController.Instance == null || !GameplayCameraController.Instance.isActiveAndEnabled ||
         GameplayCameraController.Instance.GameplayCamera != output) &&
        (brain == null || !brain.isActiveAndEnabled || brain.ActiveVirtualCamera == null);

    void LateUpdate()
    {
        if (GlobalTimeScaleManager.Instance.IsPaused) return;
        Tick(Time.unscaledDeltaTime);
    }

    void Tick(float deltaTime)
    {
        bool arrived = defender != null && defender.isActiveAndEnabled && defender.HasArrived;
        if (!arrived) observedArrival = false;
        if (output == null || !output.isActiveAndEnabled)
        {
            RestoreImmediate();
            observedArrival = arrived;
            return;
        }
        if (!CanOwnCamera())
        {
            // A cinematic/gameplay rig took over. Do not overwrite its camera pose or
            // recapture the same Block when that higher-priority owner releases it.
            hasSnapshot = false;
            observedArrival = arrived;
            return;
        }
        if (arrived && !observedArrival)
        {
            observedArrival = true;
            BeginShot();
        }
        if (!hasSnapshot) return;
        if (!arrived && !returning)
        {
            returning = true;
            elapsed = 0f;
            CaptureBlendStart();
        }
        elapsed += Mathf.Max(0f, deltaTime);
        float duration = returning ? blendOutSeconds : blendInSeconds;
        float time = returning ? Mathf.Max(0f, elapsed - holdAfterBlockSeconds) : elapsed;
        float t = duration <= 0f ? (returning && elapsed < holdAfterBlockSeconds ? 0f : 1f)
            : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration));
        Vector3 destination = returning ? originalPosition : anchorPosition + anchorRotation * localPosition;
        Quaternion rotation = returning ? originalRotation : anchorRotation * Quaternion.Euler(localEulerAngles);
        Vector3 position = Vector3.Lerp(startPosition, destination, t);
        if (!returning) position = AvoidObstruction(position);
        transform.SetPositionAndRotation(position, Quaternion.Slerp(startRotation, rotation, t));
        output.fieldOfView = Mathf.Lerp(startFov, returning ? originalFov : fieldOfView, t);
        if (returning && t >= 1f) RestoreImmediate();
    }

    void BeginShot()
    {
        if (!hasSnapshot)
        {
            originalPosition = transform.position;
            originalRotation = transform.rotation;
            originalFov = output.fieldOfView;
            hasSnapshot = true;
        }
        CaptureBlendStart();
        // Frame Player in the foreground with Aires ahead between Player and Rector.
        // Snapshot once: movement during the reaction must not drag this shot around.
        anchorPosition = protectedPlayer != null ? protectedPlayer.position : defender.transform.position;
        anchorRotation = Quaternion.Euler(0f, defender.transform.eulerAngles.y, 0f);
        returning = false;
        elapsed = 0f;
    }

    void CaptureBlendStart()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        startFov = output.fieldOfView;
    }

    Vector3 AvoidObstruction(Vector3 desired)
    {
        Vector3 origin = anchorPosition + Vector3.up * 1.2f;
        Vector3 delta = desired - origin;
        float distance = delta.magnitude;
        if (distance > 0.001f && Physics.SphereCast(origin, cameraRadius, delta / distance,
            out var hit, distance, obstacleLayers, QueryTriggerInteraction.Ignore))
            return origin + delta / distance * Mathf.Max(0f, hit.distance - 0.03f);
        return desired;
    }

    public void RestoreImmediate()
    {
        if (hasSnapshot && CanOwnCamera())
        {
            transform.SetPositionAndRotation(originalPosition, originalRotation);
            output.fieldOfView = originalFov;
        }
        hasSnapshot = false;
        returning = false;
        elapsed = 0f;
    }

    void OnDisable() { RestoreImmediate(); }
}
