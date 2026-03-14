using UnityEngine;


public class PlayerMovementCC : MonoBehaviour
{
    [Header("Ref")]
    [SerializeField] StateHub stateHub;
    
    
    [Header("Move")]
    [SerializeField] float fallbackMoveSpeed = 6f; 
    [SerializeField] float rampUpTime = 0.25f;   // 0->1 ใช้กี่วินาที
    [SerializeField] float rampDownTime = 0.20f; // 1->0 ใช้กี่วินาที
    float _move01; // ค่า locomotion 0..1 ของเราเอง


    [Header("Slow While Combat")]
    [SerializeField, Range(0.1f, 1f)] float aimSpeedMult = 0.8f;
    [SerializeField, Range(0.1f, 1f)] float fireSpeedMult = 0.65f;
    [SerializeField] float speedLerp = 12f;

    [SerializeField] LayerMask groundMask = ~0;

    private CharacterAnimBrain _brain;
    PlayerContext _characteContext;
    StatsHub statsHub;

    float currentSpeed;

    void Awake()
    {
        _characteContext = GetComponent<PlayerContext>();
        statsHub = GetComponent<StatsHub>();
        if (!stateHub) stateHub = GetComponent<StateHub>(); // ✅ เพิ่ม
        
        _brain = GetComponent<CharacterAnimBrain>();

        
        if (_characteContext != null)
            _characteContext.AnimBrain = _brain;
        
        _brain = GetComponent<CharacterAnimBrain>();
        
        if (_characteContext != null)
            _characteContext.AnimBrain = _brain;

    }

    void Start()
    {
        // init ความเร็วเริ่มต้นจาก hub (ถ้ามี)
        currentSpeed = GetBaseMoveSpeedFromHubOrFallback();
    }

    void Update()
    {
        Move();
    }

    float GetBaseMoveSpeedFromHubOrFallback()
    {
        // ถ้า StatsHub ของคุณมีเมธอดชื่ออื่น เปลี่ยนตรงนี้ได้เลย
        if (statsHub != null)
            return statsHub.GetMoveSpeed();  // <-- แนะนำให้มีเมธอดนี้ใน StatsHub

        // fallback ถ้าไม่มี hub
        // โค้ดเดิมคุณใช้ ctx.baseSpeed
        return (_characteContext != null) ? _characteContext.baseSpeed : fallbackMoveSpeed;
    }

    void HandleAiming(LayerMask groundMask)
    {

        var cameraMain = Camera.main;
        Ray ray = cameraMain.ScreenPointToRay(_characteContext.lookInput);

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
            _characteContext.aimTarget.position = hit.point;
        else
        {
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float d))
                _characteContext.aimTarget.position = ray.GetPoint(d);
        }

        Vector3 dir = _characteContext.aimTarget.position - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 0.2f);
    }

    void Move()
    {
        
        if(!stateHub.CanMove()) {return;}
        
        if (_brain != null && _brain.RootMotionActive)
        {
            _move01 = 0f;
            stateHub.SetMoveSpeed01(0f);
            HandleAiming(groundMask); 
            return;
        }
        
        float baseMoveSpeed = GetBaseMoveSpeedFromHubOrFallback();

        float targetSpeed = baseMoveSpeed;
        if (_characteContext.WeaponSystem != null)
        {
            if (_characteContext.WeaponSystem.isAiming) targetSpeed *= aimSpeedMult;
            if (_characteContext.WeaponSystem.isFiring) targetSpeed *= fireSpeedMult;
        }

        float t = 1f - Mathf.Exp(-speedLerp * Time.deltaTime);
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, t);

        // --- movement ---
        var cameraTransform = Camera.main.transform;
        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right   = Vector3.ProjectOnPlane(cameraTransform.right,   Vector3.up).normalized;

        Vector2 moveInput = _characteContext.moveInput;
        Vector3 moveWorld = forward * moveInput.y + right * moveInput.x;

        // กันเดินทแยงเร็วขึ้น (แต่ยังรองรับจอยได้)
        if (moveWorld.sqrMagnitude > 1f) moveWorld.Normalize();
        
        
        _characteContext.cc.SimpleMove(moveWorld * currentSpeed);
        
        Vector3 moveWorldDir = (moveWorld.sqrMagnitude > 0.0001f) ? moveWorld.normalized : Vector3.zero;
        
        // ---------- NEW: ramp 0..1 ----------
        float input01 = Mathf.Clamp01(moveInput.magnitude);  // WASD=0/1, จอย=0..1
        float target01 = input01;                            // “ไม่สน movespeed” → ใช้ input เป็นหลัก

        float upSpeed   = (rampUpTime   <= 0.0001f) ? 999f : 1f / rampUpTime;
        float downSpeed = (rampDownTime <= 0.0001f) ? 999f : 1f / rampDownTime;

        float speed = (target01 > _move01) ? upSpeed : downSpeed;
        _move01 = Mathf.MoveTowards(_move01, target01, speed * Time.deltaTime);
        stateHub.SetMoveSpeed01(_move01);
    
        // ------------------------------------

        HandleAiming(groundMask);
        
        if (_characteContext.AnimBrain != null)
        {
            Vector3 moveLocal3 = transform.InverseTransformDirection(moveWorldDir);
            _characteContext.AnimBrain.MoveDirLocal = (moveWorldDir.sqrMagnitude < 0.0001f)
                ? Vector2.zero
                : new Vector2(moveLocal3.x, moveLocal3.z);
        }
    }

}
