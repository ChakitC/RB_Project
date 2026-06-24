using System;
using System.Collections.Generic;
using Animancer;
using Animancer.FSM;
using UnityEngine;

[DefaultExecutionOrder(-120)]
public sealed partial class CharacterAnimBrain : MonoBehaviour
{
    private const float ActionLayerActiveWeightThreshold = 0.001f;

    private bool _initialized;
    private Animator _boundAnimator;
    private CharacterAnimProfileSO _boundAnimProfile;
    private CharacterAnimProfileSO _animProfileOverride;
    private float _baseAnimancerGraphSpeed = 1f;
    private bool _hasCachedAnimancerGraphSpeed;

    private enum PendingAction { None, Empty, Hold, Reload, Melee }
    private PendingAction _pendingAction;
    private bool _pendingPulse;

    [Header("Core")]
    [SerializeField] private AnimancerComponent animancer;
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private StatusEffectController statusEffectController;
    [SerializeField] private bool deactivateOwnerOnSkillExit;

    [Header("Chain")]
    [SerializeField, Min(0.05f)] private float chainPlaybackWatchdogGraceSeconds = 0.15f;
    [SerializeField] private bool useRootMotionForChainPlayback;

    [Header("Layer Indices")]
    [SerializeField] private int locomotionLayerIndex = 0;
    [SerializeField] private int actionLayerIndex = 1;
    public Vector2 MoveDirLocal { get; set; }
    private Vector2 _dashDirLocal;
    private float _dashDuration = 0.12f;
    private Locomotion_Dash dashState;
    private Locomotion_Knockback knockbackState;
    private Action onDashEndCache;

    private Locomotion_Dead deadState;
    private Action onDeadEndCache;

    private float _reloadDuration = 0f;
    private readonly List<PairOffsetBasePoseWeight> _activePairBasePoseWeights = new List<PairOffsetBasePoseWeight>(3);
    private PairOffsetBasePose _activePairBasePose = PairOffsetBasePose.None;
    private PairOffsetUpperAction _activePairUpperAction = PairOffsetUpperAction.None;

    public enum MeleeType { Light, Heavy }
    private enum StatusLocomotionKind { None, Root, MiniStune, Stune, Freez }

    public enum PlaybackKind
    {
        None = 0,
        Skill = 1,
        UtilityWarpOut = 2,
        UtilityWarpIn = 3,
        ChainSkill = 4,
        ChainUtilityWarpOut = 5,
        ChainUtilityWarpIn = 6,
        Melee = 7,
        Dead = 8,
        StatusEffect = 9,
    }

    public enum PlaybackPhase
    {
        Started = 0,
        CastMoment = 1,
        AdvanceMoment = 2,
        Completed = 3,
        Interrupted = 4,
    }

    public readonly struct PlaybackSignal
    {
        public readonly PlaybackKind Kind;
        public readonly PlaybackPhase Phase;
        public readonly int RequestId;

        public PlaybackSignal(PlaybackKind kind, PlaybackPhase phase, int requestId)
        {
            Kind = kind;
            Phase = phase;
            RequestId = requestId;
        }
    }

    public enum SkillAnimationVfxPhase
    {
        MainSkill = 0,
        Cutscene = 1,
    }

    public readonly struct SkillAnimationVfxCueSignal
    {
        public readonly int RequestId;
        public readonly SkillGemDefinition SkillDef;
        public readonly SkillAnimationVfxPhase Phase;
        public readonly int CueIndex;

        public SkillAnimationVfxCueSignal(int requestId, SkillGemDefinition skillDef, SkillAnimationVfxPhase phase, int cueIndex)
        {
            RequestId = requestId;
            SkillDef = skillDef;
            Phase = phase;
            CueIndex = cueIndex;
        }
    }

    public bool IsDowned { get; private set; }
    private LocomotionState_Crawl crawlState;
    private Locomotion_StatusEffect statusEffectState;
    private StatusLocomotionKind _currentStatusLocomotionKind;
    private StatusLocomotionKind _externalStatusLocomotionKind;
    private StatusLocomotionKind _staggerStatusLocomotionKind;

    // pending (กรณีเรียก SetDowned ก่อน init)
    private bool _pendingDownedSet;
    private bool _pendingDownedValue;
    private bool _pendingCrawlIntro;

    public bool RootMotionActive { get; private set; }
    public MeleeType CurrentMeleeType { get; private set; } = MeleeType.Light;
    public MeleeComboSO.Step CurrentMeleeStep { get; internal set; }
    public int CurrentMeleeStepIndex { get; internal set; }
    public event Action MeleeHitStart;
    public event Action MeleeHitEnd;
    public event Action MeleeComboEnded;

    private Action onMeleeHitStartCache;
    private Action onMeleeHitEndCache;
    private Action onMeleeEndCache;
    private Action onSkillCastMomentCache;
    

    // ----- Runtime inputs -----
    public float MoveSpeed01 { get; set; } // 0..1
    private bool _canApplyFireHold = true;
    public bool DesiredFireHold { get; private set; }
    public bool IsHoldingFire { get; private set; }

    // ----- State machines (orthogonal) -----
    private readonly StateMachine<LocomotionState> locomotionSM = new();
    private readonly StateMachine<ActionState> actionSM = new();

    private Locomotion_Skill skill;
    private Locomotion_Utility utility;
    private Action_Reload reloadState;
    private Locomotion_Reload fullBodyReloadState;
    private LocomotionState_Live locomotion;
    private Locomotion_MeleeCombo meleeCombo;
    private Action_Empty empty;
    private Action_ShootPulse shootOnce;
    private Action_ShootHold shootHold;

    // cache delegates ลด alloc
    private Action onShootEndCache;
    private Action onUtilityCastMomentCache;
    private SkillGemDefinition _activeSkillDefinition;
    private AnimationVfxPresenter _animationVfxPresenter;
    private int _activeSkillRequestId;
    private float _activeSkillCastPointNormalized = 0.35f;
    private bool _activeSkillReleaseRequested;
    private bool _activeSkillReleased;
    private readonly List<CombatTimelineEventName> _activeSkillTimelineEventNames = new List<CombatTimelineEventName>();
    private int _activeUtilityRequestId;
    private float _activeUtilityCastPointNormalized = 0.35f;
    private bool _activeUtilityReleaseRequested;
    private bool _activeUtilityReleased;

    private AnimancerLayer LocoLayer => animancer.Layers[locomotionLayerIndex];
    private AnimancerLayer ActLayer => animancer.Layers[actionLayerIndex];

    private CharacterAnimProfileSO AnimProfile => _boundAnimProfile;

