using System;
using UnityEngine;

[DefaultExecutionOrder(-90)]
[DisallowMultipleComponent]
public sealed class ThirdPersonAimController : WeaponAimSource
{
    [SerializeField] private PlayerContext playerContext;
    [SerializeField, Min(1f)] private float maximumAimDistance = 250f;

    public Vector3 AimPoint { get; private set; }
    public Vector3 CameraRayOrigin { get; private set; }
    public Vector3 CameraRayDirection { get; private set; } = Vector3.forward;
    public bool HasCameraHit { get; private set; }

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

    public override CharacteContext OwnerContext => playerContext;
    public override float MovementSpreadFactor => playerContext != null
        ? Mathf.Clamp01(playerContext.moveInput.magnitude) : 0f;

    public override bool TryGetAimSample(out WeaponAimSample sample)
    {
        sample = default;
        if (!isActiveAndEnabled || Camera.main == null)
            return false;
        TickAimPoint();
        sample = new WeaponAimSample(AimPoint, CameraRayDirection);
        return sample.IsValid;
    }

    public Vector3 ResolveShotDirection(Transform muzzle, float spreadDegrees)
    {
        if (muzzle == null) return Vector3.zero;
        return WeaponAimResolver.TryResolveDirection(muzzle.position,
            new WeaponAimSample(AimPoint, CameraRayDirection), spreadDegrees, out Vector3 direction)
            ? direction : Vector3.zero;
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
        HasCameraHit = TryGetFirstValidHit(
            ray,
            maximumAimDistance,
            0f,
            includeHurtboxTriggers: true,
            out RaycastHit cameraHit);
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
            ? playerContext.WeaponSystem.FirePoint : null;
        if (muzzle != null)
            ProbeMuzzle(muzzle.position, new WeaponAimSample(AimPoint, CameraRayDirection));
    }

    bool TryGetFirstValidHit(Ray ray, float distance, float radius,
        bool includeHurtboxTriggers, out RaycastHit result) =>
        QueryHit(ray, distance, radius, includeHurtboxTriggers, out result);

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
