using UnityEngine;

/// <summary>
/// Drives a bone's world yaw toward an AI-supplied aim point in LateUpdate, after Animancer
/// has already written the clip pose for this frame. Needed because Opsive Behavior Designer
/// tasks (e.g. AiShoot) tick in the Update phase and any rotation they write gets stomped by
/// the animation clip before render. See TurretAimAxisDriver_ImplementationPlan.md §2 for why.
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

    [Header("Release")]
    [SerializeField, Min(0f)] private float releaseBlendSpeed = 2f;

    private Quaternion _drivenRotation;
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

        Quaternion reference = _everAcquired ? _drivenRotation : yawTarget.rotation;
        Vector3 direction = worldPoint - yawTarget.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return 0f;

        return Vector3.Angle(reference * Vector3.forward, direction);
    }

    private void LateUpdate()
    {
        if (yawTarget == null)
            return;

        Quaternion clipPose = yawTarget.rotation;

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

        float targetWeight = mode == Mode.Releasing ? 0f : 1f;
        _weight = Mathf.MoveTowards(_weight, targetWeight, releaseBlendSpeed * dt);

        if (_weight <= 0.001f)
        {
            _weight = 0f;
            _everAcquired = false;
            _hasTarget = false;
            return;
        }

        if (!_everAcquired)
        {
            _drivenRotation = clipPose;
            _everAcquired = true;
        }

        if (mode == Mode.Tracking)
        {
            Vector3 direction = _aimPoint - yawTarget.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(direction.normalized, Vector3.up);
                _drivenRotation = turnMode == TurnMode.ConstantSpeed
                    ? Quaternion.RotateTowards(_drivenRotation, desired, turnSpeed * dt)
                    : Quaternion.Slerp(_drivenRotation, desired, 1f - Mathf.Exp(-turnSharpness * dt));
            }
        }

        yawTarget.rotation = Quaternion.Slerp(clipPose, _drivenRotation, _weight);

        _hasTarget = false;
    }
}