    private AvatarMask UpperBodyMask => AnimProfile.upperBodyMask;
    private float ActionFadeIn => AnimProfile.actionFadeIn;
    private float ActionFadeOut => AnimProfile.actionFadeOut;
    internal float ChainPlaybackWatchdogGraceSeconds => Mathf.Max(0.05f, chainPlaybackWatchdogGraceSeconds);
    internal bool UseRootMotionForChainPlayback => useRootMotionForChainPlayback;
    private MixerTransition2D LocomotionMixer => AnimProfile != null ? AnimProfile.ResolveLocomotionMixer() : null;
    private float LocomotionParamLerp => AnimProfile.locomotionParamLerp;
    private bool SnapTo8Directions => AnimProfile.snapTo8Directions;
    private ClipTransition DashForward => AnimProfile.dashF;
    private ClipTransition DashBackward => AnimProfile.dashB;
    private AnimationVfxTrack DashForwardVfxTrack =>
        AnimProfile.GetAnimationVfxTrack(CharacterAnimProfileSO.DashForwardVfxEntryId);
    private AnimationVfxTrack DashBackwardVfxTrack =>
        AnimProfile.GetAnimationVfxTrack(CharacterAnimProfileSO.DashBackwardVfxEntryId);
    private ClipTransition DashLeft => AnimProfile.dashL;
    private ClipTransition DashRight => AnimProfile.dashR;
    private ClipTransition DeadClip => AnimProfile.dead;
    private ClipTransition ShootPulseClip => AnimProfile.shootPulse;
    private ClipTransition ShootHoldLoopClip => AnimProfile.shootHoldLoop;
    private float HoldPulseMinInterval => AnimProfile.holdPulseMinInterval;
    private ClipTransition ReloadClip => AnimProfile.reload;
    private AnimationVfxTrack ReloadVfxTrack =>
        AnimProfile.GetAnimationVfxTrack(CharacterAnimProfileSO.ReloadVfxEntryId);
    private CharacterAnimProfileSO.ReloadBodyMode ReloadMode => AnimProfile.reloadBodyMode;
    private MeleeComboSO DefaultMeleeCombo => AnimProfile.meleeCombo;
    private MeleeComboSO LightCombo => AnimProfile.lightCombo;
    private MeleeComboSO HeavyCombo => AnimProfile.heavyCombo;
    private ClipTransition CrawlingClip => AnimProfile.crawling;
    private MixerTransition2D CrawlMixer => AnimProfile.crawlMixer;
    private float CrawlParamLerp => AnimProfile.crawlParamLerp;
    private float CrawlSpeedMultiplier01 => AnimProfile.crawlSpeedMultiplier01;
    private ClipTransition UtilityWarpOutClip => AnimProfile.utilityWarpOutClip;
    private float UtilityWarpOutCastPointNormalized => AnimProfile.utilityWarpOutCastPointNormalized;
    private ClipTransition UtilityWarpInClip => AnimProfile.utilityWarpInClip;
    private float UtilityWarpInCastPointNormalized => AnimProfile.utilityWarpInCastPointNormalized;
    private ClipTransition LegacySkillClip => AnimProfile.skillClip;
    private ClipTransition SkillClip => ResolveSkillClip(_activeSkillDefinition);
    public PairOffsetProfilesSO PairOffsetProfiles => AnimProfile != null ? AnimProfile.pairOffsetProfiles : null;
    public SkillGemDefinition ActiveSkillDefinition => _activeSkillDefinition;
    private ClipTransition MiniStuneClip => AnimProfile.miniStune;
    private ClipTransition StuneClip => AnimProfile.stune;
    private ClipTransition RootClip => AnimProfile.root;
    private ClipTransition FreezClip => AnimProfile.freez;
    private ClipTransition KnockbackClip => AnimProfile.knockback;
    private bool HasActiveSkillClip => HasValidSkillClip(_activeSkillDefinition);
    private bool HasActiveUtilityWarpOutClip => HasValidUtilityWarpOutClip();
    internal float ActiveSkillCastPointNormalized => _activeSkillCastPointNormalized;
    internal bool HasPendingSkillReleaseRequest => _activeSkillReleaseRequested;
    internal float ActiveUtilityCastPointNormalized => _activeUtilityCastPointNormalized;
    internal bool HasPendingUtilityReleaseRequest => _activeUtilityReleaseRequested;
    public bool IsSkillPlaybackActive =>
        _activeSkillReleaseRequested ||
        _activeSkillRequestId != 0 ||
        (_initialized && locomotionSM.CurrentState == skill);
    public bool IsUtilityPlaybackActive =>
        _activeUtilityReleaseRequested ||
        _activeUtilityRequestId != 0 ||
        (_initialized && locomotionSM.CurrentState == utility);
    public bool IsShootBlockingPlaybackActive =>
        IsSkillPlaybackActive ||
        IsUtilityPlaybackActive ||
        IsChainPlaybackActive;
    public bool IsSkillActive => IsShootBlockingPlaybackActive;
    public bool IsUtilityActive => IsUtilityPlaybackActive || IsChainUtilityPlaybackActive;
    public bool IsExclusiveLocomotionActive =>
        _initialized &&
        (locomotionSM.CurrentState == skill ||
         locomotionSM.CurrentState == utility ||
         locomotionSM.CurrentState == chain ||
         locomotionSM.CurrentState == meleeCombo ||
         locomotionSM.CurrentState == fullBodyReloadState ||
         locomotionSM.CurrentState == knockbackState ||
         locomotionSM.CurrentState == deadState ||
         locomotionSM.CurrentState == statusEffectState);
    public PlaybackKind CurrentPlaybackKind => ResolveCurrentPlaybackKind();

    public event Action<PlaybackSignal> PlaybackEvent;
    public event Action<int> SkillCastMomentReached;
    public event Action<int> SkillCastInterrupted;
    public event Action<int, CombatTimelineEventName> SkillTimelineEventRaised;
    public event Action<SkillAnimationVfxCueSignal> SkillAnimationVfxCueRaised;
    public event Action SkillCompleted;

    internal bool TryGetActiveSkillNormalizedTime(int requestId, out float normalizedTime)
    {
        normalizedTime = 0f;

        if (requestId <= 0 || !_initialized)
            return false;

        if (requestId == _activeSkillRequestId &&
            _activeSkillReleaseRequested &&
            locomotionSM.CurrentState == skill &&
            skill != null)
        {
            return skill.TryGetNormalizedTime(out normalizedTime);
        }

        if (requestId == _activeChainRequestId &&
            _activeChainKind == ChainPlaybackKind.Skill &&
            _activeChainReleaseRequested &&
            locomotionSM.CurrentState == chain &&
            chain != null)
        {
            return chain.TryGetNormalizedTime(out normalizedTime);
        }

        return false;
    }

    internal bool TryAcquirePreCastHold(int requestId, float speedMultiplier, float safetyMarginNormalized, out SkillPreCastHoldHandle handle)
    {
        handle = default;
        if (!_initialized || locomotionSM.CurrentState != skill) return false;
        if (requestId <= 0 || requestId != _activeSkillRequestId) return false;
        if (!HasPendingSkillReleaseRequest) return false;
        return skill.TryBeginPreCastHold(requestId, speedMultiplier, safetyMarginNormalized, out handle);
    }

    internal void ReleasePreCastHold(SkillPreCastHoldHandle handle)
    {
        if (handle.IsValid) skill?.ReleasePreCastHold(handle);
    }

    private bool TryGetAnimProfile(out CharacterAnimProfileSO animProfile)
    {
        animProfile = null;

        if (_animProfileOverride != null)
        {
            animProfile = _animProfileOverride;
            return true;
        }

        ResolveReferences();
        if (ctx == null || ctx.baseStats == null)
            return false;

        animProfile = ctx.baseStats.animProfile;
        return animProfile != null;
    }

    public void SetAnimProfileOverride(CharacterAnimProfileSO profile)
    {
        if (_animProfileOverride == profile)
            return;

        _animProfileOverride = profile;
    }

    public void ClearAnimProfileOverride()
    {
        SetAnimProfileOverride(null);
    }

    private string GetInitializationError()
    {
        if (!animancer) return "AnimancerComponent missing.";
        if (animancer.Animator == null) return "Animancer.Animator missing.";
        ResolveReferences();
        if (ctx == null) return "CharacteContext missing.";
        if (ctx.baseStats == null) return "CharacteContext.baseStats missing.";
        if (ctx.baseStats.animProfile == null)
            return $"CharacterStats '{ctx.baseStats.name}' is missing animProfile.";

        return "Unknown initialization error.";
    }

    private bool TryInitialize()
    {
        ResolveReferences();
        if (!animancer || animancer.Animator == null)
            return false;

        if (!TryGetAnimProfile(out var animProfile))
            return false;

        bool bindingChanged = _initialized &&
                              (animancer.Animator != _boundAnimator || animProfile != _boundAnimProfile);
        if (bindingChanged)
        {
            if ((_activeSkillRequestId != 0 || _activeSkillReleaseRequested) ||
                (_activeUtilityRequestId != 0 || _activeUtilityReleaseRequested))
            {
                InterruptActiveSkillRequest();
                InterruptActiveUtilityRequest();
            }

            dashState?.EndVfxSession();
            reloadState?.EndVfxSession();
            fullBodyReloadState?.EndVfxSession();
            meleeCombo?.EndVfxSession();
        }

        // ถ้า Animator หรือ profile เปลี่ยน (เช่น rebuild model / switch character) ต้อง init ใหม่
        if (_initialized && animancer.Animator == _boundAnimator && animProfile == _boundAnimProfile)
            return true;

        _boundAnimator = animancer.Animator;
        _boundAnimProfile = animProfile;

        // Setup action layer
        ActLayer.Mask = UpperBodyMask;

        ActLayer.SetLayerWeightOnPlay = false;
        ActLayer.Weight = 0f;

        locomotion = new LocomotionState_Live(this);
        empty = new Action_Empty(this);
        shootOnce = new Action_ShootPulse(this);
        shootHold = new Action_ShootHold(this);
        reloadState = new Action_Reload(this);
        fullBodyReloadState = new Locomotion_Reload(this);
        dashState = new Locomotion_Dash(this);
        knockbackState = new Locomotion_Knockback(this);
        deadState = new Locomotion_Dead(this);
        meleeCombo = new Locomotion_MeleeCombo(this);
        crawlState = new LocomotionState_Crawl(this);
        skill = new Locomotion_Skill(this);
        utility = new Locomotion_Utility(this);
        chain = new Locomotion_Chain(this);
        statusEffectState = new Locomotion_StatusEffect(this);

        locomotionSM.ForceSetState(locomotion);
        actionSM.ForceSetState(empty);
        ClearActivePairOffsetState();

        _initialized = true;

        if (_pendingDownedSet)
        {
            _pendingDownedSet = false;
            SetDowned(_pendingDownedValue);
        }

        ApplyPending();
        return true;
    }

