using UnityEngine;

public partial class PlayerMovementCC
{
    [Header("Momentum Slide")]
    [SerializeField, Min(0.01f)] private float slideEntrySpeedMultiplier = 1.15f;
    [SerializeField, Min(0.01f)] private float slideExitSpeedMultiplier = 0.6f;
    [SerializeField, Min(0.01f)] private float slideFriction = 0.75f;
    [SerializeField, Min(0f)] private float slideBrake = 1f;
    [SerializeField, Min(0f)] private float slideLateralAcceleration = 0.8f;
    [SerializeField, Min(0f)] private float slideMaxTurnSpeed = 45f;
    [SerializeField, Min(0.01f)] private float slideReleaseSeconds = 0.15f;

    private Vector3 slideVelocity;
    private Vector3 releaseVelocity;
    private float slideReleaseRemaining;
    private int velocityFrame = -10;
    private Vector3 velocityPosition;
    public bool IsSliding { get; private set; }
    public Vector3 PlanarVelocityWorld { get; private set; }

    private float SlideStaminaPerUnit => _characteContext.baseStats != null
        ? Mathf.Max(0f, _characteContext.baseStats.slideStaminaPerUnit) : 0f;

    private bool CanUsePlayerLocomotion(bool handingOffDash = false)
    {
        if (!isActiveAndEnabled || _characteContext == null || stateHub == null ||
            !stateHub.IsAlive || stateHub.Isdown || suspensionTokens.Count > 0 ||
            _characterController == null || !_characterController.enabled ||
            _characteContext.MovementMotor == null || GlobalTimeScaleManager.Instance.IsPaused)
            return false;
        var facts = stateHub.GetActionFacts();
        // Admission after the handoff must retain every restriction except the Dash being canceled.
        if (handingOffDash) facts.Dashing = false;
        return facts.UI == UIStateId.Normal && !facts.Stunned && !facts.Knockback &&
            !facts.Dashing && (facts.Blocks & ControlBlockFlags.Move) == 0 &&
            !facts.SkillPlayback && !facts.ShootBlockingPlayback &&
            (_brain == null || !_brain.RootMotionActive) &&
            CharacterActionPolicy.Evaluate(CharacterAction.Move, facts).Allowed;
    }

    private bool CanSlide(bool handingOffDash = false)
    {
        if (!CanUsePlayerLocomotion(handingOffDash) || !stateHub.IsGrounded) return false;
        if (SlideStaminaPerUnit > 0f && (_characteContext.StaminaSystem == null ||
            _characteContext.StaminaSystem.Current <= 0f)) return false;
        var facts = stateHub.GetActionFacts();
        return !facts.Melee && !facts.Reloading;
    }

    private void TryBeginSlide()
    {
        var dash = _characteContext != null ? _characteContext.DashSystem : null;
        bool fromDash = dash != null && dash.IsDashing;
        if (!CanSlide(fromDash)) return;
        Vector3 entryVelocity = PlanarVelocityWorld;
        if (fromDash)
        {
            if (!dash.TryGetPlanarVelocity(out entryVelocity)) return;
        }
        else if (Time.frameCount - velocityFrame > 1 ||
            Vector3.ProjectOnPlane((actorRoot ? actorRoot : transform).position - velocityPosition,
                Vector3.up).sqrMagnitude > 0.01f) return;
        if (entryVelocity.magnitude < GetBaseMoveSpeedFromHubOrFallback() * slideEntrySpeedMultiplier)
            return;
        // Capture before cancellation clears velocity, i-frames, and the pending Sprint request.
        if (fromDash) dash.CancelDash();
        CancelSprint();
        slideReleaseRemaining = 0f;
        slideVelocity = entryVelocity;
        PlanarVelocityWorld = entryVelocity;
        IsSliding = true;
        LowerCrouchBody();
    }

    public void CancelSlide()
    {
        IsSliding = false;
        slideVelocity = Vector3.zero;
        slideReleaseRemaining = 0f;
        PlanarVelocityWorld = Vector3.zero;
        velocityFrame = -10;
    }

