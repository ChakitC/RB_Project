using UnityEngine;

public class PlayerMovementCC : MonoBehaviour
{
    [Header("Ref")]
    [SerializeField] Transform actorRoot;
    [SerializeField] StateHub stateHub;

    [Header("Move")]
    [SerializeField] float fallbackMoveSpeed = 6f;
    [SerializeField] float rampUpTime = 0.25f;
    [SerializeField] float rampDownTime = 0.20f;
    [SerializeField, Min(0f)] float rotationSharpness = 14f;
    float _move01;

    [Header("Slow While Combat")]
    [SerializeField, Range(0.1f, 1f)] float aimSpeedMult = 0.8f;
    [SerializeField, Range(0.1f, 1f)] float fireSpeedMult = 0.65f;
    [SerializeField, Range(0.1f, 1f)] float backwardSpeedMult = 0.6f;
    [SerializeField, Range(0.1f, 1f)] float strafeSpeedMult = 0.75f;
    [SerializeField] float speedLerp = 12f;

    [SerializeField] LayerMask groundMask = ~0;
    [SerializeField] LayerMask characterSeparationLayers;

    CharacterAnimBrain _brain;
    PlayerContext _characteContext;
    StatsHub statsHub;
    CharacterController _characterController;

    float currentSpeed;

    void Awake()
    {
        ResolveReferences();
    }

    void ResolveReferences()
    {
        if (!actorRoot)
        {
            PlayerContext parentContext = GetComponentInParent<PlayerContext>();
            parentContext?.ResolveReferences();
            actorRoot = parentContext ? parentContext.transform : transform;
        }

        _characteContext = actorRoot.GetComponent<PlayerContext>();
        if (!_characteContext)
            _characteContext = GetComponentInParent<PlayerContext>();

        _characteContext?.ResolveReferences();

        statsHub = _characteContext != null ? _characteContext.StatsHub : null;
        if (!statsHub)
            statsHub = actorRoot.GetComponent<StatsHub>();
        if (!statsHub)
            statsHub = GetComponentInParent<StatsHub>();

        if (!stateHub)
            stateHub = _characteContext != null ? _characteContext.stateHub : null;
        if (!stateHub)
            stateHub = actorRoot.GetComponent<StateHub>();
        if (!stateHub)
            stateHub = GetComponentInParent<StateHub>();

        _brain = _characteContext != null ? _characteContext.AnimBrain : null;
        if (!_brain)
            _brain = actorRoot.GetComponent<CharacterAnimBrain>();
        if (!_brain)
            _brain = actorRoot.GetComponentInChildren<CharacterAnimBrain>(true);
        if (!_brain)
            _brain = GetComponentInParent<CharacterAnimBrain>();

        _characterController = _characteContext != null ? _characteContext.cc : null;
        if (!_characterController)
            _characterController = actorRoot.GetComponent<CharacterController>();

        if (_characteContext != null)
        {
            if (_characteContext.movement == null)
                _characteContext.movement = this;
            if (_characteContext.cc == null)
                _characteContext.cc = _characterController;
            if (_brain != null)
                _characteContext.AnimBrain = _brain;
        }
    }

    void Start()
    {
        currentSpeed = GetBaseMoveSpeedFromHubOrFallback();
    }

    void Update()
    {
        Move();
        ResolveCharacterOverlaps();
    }

    float GetBaseMoveSpeedFromHubOrFallback()
    {
        if (_characteContext != null)
            return _characteContext.GetMoveSpeedForCurrentLifeState();

        if (statsHub != null)
            return statsHub.GetMoveSpeed();

        return fallbackMoveSpeed;
    }

    void ResolveCharacterOverlaps()
    {
        if (_characteContext == null || _characteContext.ColliderRefs == null) return;
        if (_characterController == null || characterSeparationLayers == 0) return;

        Collider playerCol = _characteContext.ColliderRefs.CharacterPositionCollider;
        if (playerCol == null) return;

        Bounds b = playerCol.bounds;
        Collider[] hits = Physics.OverlapSphere(b.center, b.extents.magnitude, characterSeparationLayers, QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (hit == playerCol) continue;

            if (!Physics.ComputePenetration(
                playerCol, playerCol.transform.position, playerCol.transform.rotation,
                hit, hit.transform.position, hit.transform.rotation,
                out Vector3 dir, out float dist))
                continue;

            Vector3 planarDirection = ResolvePlanarSeparationDirection(playerCol, hit, dir);
            if (planarDirection.sqrMagnitude > 0.0001f)
                _characterController.Move(planarDirection * dist);
        }
    }

