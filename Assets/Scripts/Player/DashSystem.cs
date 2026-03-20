using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DashSystem : MonoBehaviour
{
    [Header("Dash")]
    public float dashDistance = 5f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.5f;
    public float dashInvincibleTime = 0.15f;
    public float dashCost = 10f;
    public LayerMask obstacleMask = ~0;

    [Header("IFrame")]
    [SerializeField] private LayerMask dashIFrameExclude;
    [SerializeField] private CombatEventBus combatEventBus;

    LayerMask _origCCExclude;
    LayerMask _origRBExclude;

    CharacteContext ctx;
    Vector3 lastMoveDir = Vector3.forward;
    bool isDashing;
    bool onCooldown;
    float _perfectDodgeWindowUntil;
    readonly HashSet<string> _consumedPerfectDodgeAttackIds = new();

    Coroutine _dashRoutine;
    Coroutine _invincibleRoutine;

    public bool IsDashing => isDashing;
    public bool IsPerfectDodgeWindowActive => isDashing && Time.time <= _perfectDodgeWindowUntil;

    void Awake()
    {
        ctx = GetComponent<CharacteContext>();
        if (!combatEventBus)
            combatEventBus = GetComponent<CombatEventBus>();
    }

    void Start()
    {
        if (ctx == null || ctx.rb == null)
            return;

        _origRBExclude = ctx.rb.excludeLayers;
        if (ctx.cc != null)
            _origCCExclude = ctx.cc.excludeLayers;
    }

    void OnDisable()
    {
        ResetPerfectDodgeWindow();
        if (ctx != null && ctx.rb != null)
            EndDashIframe();
    }

    public void StartDashIframe()
    {
        if (ctx == null || ctx.rb == null)
            return;

        if (ctx.cc != null)
        {
            _origCCExclude = ctx.cc.excludeLayers;
            ctx.cc.excludeLayers = _origCCExclude | dashIFrameExclude;
        }

        _origRBExclude = ctx.rb.excludeLayers;
        ctx.rb.excludeLayers = _origRBExclude | dashIFrameExclude;
    }

    public void EndDashIframe()
    {
        if (ctx == null || ctx.rb == null)
            return;

        ctx.rb.excludeLayers = _origRBExclude;

        if (ctx.cc != null)
            ctx.cc.excludeLayers = _origCCExclude;
    }

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
        ResetPerfectDodgeWindow();

        if (ctx != null && ctx.HealthSystem != null)
            ctx.HealthSystem.SetInvincible(false);

        isDashing = false;

        if (!keepCooldown)
            onCooldown = false;
    }

    public bool TryDash()
    {
        if (ctx == null || isDashing || onCooldown || ctx.stateHub.Isdown)
            return false;

        if (!ctx.StaminaSystem.Spend(dashCost))
        {
            Debug.Log("Not enough staminaSystem");
            return false;
        }

        Vector2 input = ctx.moveInput;
        Transform cam = Camera.main ? Camera.main.transform : null;

        Vector3 camFwd = cam ? cam.forward : transform.forward;
        Vector3 camRight = cam ? cam.right : transform.right;
        camFwd.y = 0f;
        camRight.y = 0f;

        if (camFwd.sqrMagnitude > 0.0001f) camFwd.Normalize();
        else camFwd = transform.forward;

        if (camRight.sqrMagnitude > 0.0001f) camRight.Normalize();
        else camRight = transform.right;

        Vector3 inputDirWorld = camRight * input.x + camFwd * input.y;
        Vector3 dashDir = inputDirWorld.sqrMagnitude > 0.001f ? inputDirWorld : lastMoveDir;
        if (dashDir.sqrMagnitude < 0.001f)
            dashDir = transform.forward;
        dashDir.Normalize();

        if (inputDirWorld.sqrMagnitude > 0.001f)
            lastMoveDir = dashDir;

        float radius = ctx.cc.radius;
        float height = ctx.cc.height;

        Vector3 center = transform.position + Vector3.up * (height * 0.5f);
        Vector3 p1 = center + Vector3.up * (height * 0.5f - radius);
        Vector3 p2 = center - Vector3.up * (height * 0.5f - radius);

        float maxDist = dashDistance;
        if (Physics.CapsuleCast(p1, p2, radius, dashDir, out var hit, dashDistance, obstacleMask, QueryTriggerInteraction.Ignore))
            maxDist = Mathf.Max(0f, hit.distance - 0.05f);

        if (maxDist <= 0.01f)
            return false;

        StartDashIframe();
        _consumedPerfectDodgeAttackIds.Clear();
        _perfectDodgeWindowUntil = Time.time + dashInvincibleTime;

        ctx.stateHub?.ReportDashStarted(dashDuration, dashDir);
        PublishDashEvent(PassiveEventType.DashStarted, dashDuration);

        _dashRoutine = StartCoroutine(DashRoutine(dashDir, maxDist));
        return true;
    }

    public bool TryRegisterPerfectDodge(in PassiveEventContext preventedContext)
    {
        if (combatEventBus == null || preventedContext.Type != PassiveEventType.DamagePrevented)
            return false;

        if (!IsPerfectDodgeWindowActive)
            return false;

        if (!string.IsNullOrWhiteSpace(preventedContext.AttackId) &&
            !_consumedPerfectDodgeAttackIds.Add(preventedContext.AttackId))
            return false;

        var dodgeContext = combatEventBus.CreateChildContext(
            preventedContext,
            PassiveEventType.PerfectDodge,
            preventedContext.Source,
            gameObject,
            preventedContext.SourceId,
            preventedContext.AttackId,
            preventedContext.Value,
            preventedContext.Origin,
            preventedContext.OriginPassiveId,
            preventedContext.OriginRuleId);

        combatEventBus.Publish(dodgeContext);
        return true;
    }

    IEnumerator DashRoutine(Vector3 dir, float dist)
    {
        isDashing = true;
        onCooldown = true;

        float speed = dist <= 0f ? 0f : dist / dashDuration;
        _invincibleRoutine = StartCoroutine(InvincibleTimer(dashInvincibleTime));

        float t = 0f;
        while (t < dashDuration)
        {
            ctx.cc.Move(dir * speed * Time.deltaTime);
            t += Time.deltaTime;
            yield return null;
        }

        EndDashIframe();
        ResetPerfectDodgeWindow();
        isDashing = false;
        PublishDashEvent(PassiveEventType.DashEnded, dashCooldown);

        yield return new WaitForSeconds(dashCooldown);
        onCooldown = false;
    }

    IEnumerator InvincibleTimer(float time)
    {
        ctx.HealthSystem.SetInvincible(true);
        yield return new WaitForSeconds(time);
        ctx.HealthSystem.SetInvincible(false);
    }

    void PublishDashEvent(PassiveEventType eventType, float value)
    {
        if (combatEventBus == null || eventType == PassiveEventType.None)
            return;

        var dashContext = combatEventBus.CreateExternalContext(
            eventType,
            gameObject,
            null,
            "dash",
            null,
            value);

        combatEventBus.Publish(dashContext);
    }

    void ResetPerfectDodgeWindow()
    {
        _perfectDodgeWindowUntil = 0f;
        _consumedPerfectDodgeAttackIds.Clear();
    }
}