    private void ReleaseSlide()
    {
        Vector3 velocity = slideVelocity;
        CancelSlide();
        releaseVelocity = velocity;
        slideReleaseRemaining = slideReleaseSeconds;
        currentSpeed = GetBaseMoveSpeedFromHubOrFallback();
    }

    private void RecordLocomotionDisplacement(Vector3 applied, float dt)
    {
        PlanarVelocityWorld = dt > 0f ? Vector3.ProjectOnPlane(applied, Vector3.up) / dt : Vector3.zero;
        velocityFrame = Time.frameCount;
        velocityPosition = (actorRoot ? actorRoot : transform).position;
    }

    private Vector3 BlendSlideRelease(Vector3 targetVelocity, float dt)
    {
        if (slideReleaseRemaining <= 0f) return targetVelocity;
        slideReleaseRemaining = Mathf.Max(0f, slideReleaseRemaining - dt);
        return Vector3.Lerp(targetVelocity, releaseVelocity,
            slideReleaseRemaining / Mathf.Max(0.01f, slideReleaseSeconds));
    }

    private void MoveSlide()
    {
        float dt = SprintDeltaTime;
        if (dt <= 0f) return;
        float staminaPerUnit = SlideStaminaPerUnit;
        var stamina = _characteContext.StaminaSystem;
        if (staminaPerUnit > 0f && (stamina == null || stamina.Current <= 0f))
        {
            CancelSlide();
            return;
        }
        float baseSpeed = GetBaseMoveSpeedFromHubOrFallback();
        float speed = slideVelocity.magnitude;
        Vector3 direction = slideVelocity.normalized;
        Vector3 input = Vector3.zero;
        var camera = Camera.main;
        if (camera != null)
        {
            Vector2 axes = _characteContext.moveInput;
            input = Vector3.ClampMagnitude(
                Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized * axes.y +
                Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized * axes.x, 1f);
        }
        float along = Vector3.Dot(input, direction);
        Vector3 lateral = input - direction * along;
        // Signed yaw avoids the near-parallel dot-product precision loss at very high frame rates.
        float yaw = Mathf.Atan2(Vector3.Cross(direction, lateral).y * baseSpeed * slideLateralAcceleration * dt,
            Mathf.Max(0.0001f, speed)) * Mathf.Rad2Deg;
        yaw = Mathf.Clamp(yaw, -slideMaxTurnSpeed * dt, slideMaxTurnSpeed * dt);
        direction = Quaternion.AngleAxis(yaw, Vector3.up) * direction;
        speed = Mathf.Max(0f, speed - baseSpeed * (slideFriction + slideBrake * Mathf.Max(0f, -along)) * dt);
        slideVelocity = direction * speed;
        Vector3 delta = slideVelocity * dt;
        // Limit the last paid step, then charge only what the motor actually achieved.
        if (staminaPerUnit > 0f)
            delta = Vector3.ClampMagnitude(delta, stamina.Current / staminaPerUnit);
        bool blocked = ClipLocomotionAtObstacle(ref delta, direction);
        Vector3 applied = _characteContext.MovementMotor.Move(delta, CharacterMovementSettings.Default);
        RecordLocomotionDisplacement(applied, dt);
        if (staminaPerUnit > 0f)
            stamina.Spend(Mathf.Min(stamina.Current,
                Vector3.ProjectOnPlane(applied, Vector3.up).magnitude * staminaPerUnit));
        HandleFacing(direction);
        Vector3 local = (actorRoot ? actorRoot : transform).InverseTransformDirection(direction);
        stateHub.SetMoveDirLocal(new Vector2(local.x, local.z));
        _move01 = Mathf.Clamp01(PlanarVelocityWorld.magnitude / Mathf.Max(0.01f, baseSpeed));
        stateHub.SetMoveSpeed01(_move01);
        // Collision may reduce speed, but must never redirect or amplify stored momentum.
        speed = Mathf.Min(speed, Mathf.Max(0f, Vector3.Dot(PlanarVelocityWorld, direction)));
        slideVelocity = direction * speed;
        if (blocked || speed <= baseSpeed * slideExitSpeedMultiplier ||
            (staminaPerUnit > 0f && stamina.Current <= 0f)) CancelSlide();
    }
}
