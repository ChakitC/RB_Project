using System;
using UnityEngine;
using System.Collections;

public class DashSystem : MonoBehaviour
{
    [Header("Dash")]
    public float dashDistance = 5f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.5f;
    public float dashInvincibleTime = 0.15f;
    public float dashCost = 10f;
    public LayerMask obstacleMask = ~0;
    
    [Header("Iframe")]
    [SerializeField] private LayerMask dashIFrameExclude;
    private LayerMask _origCCExclude;
    private LayerMask _origRBExclude;
    
    
    CharacteContext ctx;
    Vector3 lastMoveDir = Vector3.forward;
    bool isDashing, onCooldown;

    public bool IsDashing => isDashing;

    void Awake() => ctx = GetComponent<CharacteContext>();

    private void Start()
    {
       
        _origRBExclude = ctx.rb.excludeLayers;
        if (ctx.cc == null) { return; }
        _origCCExclude = ctx.cc.excludeLayers;
        
    }


    void OnDisable()
    {
        if (ctx != null && ctx.rb != null)
            EndDashIframe();
    }
    
    /// <summary>
    /// //////////--------------------- API ---------------------///////////////////
    /// </summary>
    public void StartDashIframe()
    {
        _origCCExclude = ctx.cc.excludeLayers;
        ctx.cc.excludeLayers |= dashIFrameExclude;

        // ถ้าคุณมี Rigidbody+Collider อื่น ๆ ติดอยู่ด้วย ค่อยทำอันนี้เพิ่ม
        _origRBExclude = ctx.rb.excludeLayers;
        ctx.rb.excludeLayers |= dashIFrameExclude;
    }

    public void EndDashIframe()
    {
        _origRBExclude = ctx.rb.excludeLayers;
        if (ctx.cc == null) { return; }
        _origCCExclude = ctx.cc.excludeLayers;
    }
    
    private Coroutine _dashRoutine;
    private Coroutine _invincibleRoutine;

    public void CancelDash(bool keepCooldown = true)
    {
        
        
        if (_dashRoutine != null)
        {
            StopCoroutine(_dashRoutine);
            _dashRoutine = null;
        }

        if (_invincibleRoutine != null)
        {
            StopCoroutine(_invincibleRoutine);
            _invincibleRoutine = null;
        }

        EndDashIframe();

        if (ctx != null && ctx.HealthSystem != null)
            ctx.HealthSystem.SetInvincible(false);

        isDashing = false;

        if (!keepCooldown)
            onCooldown = false;
    }
    
    
   
    ///////////////////////////////////////////////////////////////////////////////////////////////
 
    
    public bool TryDash()
    {
            if (isDashing || onCooldown || ctx.stateHub.Isdown) return false;

            if (!ctx.StaminaSystem.Spend(dashCost))
            {
                Debug.Log("Not enough staminaSystem");
                return false;
            }

            // --- camera-relative input direction ---
            Vector2 inp = ctx.moveInput;

            Transform cam = Camera.main ? Camera.main.transform : null;

            Vector3 camFwd = cam ? cam.forward : transform.forward;
            Vector3 camRight = cam ? cam.right : transform.right;

            camFwd.y = 0f;
            camRight.y = 0f;

            if (camFwd.sqrMagnitude > 0.0001f) camFwd.Normalize();
            else camFwd = transform.forward;

            if (camRight.sqrMagnitude > 0.0001f) camRight.Normalize();
            else camRight = transform.right;

            Vector3 inputDirWorld = camRight * inp.x + camFwd * inp.y;

            Vector3 dashDir = (inputDirWorld.sqrMagnitude > 0.001f) ? inputDirWorld : lastMoveDir;
            if (dashDir.sqrMagnitude < 0.001f) dashDir = transform.forward;
            dashDir.Normalize();

            if (inputDirWorld.sqrMagnitude > 0.001f) lastMoveDir = dashDir;

            float radius = ctx.cc.radius;
            float height = ctx.cc.height;

            Vector3 center = transform.position + Vector3.up * (height * 0.5f);
            Vector3 p1 = center + Vector3.up * (height * 0.5f - radius);
            Vector3 p2 = center - Vector3.up * (height * 0.5f - radius);

            float maxDist = dashDistance;

            if (Physics.CapsuleCast(p1, p2, radius, dashDir, out var hit, dashDistance, obstacleMask, QueryTriggerInteraction.Ignore))
                maxDist = Mathf.Max(0f, hit.distance - 0.05f);

            if (maxDist <= 0.01f) return false;

            StartDashIframe();

            ctx.stateHub?.ReportDashStarted(dashDuration, dashDir);
            _dashRoutine = StartCoroutine(DashRoutine(dashDir, maxDist));
            return true;
    }
 

    IEnumerator DashRoutine(Vector3 dir, float dist)
    {
        isDashing = true; onCooldown = true;

        float speed = (dist <= 0f) ? 0f : dist / dashDuration;
        _invincibleRoutine = StartCoroutine(InvincibleTimer(dashInvincibleTime));

        float t = 0f;
        while (t < dashDuration)
        {
            ctx.cc.Move(dir * speed * Time.deltaTime);
            t += Time.deltaTime;
            yield return null;
        }

        EndDashIframe();
        isDashing = false;
        yield return new WaitForSeconds(dashCooldown);
        onCooldown = false;
    }

    IEnumerator InvincibleTimer(float time)
    {
        ctx.HealthSystem.SetInvincible(true);
        yield return new WaitForSeconds(time);
        ctx.HealthSystem.SetInvincible(false);
    }
}
