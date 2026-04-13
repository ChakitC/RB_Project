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
    bool _collisionExcludeCaptured;
    bool _dashIframeActive;

    CharacteContext ctx;
    Vector3 lastMoveDir = Vector3.forward;
    bool isDashing;
    bool onCooldown;
    float _perfectDodgeWindowUntil;
    readonly HashSet<string> _consumedPerfectDodgeAttackIds = new();

    Coroutine _dashRoutine;
    Coroutine _invincibleRoutine;
    Coroutine _cooldownRoutine;
    readonly Dictionary<int, int> _externalCollisionIgnoreMasks = new();
    int _nextExternalCollisionIgnoreToken = 1;

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
    }

    void OnDisable()
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

        ClearCooldown();
        ResetPerfectDodgeWindow();

        ClearCollisionIgnoreRequests();

        if (ctx != null && ctx.HealthSystem != null)
            ctx.HealthSystem.SetInvincible(false);

        isDashing = false;
    }

    public void StartDashIframe()
    {
        if (ctx == null || ctx.rb == null || _dashIframeActive)
            return;

        _dashIframeActive = true;
        RefreshCollisionIgnoreState();
    }

    public void EndDashIframe()
    {
        if (ctx == null || ctx.rb == null || !_dashIframeActive)
            return;

        _dashIframeActive = false;
        RefreshCollisionIgnoreState();
    }

    public int AcquireExternalCollisionIgnoreToken(LayerMask excludeMask)
    {
        if (ctx == null || ctx.rb == null || excludeMask.value == 0)
            return 0;

        int token = _nextExternalCollisionIgnoreToken++;
        _externalCollisionIgnoreMasks[token] = excludeMask.value;
        RefreshCollisionIgnoreState();
        return token;
    }

    public void ReleaseExternalCollisionIgnoreToken(int token)
    {
        if (token <= 0)
            return;

        if (_externalCollisionIgnoreMasks.Remove(token))
            RefreshCollisionIgnoreState();
    }

    public void CancelDash(bool keepCooldown = true)
    {
        bool wasActiveDash = isDashing || _dashRoutine != null;

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

        if (keepCooldown)
        {
            if (wasActiveDash)
                BeginCooldown();
        }
        else
        {
            ClearCooldown();
        }
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
        _dashRoutine = null;
        BeginCooldown();
        PublishDashEvent(PassiveEventType.DashEnded, dashCooldown);
    }

    IEnumerator InvincibleTimer(float time)
    {
        ctx.HealthSystem.SetInvincible(true);
        yield return new WaitForSeconds(time);
        ctx.HealthSystem.SetInvincible(false);
        _invincibleRoutine = null;
    }

    void BeginCooldown()
    {
        if (dashCooldown <= 0f)
        {
            ClearCooldown();
            return;
        }

        if (_cooldownRoutine != null)
            StopCoroutine(_cooldownRoutine);

        onCooldown = true;
        _cooldownRoutine = StartCoroutine(CooldownRoutine(dashCooldown));
    }

    IEnumerator CooldownRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        _cooldownRoutine = null;
        onCooldown = false;
    }

    void RefreshCollisionIgnoreState()
    {
        if (ctx == null || ctx.rb == null)
            return;

        int activeMaskBits = ResolveActiveCollisionIgnoreMaskBits();
        if (activeMaskBits == 0)
        {
            RestoreCapturedCollisionIgnoreState();
            return;
        }

        CaptureCollisionIgnoreState();

        ctx.rb.excludeLayers = _origRBExclude | (LayerMask)activeMaskBits;
        if (ctx.cc != null)
            ctx.cc.excludeLayers = _origCCExclude | (LayerMask)activeMaskBits;
    }

    void CaptureCollisionIgnoreState()
    {
        if (_collisionExcludeCaptured || ctx == null || ctx.rb == null)
            return;

        _origRBExclude = ctx.rb.excludeLayers;
        if (ctx.cc != null)
            _origCCExclude = ctx.cc.excludeLayers;

        _collisionExcludeCaptured = true;
    }

    void RestoreCapturedCollisionIgnoreState()
    {
        if (!_collisionExcludeCaptured || ctx == null || ctx.rb == null)
            return;

        ctx.rb.excludeLayers = _origRBExclude;
        if (ctx.cc != null)
            ctx.cc.excludeLayers = _origCCExclude;

        _collisionExcludeCaptured = false;
    }

    int ResolveActiveCollisionIgnoreMaskBits()
    {
        int bits = _dashIframeActive ? dashIFrameExclude.value : 0;

        foreach (var pair in _externalCollisionIgnoreMasks)
            bits |= pair.Value;

        return bits;
    }

    void ClearCollisionIgnoreRequests()
    {
        _dashIframeActive = false;
        _externalCollisionIgnoreMasks.Clear();
        RestoreCapturedCollisionIgnoreState();
    }

    void ClearCooldown()
    {
        if (_cooldownRoutine != null)
        {
            StopCoroutine(_cooldownRoutine);
            _cooldownRoutine = null;
        }

        onCooldown = false;
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