    private void ApplyPending()
    {
        if (!_initialized) return;

        switch (_pendingAction)
        {
            case PendingAction.Empty:
                actionSM.TrySetState(empty);
                break;

            case PendingAction.Hold:
                ApplyFireHoldContext();
                break;

            case PendingAction.Reload:
                PlayReloadNow();
                break;

            case PendingAction.Melee:
                actionSM.TrySetState(empty);
                locomotionSM.TrySetState(meleeCombo);
                break;
        }

        _pendingAction = PendingAction.None;

        if (_pendingPulse)
        {
            _pendingPulse = false;
            NotifyShotFired();
        }
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (!ctx)
        {
            TryGetComponent(out ctx);
            if (!ctx)
                ctx = GetComponentInParent<CharacteContext>();
        }

        ctx?.ResolveReferences();

        if (ctx != null && ctx.AnimBrain != this)
            ctx.AnimBrain = this;

        if (!animancer)
            animancer = GetComponent<AnimancerComponent>();
        if (!animancer && ctx != null)
            animancer = ctx.GetComponentInChildren<AnimancerComponent>(true);

        if (!statusEffectController && ctx != null)
            statusEffectController = ctx.GetComponentInChildren<StatusEffectController>(true);
        if (!statusEffectController)
            statusEffectController = GetComponent<StatusEffectController>();
    }

    private bool _initWarned;

    private void Update()
    {
        if (!TryInitialize())
        {
            if (!_initWarned)
            {
                Debug.LogWarning($"[CharacterAnimBrain] Initialization failed: {GetInitializationError()}", this);
                _initWarned = true;
            }
            return;
        }

        _initWarned = false;

        ApplyWorldAnimationSpeed();
        RefreshStatusLocomotion();
        locomotionSM.CurrentState?.Update();
        actionSM.CurrentState?.Update();
    }

    private bool UsesWorldAnimationSlow()
    {
        return ctx == null || ctx.UsesWorldSlow;
    }

    internal float AnimationDeltaTime
    {
        get
        {
            if (UsesWorldAnimationSlow())
                return TimeSlowManager.Instance.WorldDeltaTime;

            return Time.deltaTime;
        }
    }

    internal float AnimationTime
    {
        get
        {
            if (UsesWorldAnimationSlow())
                return TimeSlowManager.Instance.WorldTime;

            return Time.time;
        }
    }

    private void ApplyWorldAnimationSpeed()
    {
        if (!animancer || !animancer.IsGraphInitialized)
            return;

        if (!_hasCachedAnimancerGraphSpeed)
        {
            _baseAnimancerGraphSpeed = animancer.Graph.Speed;
            _hasCachedAnimancerGraphSpeed = true;
        }

        float scale = UsesWorldAnimationSlow()
            ? TimeSlowManager.Instance.WorldTimeScale
            : 1f;

        animancer.Graph.Speed = _baseAnimancerGraphSpeed * scale;
    }

    private void RestoreWorldAnimationSpeed()
    {
        if (!animancer || !animancer.IsGraphInitialized || !_hasCachedAnimancerGraphSpeed)
            return;

        animancer.Graph.Speed = _baseAnimancerGraphSpeed;
    }

    // ===================== Public API =====================

    public void SetDesiredFireHold(bool held)
    {
        SetFireHoldContext(held, _canApplyFireHold);
    }

    public void SetFireHoldContext(bool desiredHold, bool canHoldAction)
    {
        DesiredFireHold = desiredHold;
        _canApplyFireHold = canHoldAction;

        if (!TryInitialize())
        {
            IsHoldingFire = false;
            _pendingAction = desiredHold && canHoldAction
                ? PendingAction.Hold
                : PendingAction.Empty;
            return;
        }

        ApplyFireHoldContext();
    }

    public void NotifyShotFired()
    {
        if (IsChainPlaybackActive)
            return;

        if (!TryInitialize())
        {
            _pendingPulse = true;
            return;
        }

        actionSM.TryResetState(shootOnce);
    }

    public void FireDown()
    {
        SetDesiredFireHold(true);
    }

    public void FireUp()
    {
        SetDesiredFireHold(false);
    }

    public bool TryGetActivePairOffsetState(
        out PairOffsetProfilesSO profiles,
        out PairOffsetBasePose basePose,
        out PairOffsetUpperAction upperAction,
        out float weight)
    {
        profiles = null;
        basePose = PairOffsetBasePose.None;
        upperAction = PairOffsetUpperAction.None;
        weight = 0f;

        if (!TryInitialize() ||
            IsExclusiveLocomotionActive ||
            locomotionSM.CurrentState != locomotion)
        {
            return false;
        }

        profiles = PairOffsetProfiles;
        basePose = _activePairBasePose;
        upperAction = _activePairUpperAction;
        weight = Mathf.Clamp01(ActLayer.Weight);

        return profiles != null &&
               basePose != PairOffsetBasePose.None &&
               upperAction != PairOffsetUpperAction.None &&
               weight > 0.001f;
    }

    public bool TryGetActivePairOffsetBlend(
        List<PairOffsetBasePoseWeight> basePoseWeights,
        out PairOffsetProfilesSO profiles,
        out PairOffsetUpperAction upperAction,
        out float weight)
    {
        profiles = null;
        upperAction = PairOffsetUpperAction.None;
        weight = 0f;
        basePoseWeights?.Clear();

        if (basePoseWeights == null ||
            !TryInitialize() ||
            IsExclusiveLocomotionActive ||
            locomotionSM.CurrentState != locomotion)
        {
            return false;
        }

        profiles = PairOffsetProfiles;
        upperAction = _activePairUpperAction;
        weight = Mathf.Clamp01(ActLayer.Weight);

        if (profiles == null ||
            upperAction == PairOffsetUpperAction.None ||
            weight <= 0.001f ||
            _activePairBasePoseWeights.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < _activePairBasePoseWeights.Count; i++)
        {
            PairOffsetBasePoseWeight poseWeight = _activePairBasePoseWeights[i];
            if (poseWeight.Pose != PairOffsetBasePose.None && poseWeight.Weight > 0.001f)
                basePoseWeights.Add(poseWeight);
        }

        return basePoseWeights.Count > 0;
    }

    public PairOffsetProfilesSO.PairOffsetProfile FindPairOffsetProfile(
        PairOffsetBasePose basePose,
        PairOffsetUpperAction upperAction,
        bool includeDisabled)
    {
        return AnimProfile != null
            ? AnimProfile.FindPairOffsetProfile(basePose, upperAction, includeDisabled)
            : null;
    }

    public void PlayReload(float reloadDuration)
    {
        if (IsChainPlaybackActive)
            return;

        _reloadDuration = Mathf.Max(0.01f, reloadDuration);

        _pendingAction = PendingAction.Reload;

        if (!TryInitialize())
            return;

        PlayReloadNow();
    }

    public void PlayDash(float dashDuration, Vector2 dashDirLocal)
    {
        if (IsChainPlaybackActive)
            return;

        _dashDuration = Mathf.Max(0.01f, dashDuration);
        _dashDirLocal = dashDirLocal;

        if (!TryInitialize())
            return;
        
        if (ReloadMode == CharacterAnimProfileSO.ReloadBodyMode.FullBody)
            StopReloadAction();
        if (locomotionSM.CurrentState == dashState)
            locomotionSM.TryResetState(dashState);
        else
            locomotionSM.TrySetState(dashState);
    }

    public void EndDashNow()
    {
        if (IsChainPlaybackActive)
            return;

        if (!TryInitialize()) return;
        locomotionSM.TrySetState(locomotion);
    }

