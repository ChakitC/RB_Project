using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class AIShootManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform firePoint;      // จุดยิงกระสุน
    [SerializeField] private GameObject bulletPrefab;  // Prefab กระสุนของคุณ
    [SerializeField]  private CharacteContext  CTX;
    
    
    public float maxRange = 30f;

    [Header("AI Settings")]
    [Tooltip("เลเยอร์ที่ใช้เช็คของบัง เช่น กำแพง พื้น (ไม่จำเป็นต้องใส่ layer ของศัตรู)")]
    public LayerMask obstacleMask;
    public float spreadAngle = 1.5f;    // กระจายเล็ง
    public float rotateSpeed = 15f;     // ความเร็วการหันไปหาเป้า

    private Transform _target;
    private bool _isFiring;
    private float _nextFireTime;

    public bool HasTarget => _target != null;


    private void Start()
    {
        if (CTX == null) { CTX = GetComponent<CharacteContext>(); }
        if(bulletPrefab == null) {bulletPrefab = CTX.currentWeapon.BulletPrefab;}
    }


    // ----------------------------------------------------
    // API ให้ Behavior Tree / AI script เรียกใช้
    // ----------------------------------------------------
    public void SetTarget(Transform target)
    {
        _target = target;
    }

    public void ClearTarget()
    {
        _target = null;
        _isFiring = false;
    }

    public void StartFire()
    {
        _isFiring = true;
    }

    public void StopFire()
    {
        _isFiring = false;
    }

    public bool IsTargetInRange()
    {
        if (_target == null) return false;
        float sqrDist = (_target.position - transform.position).sqrMagnitude;
        return sqrDist <= maxRange * maxRange;
    }
    
    private void Update()
    {
        if (CTX.stateHub.Isdown || !CTX.stateHub.IsAlive) return;
        if (_target == null) return;

        // RotateToTarget();
        
        if (!_isFiring) return;
        if (!IsTargetInRange()) return;
        if (!HasLineOfSight()) return;
        
    }

    // ----------------------------------------------------
    // เล็ง
    // ----------------------------------------------------
    // private void RotateToTarget()
    // {
    //     if(!CTX.stateHub.CanMove()) return;
    //     Vector3 dir = _target.position - transform.position;
    //     dir.y = 0f; // ไม่หันขึ้นลง
    //
    //     if (dir.sqrMagnitude < 0.001f) return;
    //
    //     Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
    //     transform.rotation = Quaternion.Lerp(
    //         transform.rotation,
    //         targetRot,
    //         rotateSpeed * Time.deltaTime
    //     );
    // }

    // ----------------------------------------------------
    // เช็คว่ามีของบังระหว่างปากกระบอกกับเป้าหรือเปล่า
    // ----------------------------------------------------
    private bool HasLineOfSight()
    {
        if (_target == null) return false;

        Vector3 origin = firePoint != null
            ? firePoint.position
            : transform.position + Vector3.up;

        // เล็งกลางตัวเป้า + ยกขึ้นนิดหน่อย
        Vector3 targetPos = _target.position + Vector3.up;
        Vector3 dir = (targetPos - origin).normalized;

        // ยิง ray เช็คของบัง
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxRange, ~0, QueryTriggerInteraction.Ignore))
        {
            // ถ้าเจออย่างแรกเป็นเป้า หรือ root ของเป้า → ยิงได้
            if (hit.transform == _target || hit.transform.root == _target)
                return true;

            // ถ้าเจออะไรที่ไม่ใช่เป้า และอยู่บน obstacleMask → ถือว่ามีของบัง
            if (((1 << hit.collider.gameObject.layer) & obstacleMask) != 0)
                return false;

            // ถ้าเจออย่างอื่นที่ไม่ใช่ obstacleMask → แล้วแต่ดีไซน์ จะถือว่าบังหรือไม่บังก็ได้
        }

        // ไม่ชนอะไรเลย → มองว่ามองเห็น
        return true;
    }
    

    private Vector3 ApplySpread(Vector3 dir)
    {
        if (spreadAngle <= 0f) return dir;

        float yaw = Random.Range(-spreadAngle, spreadAngle);
        float pitch = Random.Range(-spreadAngle, spreadAngle);

        Quaternion spreadRot = Quaternion.Euler(pitch, yaw, 0f);
        return spreadRot * dir;
    }
}
