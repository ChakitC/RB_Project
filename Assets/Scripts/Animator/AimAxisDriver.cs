using UnityEngine;

/// <summary>
/// Drives a bone's yaw toward an AI-supplied aim point in LateUpdate, after Animancer
/// has already written the clip pose for this frame. Needed because Opsive Behavior Designer
/// tasks (e.g. AiShoot) tick in the Update phase and any rotation they write gets stomped by
/// the animation clip before render. See TurretAimAxisDriver_ImplementationPlan.md §2 for why.
///
/// The yaw is applied around the bone's own local Y axis, matching how the clip animates it.
/// Writing a world-space LookRotation instead leaks into the bone's local X/Z whenever the
/// parent chain is not axis-aligned, which reads as a tilted model.
/// </summary>
[DefaultExecutionOrder(10150)]
[DisallowMultipleComponent]
public sealed class AimAxisDriver : MonoBehaviour
{
    public enum TurnMode
    {
        ConstantSpeed,
        Damped,
    }

    private enum Mode
    {
        Tracking,
        Holding,
        Frozen,
        Releasing,
    }

    [Header("Targets")]
    [SerializeField] private Transform yawTarget;
    // TODO(pitch): [SerializeField] private Transform pitchTarget; // Terret bone — reserved, see plan §3/§6.1 Q22

    [Header("Context")]
    [SerializeField] private CharacteContext characterContext;

    [Header("Turn")]
    [SerializeField] private TurnMode turnMode = TurnMode.ConstantSpeed;
    [SerializeField, Min(0f)] private float turnSpeed = 720f;
    [SerializeField, Min(0f)] private float turnSharpness = 14f;

    [Header("Blend")]
    // Acquiring needs no easing: _drivenLocalYaw is seeded from the clip pose, so weight can snap in
    // without a pop. Blending in slowly instead mixes the aim angle with the clip's own spinning yaw,
    // which reads as the turret sweeping along with the idle before settling.
    [SerializeField, Min(0f)] private float acquireBlendSpeed = 12f;
    [SerializeField, Min(0f)] private float releaseBlendSpeed = 2f;

    private float _drivenLocalYaw;
    private Vector3 _drivenForward = Vector3.forward;
    private float _weight;
    private bool _hasTarget;
    private Vector3 _aimPoint;
    private bool _released;
    private bool _everAcquired;

    public bool IsDriving => _weight > 0f;

    private void Awake()
    {
        if (characterContext == null)
            characterContext = GetComponent<CharacteContext>();
        if (characterContext == null)
            characterContext = GetComponentInParent<CharacteContext>();
    }

    public void SetAimPoint(Vector3 worldPoint)
    {
        _aimPoint = worldPoint;
        _hasTarget = true;
    }

    public void ClearAimPoint()
    {
        _hasTarget = false;
    }

    public void Release()
    {
        _released = true;
    }

    public void Reacquire()
    {
        _released = false;
    }

    public float AngleTo(Vector3 worldPoint)
    {
        if (yawTarget == null)
            return 0f;

        Vector3 reference = _everAcquired ? _drivenForward : yawTarget.forward;
        reference.y = 0f;
        Vector3 direction = worldPoint - yawTarget.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f || reference.sqrMagnitude <= 0.0001f)
            return 0f;

        return Vector3.Angle(reference, direction);
    }

    private void LateUpdate()
    {
        if (yawTarget == null)
            return;

        // The clip pose Animancer just wrote. Only its Y is ours to override; X/Z are passed through
        // so any authored tilt in the rig survives.
        Vector3 clipEuler = yawTarget.localRotation.eulerAngles;

        StateHub stateHub = characterContext != null ? characterContext.stateHub : null;
        Mode mode;
        if (_released)
            mode = Mode.Releasing;
        else if (stateHub == null)
            mode = Mode.Releasing;
        else if (!stateHub.IsAlive || stateHub.Isdown)
            mode = Mode.Releasing;
        else if (!stateHub.CanRotate())
            mode = Mode.Frozen;
        else if (!_hasTarget)
            mode = Mode.Holding;
        else
            mode = Mode.Tracking;

        float dt = characterContext != null &&
                   characterContext.UsesWorldSlow &&
                   TimeSlowManager.Instance != null
            ? TimeSlowManager.Instance.WorldDeltaTime
            : Time.deltaTime;

        bool releasing = mode == Mode.Releasing;
        float targetWeight = releasing ? 0f : 1f;
        float blendSpeed = releasing ? releaseBlendSpeed : acquireBlendSpeed;
        _weight = Mathf.MoveTowards(_weight, targetWeight, blendSpeed * dt);

        if (_weight <= 0.001f)
        {
            _weight = 0f;
            _everAcquired = false;
            _hasTarget = false;
            return;
        }

        if (!_everAcquired)
        {
            _drivenLocalYaw = clipEuler.y;
            _everAcquired = true;
        }

        if (mode == Mode.Tracking && TryResolveDesiredLocalYaw(out float desiredYaw))
        {
            _drivenLocalYaw = turnMode == TurnMode.ConstantSpeed
                ? Mathf.MoveTowardsAngle(_drivenLocalYaw, desiredYaw, turnSpeed * dt)
                : Mathf.LerpAngle(_drivenLocalYaw, desiredYaw, 1f - Mathf.Exp(-turnSharpness * dt));
        }

        float outYaw = Mathf.LerpAngle(clipEuler.y, _drivenLocalYaw, _weight);
        yawTarget.localRotation = Quaternion.Euler(clipEuler.x, outYaw, clipEuler.z);
        _drivenForward = yawTarget.forward;

        _hasTarget = false;
    }

    /// <summary>
    /// Converts the aim point into the parent bone's space and returns the local Y angle that
    /// points this bone's forward at it. Returns false when the aim point is degenerate.
    /// </summary>
    private bool TryResolveDesiredLocalYaw(out float yaw)
    {
        yaw = _drivenLocalYaw;

        Vector3 direction = _aimPoint - yawTarget.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        Transform parent = yawTarget.parent;
        Vector3 localDirection = parent != null
            ? parent.InverseTransformDirection(direction.normalized)
            : direction.normalized;

        if (localDirection.sqrMagnitude <= 0.0001f)
            return false;

        yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        return true;
    }
}
