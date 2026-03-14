using System.Collections;
using SingularityGroup.HotReload;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class CoverSystem : MonoBehaviour
{
    [Header("ตั้งค่า Cover Detection")]
    [Tooltip("รัศมีค้นหา CoverPoint รอบตัว")]
    public float searchRadius = 2f;

    [Tooltip("Layer ของ CoverPoint / กำแพง (ไว้ใช้กับ OverlapSphere)")]
    public LayerMask coverLayer;

    [Header("ปุ่มควบคุม (Input Manager เดิม)")]
    public KeyCode coverKey = KeyCode.LeftControl; // ปุ่มกดเข้า/ออก coverSystem
    public string aimButton = "Fire2";             // ปุ่มเล็ง (ขวาเมาส์)
    public string shootButton = "Fire1";           // ปุ่มยิง (ซ้ายเมาส์)
    
    [Header("Enter Cover Slide")]
    [Tooltip("ความเร็วในการเลื่อนเข้าไปชิดกำบัง")]
    public float enterCoverSpeed = 6f;

    [Tooltip("ความเร็วในการหมุนไปหันออกจากกำแพง")]
    public float rotateToCoverSpeed = 10f;

    private Coroutine _enterCoverRoutine;
    
    private bool _isEnteringCover;
    public bool IsEnteringCover => _isEnteringCover;

    private CharacterController _cc;
    private Animator _anim;

    private bool _isInCover;       //เอาไว้เช็ตว่า player อยุ่ใน coverSystem หรือป่าว
    private bool _isCrouching;
    
    
    private CoverPoint _currentCover;

    [SerializeField]
    public bool IsInCover   => _isInCover;
    public bool IsCrouching => _isCrouching;
    public CoverPoint CurrentCover => _currentCover;
    
    
    
    private void Awake()
    {
        _cc   = GetComponent<CharacterController>();
    //     _anim = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        HandleCoverToggle();
        HandleCoverLogic();
        // UpdateAnimator();
    }
    
    private void HandleCoverToggle()
    {
      
        
        if (Input.GetKeyDown(coverKey))
        {
            
            if (!_isInCover)
            {
                TryEnterCover();
            }
            else
            {
                ExitCover();
            }
        }
    }

   
    private void HandleCoverLogic()
    {
        if (!_isInCover || _currentCover == null)
            return;

        bool isAiming  = Input.GetButton(aimButton);
        bool isShooting = Input.GetButton(shootButton);

        
        if (_currentCover.height == CoverHeight.Low)
        {
            _isCrouching = !isAiming;
        }
        else 
        {
            _isCrouching = false;
        }

        // ตรงนี้ไม่ได้ยิงให้จริง ๆ แค่บอกว่ากำลังยิงอยู่หรือเปล่า
        // คุณสามารถเอา IsInCover / IsCrouching / isShooting ไปเชื่อมกับระบบปืนเดิมได้
        if (isShooting)
        {
            // TODO: เรียกฟังก์ชันยิงปืนของคุณ เช่น:
            // WeaponSystem.TryShootFromCover(_currentCover, _isCrouching);
        }
    }

    
    private void TryEnterCover()
    {
        CoverPoint best = FindBestCover();
        if (best == null)
            return;
        Debug.Log("Enter Cover");
        _currentCover = best;

        // ตั้งค่าก้มตามประเภทความสูง (ให้ Animator รู้ตั้งแต่เริ่มวิ่งเข้าไป)
        _isCrouching = _currentCover.height == CoverHeight.Low;
        _isInCover   = true;
    
        // คำนวณเป้าหมาย
        Vector3 targetPos = _currentCover.SnapPosition;
        targetPos.y = transform.position.y;

        Vector3 faceDir = -_currentCover.CoverForward;
        faceDir.y = 0f;
        if (faceDir.sqrMagnitude < 0.0001f)
            faceDir = transform.forward;

        // ถ้ามี coroutine ค้างอยู่ ให้หยุดก่อน
        if (_enterCoverRoutine != null)
            StopCoroutine(_enterCoverRoutine);

        _enterCoverRoutine = StartCoroutine(SlideIntoCover(targetPos, faceDir));
    }

    // ----------------------------
    // 4) หา CoverPoint ที่เหมาะที่สุดรอบตัว
    // ----------------------------
    private CoverPoint FindBestCover()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, searchRadius, coverLayer);

        CoverPoint best = null;
        float bestDistSqr = float.MaxValue;

        foreach (var col in hits)
        {
            var cp = col.GetComponentInParent<CoverPoint>();
            if (cp == null)
                continue;

            // เช็คว่าคนเล่น "หันหน้าเข้าหากำแพง" พอสมควร
            Vector3 toCover = cp.SnapPosition - transform.position;
            toCover.y = 0;
            if (toCover.sqrMagnitude < 0.01f)
                continue;

            Vector3 dirToCover = toCover.normalized;
            float dot = Vector3.Dot(dirToCover, transform.forward);

            // dot > 0 : player หันไปทาง coverSystem
            // ถ้า dot ต่ำเกินไป = player หันไปทางอื่น
            if (dot < 0.2f)
                continue;

            float distSqr = toCover.sqrMagnitude;
            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                best = cp;
            }
        }

        return best;
    }

    // ----------------------------
    // 5) ออกจาก coverSystem
    // ----------------------------
    private void ExitCover()
    {
        _isInCover = false;
        _isCrouching = false;
        _currentCover = null;
    }

    // ----------------------------
    // 6) อัพเดต Animator
    // ----------------------------
    // private void UpdateAnimator()
    // {
    //     if (_anim == null)
    //         return;
    //
    //     _anim.SetBool("IsInCover", _isInCover);
    //     _anim.SetBool("IsCrouching", _isCrouching);
    // }

    // ----------------------------
    // 7) Debug ใน Scene View
    // ----------------------------
    
    
    private IEnumerator SlideIntoCover(Vector3 targetPos, Vector3 faceDir)
    {
        _isEnteringCover = true;

        // กันไม่ให้ vector แปลก ๆ
        faceDir.y = 0f;
        if (faceDir.sqrMagnitude > 0.0001f)
            faceDir.Normalize();

        while (true)
        {
            Vector3 currentPos = transform.position;
            Vector3 toTarget   = targetPos - currentPos;
            toTarget.y = 0f;

            float dist = toTarget.magnitude;
            if (dist < 0.05f)
                break; // ใกล้พอแล้ว

            // ทิศทางวิ่งเข้า coverSystem
            Vector3 moveDir = toTarget.normalized;

            // เลื่อนด้วย CharacterController
            if (_cc != null)
            {
                Vector3 move = moveDir * enterCoverSpeed * Time.deltaTime;
                // กัน overshoot (วิ่งเกิน)
                if (move.magnitude > dist)
                    move = toTarget;

                _cc.Move(move);
            }
            else
            {
                // เผื่อกรณีไม่มี CC
                transform.position = Vector3.MoveTowards(
                    currentPos, targetPos, enterCoverSpeed * Time.deltaTime
                );
            }

            // หมุนตัวให้หันออกจากกำแพงแบบนุ่ม ๆ
            if (faceDir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(faceDir);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    rotateToCoverSpeed * Time.deltaTime
                );
            }

            yield return null;
            
        }

        // snap ตำแหน่งสุดท้ายเล็กน้อยให้เป๊ะ
        if (_cc != null)
        {
            _cc.enabled = false;
            Vector3 finalPos = targetPos;
            finalPos.y = transform.position.y;
            transform.position = finalPos;
            _cc.enabled = true;
        }
        else
        {
            transform.position = targetPos;
        }

        _isEnteringCover = false;
        _enterCoverRoutine = null;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, searchRadius);
    }
}
