using System;
using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PreCastBlockController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CharacterSkillManager skillManager;
    [SerializeField] private CharacterAnimBrain animBrain;
    [SerializeField] private StateHub stateHub;
    [SerializeField] private StaggerMeter staggerMeter;
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

    ActiveSkillCastInfo activeCast;
    SkillGemDefinition activeSkillDef;
    GameObject activeIndicator;
    GameObject activeBlockWindowVfx;
    bool hasActiveBlockableCast;
    bool preCastWindowOpen;
    CharacteContext ctx;

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
            ReleaseHoldInternal();
            ClearReservationState();
        }
        ClearActiveCast();
    }

    public bool CanBlockActiveCast()
    {
        if (!hasActiveBlockableCast || !preCastWindowOpen)
            return false;

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
        if (skillManager == null)
            return false;

        bool cancelled = skillManager.TryCancelActiveCast(SkillCastCancelReason.Blocked);
        if (!cancelled)
            return false;

        CastBlocked?.Invoke(blockedCast, source);

        if (logBlocks)
        {
            string skillName = blockedCast.SkillDef != null ? blockedCast.SkillDef.name : "<unknown>";
            string sourceName = source != null ? source.name : "<none>";
            Debug.Log($"[PreCastBlockController] Blocked cast '{skillName}' from '{name}' by '{sourceName}'.", this);
        }

        return true;
    }

    public bool TryReserveBlock(GameObject source, float holdSpeedMultiplier, float holdSafetyMargin, out PreCastBlockReservation reservation)
    {
        reservation = default;
        if (_hasReservation) return false;
        if (!CanBlockActiveCast() || animBrain == null) return false;
        if (!animBrain.TryAcquirePreCastHold(activeCast.RequestId, holdSpeedMultiplier, holdSafetyMargin, out var hold))
            return false;

        _hasReservation = true;
        _reservedCast = activeCast;
        _reservationSource = source;
        _holdHandle = hold;
        _reservationId = ++_reservationCounter;

        ClosePreCastWindow();

        reservation = new PreCastBlockReservation(this, _reservedCast.RequestId, _reservationId);
        return true;
    }

    public ReservedBlockResult CompleteReservedBlock(PreCastBlockReservation reservation)
    {
        if (!_hasReservation || reservation.ReservationId != _reservationId || reservation.RequestId != _reservedCast.RequestId)
            return ReservedBlockResult.InvalidReservation;

        ActiveSkillCastInfo blockedCast = _reservedCast;
        GameObject source = _reservationSource;
        ReleaseHoldInternal();
        ClearReservationState();

        return DoBlockInternal(blockedCast, source) ? ReservedBlockResult.Success : ReservedBlockResult.CastAlreadyGone;
    }

    void ReleaseHoldInternal()
    {
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
                ctx = GetComponentInParent<CharacteContext>();
        }

        ctx?.ResolveReferences();

        if (!skillManager)
            skillManager = ctx != null ? ctx.SkillManager : null;
        if (!skillManager)
            skillManager = GetComponent<CharacterSkillManager>();
        if (!skillManager)
            skillManager = GetComponentInParent<CharacterSkillManager>();

        if (!animBrain)
            animBrain = ctx != null ? ctx.AnimBrain : null;
        if (!animBrain)
            animBrain = GetComponent<CharacterAnimBrain>();
        if (!animBrain)
            animBrain = GetComponentInChildren<CharacterAnimBrain>(true);
        if (!animBrain)
            animBrain = GetComponentInParent<CharacterAnimBrain>();

        if (!stateHub)
            stateHub = ctx != null ? ctx.stateHub : null;
        if (!stateHub)
            stateHub = GetComponent<StateHub>();
        if (!stateHub)
            stateHub = GetComponentInParent<StateHub>();

        if (!staggerMeter)
            staggerMeter = GetComponent<StaggerMeter>();
        if (!staggerMeter)
            staggerMeter = GetComponentInParent<StaggerMeter>();

        if (!indicatorAnchor)
            indicatorAnchor = transform;
    }

    void Subscribe()
    {
        if (skillManager != null)
        {
            skillManager.CastStarted -= OnCastStarted;
            skillManager.CastReleased -= OnCastReleased;
            skillManager.CastCancelled -= OnCastCancelled;
            skillManager.CastStarted += OnCastStarted;
            skillManager.CastReleased += OnCastReleased;
            skillManager.CastCancelled += OnCastCancelled;
        }

        if (animBrain != null)
        {
            animBrain.SkillTimelineEventRaised -= OnSkillTimelineEventRaised;
            animBrain.SkillTimelineEventRaised += OnSkillTimelineEventRaised;
        }

        if (stateHub != null)
        {
            stateHub.StunStarted -= OnStunStarted;
            stateHub.StunStarted += OnStunStarted;
        }

        if (staggerMeter != null)
        {
            staggerMeter.StaggerStarted -= OnStaggerStarted;
            staggerMeter.StaggerStarted += OnStaggerStarted;
        }
    }

    void Unsubscribe()
    {
        if (skillManager != null)
        {
            skillManager.CastStarted -= OnCastStarted;
            skillManager.CastReleased -= OnCastReleased;
            skillManager.CastCancelled -= OnCastCancelled;
        }

        if (animBrain != null)
            animBrain.SkillTimelineEventRaised -= OnSkillTimelineEventRaised;

        if (stateHub != null)
            stateHub.StunStarted -= OnStunStarted;

        if (staggerMeter != null)
            staggerMeter.StaggerStarted -= OnStaggerStarted;
    }

    void OnCastStarted(ActiveSkillCastInfo castInfo)
    {
        SkillGemDefinition skillDef = castInfo.SkillDef;
        if (!castInfo.IsValid || skillDef == null || !skillDef.BlockablePreCast)
        {
            ClearActiveCast();
            return;
        }

        activeCast = castInfo;
        activeSkillDef = skillDef;
        hasActiveBlockableCast = true;
        preCastWindowOpen = false;

        if (skillDef.UseFallbackPreCastWindow && skillDef.FallbackPreCastOpenNormalized <= 0.0001f)
            OpenPreCastWindow();
    }

    void OnCastReleased(ActiveSkillCastInfo castInfo)
    {
        if (_hasReservation && _reservedCast.RequestId == castInfo.RequestId)
        {
            Debug.LogError("[PreCastBlockController] Safety hold failed: cast released while reserved.", this);
            ReleaseHoldInternal();
            ClearReservationState();
        }

        if (!MatchesActiveCast(castInfo.RequestId))
            return;

        ClearActiveCast();
    }

    void OnCastCancelled(ActiveSkillCastInfo castInfo, SkillCastCancelReason reason)
    {
        if (_hasReservation && _reservedCast.RequestId == castInfo.RequestId)
        {
            ReleaseHoldInternal();
            ClearReservationState();
        }

        if (!MatchesActiveCast(castInfo.RequestId))
            return;

        ClearActiveCast();
    }

    void OnSkillTimelineEventRaised(int requestId, CombatTimelineEventName eventName)
    {
        if (!MatchesActiveCast(requestId) || activeSkillDef == null)
            return;

        if (activeSkillDef.IsPreCastOpenEvent(eventName))
        {
            OpenPreCastWindow();
            return;
        }

        if (activeSkillDef.IsPreCastCloseEvent(eventName))
            ClosePreCastWindow();
    }

    void OnStunStarted()
    {
        if (!hasActiveBlockableCast || activeSkillDef == null || !activeSkillDef.CancelPreCastOnStun)
            return;

        skillManager?.TryCancelActiveCast(SkillCastCancelReason.Stunned);
    }

    void OnStaggerStarted()
    {
        if (!hasActiveBlockableCast || activeSkillDef == null || !activeSkillDef.CancelPreCastOnStagger)
            return;

        skillManager?.TryCancelActiveCast(SkillCastCancelReason.Staggered);
    }

    void OpenPreCastWindow()
    {
        if (!hasActiveBlockableCast || preCastWindowOpen)
            return;

        preCastWindowOpen = true;
        SpawnIndicator();
        SpawnBlockWindowVfx();
        PreCastWindowOpened?.Invoke(activeCast);
    }

    void ClosePreCastWindow()
    {
        if (!preCastWindowOpen)
            return;

        preCastWindowOpen = false;
        DespawnIndicator();
        DespawnBlockWindowVfx();
        PreCastWindowClosed?.Invoke(activeCast);
    }

    void ClearActiveCast()
    {
        ClosePreCastWindow();
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
}