    private void HandleDashEnd()
    {
        if (!_initialized || locomotionSM.CurrentState != dashState)
            return;

        locomotionSM.TrySetState(locomotion);
    }

    public bool PlayKnockback(KnockbackData knockback)
    {
        if (IsChainPlaybackActive)
            return false;

        if (!knockback.IsValid)
            return false;

        if (!TryInitialize() || KnockbackClip == null || !KnockbackClip.IsValid)
            return false;

        if (IsDowned || locomotionSM.CurrentState == deadState)
            return false;

        StopReloadAction();
        knockbackState.SetKnockback(knockback);

        try
        {
            return locomotionSM.TryResetState(knockbackState);
        }
        catch (ArgumentException ex)
        {
            Debug.LogWarning($"[CharacterAnimBrain] Invalid knockback clip. {ex.Message}", this);
            return false;
        }
    }

    public void StopKnockbackPlayback()
    {
        if (!TryInitialize())
            return;

        if (locomotionSM.CurrentState != knockbackState)
            return;

        locomotionSM.TrySetState(IsDowned ? crawlState : locomotion);
    }

    public void PressMelee(MeleeType type)
    {
        if (IsChainPlaybackActive)
            return;

        CurrentMeleeType = type;

        var selected = (type == MeleeType.Light) ? LightCombo : HeavyCombo;
        if (selected == null)
            selected = DefaultMeleeCombo;
        if (selected == null) return;

        if (!TryInitialize()) return;

        if (locomotionSM.CurrentState == meleeCombo)
        {
            if (meleeCombo.CurrentCombo == selected)
                meleeCombo.QueueNextPress();
            return;
        }

        StopReloadAction();

        meleeCombo.SetCombo(selected);
        locomotionSM.TrySetState(meleeCombo);
    }

    public void PlayDead()
    {
        AbortActiveChainPlaybackForExternalState();
        
        if (!TryInitialize()) return;

        StopReloadAction();

        locomotionSM.TrySetState(deadState);
    }

    public void SetDowned(bool downed)
    {
        AbortActiveChainPlaybackForExternalState();

        if (!TryInitialize())
        {
            _pendingDownedSet = true;
            _pendingDownedValue = downed;
            return;
        }

        if (locomotionSM.CurrentState == deadState) return;
        if (IsDowned == downed) return;

        IsDowned = downed;
        _pendingCrawlIntro = downed;

        if (locomotionSM.CurrentState == knockbackState)
            return;

        locomotionSM.ForceSetState(IsDowned ? crawlState : locomotion);
    }

    private bool ConsumePendingCrawlIntro()
    {
        bool shouldPlayIntro = _pendingCrawlIntro;
        _pendingCrawlIntro = false;
        return shouldPlayIntro;
    }

    public void SetExternalStatusLocomotion(ImpactReactionKind reaction)
    {
        StatusLocomotionKind desired = MapReactionToStatusLocomotion(reaction);
        if (_externalStatusLocomotionKind == desired)
            return;

        _externalStatusLocomotionKind = desired;
        RefreshStatusLocomotion();
    }

    public void SetStaggerStatusLocomotion(ImpactReactionKind reaction)
    {
        StatusLocomotionKind desired = MapReactionToStatusLocomotion(reaction);
        if (_staggerStatusLocomotionKind == desired)
            return;

        _staggerStatusLocomotionKind = desired;
        RefreshStatusLocomotion();
    }

    public void PlaySkill()
    {
        PlaySkill(null);
    }

    public void PlaySkill(SkillGemDefinition skillDef)
    {
        if (IsChainPlaybackActive)
            return;

        if (!TryInitialize() || !HasValidSkillClip(skillDef))
            return;

        if (IsShootBlockingPlaybackActive)
            return;

        StopReloadAction();
        ClearActiveSkillRequest();
        _activeSkillDefinition = skillDef;
        locomotionSM.TryResetState(skill);
    }

    public bool TryPlaySkill(int requestId, float castPointNormalized)
    {
        return TryPlaySkill(requestId, null, castPointNormalized, null);
    }

    public bool TryPlaySkill(int requestId, SkillGemDefinition skillDef, float castPointNormalized)
    {
        return TryPlaySkill(requestId, skillDef, castPointNormalized, null);
    }

    public bool TryPlaySkill(
        int requestId,
        SkillGemDefinition skillDef,
        float castPointNormalized,
        IReadOnlyList<CombatTimelineEventName> timelineEventNames)
    {
        if (IsChainPlaybackActive)
            return false;

        if (requestId <= 0)
            return false;

        if (!TryInitialize() || !HasValidSkillClip(skillDef))
            return false;

        if (IsShootBlockingPlaybackActive)
            return false;

        StopReloadAction();
        ArmSkillRequest(requestId, skillDef, castPointNormalized, timelineEventNames);

        try
        {
            if (locomotionSM.TryResetState(skill))
                return true;
        }
        catch (ArgumentException ex)
        {
            Debug.LogWarning($"[CharacterAnimBrain] Invalid skill clip. Falling back to immediate cast. {ex.Message}", this);
        }

        ClearActiveSkillRequest();
        return false;
    }

    public bool TryPlayUtilityWarpOut(int requestId)
    {
        if (IsChainPlaybackActive)
            return false;

        if (requestId <= 0)
            return false;

        if (!TryInitialize() || !HasValidUtilityWarpOutClip())
            return false;

        StopReloadAction();
        ArmUtilityRequest(requestId, UtilityWarpOutCastPointNormalized);

        try
        {
            if (locomotionSM.TryResetState(utility))
                return true;
        }
        catch (ArgumentException ex)
        {
            Debug.LogWarning($"[CharacterAnimBrain] Invalid utility warp-out clip. Falling back to immediate cast. {ex.Message}", this);
        }

        ClearActiveUtilityRequest();
        return false;
    }

    public void CancelSkillCastRequest(int requestId)
    {
        if (IsChainPlaybackActive)
            return;

        if (requestId <= 0 || requestId != _activeSkillRequestId)
            return;

        ClearActiveSkillRequest();

        if (!TryInitialize())
            return;

        if (locomotionSM.CurrentState == skill)
            locomotionSM.TrySetState(IsDowned ? crawlState : locomotion);
    }

    public void CancelUtilityCastRequest(int requestId)
    {
        if (IsChainPlaybackActive)
            return;

        if (requestId <= 0 || requestId != _activeUtilityRequestId)
            return;

        ClearActiveUtilityRequest();

        if (!TryInitialize())
            return;

        if (locomotionSM.CurrentState == utility)
            locomotionSM.TrySetState(IsDowned ? crawlState : locomotion);
    }

    public void CancelMeleeNow()
    {
        if (IsChainPlaybackActive)
            return;

        if (!TryInitialize())
            return;
        if (locomotionSM.CurrentState != meleeCombo)
            return;

        locomotionSM.TrySetState(IsDowned ? crawlState : locomotion);
    }

    public void InterruptActivePlaybackForExternalControlLoss()
    {
        AbortActiveChainPlaybackForExternalState();
        InterruptActiveSkillRequest();
        InterruptActiveUtilityRequest();

        if (!TryInitialize())
            return;

        StopReloadAction();

        if (locomotionSM.CurrentState == skill ||
            locomotionSM.CurrentState == utility ||
            locomotionSM.CurrentState == meleeCombo ||
            locomotionSM.CurrentState == dashState)
        {
            locomotionSM.TrySetState(IsDowned ? crawlState : locomotion);
        }
    }
    
    // ===================== Helpers =====================
    
    private void PlayReloadNow()
    {
        if (!_initialized)
            return;

        if (ReloadMode == CharacterAnimProfileSO.ReloadBodyMode.FullBody)
        {
            if (locomotionSM.CurrentState == fullBodyReloadState)
                locomotionSM.TryResetState(fullBodyReloadState);
            else
                locomotionSM.TrySetState(fullBodyReloadState);
            return;
        }

        actionSM.TrySetState(reloadState);
    }

    public void StopReloadAction()
    {
        if (IsChainPlaybackActive)
            return;

        if (!TryInitialize()) return;

        if (actionSM.CurrentState == reloadState)
        {
            reloadState.CancelNow();
            actionSM.TrySetState(empty);
        }

        if (locomotionSM.CurrentState == fullBodyReloadState)
        {
            fullBodyReloadState.CancelNow();
            locomotionSM.TrySetState(IsDowned ? crawlState : locomotion);
        }
    }

