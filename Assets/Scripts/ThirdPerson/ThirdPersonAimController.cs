using System;
using UnityEngine;

[DefaultExecutionOrder(-90)]
[DisallowMultipleComponent]
public sealed class ThirdPersonAimController : MonoBehaviour
{
    [SerializeField] private PlayerContext playerContext;
    [SerializeField] private LayerMask aimCollisionMask = ~0;
    [SerializeField, Min(1f)] private float maximumAimDistance = 250f;
    [SerializeField, Min(0f)] private float muzzleProbeRadius = 0.045f;

    readonly RaycastHit[] raycastHits = new RaycastHit[64];

    public Vector3 AimPoint { get; private set; }
    public Vector3 CameraRayOrigin { get; private set; }
    public Vector3 CameraRayDirection { get; private set; } = Vector3.forward;
    public bool HasCameraHit { get; private set; }
    public bool IsMuzzleBlocked { get; private set; }
    public Vector3 MuzzleHitPoint { get; private set; }

    public event Action AimPointChanged;

    void Awake()
    {
        ResolveReferences();
        EnsureAimTarget();
    }

    void OnEnable()
    {
        ResolveReferences();
        EnsureAimTarget();
    }

    void Update()
    {
        TickAimPoint();
    }

    public Vector3 ResolveShotDirection(Transform muzzle, float spreadDegrees)
    {
        Vector3 origin = muzzle != null ? muzzle.position : transform.position;
        Vector3 direction = AimPoint - origin;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = CameraRayDirection;

        direction.Normalize();
        if (spreadDegrees <= 0.001f)
            return direction;

        Quaternion basis = Quaternion.LookRotation(direction, Vector3.up);
        Vector2 random = UnityEngine.Random.insideUnitCircle * spreadDegrees;
        return basis * Quaternion.Euler(random.y, random.x, 0f) * Vector3.forward;
    }

    public Vector3 GetPlanarCameraForward()
    {
        Vector3 forward = Vector3.ProjectOnPlane(CameraRayDirection, Vector3.up);
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
    }

    void TickAimPoint()
    {
        ResolveReferences();

        Camera gameplayCamera = Camera.main;
        if (gameplayCamera == null)
            return;

        CameraRayOrigin = gameplayCamera.transform.position;
        CameraRayDirection = gameplayCamera.transform.forward;

        Ray ray = new(CameraRayOrigin, CameraRayDirection);
        HasCameraHit = TryGetFirstValidHit(ray, maximumAimDistance, 0f, out RaycastHit cameraHit);
        AimPoint = HasCameraHit
            ? cameraHit.point
            : ray.GetPoint(maximumAimDistance);

        EnsureAimTarget();
        if (playerContext != null && playerContext.aimTarget != null)
            playerContext.aimTarget.position = AimPoint;

        TickMuzzleBlock();
        AimPointChanged?.Invoke();
    }

    void TickMuzzleBlock()
    {
        Transform muzzle = playerContext != null && playerContext.WeaponSystem != null
            ? playerContext.WeaponSystem.FirePoint
            : null;

        IsMuzzleBlocked = false;
        MuzzleHitPoint = AimPoint;
        if (muzzle == null)
            return;

        Vector3 toAim = AimPoint - muzzle.position;
        float distance = toAim.magnitude;
        if (distance <= 0.001f)
            return;

        Ray ray = new(muzzle.position, toAim / distance);
        if (!TryGetFirstValidHit(ray, distance, muzzleProbeRadius, out RaycastHit hit))
            return;

        IsMuzzleBlocked = true;
        MuzzleHitPoint = hit.point;
    }

    bool TryGetFirstValidHit(Ray ray, float distance, float radius, out RaycastHit result)
    {
        int hitCount = radius > 0f
            ? Physics.SphereCastNonAlloc(
                ray,
                radius,
                raycastHits,
                distance,
                aimCollisionMask,
                QueryTriggerInteraction.Ignore)
            : Physics.RaycastNonAlloc(
                ray,
                raycastHits,
                distance,
                aimCollisionMask,
                QueryTriggerInteraction.Ignore);

        float nearestDistance = float.PositiveInfinity;
        result = default;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];
            if (hit.collider == null || ShouldIgnore(hit.collider))
                continue;

            if (hit.distance >= nearestDistance)
                continue;

            nearestDistance = hit.distance;
            result = hit;
        }

        return nearestDistance < float.PositiveInfinity;
    }

    bool ShouldIgnore(Collider candidate)
    {
        if (candidate == null)
            return true;

        if (playerContext != null && candidate.transform.IsChildOf(playerContext.transform))
            return true;

        CharacteContext other = candidate.GetComponentInParent<CharacteContext>();
        if (other == null)
            return false;

        return other == playerContext ||
               other.TargetIdentity == AITargetIdentity.Player ||
               other.TargetIdentity == AITargetIdentity.Companion;
    }

    void ResolveReferences()
    {
        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();
        if (playerContext == null)
            playerContext = GetComponentInParent<PlayerContext>();
    }

    void EnsureAimTarget()
    {
        if (playerContext == null || playerContext.aimTarget != null)
            return;

        GameObject aimTargetObject = new("Third Person Aim Target");
        aimTargetObject.transform.SetParent(playerContext.transform, false);
        aimTargetObject.transform.position = playerContext.transform.position + playerContext.transform.forward * maximumAimDistance;
        playerContext.aimTarget = aimTargetObject.transform;
    }
}
