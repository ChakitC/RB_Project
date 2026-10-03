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
    Transform cooperativeShotAlly, cooperativeShotCaster;
    Vector3 cooperativeShotBasePosition;
    Vector3 cooperativeApproachEuler, cooperativeStrugglePosition, cooperativeStruggleEuler;
    float cooperativeApproachFov, cooperativeStruggleFov, cooperativeFramingBlendSeconds, cooperativeFramingWeight;
    Vector3 blockShotOrigin;
    Quaternion blockShotHeading;
    float blockShotWeight, blockShotHold;
    readonly RaycastHit[] blockShotObstacleHits = new RaycastHit[16];
    public bool IsDefensiveBlockShotActive => blockShotOwner != null || blockShotWeight > 0f;

    public bool BeginCooperativeBlockShot(object owner, PlayerContext player, Transform helper, Transform caster,
        DefensiveBlockActorProfile settings)
    {
        if (!BeginDefensiveBlockShot(owner, player, player.transform, settings)) return false;
        cooperativeShotAlly = helper; cooperativeShotCaster = caster;
        cooperativeShotBasePosition = blockLocalPosition;
        cooperativeApproachEuler = blockLocalEulerAngles;
        cooperativeApproachFov = blockFieldOfView;
        cooperativeStrugglePosition = settings != null ? settings.cooperativeCameraLocalPosition : new Vector3(1.9f, 2.2f, -3.4f);
        cooperativeStruggleEuler = settings != null ? settings.cooperativeCameraLocalEulerAngles : new Vector3(10f, -12f, 8f);
        cooperativeStruggleFov = Mathf.Clamp(settings != null ? settings.cooperativeCameraFieldOfView : 55f, 20f, 100f);
        cooperativeFramingBlendSeconds = Mathf.Max(.01f, settings != null ? settings.cooperativeCameraBlendSeconds : .2f);
        cooperativeFramingWeight = 0f;
        ConfigureCooperativeShake(settings);
        return true;
    }

    public bool BeginDefensiveBlockShot(object owner, PlayerContext player, Transform guard)
        => BeginDefensiveBlockShot(owner, player, guard, null);

    public bool BeginDefensiveBlockShot(object owner, PlayerContext player, Transform guard, DefensiveBlockActorProfile settings)
    {
        if (settings != null && !settings.cameraEnabled) return false;
        if (owner == null || player == null || guard == null || !GameplayInputEnabled ||
            player != playerContext || followUpProfile != null || virtualCamera == null ||
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
        ResetCooperativeShake();
        ConfigureNormalBlockShake(settings);
        cooperativeShotAlly = cooperativeShotCaster = null;
        blockShotActor = guard;
        blockShotOrigin = player.transform.position;
        blockShotHeading = Quaternion.Euler(0, guard.eulerAngles.y, 0);
        blockShotHold = 0f;
        return true;
    }

    public void EndDefensiveBlockShot(object owner, bool hold)
    {
        if (!ReferenceEquals(owner, blockShotOwner)) return;
        ResetCooperativeShake();
        ResetNormalBlockShake();
        blockShotOwner = null;
        blockShotActor = null;
        cooperativeShotAlly = cooperativeShotCaster = null;
        blockShotHold = hold ? blockHoldSeconds : 0f;
    }

    void ClearDefensiveBlockShot()
    {
        ResetCooperativeShake();
        ResetNormalBlockShake();
        blockShotOwner = null; blockShotActor = null;
        cooperativeShotAlly = cooperativeShotCaster = null;
        blockShotWeight = blockShotHold = 0f;
    }

    void TickDefensiveBlockShot()
    {
        if (!IsDefensiveBlockShotActive) return;
        if (!GameplayInputEnabled || followUpProfile != null || playerContext == null ||
            playerContext.HealthSystem == null || !playerContext.HealthSystem.IsAlive ||
            (brain != null && brain.ActiveVirtualCamera != null && !ReferenceEquals(brain.ActiveVirtualCamera, virtualCamera)))
        { ClearDefensiveBlockShot(); return; }
        if (blockShotOwner != null && (blockShotActor == null || !blockShotActor.gameObject.activeInHierarchy))
            EndDefensiveBlockShot(blockShotOwner, false);
        if (blockShotOwner != null && cooperativeShotAlly != null && cooperativeShotCaster != null)
        {
            TickCooperativeFraming(GlobalTimeScaleManager.Instance.IsPaused ? 0f : Time.unscaledDeltaTime);
        }
        float delta = Time.unscaledDeltaTime;
        AdvanceNormalBlockShake(delta, GlobalTimeScaleManager.Instance.IsPaused);
        if (blockShotOwner == null && blockShotHold > 0f)
        { blockShotHold = Mathf.Max(0f, blockShotHold - delta); return; }
        bool active = blockShotOwner != null;
        blockShotWeight = Mathf.MoveTowards(blockShotWeight, active ? 1f : 0f,
            delta / Mathf.Max(0.01f, active ? blockBlendInSeconds : blockBlendOutSeconds));
    }

    void TickCooperativeFraming(float deltaTime)
    {
        // Begin the closer, left-weighted composition at contact, not during approach.
        cooperativeFramingWeight = Mathf.MoveTowards(cooperativeFramingWeight, cooperativeShakeStarted ? 1f : 0f,
            deltaTime / Mathf.Max(.01f, cooperativeFramingBlendSeconds));
        float t = Mathf.SmoothStep(0f, 1f, cooperativeFramingWeight);
        Vector3 position = Vector3.Lerp(cooperativeShotBasePosition, cooperativeStrugglePosition, t);
        Quaternion localRotation = Quaternion.Slerp(Quaternion.Euler(cooperativeApproachEuler),
            Quaternion.Euler(cooperativeStruggleEuler), t);
        blockLocalEulerAngles = localRotation.eulerAngles;
        blockFieldOfView = Mathf.Lerp(cooperativeApproachFov, cooperativeStruggleFov, t);
        blockShotOrigin = playerContext.transform.position;
        var bounds = new Bounds(blockShotActor.position + Vector3.up * 1.1f, new Vector3(1.3f, 2.4f, 1.3f));
        bounds.Encapsulate(new Bounds(cooperativeShotAlly.position + Vector3.up * 1.1f, new Vector3(1.3f, 2.4f, 1.3f)));
        float retreat = CooperativeFramingRetreat(bounds, blockShotOrigin + blockShotHeading * position,
            blockShotHeading * localRotation, blockFieldOfView, gameplayCamera != null ? gameplayCamera.aspect : 16f / 9f);
        blockLocalPosition = position - localRotation * Vector3.forward * retreat;
    }

    static float CooperativeFramingRetreat(Bounds bounds, Vector3 position, Quaternion rotation, float fov, float aspect)
    {
        // Fit padded body bounds inside the central 85% of the frame. Only dolly
        // backwards: repeated updates cannot accumulate distance or change the angle.
        float vertical = Mathf.Tan(fov * .5f * Mathf.Deg2Rad) * .85f;
        float horizontal = vertical * Mathf.Max(.1f, aspect);
        Quaternion inverse = Quaternion.Inverse(rotation);
        float retreat = 0f;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
            Vector3 view = inverse * (point - position);
            retreat = Mathf.Max(retreat, Mathf.Max(Mathf.Abs(view.x) / horizontal - view.z,
                Mathf.Abs(view.y) / vertical - view.z));
        }
        return retreat;
    }

    internal void ApplyDefensiveBlockShot(ref CameraState state)
    {
        if (blockShotWeight <= 0f) return;
        // Read the live Player position at camera evaluation, after movement/recoil.
        // Keep the accepted heading so turning does not swing the Block shot around.
        if (playerContext != null) blockShotOrigin = playerContext.transform.position;
        float weight = Mathf.SmoothStep(0f, 1f, blockShotWeight);
        Vector3 desired = blockShotOrigin + blockShotHeading * blockLocalPosition;
        Vector3 origin = blockShotOrigin + Vector3.up * 1.2f;
        Vector3 delta = desired - origin;
        if (TryGetDefensiveBlockObstacle(origin, delta, out var hit))
            desired = origin + delta.normalized * Mathf.Max(0f, hit.distance - 0.03f);
        state.RawPosition = Vector3.Lerp(state.RawPosition, desired - state.PositionCorrection, weight);
        Quaternion rotation = blockShotHeading * Quaternion.Euler(blockLocalEulerAngles) *
            Quaternion.Euler(SampleCooperativeShake() + SampleNormalBlockShake());
        state.RawOrientation = Quaternion.Slerp(state.RawOrientation,
            rotation * Quaternion.Inverse(state.OrientationCorrection), weight);
        var lens = state.Lens;
        lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, blockFieldOfView, weight);
        state.Lens = lens;
    }

    bool TryGetDefensiveBlockObstacle(Vector3 origin, Vector3 delta, out RaycastHit nearest)
    {
        nearest = default;
        if (delta.sqrMagnitude <= 0.001f) return false;
        int count = Physics.SphereCastNonAlloc(origin, 0.15f, delta.normalized, blockShotObstacleHits,
            delta.magnitude, ResolveCameraObstacleMask(), QueryTriggerInteraction.Ignore);
        var hits = blockShotObstacleHits;
        // NonAlloc results are unordered and may omit a wall when the buffer is full.
        // Only dense casts allocate, so ignoring Player can never hide a wall behind it.
        if (count == hits.Length)
        {
            hits = Physics.SphereCastAll(origin, 0.15f, delta.normalized, delta.magnitude,
                ResolveCameraObstacleMask(), QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }
        float distance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var collider = hits[i].collider;
            if (collider == null || (playerContext != null && (collider == playerContext.cc ||
                collider.transform.IsChildOf(playerContext.transform)))) continue;
            if ((cooperativeShotAlly != null && collider.transform.IsChildOf(cooperativeShotAlly)) ||
                (cooperativeShotCaster != null && collider.transform.IsChildOf(cooperativeShotCaster))) continue;
            if (hits[i].distance >= distance) continue;
            nearest = hits[i];
            distance = nearest.distance;
        }
        return distance < float.PositiveInfinity;
    }
}