    private void ClearActionLayerForExclusiveLocomotion()
    {
        _pendingAction = PendingAction.Empty;
        _pendingPulse = false;
        ClearActivePairUpperAction();

        if (!_initialized)
            return;

        if (actionSM.CurrentState == reloadState)
            reloadState.CancelNow();

        actionSM.ForceSetState(empty);
    }

    private bool EnterExclusiveLocomotion(bool usesRootMotion, bool preserveFireHoldIntent)
    {
        bool previousApplyRootMotion = animancer.Animator.applyRootMotion;
        animancer.Animator.applyRootMotion = usesRootMotion;
        RootMotionActive = usesRootMotion;

        if (preserveFireHoldIntent)
            SuspendFireHoldIntent();
        else
            DropFireHoldIntent();

        ClearActionLayerForExclusiveLocomotion();
        return previousApplyRootMotion;
    }

    private void ExitExclusiveLocomotion(bool previousApplyRootMotion)
    {
        animancer.Animator.applyRootMotion = previousApplyRootMotion;
        RootMotionActive = false;

        ClearActionLayerForExclusiveLocomotion();
        ApplyFireHoldContext();
    }

    private void SuspendFireHoldIntent()
    {
        IsHoldingFire = false;
    }

    private void DropFireHoldIntent()
    {
        DesiredFireHold = false;
        IsHoldingFire = false;
        _pendingAction = PendingAction.Empty;
        _pendingPulse = false;
    }

    private void ApplyFireHoldContext()
    {
        if (!_initialized)
        {
            IsHoldingFire = false;
            _pendingAction = DesiredFireHold && _canApplyFireHold
                ? PendingAction.Hold
                : PendingAction.Empty;
            return;
        }

        bool shouldHold = DesiredFireHold && _canApplyFireHold;
        IsHoldingFire = shouldHold;

        if (!shouldHold)
        {
            if (actionSM.CurrentState == shootHold)
                actionSM.TrySetState(empty);

            return;
        }

        TryResumeHoldAction();
    }

    private void TryResumeHoldAction()
    {
        if (!_initialized)
            return;

        if (!IsHoldingFire || ShootHoldLoopClip == null)
            return;

        if (actionSM.CurrentState == empty)
            actionSM.TrySetState(shootHold);
    }

    private AnimancerState PlayActionTransition(ClipTransition transition, PairOffsetUpperAction upperAction)
    {
        if (transition == null || !transition.IsValid)
            return null;

        SetActivePairUpperAction(upperAction);

        if (ActLayer.Weight > ActionLayerActiveWeightThreshold && ActLayer.CurrentState != null)
        {
            ActLayer.StartFade(1f, ActionFadeIn);
            return ActLayer.Play(transition);
        }

        float layerWeight = ActLayer.Weight;
        AnimancerState state = ActLayer.Play(transition, 0f, transition.FadeMode);
        ActLayer.Weight = layerWeight;
        ActLayer.StartFade(1f, ActionFadeIn);
        return state;
    }

    private void SetActivePairBasePose(PairOffsetBasePose pose)
    {
        _activePairBasePose = pose;
        _activePairBasePoseWeights.Clear();
        if (pose != PairOffsetBasePose.None)
            _activePairBasePoseWeights.Add(new PairOffsetBasePoseWeight(pose, 1f));
    }

    private void SetActivePairUpperAction(PairOffsetUpperAction action)
    {
        _activePairUpperAction = action;
    }

    private void ClearActivePairUpperAction()
    {
        _activePairUpperAction = PairOffsetUpperAction.None;
    }

    private void ClearActivePairOffsetState()
    {
        _activePairBasePose = PairOffsetBasePose.None;
        _activePairBasePoseWeights.Clear();
        _activePairUpperAction = PairOffsetUpperAction.None;
    }

    private void SetActivePairBasePoseBlend(Vector2 parameter)
    {
        _activePairBasePoseWeights.Clear();

        float magnitude = Mathf.Clamp01(parameter.magnitude);
        if (magnitude <= 0.001f)
        {
            _activePairBasePose = PairOffsetBasePose.Idle;
            _activePairBasePoseWeights.Add(new PairOffsetBasePoseWeight(PairOffsetBasePose.Idle, 1f));
            return;
        }

        float idleWeight = 1f - magnitude;
        if (idleWeight > 0.001f)
            _activePairBasePoseWeights.Add(new PairOffsetBasePoseWeight(PairOffsetBasePose.Idle, idleWeight));

        Vector2 direction = parameter / magnitude;
        float angle = Mathf.Atan2(direction.y, direction.x);
        float octantFloat = angle / (Mathf.PI * 0.25f);
        octantFloat = octantFloat % 8f;
        if (octantFloat < 0f)
            octantFloat += 8f;

        int lowerOctant = Mathf.FloorToInt(octantFloat);
        int upperOctant = (lowerOctant + 1) % 8;
        float upperWeight = octantFloat - lowerOctant;
        float lowerWeight = 1f - upperWeight;

        AddActivePairBasePoseWeight(MapOctantToPairBasePose(lowerOctant), magnitude * lowerWeight);
        AddActivePairBasePoseWeight(MapOctantToPairBasePose(upperOctant), magnitude * upperWeight);

        _activePairBasePose = ResolveDominantLocomotionPairBasePose(parameter);
    }

    private void AddActivePairBasePoseWeight(PairOffsetBasePose pose, float weight)
    {
        if (pose == PairOffsetBasePose.None || weight <= 0.001f)
            return;

        for (int i = 0; i < _activePairBasePoseWeights.Count; i++)
        {
            PairOffsetBasePoseWeight existing = _activePairBasePoseWeights[i];
            if (existing.Pose != pose)
                continue;

            _activePairBasePoseWeights[i] = new PairOffsetBasePoseWeight(pose, existing.Weight + weight);
            return;
        }

        _activePairBasePoseWeights.Add(new PairOffsetBasePoseWeight(pose, weight));
    }

    private PairOffsetBasePose ResolveDominantLocomotionPairBasePose(Vector2 parameter)
    {
        if (parameter.sqrMagnitude < 0.0001f)
            return PairOffsetBasePose.Idle;

        parameter.Normalize();

        float angle = Mathf.Atan2(parameter.y, parameter.x);
        int octant = Mathf.RoundToInt(angle / (Mathf.PI * 0.25f));
        octant = (octant % 8 + 8) % 8;

        return MapOctantToPairBasePose(octant);
    }

    private static PairOffsetBasePose MapOctantToPairBasePose(int octant)
    {
        return octant switch
        {
            0 => PairOffsetBasePose.Right,
            1 => PairOffsetBasePose.ForwardRight,
            2 => PairOffsetBasePose.Forward,
            3 => PairOffsetBasePose.ForwardLeft,
            4 => PairOffsetBasePose.Left,
            5 => PairOffsetBasePose.BackwardLeft,
            6 => PairOffsetBasePose.Backward,
            7 => PairOffsetBasePose.BackwardRight,
            _ => PairOffsetBasePose.Idle,
        };
    }

    private void HandleShootPulseEnd()
    {
        IsHoldingFire = DesiredFireHold && _canApplyFireHold;

        if (IsHoldingFire && ShootHoldLoopClip != null)
            actionSM.TrySetState(shootHold);
        else
            actionSM.TrySetState(empty);
    }

    private void RefreshStatusLocomotion()
    {
        if (IsChainPlaybackActive)
            return;

        if (!_initialized)
            return;

        if (locomotionSM.CurrentState == deadState)
            return;

        if (locomotionSM.CurrentState == knockbackState)
            return;

        StatusLocomotionKind desired = ResolveStatusLocomotionKind();

        if (desired == StatusLocomotionKind.None)
        {
            if (locomotionSM.CurrentState == statusEffectState)
            {
                _currentStatusLocomotionKind = StatusLocomotionKind.None;
                locomotionSM.TrySetState(IsDowned ? crawlState : locomotion);
            }

            return;
        }

        bool hardOverride = IsHardStatusLocomotion(desired);
        if (hardOverride && locomotionSM.CurrentState == meleeCombo)
        {
            var meleeController = ctx != null ? ctx.MeleeController : null;
            if (!meleeController && ctx != null)
                meleeController = ctx.GetComponent<MeleeController>();

            meleeController?.InterruptMelee();
        }

        if (hardOverride && locomotionSM.CurrentState == fullBodyReloadState)
            fullBodyReloadState.CancelNow();

        bool canTakeOver = hardOverride ||
                           locomotionSM.CurrentState == locomotion ||
                           locomotionSM.CurrentState == crawlState ||
                           locomotionSM.CurrentState == statusEffectState;

        if (!canTakeOver)
            return;

        statusEffectState.SetKind(desired);

        if (locomotionSM.CurrentState == statusEffectState)
        {
            if (_currentStatusLocomotionKind != desired)
            {
                _currentStatusLocomotionKind = desired;
                locomotionSM.TryResetState(statusEffectState);
            }

            return;
        }

        _currentStatusLocomotionKind = desired;
        locomotionSM.TrySetState(statusEffectState);
    }

