using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DashSystem : MonoBehaviour
{
    [Header("Ref")]
    [SerializeField] private Transform actorRoot;
    [SerializeField] private CombatEventBus combatEventBus;

    [Header("Config")]
    [Tooltip("ค่าจูน dash ทั้งหมด จำเป็นต้องมี ไม่งั้น dash จะไม่ทำงาน")]
    [SerializeField] private DashSetting dashSetting;

    LayerMask _origCCExclude;
    LayerMask _origRBExclude;
    bool _collisionExcludeCaptured;
    bool _dashIframeActive;

    CharacteContext ctx;
    Vector3 lastMoveDir = Vector3.forward;
    bool isDashing;
    float _perfectDodgeWindowRemaining;
    int _dashInvincibilityToken;
    int _perfectDodgeSlowHandle;
    bool _perfectDodgeTriggeredThisDash;
    readonly HashSet<string> _consumedPerfectDodgeAttackIds = new();
    readonly Collider[] _perfectDodgeThreatHits = new Collider[24];
    Vector3 _lastDashDirection = Vector3.forward;

    Coroutine _dashRoutine;
    Coroutine _invincibleRoutine;
    readonly Dictionary<int, int> _externalCollisionIgnoreMasks = new();
    int _nextExternalCollisionIgnoreToken = 1;

    public bool IsDashing => isDashing;
    public bool IsPerfectDodgeWindowActive =>
        isDashing && _dashInvincibilityToken != 0 && _perfectDodgeWindowRemaining > 0f;
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

        StopPerfectDodgeSlow();
        ResetPerfectDodgeWindow();

        ClearCollisionIgnoreRequests();

        ReleaseDashInvincibility();

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

    public void CancelDash()
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
        StopPerfectDodgeSlow();
        ResetPerfectDodgeWindow();
        ReleaseDashInvincibility();

        isDashing = false;
    }

    public bool TryDash()
    {
        if (ctx == null || ctx.stateHub == null || ctx.cc == null || ctx.StaminaSystem == null || isDashing || ctx.stateHub.Isdown)
            return false;

        if (dashSetting == null)
        {
            Debug.LogError($"[DashSystem] {name} ไม่ได้ assign DashSetting จึง dash ไม่ได้", this);
            return false;
        }

        if (!ctx.StaminaSystem.CanSpend(dashSetting.dashCost))
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

        float maxDist = dashSetting.dashDistance;
        if (Physics.CapsuleCast(p1, p2, radius, dashDir, out var hit, dashSetting.dashDistance, dashSetting.obstacleMask, QueryTriggerInteraction.Ignore))
            maxDist = Mathf.Max(0f, hit.distance - 0.05f);

        if (maxDist <= 0.01f)
            return false;

        if (!ctx.StaminaSystem.Spend(dashSetting.dashCost))
            return false;

        if (!ShouldUseBackwardDashAnimation(dashDirLocalBeforeTurn))
            FaceDashDirection(dashDir);
        StartDashIframe();
        _consumedPerfectDodgeAttackIds.Clear();
        _perfectDodgeTriggeredThisDash = false;
        _perfectDodgeWindowRemaining = ResolvePerfectDodgeWindow();

        ctx.stateHub?.ReportDashStarted(dashSetting.dashDuration, dashDir);
        PublishDashEvent(PassiveEventType.DashStarted, dashSetting.dashDuration);

        _dashRoutine = StartCoroutine(DashRoutine(dashDir, maxDist));
        return true;
    }

    public bool TryRegisterPerfectDodge(in PassiveEventContext preventedContext)
    {
        if (preventedContext.Type != PassiveEventType.DamagePrevented)
            return false;

        if (!IsPerfectDodgeWindowActive)
            return false;

        if (!string.IsNullOrWhiteSpace(preventedContext.AttackId) &&
            !_consumedPerfectDodgeAttackIds.Add(preventedContext.AttackId))
            return false;

        if (combatEventBus != null)
        {
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
        }

        TriggerPerfectDodgeSlow();
        return true;
    }

    IEnumerator DashRoutine(Vector3 dir, float dist)
    {
        isDashing = true;

        float dashDuration = Mathf.Max(0.01f, dashSetting.dashDuration);
        float speed = dist <= 0f ? 0f : dist / dashDuration;
        _invincibleRoutine = StartCoroutine(InvincibleTimer(dashSetting.dashInvincibleTime));

        float t = 0f;
        while (t < dashDuration)
        {
            TryDetectPerfectDodgeThreat();

            float dt = ActorDeltaTime;
            if (_perfectDodgeWindowRemaining > 0f)
                _perfectDodgeWindowRemaining = Mathf.Max(0f, _perfectDodgeWindowRemaining - dt);

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
        AcquireDashInvincibility();

        float remaining = time;
        while (remaining > 0f)
        {
            yield return null;
            remaining -= ActorDeltaTime;
        }

        ReleaseDashInvincibility();
        _invincibleRoutine = null;
    }

    void TryDetectPerfectDodgeThreat()
    {
        if (_perfectDodgeTriggeredThisDash || !IsPerfectDodgeWindowActive || ctx == null || ctx.cc == null)
            return;

        if (dashSetting == null || dashSetting.perfectDodgeThreatLayers.value == 0)
            return;

        float radius = ctx.cc.radius + dashSetting.perfectDodgeThreatScanPadding;
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
            dashSetting.perfectDodgeThreatLayers,
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

            if (!IsValidPerfectDodgeThreat(hitRoot))
                continue;

            if (!TryPublishExternalPerfectDodge(hitRoot.gameObject))
                continue;

            TriggerPerfectDodgeSlow();
            return;
        }
    }

    static bool IsValidPerfectDodgeThreat(Transform threatRoot)
    {
        var threatContext = threatRoot.GetComponentInChildren<CharacteContext>(true);
        if (threatContext == null)
            return true;

        HealthSystem threatHealth = threatContext.HealthSystem;
        return threatHealth == null || threatHealth.IsAlive;
    }

    bool TryPublishExternalPerfectDodge(GameObject source)
    {
        string attackId = source != null ? $"dash-threat:{source.GetInstanceID()}" : null;
        if (!string.IsNullOrWhiteSpace(attackId) && !_consumedPerfectDodgeAttackIds.Add(attackId))
            return false;

        if (combatEventBus == null)
            return true;

        var dodgeContext = combatEventBus.CreateExternalContext(
            PassiveEventType.PerfectDodge,
            source,
            ResolveActorGameObject(),
            "dash:threat-scan",
            attackId,
            0f);

        combatEventBus.Publish(dodgeContext);
        return true;
    }

    void TriggerPerfectDodgeSlow()
    {
        if (_perfectDodgeTriggeredThisDash || dashSetting == null)
            return;

        _perfectDodgeTriggeredThisDash = true;

        float slowScale = dashSetting.perfectDashSlowScale;
        float slowDuration = dashSetting.perfectDashSlowDuration;

        ctx?.WeaponSystem?.GrantFreeAmmo(dashSetting.perfectDashFreeAmmoDuration);
        _perfectDodgeSlowHandle = TimeSlowManager.Instance.StartSlow(
            slowScale, slowDuration, dashSetting.perfectDashSlowShape);

        bool handled = PerfectDodgeHandler != null &&
            PerfectDodgeHandler(_lastDashDirection, slowDuration, slowScale);

        if (!handled)
            PerfectDashScreenFx.Instance?.Play(_lastDashDirection, slowDuration, slowScale);
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
        int bits = _dashIframeActive && dashSetting != null ? dashSetting.dashIFrameExclude.value : 0;

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
        _perfectDodgeWindowRemaining = 0f;
        _perfectDodgeSlowHandle = 0;
        _perfectDodgeTriggeredThisDash = false;
        _consumedPerfectDodgeAttackIds.Clear();
    }

    float ResolvePerfectDodgeWindow()
    {
        if (dashSetting == null)
            return 0f;

        float iframe = dashSetting.dashInvincibleTime;
        float window = dashSetting.perfectDodgeWindow > 0f ? dashSetting.perfectDodgeWindow : iframe;
        return Mathf.Min(window, iframe);
    }

    float ActorDeltaTime =>
        ctx == null || ctx.UsesWorldSlow
            ? TimeSlowManager.Instance.WorldDeltaTime
            : Time.deltaTime;

    void AcquireDashInvincibility()
    {
        if (_dashInvincibilityToken != 0 || ctx == null || ctx.HealthSystem == null)
            return;

        _dashInvincibilityToken = ctx.HealthSystem.AcquireInvincibilityToken();
    }

    void ReleaseDashInvincibility()
    {
        if (_dashInvincibilityToken == 0)
            return;

        if (ctx != null && ctx.HealthSystem != null)
            ctx.HealthSystem.ReleaseInvincibilityToken(_dashInvincibilityToken);

        _dashInvincibilityToken = 0;
    }

    void StopPerfectDodgeSlow()
    {
        if (_perfectDodgeSlowHandle == 0)
            return;

        TimeSlowManager.Instance.StopSlow(_perfectDodgeSlowHandle);
        _perfectDodgeSlowHandle = 0;
    }
}
