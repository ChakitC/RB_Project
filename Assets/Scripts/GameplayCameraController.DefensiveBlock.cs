using UnityEngine;
using Unity.Cinemachine;

public partial class GameplayCameraController
{
    // Retain serialized fields/API for existing scenes and legacy callers.
    // Guard-driven shots overwrite these runtime copies from the actor's SO at entry.
    [HideInInspector] public Vector3 blockLocalPosition = new Vector3(2.65f, 1.55f, -1.91f);
    [HideInInspector] public Vector3 blockLocalEulerAngles = new Vector3(-0.35f, -35.39f, 10.49f);
    [HideInInspector] public float blockFieldOfView = 60f;
    [HideInInspector] public float blockBlendInSeconds = 0.16f;
    [HideInInspector] public float blockHoldSeconds = 0.12f;
    [HideInInspector] public float blockBlendOutSeconds = 0.4f;
    object blockShotOwner;
    Transform blockShotActor;
    Vector3 blockShotOrigin;
    Quaternion blockShotHeading;
    float blockShotWeight, blockShotHold;
    public bool IsDefensiveBlockShotActive => blockShotOwner != null || blockShotWeight > 0f;

    public bool BeginDefensiveBlockShot(object owner, PlayerContext player, Transform guard)
        => BeginDefensiveBlockShot(owner, player, guard, null);

    public bool BeginDefensiveBlockShot(object owner, PlayerContext player, Transform guard, DefensiveBlockActorProfile settings)
    {
        if (settings != null && !settings.cameraEnabled) return false;
        if (owner == null || player == null || guard == null || !GameplayInputEnabled ||
            player != playerContext || comboProfile != null || virtualCamera == null ||
            (brain != null && brain.ActiveVirtualCamera != null && !ReferenceEquals(brain.ActiveVirtualCamera, virtualCamera))) return false;
        // Snapshot all shot settings, including the return blend, so editing/replacing a
        // shared profile cannot change an in-flight shot or another guard's cleanup.
        if (settings != null)
        {
            blockLocalPosition = settings.cameraLocalPosition;
            blockLocalEulerAngles = settings.cameraLocalEulerAngles;
            blockFieldOfView = Mathf.Clamp(settings.cameraFieldOfView, 20f, 100f);
            blockBlendInSeconds = Mathf.Max(.01f, settings.cameraBlendInSeconds);
            blockHoldSeconds = Mathf.Max(0f, settings.cameraHoldSeconds);
            blockBlendOutSeconds = Mathf.Max(.01f, settings.cameraBlendOutSeconds);
        }
        blockShotOwner = owner;
        blockShotActor = guard;
        blockShotOrigin = player.transform.position;
        blockShotHeading = Quaternion.Euler(0, guard.eulerAngles.y, 0);
        blockShotHold = 0f;
        return true;
    }

    public void EndDefensiveBlockShot(object owner, bool hold)
    {
        if (!ReferenceEquals(owner, blockShotOwner)) return;
        blockShotOwner = null;
        blockShotActor = null;
        blockShotHold = hold ? blockHoldSeconds : 0f;
    }

    void ClearDefensiveBlockShot()
    {
        blockShotOwner = null; blockShotActor = null;
        blockShotWeight = blockShotHold = 0f;
    }

    void TickDefensiveBlockShot()
    {
        if (!IsDefensiveBlockShotActive) return;
        if (!GameplayInputEnabled || comboProfile != null || playerContext == null ||
            playerContext.HealthSystem == null || !playerContext.HealthSystem.IsAlive ||
            (brain != null && brain.ActiveVirtualCamera != null && !ReferenceEquals(brain.ActiveVirtualCamera, virtualCamera)))
        { ClearDefensiveBlockShot(); return; }
        if (blockShotOwner != null && (blockShotActor == null || !blockShotActor.gameObject.activeInHierarchy))
            EndDefensiveBlockShot(blockShotOwner, false);
        float delta = Time.unscaledDeltaTime;
        if (blockShotOwner == null && blockShotHold > 0f)
        { blockShotHold = Mathf.Max(0f, blockShotHold - delta); return; }
        bool active = blockShotOwner != null;
        blockShotWeight = Mathf.MoveTowards(blockShotWeight, active ? 1f : 0f,
            delta / Mathf.Max(0.01f, active ? blockBlendInSeconds : blockBlendOutSeconds));
    }

    internal void ApplyDefensiveBlockShot(ref CameraState state)
    {
        if (blockShotWeight <= 0f) return;
        float weight = Mathf.SmoothStep(0f, 1f, blockShotWeight);
        Vector3 desired = blockShotOrigin + blockShotHeading * blockLocalPosition;
        Vector3 origin = blockShotOrigin + Vector3.up * 1.2f;
        Vector3 delta = desired - origin;
        if (delta.sqrMagnitude > 0.001f && Physics.SphereCast(origin, 0.15f, delta.normalized,
            out var hit, delta.magnitude, ResolveCameraObstacleMask(), QueryTriggerInteraction.Ignore))
            desired = origin + delta.normalized * Mathf.Max(0f, hit.distance - 0.03f);
        state.RawPosition = Vector3.Lerp(state.RawPosition, desired - state.PositionCorrection, weight);
        Quaternion rotation = blockShotHeading * Quaternion.Euler(blockLocalEulerAngles);
        state.RawOrientation = Quaternion.Slerp(state.RawOrientation,
            rotation * Quaternion.Inverse(state.OrientationCorrection), weight);
        var lens = state.Lens;
        lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, blockFieldOfView, weight);
        state.Lens = lens;
    }
}
