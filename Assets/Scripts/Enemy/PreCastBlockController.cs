using System;
using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PreCastBlockController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform indicatorAnchor;

    [Header("Block Window VFX")]
    [SerializeField, AssetsOnly, AssetSelector(Paths = "Assets", Filter = "t:Prefab", FlattenTreeView = true)]
    [LabelText("VFX Prefab"), PreviewField(60, ObjectFieldAlignment.Left)]
    private GameObject blockWindowVfxPrefab;
    [SerializeField] private Transform blockWindowVfxAnchor;
    [SerializeField] private Vector3 blockWindowVfxLocalOffset;
    [SerializeField] private Vector3 blockWindowVfxRotationEuler;
    [SerializeField, Min(0f)] private float blockWindowVfxScale = 1f;
    [SerializeField] private bool parentBlockWindowVfxToAnchor = true;
    [SerializeField] private bool useAnchorRotationForBlockWindowVfx = true;
    [SerializeField] private bool allowBlockWindowVfxParticlesToFinish = true;
    [SerializeField, Min(0f)] private float blockWindowVfxExtraLife;

    [Header("Debug")]
    [SerializeField] private bool logBlocks;
    [SerializeField] private bool logPreCastFlow;

    ActiveSkillCastInfo activeCast;
    SkillGemDefinition activeSkillDef;
    GameObject activeIndicator;
    GameObject activeBlockWindowVfx;
    bool hasActiveBlockableCast;
    bool preCastWindowOpen;
    EnemyContext ctx;

    bool _hasReservation;
    ActiveSkillCastInfo _reservedCast;
    GameObject _reservationSource;
    SkillPreCastHoldHandle _holdHandle;
    int _reservationId;
    int _reservationCounter;

    public bool HasActiveBlockableCast => hasActiveBlockableCast;
    public bool IsPreCastWindowOpen => preCastWindowOpen;
    public int ActiveRequestId => hasActiveBlockableCast ? activeCast.RequestId : 0;
    public SkillGemDefinition ActiveSkillDef => activeSkillDef;
    public bool HasActiveReservation => _hasReservation;

    public event Action<ActiveSkillCastInfo> PreCastWindowOpened;
    public event Action<ActiveSkillCastInfo> PreCastWindowClosed;
    public event Action<ActiveSkillCastInfo, GameObject> CastBlocked;

    void Awake()
    {
        CacheReferences();
    }

    void OnEnable()
    {
        CacheReferences();
        Subscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
        if (_hasReservation)
        {
            LogFlow($"disabled with active reservation requestId={_reservedCast.RequestId} reservationId={_reservationId}", warning: true);
            ReleaseHoldInternal();
            ClearReservationState();
        }
        ClearActiveCast("Disabled");
    }

    public bool CanBlockActiveCast()
    {
        if (!hasActiveBlockableCast || !preCastWindowOpen)
            return false;

        CharacterSkillManager skillManager = ctx != null ? ctx.SkillManager : null;
        if (skillManager == null || activeSkillDef == null || !activeSkillDef.BlockablePreCast)
            return false;

        if (!skillManager.TryGetActiveCast(out ActiveSkillCastInfo currentCast))
            return false;

        return currentCast.RequestId == activeCast.RequestId && !currentCast.Released;
    }

    public bool TryBlockCast(GameObject source = null)
    {
        if (!CanBlockActiveCast())
            return false;

        return DoBlockInternal(activeCast, source);
    }

    bool DoBlockInternal(ActiveSkillCastInfo blockedCast, GameObject source)
    {
        CharacterSkillManager skillManager = ctx != null ? ctx.SkillManager : null;
        if (skillManager == null)
        {
            LogFlow($"block failed requestId={blockedCast.RequestId} reason=MissingSkillManager", warning: true);
            return false;
        }

        bool cancelled = skillManager.TryCancelActiveCast(SkillCastCancelReason.Blocked);
        if (!cancelled)
        {
            LogFlow($"block failed requestId={blockedCast.RequestId} reason=CastAlreadyGone", warning: true);
            return false;
        }

        CastBlocked?.Invoke(blockedCast, source);

        string skillName = blockedCast.SkillDef != null ? blockedCast.SkillDef.name : "<unknown>";
        string sourceName = source != null ? source.name : "<none>";
        LogFlow(
            $"blocked requestId={blockedCast.RequestId} skill='{skillName}' target='{name}' source='{sourceName}'",
            force: logBlocks);

        return true;
    }

    public bool TryReserveBlock(GameObject source, float holdSpeedMultiplier, float holdSafetyMargin, out PreCastBlockReservation reservation)
    {
        reservation = default;
        if (_hasReservation)
        {
            LogFlow($"reservation rejected reason=AlreadyReserved requestId={_reservedCast.RequestId} reservationId={_reservationId}");
            return false;
        }

        if (!CanBlockActiveCast())
        {
            LogFlow($"reservation rejected reason=WindowClosed requestId={ActiveRequestId}");
            return false;
        }

        CharacterAnimBrain animBrain = ctx != null ? ctx.AnimBrain : null;
        if (animBrain == null)
        {
            LogFlow($"reservation rejected reason=MissingAnimBrain requestId={activeCast.RequestId}", warning: true);
            return false;
        }

        if (!animBrain.TryAcquirePreCastHold(activeCast.RequestId, holdSpeedMultiplier, holdSafetyMargin, out var hold))
        {
            LogFlow($"reservation rejected reason=HoldRejected requestId={activeCast.RequestId}", warning: true);
            return false;
        }

        _hasReservation = true;
        _reservedCast = activeCast;
        _reservationSource = source;
        _holdHandle = hold;
        _reservationId = ++_reservationCounter;

        ClosePreCastWindow("Reservation");

        reservation = new PreCastBlockReservation(this, _reservedCast.RequestId, _reservationId);
        LogFlow(
            $"reservation acquired requestId={reservation.RequestId} reservationId={reservation.ReservationId} source='{ResolveName(source)}' speedMultiplier={holdSpeedMultiplier:0.###} safetyMargin={holdSafetyMargin:0.###}");
        return true;
    }

    public ReservedBlockResult CompleteReservedBlock(PreCastBlockReservation reservation)
    {
        if (!_hasReservation || reservation.ReservationId != _reservationId || reservation.RequestId != _reservedCast.RequestId)
        {
            LogFlow(
                $"reservation completion rejected result={ReservedBlockResult.InvalidReservation} suppliedRequestId={reservation.RequestId} suppliedReservationId={reservation.ReservationId} activeRequestId={_reservedCast.RequestId} activeReservationId={_reservationId}",
                warning: true);
            return ReservedBlockResult.InvalidReservation;
        }

        ActiveSkillCastInfo blockedCast = _reservedCast;
        GameObject source = _reservationSource;
        int reservationId = _reservationId;
        ReleaseHoldInternal();
        ClearReservationState();

        ReservedBlockResult result = DoBlockInternal(blockedCast, source)
            ? ReservedBlockResult.Success
            : ReservedBlockResult.CastAlreadyGone;
        LogFlow($"reservation completed requestId={blockedCast.RequestId} reservationId={reservationId} result={result}");
        return result;
    }

    void ReleaseHoldInternal()
    {
        CharacterAnimBrain animBrain = ctx != null ? ctx.AnimBrain : null;
        if (_holdHandle.IsValid && animBrain != null)
            animBrain.ReleasePreCastHold(_holdHandle);
        _holdHandle = default;
    }

    void ClearReservationState()
    {
        _hasReservation = false;
        _reservedCast = default;
        _reservationSource = null;
        _reservationId = 0;
    }

    void CacheReferences()
    {
        if (!ctx)
        {
            TryGetComponent(out ctx);
            if (!ctx)
                ctx = GetComponentInParent<EnemyContext>();
        }

        ctx?.ResolveReferences();

        if (!indicatorAnchor)
            indicatorAnchor = transform;
    }

    void Subscribe()
    {
        CharacterSkillManager skillManager = ctx != null ? ctx.SkillManager : null;
        if (skillManager != null)
        {
            skillManager.CastStarted -= OnCastStarted;
            skillManager.CastReleased -= OnCastReleased;
            skillManager.CastCancelled -= OnCastCancelled;
            skillManager.CastStarted += OnCastStarted;
            skillManager.CastReleased += OnCastReleased;
            skillManager.CastCancelled += OnCastCancelled;
        }

        CharacterAnimBrain animBrain = ctx != null ? ctx.AnimBrain : null;
        if (animBrain != null)
        {
            animBrain.SkillTimelineEventRaised -= OnSkillTimelineEventRaised;
            animBrain.SkillTimelineEventRaised += OnSkillTimelineEventRaised;
        }

        StateHub stateHub = ctx != null ? ctx.stateHub : null;
        if (stateHub != null)
        {
            stateHub.StunStarted -= OnStunStarted;
            stateHub.StunStarted += OnStunStarted;
        }

        StaggerMeter staggerMeter = ctx != null ? ctx.StaggerMeter : null;
        if (staggerMeter != null)
        {
            staggerMeter.StaggerStarted -= OnStaggerStarted;
            staggerMeter.StaggerStarted += OnStaggerStarted;
        }
    }

    void Unsubscribe()
    {
        CharacterSkillManager skillManager = ctx != null ? ctx.SkillManager : null;
        if (skillManager != null)
        {
            skillManager.CastStarted -= OnCastStarted;
            skillManager.CastReleased -= OnCastReleased;
            skillManager.CastCancelled -= OnCastCancelled;
        }

        CharacterAnimBrain animBrain = ctx != null ? ctx.AnimBrain : null;
        if (animBrain != null)
            animBrain.SkillTimelineEventRaised -= OnSkillTimelineEventRaised;

        StateHub stateHub = ctx != null ? ctx.stateHub : null;
        if (stateHub != null)
            stateHub.StunStarted -= OnStunStarted;

        StaggerMeter staggerMeter = ctx != null ? ctx.StaggerMeter : null;
        if (staggerMeter != null)
            staggerMeter.StaggerStarted -= OnStaggerStarted;
    }

    void OnCastStarted(ActiveSkillCastInfo castInfo)
    {
        SkillGemDefinition skillDef = castInfo.SkillDef;
        if (!castInfo.IsValid || skillDef == null || !skillDef.BlockablePreCast)
        {
            ClearActiveCast("NonBlockableCast");
            return;
        }

        activeCast = castInfo;
        activeSkillDef = skillDef;
        hasActiveBlockableCast = true;
        preCastWindowOpen = false;

        LogFlow(
            $"cast tracked requestId={castInfo.RequestId} skill='{skillDef.name}' fallbackEnabled={skillDef.UseFallbackPreCastWindow} fallbackOpenNormalized={skillDef.FallbackPreCastOpenNormalized:0.###}");

        if (skillDef.UseFallbackPreCastWindow && skillDef.FallbackPreCastOpenNormalized <= 0.0001f)
            OpenPreCastWindow("FallbackStart");
    }

    void OnCastReleased(ActiveSkillCastInfo castInfo)
    {
        if (_hasReservation && _reservedCast.RequestId == castInfo.RequestId)
        {
            Debug.LogError(
                $"[PreCast.Target] invariant=SafetyHoldFailed requestId={castInfo.RequestId} reservationId={_reservationId} cast released while reserved",
                this);
            ReleaseHoldInternal();
            ClearReservationState();
        }

        if (!MatchesActiveCast(castInfo.RequestId))
            return;

        ClearActiveCast("CastReleased");
    }

    void OnCastCancelled(ActiveSkillCastInfo castInfo, SkillCastCancelReason reason)
    {
        LogFlow($"cast cancelled requestId={castInfo.RequestId} reason={reason}");

        if (_hasReservation && _reservedCast.RequestId == castInfo.RequestId)
        {
            ReleaseHoldInternal();
            ClearReservationState();
        }

        if (!MatchesActiveCast(castInfo.RequestId))
            return;

        ClearActiveCast($"CastCancelled:{reason}");
    }

    void OnSkillTimelineEventRaised(int requestId, CombatTimelineEventName eventName)
    {
        if (!MatchesActiveCast(requestId) || activeSkillDef == null)
            return;

        if (activeSkillDef.IsPreCastOpenEvent(eventName))
        {
            OpenPreCastWindow($"Timeline:{eventName}");
            return;
        }

        if (activeSkillDef.IsPreCastCloseEvent(eventName))
            ClosePreCastWindow($"Timeline:{eventName}");
    }

    void OnStunStarted()
    {
        if (!hasActiveBlockableCast || activeSkillDef == null || !activeSkillDef.CancelPreCastOnStun)
            return;

        ctx?.SkillManager?.TryCancelActiveCast(SkillCastCancelReason.Stunned);
    }

    void OnStaggerStarted()
    {
        if (!hasActiveBlockableCast || activeSkillDef == null || !activeSkillDef.CancelPreCastOnStagger)
            return;

        ctx?.SkillManager?.TryCancelActiveCast(SkillCastCancelReason.Staggered);
    }

    void OpenPreCastWindow(string source)
    {
        if (!hasActiveBlockableCast || preCastWindowOpen)
            return;

        preCastWindowOpen = true;
        SpawnIndicator();
        SpawnBlockWindowVfx();
        PreCastWindowOpened?.Invoke(activeCast);
        LogFlow($"window opened requestId={activeCast.RequestId} source={source}");
    }

    void ClosePreCastWindow(string source)
    {
        if (!preCastWindowOpen)
            return;

        preCastWindowOpen = false;
        DespawnIndicator();
        DespawnBlockWindowVfx();
        PreCastWindowClosed?.Invoke(activeCast);
        LogFlow($"window closed requestId={activeCast.RequestId} source={source}");
    }

    void ClearActiveCast(string source)
    {
        if (hasActiveBlockableCast)
            LogFlow($"cast cleared requestId={activeCast.RequestId} source={source}");

        ClosePreCastWindow(source);
        activeCast = default;
        activeSkillDef = null;
        hasActiveBlockableCast = false;
    }

    bool MatchesActiveCast(int requestId)
    {
        return hasActiveBlockableCast && requestId > 0 && activeCast.RequestId == requestId;
    }

    void SpawnIndicator()
    {
        if (activeIndicator != null || activeSkillDef == null || activeSkillDef.PreCastIndicatorPrefab == null)
            return;

        Transform anchor = indicatorAnchor != null ? indicatorAnchor : transform;
        activeIndicator = Instantiate(
            activeSkillDef.PreCastIndicatorPrefab,
            anchor.position,
            anchor.rotation,
            anchor);
    }

    void DespawnIndicator()
    {
        if (activeIndicator == null)
            return;

        Destroy(activeIndicator);
        activeIndicator = null;
    }

    void SpawnBlockWindowVfx()
    {
        if (activeBlockWindowVfx != null || blockWindowVfxPrefab == null)
            return;

        Transform anchor = ResolveBlockWindowVfxAnchor();
        Vector3 position = anchor.TransformPoint(blockWindowVfxLocalOffset);
        Quaternion rotation = ResolveBlockWindowVfxRotation(anchor);
        Transform parent = parentBlockWindowVfxToAnchor ? anchor : null;
        VfxSpawner spawner = VfxSpawner.Instance;

        activeBlockWindowVfx = spawner != null
            ? spawner.SpawnLoopingVfx(
                blockWindowVfxPrefab,
                position,
                rotation,
                parent,
                blockWindowVfxScale)
            : Instantiate(blockWindowVfxPrefab, position, rotation, parent);

        if (activeBlockWindowVfx != null && spawner == null)
            activeBlockWindowVfx.transform.localScale *= Mathf.Max(0f, blockWindowVfxScale);
    }

    void DespawnBlockWindowVfx()
    {
        if (activeBlockWindowVfx == null)
            return;

        VfxSpawner spawner = VfxSpawner.Instance;
        if (spawner != null)
        {
            spawner.StopLoopingVfx(
                activeBlockWindowVfx,
                allowBlockWindowVfxParticlesToFinish,
                blockWindowVfxExtraLife);
        }
        else
        {
            Destroy(activeBlockWindowVfx);
        }

        activeBlockWindowVfx = null;
    }

    Transform ResolveBlockWindowVfxAnchor()
    {
        if (blockWindowVfxAnchor != null)
            return blockWindowVfxAnchor;

        if (indicatorAnchor != null)
            return indicatorAnchor;

        return transform;
    }

    Quaternion ResolveBlockWindowVfxRotation(Transform anchor)
    {
        Quaternion baseRotation = useAnchorRotationForBlockWindowVfx && anchor != null
            ? anchor.rotation
            : Quaternion.identity;

        return baseRotation * Quaternion.Euler(blockWindowVfxRotationEuler);
    }

    void LogFlow(string message, bool warning = false, bool force = false)
    {
        if (!logPreCastFlow && !force)
            return;

        string formatted = $"[PreCast.Target] {message}";
        if (warning)
            Debug.LogWarning(formatted, this);
        else
            Debug.Log(formatted, this);
    }

    static string ResolveName(GameObject source)
    {
        return source != null ? source.name : "<none>";
    }
}
