using UnityEngine;

public partial class PlayerMovementCC
{
    [Header("Sprint after held Dash")]
    [SerializeField, Min(1f)] float sprintSpeedMultiplier = 1.6f;
    [SerializeField, Min(0.01f)] float sprintAccelerationSeconds = 0.2f;
    [SerializeField, Min(0.01f)] float sprintReleaseSeconds = 0.12f;
    [SerializeField, Min(0.01f)] float sprintTurnSmoothTime = 0.25f;
    [SerializeField, Min(1f)] float sprintMaxTurnSpeed = 360f;

    readonly RaycastHit[] sprintHits = new RaycastHit[32];
    DashSystem sprintDash;
    bool dashHeld;
    bool sprintRequested;
    float sprintSpeedBonus;
    float sprintTurnVelocity;
    public bool IsSprinting { get; private set; }
    public Vector3 SprintDirectionWorld { get; private set; }
    float SprintDeltaTime => _characteContext != null && _characteContext.UsesWorldSlow
        ? TimeSlowManager.Instance.WorldDeltaTime : Time.deltaTime;

    void OnEnable() => RefreshDashBinding();

    void OnDisable()
    {
        CancelSprint();
        ResetCrouchInput();
        UnbindSprintDash();
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused) { CancelSprint(); ResetCrouchInput(); }
    }

    void RefreshDashBinding()
    {
        var next = _characteContext != null ? _characteContext.DashSystem : null;
        if (sprintDash == next) return;
        CancelSprint();
        UnbindSprintDash();
        sprintDash = next;
        if (sprintDash == null) return;
        sprintDash.DashCompleted += OnDashCompleted;
        sprintDash.DashCanceled += CancelSprint;
    }

    void UnbindSprintDash()
    {
        if (sprintDash != null)
        {
            sprintDash.DashCompleted -= OnDashCompleted;
            sprintDash.DashCanceled -= CancelSprint;
        }
        sprintDash = null;
    }

    public void SetDashHeld(bool held)
    {
        if (!held)
        {
            dashHeld = false;
            sprintRequested = false;
            IsSprinting = false;
            return;
        }
        if (dashHeld || !isActiveAndEnabled) return;
        RefreshDashBinding();
        dashHeld = true;
        sprintRequested = true;
        stateHub?.RequestOnDash();
        if (sprintDash == null || !sprintDash.IsDashing) sprintRequested = false;
    }

    public void CancelSprint()
    {
        if (IsSprinting || sprintSpeedBonus > 0f)
            currentSpeed = Mathf.Min(currentSpeed, GetBaseMoveSpeedFromHubOrFallback());
        sprintRequested = false;
        IsSprinting = false;
        sprintSpeedBonus = 0f;
        sprintTurnVelocity = 0f;
    }

    void OnDashCompleted(Vector3 direction)
    {
        if (!dashHeld || !sprintRequested || !CanSprint()) return;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        SprintDirectionWorld = direction.normalized;
        sprintTurnVelocity = 0f;
        sprintRequested = false;
        sprintSpeedBonus = 0f;
        currentSpeed = GetBaseMoveSpeedFromHubOrFallback();
        IsSprinting = true;
    }

    bool CanSprint()
    {
        if (crouchHeld || ownsCrouchBody || IsSliding) return false;
        if (!isActiveAndEnabled || _characteContext == null || stateHub == null ||
            suspensionTokens.Count > 0 ||
            _characterController == null || !_characterController.enabled ||
            _characteContext.MovementMotor == null)
            return false;
        return stateHub.CanContinue(CharacterAction.Sprint);
    }

    void ValidateSprint()
    {
        if ((IsSprinting || sprintRequested || sprintSpeedBonus > 0f) && !CanSprint())
            CancelSprint();
        if (IsSprinting && sprintDash != null && sprintDash.IsDashing) CancelSprint();
    }

    void MoveSprint()
    {
        float dt = SprintDeltaTime;
        if (dt <= 0f) return;
        UpdateSprintDirection(dt);
        float baseSpeed = GetBaseMoveSpeedFromHubOrFallback();
        float bonus = baseSpeed * (sprintSpeedMultiplier - 1f);
        sprintSpeedBonus = Mathf.MoveTowards(sprintSpeedBonus, bonus,
            Mathf.Max(0.01f, bonus) * dt / sprintAccelerationSeconds);
        currentSpeed = baseSpeed + sprintSpeedBonus;
        Vector3 delta = SprintDirectionWorld * (currentSpeed * dt);
        bool blocked = ClipSprintAtObstacle(ref delta);
        Vector3 applied = _characteContext.MovementMotor.Move(delta, CharacterMovementSettings.Default);
        RecordLocomotionDisplacement(applied, dt);
        HandleFacing(SprintDirectionWorld);
        Vector3 local = (actorRoot ? actorRoot : transform).InverseTransformDirection(SprintDirectionWorld);
        stateHub.SetMoveDirLocal(new Vector2(local.x, local.z));
        _move01 = 1f;
        stateHub.SetMoveSpeed01(1f);
        if (blocked || (delta.sqrMagnitude > 0.0001f &&
            Vector3.Dot(applied, SprintDirectionWorld) < delta.magnitude * 0.1f))
            CancelSprint();
    }

    void UpdateSprintDirection(float dt)
    {
        var camera = Camera.main;
        if (camera == null || !stateHub.CanRotate())
        {
            sprintTurnVelocity = 0f;
            return;
        }
        Vector3 target = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
        if (target.sqrMagnitude < 0.0001f)
        {
            sprintTurnVelocity = 0f;
            return;
        }
        float yaw = Mathf.Atan2(SprintDirectionWorld.x, SprintDirectionWorld.z) * Mathf.Rad2Deg;
        float targetYaw = Mathf.Atan2(target.x, target.z) * Mathf.Rad2Deg;
        yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref sprintTurnVelocity,
            Mathf.Max(0.01f, sprintTurnSmoothTime), Mathf.Max(1f, sprintMaxTurnSpeed), dt);
        SprintDirectionWorld = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
    }

    bool ClipSprintAtObstacle(ref Vector3 delta) => ClipLocomotionAtObstacle(ref delta, SprintDirectionWorld);

    bool ClipLocomotionAtObstacle(ref Vector3 delta, Vector3 direction)
    {
        var cc = _characterController;
        Vector3 scale = cc.transform.lossyScale;
        float radius = cc.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = Mathf.Max(radius * 2f, cc.height * Mathf.Abs(scale.y));
        Vector3 center = cc.transform.TransformPoint(cc.center);
        float halfLine = height * 0.5f - radius;
        // Lift the bottom above walkable steps, while retaining the upper body sweep.
        Vector3 top = center + Vector3.up * halfLine;
        Vector3 bottom = center - Vector3.up * halfLine;
        bottom.y = Mathf.Min(top.y, bottom.y + cc.stepOffset);
        float distance = delta.magnitude;
        int count = Physics.CapsuleCastNonAlloc(bottom, top, radius + cc.skinWidth,
            direction, sprintHits, distance + cc.skinWidth,
            Physics.DefaultRaycastLayers & ~cc.excludeLayers.value, QueryTriggerInteraction.Ignore);
        float allowed = distance;
        float walkableNormalY = Mathf.Cos(cc.slopeLimit * Mathf.Deg2Rad);
        bool blocked = count == sprintHits.Length;
        if (blocked) allowed = 0f;
        Transform root = actorRoot ? actorRoot : transform;
        for (int i = 0; i < count; i++)
        {
            var hit = sprintHits[i];
            if (hit.collider.transform.IsChildOf(root) ||
                Physics.GetIgnoreLayerCollision(cc.gameObject.layer, hit.collider.gameObject.layer) ||
                Physics.GetIgnoreCollision(cc, hit.collider) || hit.normal.y >= walkableNormalY ||
                Vector3.Dot(hit.normal, direction) >= -0.001f) continue;
            blocked = true;
            allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - cc.skinWidth));
        }
        delta = direction * allowed;
        return blocked;
    }
}