    private StatusLocomotionKind ResolveStatusLocomotionKind()
    {
        if (!statusEffectController)
            ResolveReferences();

        var activeEffects = statusEffectController?.ActiveEffects;
        StatusLocomotionKind best = StatusLocomotionKind.None;
        int bestPriority = 0;

        if (activeEffects != null)
        {
            for (int i = 0; i < activeEffects.Count; i++)
            {
                var instance = activeEffects[i];
                var definition = instance?.Definition;
                if (definition == null || instance.CurrentStacks <= 0)
                    continue;

                StatusLocomotionKind candidate = ResolveStatusLocomotionKind(definition);
                if (candidate == StatusLocomotionKind.None || GetStatusLocomotionClip(candidate) == null)
                    continue;

                int priority = GetStatusLocomotionPriority(candidate);
                if (priority <= bestPriority)
                    continue;

                best = candidate;
                bestPriority = priority;
            }
        }

        if (_externalStatusLocomotionKind != StatusLocomotionKind.None &&
            GetStatusLocomotionClip(_externalStatusLocomotionKind) != null)
        {
            int priority = GetStatusLocomotionPriority(_externalStatusLocomotionKind);
            if (priority > bestPriority)
            {
                best = _externalStatusLocomotionKind;
                bestPriority = priority;
            }
        }

        if (_staggerStatusLocomotionKind != StatusLocomotionKind.None &&
            GetStatusLocomotionClip(_staggerStatusLocomotionKind) != null)
        {
            int priority = GetStatusLocomotionPriority(_staggerStatusLocomotionKind);
            if (priority > bestPriority)
                return _staggerStatusLocomotionKind;
        }

        return best;
    }

    private StatusLocomotionKind ResolveStatusLocomotionKind(StatusEffectDef definition)
    {
        if (definition == null)
            return StatusLocomotionKind.None;

        if (HasStatusMarker(definition, "miniStune", "miniStun", "mini_stun", "mini-stun"))
            return StatusLocomotionKind.MiniStune;

        if (HasStatusMarker(definition, "freez", "freeze", "frozen"))
            return StatusLocomotionKind.Freez;

        if (HasStatusMarker(definition, "stune", "stun"))
            return StatusLocomotionKind.Stune;

        if (HasStatusMarker(definition, "root", "rooted"))
            return StatusLocomotionKind.Root;

        if (definition.pushStunnedState)
            return StatusLocomotionKind.Stune;

        if ((definition.controlBlocks & ControlBlockFlags.Move) != 0)
            return StatusLocomotionKind.Root;

        return StatusLocomotionKind.None;
    }

    private static bool HasStatusMarker(StatusEffectDef definition, params string[] tokens)
    {
        if (definition == null)
            return false;

        if (ContainsStatusToken(definition.effectId, tokens))
            return true;

        if (ContainsStatusToken(definition.name, tokens))
            return true;

        if (definition.tags == null)
            return false;

        for (int i = 0; i < definition.tags.Count; i++)
        {
            if (ContainsStatusToken(definition.tags[i], tokens))
                return true;
        }

        return false;
    }

    private static bool ContainsStatusToken(string value, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(value) || tokens == null || tokens.Length == 0)
            return false;

        string normalizedValue = NormalizeStatusToken(value);
        if (string.IsNullOrEmpty(normalizedValue))
            return false;

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = NormalizeStatusToken(tokens[i]);
            if (string.IsNullOrEmpty(token))
                continue;

