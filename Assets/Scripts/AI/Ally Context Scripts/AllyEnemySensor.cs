using System.Collections.Generic;
using SingularityGroup.HotReload;
using UnityEngine;
using Log = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Log;

public class AllyEnemySensor : MonoBehaviour
{
    [Header("References")] public CharacteContext CTX;
    
    [Header("Settings")]
    public float detectionRadius = 10f;
    public LayerMask enemyLayer;
    public LayerMask obstacleLayer;
    public float checkInterval = 0.2f;

    [Header("Action Range")]
    public float actionRange = 2.5f;   // ระยะที่ถือว่า "ถึงแล้ว"
    
    [Header("Debug")]
    public Transform currentTarget;

    [Header("Gizmos")]
    public bool drawAlways = false;
    public bool drawLineToTarget = true;
    public float eyeHeight = 1f;

    float _nextCheckTime;
    bool _isTargetInActionRange; // จำสถานะเดิมไว้ (เข้า/ออก)
    public bool HasTarget => currentTarget != null;
    
    private void Update()
    {
        if(CTX.stateHub.Isdown || !CTX.stateHub.IsAlive) return;
        
        
        if (Time.time >= _nextCheckTime)
        {
            _nextCheckTime = Time.time + checkInterval;
            Scan();
        }

        CheckTargetActionRange(); 
    }

    void Scan()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            detectionRadius,
            enemyLayer,
            QueryTriggerInteraction.Ignore
        );

        float closestDistSqr = Mathf.Infinity;
        Transform closest = null;

        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        HashSet<Transform> seenRoots = new HashSet<Transform>();

        foreach (var hit in hits)
        {
            if (!hit) continue;

            var targetCtx = hit.GetComponentInParent<CharacteContext>();
            if (targetCtx == null) continue;

            Transform targetRoot = targetCtx.transform;

            if (!seenRoots.Add(targetRoot)) continue;
            if (targetRoot == transform.root) continue;
            if (targetCtx.stateHub == null) continue;
            if (!targetCtx.stateHub.IsAlive || targetCtx.stateHub.Isdown) continue;

            Vector3 targetPoint = hit.bounds.center; // สำหรับ LOS
            Vector3 dir = targetPoint - origin;
            float rayDist = dir.magnitude;

            if (rayDist <= 0.001f) continue;

            if (Physics.Raycast(origin, dir.normalized, rayDist, obstacleLayer, QueryTriggerInteraction.Ignore))
                continue;

            float distSqr = (targetRoot.position - transform.position).sqrMagnitude;

            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                closest = targetRoot;
            }
        }

        currentTarget = closest;
    }
    void CheckTargetActionRange()
    {
        // ไม่มีเป้าหมาย -> ถ้าเคยอยู่ในระยะ ให้ถือว่าออกระยะ
        
       
        
        if (currentTarget == null)
        {
            if (_isTargetInActionRange)
            {
                _isTargetInActionRange = false;
                OnTargetExitActionRange();
            }
            return;
        }

        float rangeSqr = actionRange * actionRange;
        float distSqr = (currentTarget.position - transform.position).sqrMagnitude;

        bool nowInRange = distSqr <= rangeSqr;

        // เข้าเขตครั้งแรก
        if (nowInRange && !_isTargetInActionRange)
        {
            _isTargetInActionRange = true;
            OnTargetEnterActionRange();
        }
        // ออกจากเขต
        else if (!nowInRange && _isTargetInActionRange)
        {
            _isTargetInActionRange = false;
            OnTargetExitActionRange();
        }

        // อยู่ในเขต 
        if (nowInRange)
        {
            OnTargetStayInActionRange();
        }
    }
    public void RotateToTarget()
    {
        // Vector3 dir = currentTarget.position - transform.position;
        // dir.y = 0f; 
        // Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        // transform.rotation = Quaternion.Lerp(
        //     transform.rotation,
        //     targetRot,
        //    0.05f);
        
        if(!CTX.stateHub.CanMove()) return;
        if (currentTarget == null)
        {
            return;
        }
     
        Vector3 dir = currentTarget.position - transform.position;
        dir.y = 0f;
        
        if (dir.sqrMagnitude < 0.0001f) return;
        
        transform.rotation = Quaternion.LookRotation(dir.normalized);

    }
    
    /// <summary>
    /// ////////////////////// Dosumthing
    /// </summary>
    /// 
    void OnTargetEnterActionRange()
    { 
        if(CTX.stateHub.Isdown || !CTX.stateHub.IsAlive) return;
        if (CTX.stateHub.WeaponSM.CurrentId == WeaponStateId.Melee) return;
   
        RotateToTarget();
        
        CTX.stateHub.RequestOnMelee(CharacterAnimBrain.MeleeType.Heavy);
        Debug.Log("Heavy Attack", this);
      
    }
    
    [SerializeField] private float repeatInterval = 0.7f;
    private float _nextTime;
    
    void OnTargetStayInActionRange()
    {
        if(CTX.stateHub.Isdown || !CTX.stateHub.IsAlive) return;
        if (CTX.stateHub.WeaponSM.CurrentId == WeaponStateId.Melee) return;
        
        RotateToTarget();
        if (Time.time < _nextTime) return;
        _nextTime = Time.time + repeatInterval;
        
        CTX.stateHub.RequestOnMelee(CharacterAnimBrain.MeleeType.Light);
        Debug.Log("Light Attack", this);
    }
   
    void OnTargetExitActionRange()
    { 
        
        if(CTX.stateHub.Isdown || !CTX.stateHub.IsAlive) return;
        if (CTX.stateHub.WeaponSM.CurrentId == WeaponStateId.Melee) return;
        
        // ทำครั้งเดียวตอน "ออก" ระยะ
        RotateToTarget();
        CTX.stateHub.RequestOnMelee(CharacterAnimBrain.MeleeType.Heavy);
        Debug.Log("Heavy Attack", this);
        
    }



    void OnDrawGizmos()
    {
        if (!drawAlways) return;
        DrawSensorGizmos();
    }

    void OnDrawGizmosSelected()
    {
        if (drawAlways) return;
        DrawSensorGizmos();
    }

    void DrawSensorGizmos()
    {
        Gizmos.color = HasTarget ? new Color(0f, 1f, 0f, 0.25f) : new Color(1f, 1f, 0f, 0.25f);
        Gizmos.DrawSphere(transform.position, detectionRadius);

        Gizmos.color = HasTarget ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // วาด action range เพิ่ม (ช่วย debug)
        Gizmos.color = new Color(1f, 0f, 1f, 0.15f);
        Gizmos.DrawSphere(transform.position, actionRange);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, actionRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * eyeHeight, 0.1f);

        if (drawLineToTarget && currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position + Vector3.up * eyeHeight, currentTarget.position);
        }
    }
}