    Vector3 ResolvePlanarSeparationDirection(Collider playerCol, Collider hit, Vector3 penetrationDirection)
    {
        Vector3 planarDirection = Vector3.ProjectOnPlane(penetrationDirection, Vector3.up);
        if (planarDirection.sqrMagnitude <= 0.0001f)
        {
            planarDirection = playerCol.bounds.center - hit.bounds.center;
            planarDirection.y = 0f;
        }

        if (planarDirection.sqrMagnitude <= 0.0001f)
        {
            Transform fallbackRoot = actorRoot ? actorRoot : transform;
            planarDirection = Vector3.ProjectOnPlane(fallbackRoot.forward, Vector3.up);
        }

        return planarDirection.sqrMagnitude > 0.0001f
            ? planarDirection.normalized
            : Vector3.zero;
    }

    void HandleFacing(Vector3 movementDirection)
    {
        if (stateHub != null && !stateHub.CanRotate())
            return;

        Transform rotateRoot = actorRoot ? actorRoot : transform;
        Vector3 facingDirection = movementDirection;
        GameplayCameraController gameplayCamera = GameplayCameraController.Instance;
        bool combatAligned = gameplayCamera != null && gameplayCamera.HasCombatAlignment;

        if (combatAligned)
            facingDirection = gameplayCamera.PlanarForward;

        facingDirection = Vector3.ProjectOnPlane(facingDirection, Vector3.up);
        if (facingDirection.sqrMagnitude <= 0.0001f)
            return;

        float blend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
        rotateRoot.rotation = Quaternion.Slerp(
            rotateRoot.rotation,
            Quaternion.LookRotation(facingDirection.normalized, Vector3.up),
            blend);
    }

    void Move()
    {
        if (_characteContext == null || stateHub == null || _characterController == null)
            return;

        if (!stateHub.CanMove())
        {
            _move01 = 0f;
            stateHub.SetMoveSpeed01(0f);
            return;
        }

        if (_brain != null && _brain.RootMotionActive)
        {
            _move01 = 0f;
            stateHub.SetMoveSpeed01(0f);
            HandleFacing(Vector3.zero);
            return;
        }

        var cameraMain = Camera.main;
        if (cameraMain == null)
            return;

        var cameraTransform = cameraMain.transform;
        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;

        Vector2 moveInput = _characteContext.moveInput;
        Vector3 moveWorld = forward * moveInput.y + right * moveInput.x;

        if (moveWorld.sqrMagnitude > 1f)
            moveWorld.Normalize();

        Vector3 moveWorldDir = moveWorld.sqrMagnitude > 0.0001f ? moveWorld.normalized : Vector3.zero;
        Transform movementRoot = actorRoot ? actorRoot : transform;
        Vector3 moveLocal3 = movementRoot.InverseTransformDirection(moveWorldDir);

        float baseMoveSpeed = GetBaseMoveSpeedFromHubOrFallback();
        float targetSpeed = baseMoveSpeed;
        float directionalSpeedMult = 1f;

        if (moveLocal3.z < 0f)
        {
            float backwardAmount = Mathf.Clamp01(-moveLocal3.z);
            directionalSpeedMult = Mathf.Min(
                directionalSpeedMult,
                Mathf.Lerp(1f, backwardSpeedMult, backwardAmount));
        }

        float strafeAmount = Mathf.Clamp01(Mathf.Abs(moveLocal3.x));
        if (strafeAmount > 0f)
        {
            directionalSpeedMult = Mathf.Min(
                directionalSpeedMult,
                Mathf.Lerp(1f, strafeSpeedMult, strafeAmount));
        }

        targetSpeed *= directionalSpeedMult;

        if (_characteContext.WeaponSystem != null)
        {
            if (_characteContext.WeaponSystem.IsAiming)
                targetSpeed *= aimSpeedMult;
            if (_characteContext.WeaponSystem.IsFiringActivity)
                targetSpeed *= fireSpeedMult;
        }

        float t = 1f - Mathf.Exp(-speedLerp * Time.deltaTime);
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, t);
        if (stateHub.Isdown && currentSpeed > targetSpeed)
            currentSpeed = targetSpeed;

        // Planar only. Vertical motion is owned by CharacterVerticalMotor, so this must not use
        // SimpleMove: SimpleMove applies its own gravity, and this call sits behind early-returns
        // that would silently disable gravity while stunned, dashing, or dead.
        _characterController.Move(moveWorld * (currentSpeed * Time.deltaTime));

        float input01 = Mathf.Clamp01(moveInput.magnitude);
        float target01 = input01;

        float upSpeed = rampUpTime <= 0.0001f ? 999f : 1f / rampUpTime;
        float downSpeed = rampDownTime <= 0.0001f ? 999f : 1f / rampDownTime;

        float speed = target01 > _move01 ? upSpeed : downSpeed;
        _move01 = Mathf.MoveTowards(_move01, target01, speed * Time.deltaTime);
        stateHub.SetMoveSpeed01(_move01);

        HandleFacing(moveWorldDir);

        stateHub.SetMoveDirLocal(
            moveWorldDir.sqrMagnitude < 0.0001f
                ? Vector2.zero
                : new Vector2(moveLocal3.x, moveLocal3.z));
    }
}