            if (normalizedValue.IndexOf(token, StringComparison.Ordinal) >= 0)
                return true;
        }

        return false;
    }

    private static string NormalizeStatusToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim()
            .ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(":", string.Empty);
    }

    private int GetStatusLocomotionPriority(StatusLocomotionKind kind)
    {
        return kind switch
        {
            StatusLocomotionKind.Freez => 40,
            StatusLocomotionKind.Stune => 30,
            StatusLocomotionKind.MiniStune => 20,
            StatusLocomotionKind.Root => 10,
            _ => 0,
        };
    }

    private static StatusLocomotionKind MapReactionToStatusLocomotion(ImpactReactionKind reaction)
    {
        return reaction switch
        {
            ImpactReactionKind.Root => StatusLocomotionKind.Root,
            ImpactReactionKind.MiniStun => StatusLocomotionKind.MiniStune,
            ImpactReactionKind.Stun => StatusLocomotionKind.Stune,
            _ => StatusLocomotionKind.None,
        };
    }

    private bool IsHardStatusLocomotion(StatusLocomotionKind kind)
    {
        return kind == StatusLocomotionKind.MiniStune ||
               kind == StatusLocomotionKind.Stune ||
               kind == StatusLocomotionKind.Freez;
    }

    private bool ShouldInterruptActionLayer(StatusLocomotionKind kind)
    {
        return IsHardStatusLocomotion(kind);
    }

    private ClipTransition GetStatusLocomotionClip(StatusLocomotionKind kind)
    {
        return kind switch
        {
            StatusLocomotionKind.MiniStune => MiniStuneClip,
            StatusLocomotionKind.Stune => StuneClip,
            StatusLocomotionKind.Root => RootClip,
            StatusLocomotionKind.Freez => FreezClip,
            _ => null,
        };
    }

    private ClipTransition ResolveSkillClip(SkillGemDefinition skillDef)
    {
        if (skillDef != null && skillDef.skillClip != null && skillDef.skillClip.IsValid)
            return skillDef.skillClip;

        return LegacySkillClip;
    }

    private bool HasValidUtilityWarpOutClip()
    {
        var clip = UtilityWarpOutClip;
        return clip != null && clip.IsValid;
    }

    private bool HasValidSkillClip(SkillGemDefinition skillDef)
    {
        var clip = ResolveSkillClip(skillDef);
        return clip != null && clip.IsValid;
    }

    private PlaybackKind ResolveCurrentPlaybackKind()
    {
        if (IsChainPlaybackActive)
            return ResolveActiveChainPlaybackKind();

        if (IsUtilityPlaybackActive)
            return PlaybackKind.UtilityWarpOut;

        if (IsSkillPlaybackActive)
            return PlaybackKind.Skill;

        if (_initialized && locomotionSM.CurrentState == meleeCombo)
            return PlaybackKind.Melee;

        if (_initialized && locomotionSM.CurrentState == deadState)
            return PlaybackKind.Dead;

        if (_initialized && locomotionSM.CurrentState == statusEffectState)
            return PlaybackKind.StatusEffect;

        return PlaybackKind.None;
    }

    private PlaybackKind ResolveActiveChainPlaybackKind()
    {
        return _activeChainKind switch
        {
            ChainPlaybackKind.Skill => PlaybackKind.ChainSkill,
            ChainPlaybackKind.UtilityWarpOut => PlaybackKind.ChainUtilityWarpOut,
            ChainPlaybackKind.UtilityWarpIn => PlaybackKind.ChainUtilityWarpIn,
            _ => PlaybackKind.None,
        };
    }

    private void EmitPlaybackSignal(PlaybackKind kind, PlaybackPhase phase, int requestId)
    {
        if (kind == PlaybackKind.None)
            return;

        PlaybackEvent?.Invoke(new PlaybackSignal(kind, phase, requestId));
    }

    internal void NotifySkillCastMoment()
    {
        if (!_activeSkillReleaseRequested || _activeSkillReleased)
            return;

        _activeSkillReleased = true;
        EmitPlaybackSignal(PlaybackKind.Skill, PlaybackPhase.CastMoment, _activeSkillRequestId);
        SkillCastMomentReached?.Invoke(_activeSkillRequestId);
    }

    internal void NotifyUtilityCastMoment()
    {
        if (!_activeUtilityReleaseRequested || _activeUtilityReleased)
            return;

        _activeUtilityReleased = true;
        EmitPlaybackSignal(PlaybackKind.UtilityWarpOut, PlaybackPhase.CastMoment, _activeUtilityRequestId);
        SkillCastMomentReached?.Invoke(_activeUtilityRequestId);
    }

    internal void NotifySkillStateExited(bool completedNormally)
    {
        int requestId = _activeSkillRequestId;
        SkillGemDefinition activeSkillDefinition = _activeSkillDefinition;
        bool shouldReleaseOnComplete =
            completedNormally &&
            _activeSkillReleaseRequested &&
            !_activeSkillReleased &&
            requestId > 0;
        bool interrupted = !completedNormally && _activeSkillReleaseRequested && requestId > 0;
        bool shouldDeactivateOwner =
            deactivateOwnerOnSkillExit &&
            activeSkillDefinition == null &&
            gameObject.activeSelf;

        if (shouldReleaseOnComplete)
        {
            _activeSkillReleased = true;
            EmitPlaybackSignal(PlaybackKind.Skill, PlaybackPhase.CastMoment, requestId);
            SkillCastMomentReached?.Invoke(requestId);
        }

        ClearActiveSkillRequest();

        if (completedNormally)
        {
            EmitPlaybackSignal(PlaybackKind.Skill, PlaybackPhase.Completed, requestId);
            SkillCompleted?.Invoke();

            if (shouldDeactivateOwner)
                gameObject.SetActive(false);
        }

        if (interrupted && requestId > 0)
        {
            EmitPlaybackSignal(PlaybackKind.Skill, PlaybackPhase.Interrupted, requestId);
            SkillCastInterrupted?.Invoke(requestId);
        }
    }

    internal void NotifyUtilityStateExited(bool completedNormally)
    {
        int requestId = _activeUtilityRequestId;
        bool shouldReleaseOnComplete =
            completedNormally &&
            _activeUtilityReleaseRequested &&
            !_activeUtilityReleased &&
            requestId > 0;
        // Utility interruptions after release still need to unwind dependent sequences.
        bool interrupted = !completedNormally && _activeUtilityReleaseRequested && requestId > 0;

        if (shouldReleaseOnComplete)
        {
            _activeUtilityReleased = true;
            EmitPlaybackSignal(PlaybackKind.UtilityWarpOut, PlaybackPhase.CastMoment, requestId);
            SkillCastMomentReached?.Invoke(requestId);
        }

        ClearActiveUtilityRequest();

        if (completedNormally)
        {
            EmitPlaybackSignal(PlaybackKind.UtilityWarpOut, PlaybackPhase.Completed, requestId);
            SkillCompleted?.Invoke();
        }

        if (interrupted)
        {
            EmitPlaybackSignal(PlaybackKind.UtilityWarpOut, PlaybackPhase.Interrupted, requestId);
            SkillCastInterrupted?.Invoke(requestId);
        }
    }

    internal void BindActiveSkillTimelineEvents(AnimancerEvent.Sequence runtimeEvents)
    {
        ClipTransition clip = ResolveSkillClip(_activeSkillDefinition);
        BindTimelineEventCallbacks(
            runtimeEvents,
            _activeSkillTimelineEventNames,
            clip,
            RaiseSkillTimelineEvent,
            warnMissing: true);

        BindVfxTimelineEventCallbacks(
            runtimeEvents,
            _activeSkillDefinition,
            clip,
            RaiseSkillVfxTimelineEvent);

        BindActiveSkillPreCastTimelineEvents(runtimeEvents);
    }

    internal AnimationVfxSessionToken BeginAnimationVfxSession(IAnimationVfxCueSource source)
    {
        if (source == null)
            return default;

        if (_animationVfxPresenter == null)
            _animationVfxPresenter = GetComponent<AnimationVfxPresenter>();
        if (_animationVfxPresenter == null)
            _animationVfxPresenter = gameObject.AddComponent<AnimationVfxPresenter>();

        Transform root = ctx != null ? ctx.transform : transform;
        SkillUserSystem skillUser = ctx != null ? ctx.EnegySystem : null;
        Animator animator = animancer != null ? animancer.Animator : null;
        return _animationVfxPresenter.BeginSession(
            source,
            new AnimationVfxAnchorContext(
                root,
                skillUser != null ? skillUser.CastOrigin : root,
                skillUser != null ? skillUser.AimTransform : root,
                animator));
    }

    internal void HandleAnimationVfxCue(AnimationVfxSessionToken token, int cueIndex)
    {
        _animationVfxPresenter?.HandleCue(token, cueIndex);
    }

    internal void EndAnimationVfxSession(AnimationVfxSessionToken token)
    {
        _animationVfxPresenter?.EndSession(token);
    }

    internal void BindActiveChainTimelineEvents(AnimancerEvent.Sequence runtimeEvents)
    {
        if (_activeChainKind != ChainPlaybackKind.Skill)
            return;

        ClipTransition clip = ResolveSkillClip(_activeChainSkillDefinition);
        BindTimelineEventCallbacks(
            runtimeEvents,
            _activeChainTimelineEventNames,
            clip,
            RaiseChainSkillTimelineEvent,
            warnMissing: true);

        BindVfxTimelineEventCallbacks(
            runtimeEvents,
            _activeChainSkillDefinition,
            clip,
            RaiseChainSkillVfxTimelineEvent);
    }

    private void BindTimelineEventCallbacks(
        AnimancerEvent.Sequence runtimeEvents,
        IReadOnlyList<CombatTimelineEventName> eventNames,
        ClipTransition clip,
        Action<CombatTimelineEventName> raiseTimelineEvent,
        bool warnMissing)
    {
        if (runtimeEvents == null || eventNames == null || eventNames.Count == 0 || raiseTimelineEvent == null)
            return;

        string clipName = clip != null && clip.Clip != null ? clip.Clip.name : "<none>";

        for (int i = 0; i < eventNames.Count; i++)
        {
            CombatTimelineEventName eventName = eventNames[i];
            if (!CombatTimelineEventNames.IsValid(eventName) || eventName == CombatTimelineEventName.Vfx)
                continue;

            BindTimelineEventCallback(runtimeEvents, eventName, clipName, raiseTimelineEvent, warnMissing);
        }
    }

    private void BindVfxTimelineEventCallbacks(
        AnimancerEvent.Sequence runtimeEvents,
        SkillGemDefinition skillDef,
        ClipTransition clip,
        Action<int> raiseVfxCue)
    {
        if (runtimeEvents == null || skillDef == null || !skillDef.HasSkillVfxEvents || raiseVfxCue == null)
            return;

        int boundCueCount = AnimationVfxEventBinder.Bind(runtimeEvents, raiseVfxCue);
        if (boundCueCount == 0)
        {
            string clipName = clip != null && clip.Clip != null ? clip.Clip.name : "<none>";
            Debug.LogWarning(
                $"[CharacterAnimBrain] Skill clip '{clipName}' has VFX data but no 'Vfx' timeline event.",
                this);
        }
    }

    private void BindTimelineEventCallback(
        AnimancerEvent.Sequence runtimeEvents,
        CombatTimelineEventName eventName,
        string clipName,
        Action<CombatTimelineEventName> raiseTimelineEvent,
        bool warnMissing)
    {
        if (runtimeEvents == null ||
            !CombatTimelineEventNames.IsValid(eventName) ||
            raiseTimelineEvent == null)
        {
            return;
        }

        CombatTimelineEventName capturedEventName = eventName;
        StringReference animancerEventName = CombatTimelineEventNames.ToStringReference(capturedEventName);
        if (animancerEventName == null || string.IsNullOrWhiteSpace(animancerEventName.String))
            return;

        int count = runtimeEvents.SetCallbacks(
            animancerEventName,
            () => raiseTimelineEvent(capturedEventName));

        if (count == 0 && warnMissing)
        {
            Debug.LogWarning(
                $"[CharacterAnimBrain] Skill clip '{clipName}' is missing timeline event '{capturedEventName}' ({animancerEventName}).",
                this);
        }
    }

    private void BindActiveSkillPreCastTimelineEvents(AnimancerEvent.Sequence runtimeEvents)
    {
        SkillGemDefinition skillDef = _activeSkillDefinition;
        if (runtimeEvents == null || skillDef == null || !skillDef.BlockablePreCast)
            return;

        string clipName = ResolveSkillClip(skillDef) != null && ResolveSkillClip(skillDef).Clip != null
            ? ResolveSkillClip(skillDef).Clip.name
            : "<none>";

        BindTimelineEventCallback(
            runtimeEvents,
            skillDef.PreCastOpenEventName,
            clipName,
            RaiseSkillTimelineEvent,
            warnMissing: false);

        BindTimelineEventCallback(
            runtimeEvents,
            skillDef.PreCastCloseEventName,
            clipName,
            RaiseSkillTimelineEvent,
            warnMissing: false);

        if (!skillDef.UseFallbackPreCastWindow)
            return;

        AddFallbackSkillTimelineEvent(
            runtimeEvents,
            skillDef.FallbackPreCastOpenNormalized,
            skillDef.PreCastOpenEventName);

        AddFallbackSkillTimelineEvent(
            runtimeEvents,
            skillDef.FallbackPreCastCloseNormalized,
            skillDef.PreCastCloseEventName);
    }

    private void AddFallbackSkillTimelineEvent(
        AnimancerEvent.Sequence runtimeEvents,
        float normalizedTime,
        CombatTimelineEventName eventName)
    {
        if (runtimeEvents == null || !CombatTimelineEventNames.IsValid(eventName))
            return;

        float clampedTime = Mathf.Clamp(normalizedTime, 0f, 0.999f);
        CombatTimelineEventName capturedEventName = eventName;
        runtimeEvents.Add(clampedTime, () => RaiseSkillTimelineEvent(capturedEventName));
    }

    private void RaiseSkillTimelineEvent(CombatTimelineEventName eventName)
    {
        if (!_activeSkillReleaseRequested ||
            _activeSkillRequestId <= 0 ||
            !CombatTimelineEventNames.IsValid(eventName))
        {
            return;
        }

        SkillTimelineEventRaised?.Invoke(_activeSkillRequestId, eventName);
    }

    private void RaiseChainSkillTimelineEvent(CombatTimelineEventName eventName)
    {
        if (_activeChainKind != ChainPlaybackKind.Skill ||
            !_activeChainReleaseRequested ||
            _activeChainRequestId <= 0 ||
            !CombatTimelineEventNames.IsValid(eventName))
        {
            return;
        }

        SkillTimelineEventRaised?.Invoke(_activeChainRequestId, eventName);
    }

    private void RaiseSkillVfxTimelineEvent(int cueIndex)
    {
        if (!_activeSkillReleaseRequested || _activeSkillRequestId <= 0 || cueIndex < 0)
            return;

        SkillAnimationVfxCueRaised?.Invoke(new SkillAnimationVfxCueSignal(
            _activeSkillRequestId, _activeSkillDefinition, SkillAnimationVfxPhase.MainSkill, cueIndex));
        SkillTimelineEventRaised?.Invoke(_activeSkillRequestId, CombatTimelineEventName.Vfx);
    }

    private void RaiseChainSkillVfxTimelineEvent(int cueIndex)
    {
        if (_activeChainKind != ChainPlaybackKind.Skill ||
            !_activeChainReleaseRequested ||
            _activeChainRequestId <= 0 ||
            cueIndex < 0)
        {
            return;
        }

        SkillAnimationVfxCueRaised?.Invoke(new SkillAnimationVfxCueSignal(
            _activeChainRequestId, _activeChainSkillDefinition, SkillAnimationVfxPhase.MainSkill, cueIndex));
        SkillTimelineEventRaised?.Invoke(_activeChainRequestId, CombatTimelineEventName.Vfx);
    }

    private void RaiseCutsceneVfxCueInternal(int cueIndex)
    {
        if (!_activeSkillReleaseRequested || _activeSkillRequestId <= 0 || cueIndex < 0)
            return;

        SkillAnimationVfxCueRaised?.Invoke(new SkillAnimationVfxCueSignal(
            _activeSkillRequestId, _activeSkillDefinition, SkillAnimationVfxPhase.Cutscene, cueIndex));
    }

    private void ArmSkillRequest(
        int requestId,
        SkillGemDefinition skillDef,
        float castPointNormalized,
        IReadOnlyList<CombatTimelineEventName> timelineEventNames)
    {
        _activeSkillDefinition = skillDef;
        _activeSkillRequestId = requestId;
        _activeSkillCastPointNormalized = Mathf.Clamp(castPointNormalized, 0f, 0.999f);
        _activeSkillReleaseRequested = true;
        _activeSkillReleased = false;
        SetActiveSkillTimelineEventNames(skillDef, timelineEventNames);
        EnsureSkillVfxPresenter(skillDef);
    }

    private void EnsureSkillVfxPresenter(SkillGemDefinition skillDef)
    {
        if (skillDef == null || !skillDef.HasSkillVfxEvents)
            return;
        if (GetComponent<SkillVfxPresenter>() != null)
            return;
        gameObject.AddComponent<SkillVfxPresenter>();
    }

    private void ArmUtilityRequest(int requestId, float castPointNormalized)
    {
        _activeUtilityRequestId = requestId;
        _activeUtilityCastPointNormalized = Mathf.Clamp(castPointNormalized, 0f, 0.999f);
        _activeUtilityReleaseRequested = true;
        _activeUtilityReleased = false;
    }

    private void ClearActiveSkillRequest()
    {
        _activeSkillDefinition = null;
        _activeSkillRequestId = 0;
        _activeSkillCastPointNormalized = 0.35f;
        _activeSkillReleaseRequested = false;
        _activeSkillReleased = false;
        _activeSkillTimelineEventNames.Clear();
    }

    private void SetActiveSkillTimelineEventNames(
        SkillGemDefinition skillDef,
        IReadOnlyList<CombatTimelineEventName> timelineEventNames)
    {
        _activeSkillTimelineEventNames.Clear();

        skillDef?.CollectTimelineEventNames(_activeSkillTimelineEventNames);

        if (timelineEventNames == null || timelineEventNames.Count == 0)
            return;

        for (int i = 0; i < timelineEventNames.Count; i++)
        {
            CombatTimelineEventName eventName = timelineEventNames[i];
            if (!CombatTimelineEventNames.IsValid(eventName))
                continue;

            if (_activeSkillTimelineEventNames.Contains(eventName))
                continue;

            _activeSkillTimelineEventNames.Add(eventName);
        }
    }

    private void ClearActiveUtilityRequest()
    {
        _activeUtilityRequestId = 0;
        _activeUtilityCastPointNormalized = 0.35f;
        _activeUtilityReleaseRequested = false;
        _activeUtilityReleased = false;
    }

    private void InterruptActiveSkillRequest()
    {
        int requestId = _activeSkillRequestId;
        bool shouldNotify = _activeSkillReleaseRequested && !_activeSkillReleased && requestId > 0;

        ClearActiveSkillRequest();

        if (shouldNotify)
        {
            EmitPlaybackSignal(PlaybackKind.Skill, PlaybackPhase.Interrupted, requestId);
            SkillCastInterrupted?.Invoke(requestId);
        }
    }

    private void InterruptActiveUtilityRequest()
    {
        int requestId = _activeUtilityRequestId;
        bool shouldNotify = _activeUtilityReleaseRequested && requestId > 0;

        ClearActiveUtilityRequest();

        if (shouldNotify)
        {
            EmitPlaybackSignal(PlaybackKind.UtilityWarpOut, PlaybackPhase.Interrupted, requestId);
            SkillCastInterrupted?.Invoke(requestId);
        }
    }

    private void OnDisable()
    {
        dashState?.EndVfxSession();
        reloadState?.EndVfxSession();
        fullBodyReloadState?.EndVfxSession();
        meleeCombo?.EndVfxSession();
        RestoreWorldAnimationSpeed();
        InterruptActiveSkillRequest();
        InterruptActiveUtilityRequest();
        InterruptActiveChainRequest();
    }

    private void OnDestroy()
    {
        dashState?.EndVfxSession();
        reloadState?.EndVfxSession();
        fullBodyReloadState?.EndVfxSession();
        meleeCombo?.EndVfxSession();
        RestoreWorldAnimationSpeed();
        InterruptActiveSkillRequest();
        InterruptActiveUtilityRequest();
        InterruptActiveChainRequest();
    }
}
