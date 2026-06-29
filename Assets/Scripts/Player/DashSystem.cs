using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DashSystem : MonoBehaviour
{
    [Header("Ref")]
    [SerializeField] private Transform actorRoot;

    [Header("Dash")]
    public float dashDistance = 5f;
    public float dashDuration = 0.15f;
    [HideInInspector] public float dashCooldown = 0f;
    public float dashInvincibleTime = 0.15f;
    public float dashCost = 10f;
    public LayerMask obstacleMask = ~0;

    [Header("IFrame")]
    [SerializeField] private LayerMask dashIFrameExclude;
    [SerializeField] private CombatEventBus combatEventBus;

    [Header("Perfect Dodge")]
    [SerializeField, Range(0.05f, 1f)] private float perfectDashSlowScale = 0.2f;
    [SerializeField, Min(0f)] private float perfectDashSlowDuration = 0.35f;
    [SerializeField] private AnimationCurve perfectDashSlowShape;
    [SerializeField, Min(0f)] private float perfectDashFreeAmmoDuration = 0.35f;
    [SerializeField] private LayerMask perfectDodgeThreatLayers;
    [SerializeField, Min(0f)] private float perfectDodgeThreatScanPadding = 0.25f;

    LayerMask _origCCExclude;
    LayerMask _origRBExclude;
    bool _collisionExcludeCaptured;
    bool _dashIframeActive;

    CharacteContext ctx;
    Vector3 lastMoveDir = Vector3.forward;
    bool isDashing;
    float _perfectDodgeWindowUntil;
    bool _perfectDodgeTriggeredThisDash;
    readonly HashSet<string> _consumedPerfectDodgeAttackIds = new();
    readonly Collider[] _perfectDodgeThreatHits = new Collider[24];
    Vector3 _lastDashDirection = Vector3.forward;

    Coroutine _dashRoutine;
    Coroutine _invincibleRoutine;
    readonly Dictionary<int, int> _externalCollisionIgnoreMasks = new();
    int _nextExternalCollisionIgnoreToken = 1;

    public bool IsDashing => isDashing;
    public bool IsPerfectDodgeWindowActive => isDashing && Time.unscaledTime <= _perfectDodgeWindowUntil;
    public Func<Vector3, float, float, bool> PerfectDodgeHandler;
    Transform ActorRoot => actorRoot ? actorRoot : transform;

    void Awake()
    {
        ResolveReferences();
    }

    void ResolveReferences()
    {
        if (!actorRoot)
        {
            CharacteContext parentContext = GetComponentInParent<CharacteContext>();
            actorRoot = parentContext ? parentContext.transform : transform;
        }

        ctx = actorRoot.GetComponent<CharacteContext>();
        if (ctx == null)
            ctx = GetComponentInParent<CharacteContext>();

        ctx?.ResolveReferences();

        if (!combatEventBus)
            combatEventBus = ctx != null ? ctx.CombatEventBus : null;
        if (!combatEventBus)
            combatEventBus = actorRoot.GetComponent<CombatEventBus>();
        if (!combatEventBus)
            combatEventBus = GetComponentInParent<CombatEventBus>();

        if (ctx != null)
        {
            if (ctx.DashSystem == null)
                ctx.DashSystem = this;

            lastMoveDir = ActorRoot.forward;
        }
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

    }

    public bool TryDash()
    {
        if (ctx == null || ctx.stateHub == null || ctx.cc == null || ctx.StaminaSystem == null || isDashing || ctx.stateHub.Isdown)
            return false;

        if (!ctx.StaminaSystem.Spend(dashCost))
        {
            Debug.Log("Not enough staminaSystem");
            return false;
        }

        Vector2 input = ctx.moveInput;
        Transform cam = Camera.main ? Camera.main.transform : null;
        Transform root = ActorRoot;

        Vector3 camFwd = cam ? cam.forward : root.forward;
        Vector3 camRight = cam ? cam.right : root.right;
        camFwd.y = 0f;
        camRight.y = 0f;

        if (camFwd.sqrMagnitude > 0.0001f) camFwd.Normalize();
        else camFwd = root.forward;

        if (camRight.sqrMagnitude > 0.0001f) camRight.Normalize();
        else camRight = root.right;

        Vector3 inputDirWorld = camRight * input.x + camFwd * input.y;
        Vector3 dashDir = inputDirWorld.sqrMagnitude > 0.001f ? inputDirWorld : lastMoveDir;
        if (dashDir.sqrMagnitude < 0.001f)
            dashDir = root.forward;
        dashDir.Normalize();
        _lastDashDirection = dashDir;

        Vector3 dashDirLocalBeforeTurn = root.InverseTransformDirection(dashDir);

        if (inputDirWorld.sqrMagnitude > 0.001f)
            lastMoveDir = dashDir;

        float radius = ctx.cc.radius;
        float height = ctx.cc.height;

        Vector3 center = ctx.cc.transform.TransformPoint(ctx.cc.center);
        float capsuleHalfLine = Mathf.Max(0f, height * 0.5f - radius);
        Vector3 p1 = center + Vector3.up * capsuleHalfLine;
        Vector3 p2 = center - Vector3.up * capsuleHalfLine;

        float maxDist = dashDistance;
        if (Physics.CapsuleCast(p1, p2, radius, dashDir, out var hit, dashDistance, obstacleMask, QueryTriggerInteraction.Ignore))
            maxDist = Mathf.Max(0f, hit.distance - 0.05f);

        if (maxDist <= 0.01f)
            return false;

        if (!ShouldUseBackwardDashAnimation(dashDirLocalBeforeTurn))
            FaceDashDirection(dashDir);
        StartDashIframe();
        _consumedPerfectDodgeAttackIds.Clear();
        _perfectDodgeTriggeredThisDash = false;
        _perfectDodgeWindowUntil = Time.unscaledTime + dashInvincibleTime;

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
            ResolveActorGameObject(),
            preventedContext.EventSourceId,
            preventedContext.AttackId,
            preventedContext.Value,
            preventedContext.Origin,
            preventedContext.OriginPassiveId,
            preventedContext.OriginRuleId);

        combatEventBus.Publish(dodgeContext);
        TriggerPerfectDodgeSlow();
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
            TryDetectPerfectDodgeThreat();

            float dt = Time.unscaledDeltaTime;
            ctx.cc.Move(dir * speed * dt);
            t += dt;
            yield return null;
        }

        EndDashIframe();
        ResetPerfectDodgeWindow();
        isDashing = false;
        _dashRoutine = null;
        PublishDashEvent(PassiveEventType.DashEnded, 0f);
    }

    IEnumerator InvincibleTimer(float time)
    {
        if (ctx != null && ctx.HealthSystem != null)
            ctx.HealthSystem.SetInvincible(true);
        yield return new WaitForSecondsRealtime(time);
        if (ctx != null && ctx.HealthSystem != null)
            ctx.HealthSystem.SetInvincible(false);
        _invincibleRoutine = null;
    }

    void TryDetectPerfectDodgeThreat()
    {
        if (_perfectDodgeTriggeredThisDash || !IsPerfectDodgeWindowActive || ctx == null || ctx.cc == null)
            return;

        if (perfectDodgeThreatLayers.value == 0)
            return;

        float radius = ctx.cc.radius + perfectDodgeThreatScanPadding;
        float height = ctx.cc.height;
        Vector3 center = ctx.cc.transform.TransformPoint(ctx.cc.center);
        float capsuleHalfLine = Mathf.Max(0f, height * 0.5f - ctx.cc.radius);
        Vector3 p1 = center + Vector3.up * capsuleHalfLine;
        Vector3 p2 = center - Vector3.up * capsuleHalfLine;

        int count = Physics.OverlapCapsuleNonAlloc(
            p1,
            p2,
            radius,
            _perfectDodgeThreatHits,
            perfectDodgeThreatLayers,
            QueryTriggerInteraction.Collide);

        Transform actorRootTransform = ActorRoot.root;
        for (int i = 0; i < count; i++)
        {
            Collider hit = _perfectDodgeThreatHits[i];
            _perfectDodgeThreatHits[i] = null;
            if (hit == null)
                continue;

            Transform hitRoot = hit.attachedRigidbody != null
                ? hit.attachedRigidbody.transform.root
                : hit.transform.root;

            if (hitRoot == null || hitRoot == actorRootTransform)
                continue;

            PublishExternalPerfectDodge(hitRoot.gameObject);
            TriggerPerfectDodgeSlow();
            return;
        }
    }

    void PublishExternalPerfectDodge(GameObject source)
    {
        if (combatEventBus == null)
            return;

        string attackId = source != null ? $"dash-threat:{source.GetInstanceID()}" : null;
        if (!string.IsNullOrWhiteSpace(attackId) && !_consumedPerfectDodgeAttackIds.Add(attackId))
            return;

        var dodgeContext = combatEventBus.CreateExternalContext(
            PassiveEventType.PerfectDodge,
            source,
            ResolveActorGameObject(),
            "dash:threat-scan",
            attackId,
            0f);

        combatEventBus.Publish(dodgeContext);
    }

    void TriggerPerfectDodgeSlow()
    {
        if (_perfectDodgeTriggeredThisDash)
            return;

        _perfectDodgeTriggeredThisDash = true;
        ctx?.WeaponSystem?.GrantFreeAmmo(perfectDashFreeAmmoDuration);
        TimeSlowManager.Instance.StartSlow(perfectDashSlowScale, perfectDashSlowDuration, perfectDashSlowShape);

        bool handled = PerfectDodgeHandler != null &&
            PerfectDodgeHandler(_lastDashDirection, perfectDashSlowDuration, perfectDashSlowScale);

        if (!handled)
            PerfectDashScreenFx.Instance?.Play(_lastDashDirection, perfectDashSlowDuration, perfectDashSlowScale);
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

    void FaceDashDirection(Vector3 dashDir)
    {
        dashDir.y = 0f;
        if (dashDir.sqrMagnitude <= 0.0001f)
            return;

        ActorRoot.rotation = Quaternion.LookRotation(dashDir.normalized, Vector3.up);
    }

    bool ShouldUseBackwardDashAnimation(Vector3 dashDirLocal)
    {
        Vector2 localPlanar = new Vector2(dashDirLocal.x, dashDirLocal.z);
        if (localPlanar.sqrMagnitude <= 0.0001f)
            return false;

        return localPlanar.y < 0f && Mathf.Abs(localPlanar.y) >= Mathf.Abs(localPlanar.x);
    }

    void PublishDashEvent(PassiveEventType eventType, float value)
    {
        if (combatEventBus == null || eventType == PassiveEventType.None)
            return;

        var dashContext = combatEventBus.CreateExternalContext(
            eventType,
            ResolveActorGameObject(),
            null,
            "dash",
            null,
            value);

        combatEventBus.Publish(dashContext);
    }

    GameObject ResolveActorGameObject()
    {
        if (ctx != null)
            return ctx.gameObject;

        Transform root = ActorRoot;
        return root ? root.gameObject : gameObject;
    }

    void ResetPerfectDodgeWindow()
    {
        _perfectDodgeWindowUntil = 0f;
        _perfectDodgeTriggeredThisDash = false;
        _consumedPerfectDodgeAttackIds.Clear();
    }
}
