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
    private bool _bindingDirty = true;
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
    [Tooltip("ใช้แทน baseStats.animProfile สำหรับตัวละครที่ไม่มี CharacterStats เช่น summon/turret")]
    [SerializeField] private CharacterAnimProfileSO inspectorAnimProfile;
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

    private Locomotion_StageIntro stageIntroState;
    private Action onStageIntroEndCache;

    private Locomotion_SpecialReaction specialReactionState;

    private float _reloadDuration = 0f;
    private readonly List<PairOffsetBasePoseWeight> _activePairBasePoseWeights = new List<PairOffsetBasePoseWeight>(3);
    private PairOffsetBasePose _activePairBasePose = PairOffsetBasePose.None;
    private PairOffsetUpperAction _activePairUpperAction = PairOffsetUpperAction.None;

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
        ChainCutscene = 10,
        StageIntro = 11,
        SpecialReaction = 12,
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
    private StatusLocomotionPose _currentStatusLocomotionPose;
    private StatusLocomotionPose _statusLocomotionIntent;

    // pending (กรณีเรียก SetDowned ก่อน init)
    private bool _pendingDownedSet;
    private bool _pendingDownedValue;
    private bool _pendingCrawlIntro;

    public MeleeComboSO.Step CurrentMeleeStep { get; internal set; }
    public int CurrentMeleeStepIndex { get; internal set; }
    public bool IsMeleePlaybackActive => _initialized && locomotionSM.CurrentState == meleeCombo;
    public event Action MeleeHitStart;
    public event Action MeleeHitEnd;
    public event Action MeleeComboEnded;
    public event Action MeleeChainWindowOpened;
    public event Action MeleeChainWindowClosed;
    public event Action MeleeStepCompleted;

    private Action onMeleeHitStartCache;
    private Action onMeleeHitEndCache;
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
    private readonly PlaybackChannel _skillChannel = new() { Kind = PlaybackKind.Skill };
    private AnimationVfxPresenter _animationVfxPresenter;
    private readonly PlaybackChannel _utilityChannel = new() { Kind = PlaybackKind.UtilityWarpOut };

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
    private ClipTransition SkillClip => ResolveSkillClip(_skillChannel.Request.Definition);
    public PairOffsetProfilesSO PairOffsetProfiles => AnimProfile != null ? AnimProfile.pairOffsetProfiles : null;
    public SkillGemDefinition ActiveSkillDefinition => _skillChannel.Request.Definition;
    private ClipTransition MiniStuneClip => AnimProfile.miniStune;
    private ClipTransition StuneClip => AnimProfile.stune;
    private ClipTransition RootClip => AnimProfile.root;
    private ClipTransition FreezClip => AnimProfile.freez;
    private ClipTransition ChainReadyClip => AnimProfile.chainReady;
    private ClipTransition KnockbackClip => AnimProfile.knockback;
    private bool HasActiveSkillClip => HasValidSkillClip(_skillChannel.Request.Definition);
    private bool HasActiveUtilityWarpOutClip => HasValidUtilityWarpOutClip();
    internal float ActiveSkillCastPointNormalized => _skillChannel.Request.CastPointNormalized;
    internal bool HasPendingSkillReleaseRequest => _skillChannel.Request.ReleaseRequested;
    internal float ActiveUtilityCastPointNormalized => _utilityChannel.Request.CastPointNormalized;
    internal bool HasPendingUtilityReleaseRequest => _utilityChannel.Request.ReleaseRequested;
    public bool IsSkillPlaybackActive =>
        _skillChannel.IsActive ||
        (_initialized && locomotionSM.CurrentState == skill);
    private bool IsUtilityPlaybackActive =>
        _utilityChannel.IsActive ||
        (_initialized && locomotionSM.CurrentState == utility);
    /// <summary>True when any skill, utility, or chain playback blocks shooting.</summary>
    public bool IsShootBlockingPlaybackActive =>
        IsSkillPlaybackActive ||
        IsUtilityPlaybackActive ||
        IsChainPlaybackActive;
    /// <summary>True when a utility warp or chain-utility is active.</summary>
    public bool IsUtilityActive => IsUtilityPlaybackActive || IsChainUtilityPlaybackActive;
    /// <summary>True when locomotion is fully owned by an exclusive action (skill/utility/chain/melee/reload/knockback/dead/status).</summary>
    public bool IsExclusiveLocomotionActive =>
        _initialized &&
        (locomotionSM.CurrentState == skill ||
         locomotionSM.CurrentState == utility ||
         locomotionSM.CurrentState == chain ||
         locomotionSM.CurrentState == meleeCombo ||
         locomotionSM.CurrentState == fullBodyReloadState ||
         locomotionSM.CurrentState == knockbackState ||
         locomotionSM.CurrentState == deadState ||
         locomotionSM.CurrentState == statusEffectState ||
         locomotionSM.CurrentState == stageIntroState ||
         locomotionSM.CurrentState == specialReactionState);
    public PlaybackKind CurrentPlaybackKind => ResolveCurrentPlaybackKind();

    /// <summary>What owns locomotion right now, in the vocabulary <see cref="CharacterAnimationTransitionPolicy"/> speaks.</summary>
    public CharacterAnimationMode CurrentAnimationMode
    {
        get
        {
            if (!_initialized)
                return CharacterAnimationMode.None;

            if (locomotionSM.CurrentState == deadState) return CharacterAnimationMode.Dead;

            // Chain/skill/utility are read through their channels, not the FSM state, because a
            // request is armed just before its state is entered and the gates have always treated
            // that window as occupied.
            if (IsChainPlaybackActive) return CharacterAnimationMode.Chain;
            if (IsUtilityPlaybackActive) return CharacterAnimationMode.Utility;
            if (IsSkillPlaybackActive) return CharacterAnimationMode.Skill;

            // Above knockback and status: the Special Point reaction outranks every other combat
            // reaction once it owns locomotion.
            if (locomotionSM.CurrentState == specialReactionState) return CharacterAnimationMode.SpecialReaction;

            if (locomotionSM.CurrentState == knockbackState) return CharacterAnimationMode.Knockback;
            if (locomotionSM.CurrentState == stageIntroState) return CharacterAnimationMode.StageIntro;
            if (locomotionSM.CurrentState == statusEffectState)
            {
                return IsHardStatusLocomotion(_currentStatusLocomotionPose)
                    ? CharacterAnimationMode.HardStatus
                    : CharacterAnimationMode.SoftStatus;
            }

            if (locomotionSM.CurrentState == meleeCombo) return CharacterAnimationMode.Melee;
            if (locomotionSM.CurrentState == fullBodyReloadState) return CharacterAnimationMode.FullBodyReload;
            if (locomotionSM.CurrentState == dashState) return CharacterAnimationMode.Dash;
            if (locomotionSM.CurrentState == crawlState) return CharacterAnimationMode.Crawl;

            return CharacterAnimationMode.Locomotion;
        }
    }

    private bool CanStartAnimation(CharacterAnimationMode requested, CharacterAnimationTransitionReason reason)
    {
        return CharacterAnimationTransitionPolicy.CanStart(
            CurrentAnimationMode,
            requested,
            reason,
            IsDowned);
    }

    /// <summary>
    /// Used by commands that stop or cancel rather than start a mode, where there is no requested
    /// mode to weigh. Only the chain-ownership rule applies to those.
    /// </summary>
    private bool ExternalCommandBlockedByChain() =>
        !CharacterAnimationTransitionPolicy.AllowsExternalCommand(
            CurrentAnimationMode,
            CharacterAnimationTransitionReason.NormalCommand);

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

        if (requestId == _skillChannel.Request.RequestId &&
            _skillChannel.Request.ReleaseRequested &&
            locomotionSM.CurrentState == skill &&
            skill != null)
        {
            return skill.TryGetNormalizedTime(out normalizedTime);
        }

        if (requestId == _chainChannel.Request.RequestId &&
            _activeChainKind == ChainPlaybackKind.Skill &&
            _chainChannel.Request.ReleaseRequested &&
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
        if (requestId <= 0 || requestId != _skillChannel.Request.RequestId) return false;
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
        InvalidateAnimationBinding();
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

    /// <summary>
    /// Forces the next initialization check to re-resolve the character hierarchy. Call this after
    /// rebuilding the model or repointing the Animancer rig; the Brain otherwise trusts its cached
    /// binding and never walks the hierarchy again.
    /// </summary>
    public void InvalidateAnimationBinding()
    {
        _bindingDirty = true;
    }

    /// <summary>
    /// Reads the anim profile from what is already bound, without resolving anything. Used by the
    /// steady-state check, so it must not call <see cref="ResolveReferences"/>.
    /// </summary>
    private bool TryPeekAnimProfile(out CharacterAnimProfileSO animProfile)
    {
        if (_animProfileOverride != null)
        {
            animProfile = _animProfileOverride;
            return true;
        }

        animProfile = ctx != null && ctx.baseStats != null ? ctx.baseStats.animProfile : null;
        return animProfile != null;
    }

    private bool TryInitialize()
    {
        // Steady state. This runs once per frame per character, so it stays to reference
        // comparisons only: no GetComponent walk, no ctx.ResolveReferences(), no allocation.
        // Any binding change - a rebuilt model, a Morph profile override, a destroyed rig - shows
        // up as one of these comparisons failing, which falls through to the full rebind below.
        if (!_bindingDirty &&
            _initialized &&
            animancer != null &&
            _boundAnimator != null &&
            _boundAnimProfile != null &&
            animancer.Animator == _boundAnimator &&
            TryPeekAnimProfile(out CharacterAnimProfileSO boundProfile) &&
            boundProfile == _boundAnimProfile)
        {
            return true;
        }

        return TryRebind();
    }

    private bool TryRebind()
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
            if (_skillChannel.IsActive || _utilityChannel.IsActive)
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
        {
            _bindingDirty = false;
            return true;
        }

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
        stageIntroState = new Locomotion_StageIntro(this);
        specialReactionState = new Locomotion_SpecialReaction(this);

        ForceSetLocomotionState(locomotion);
        actionSM.ForceSetState(empty);
        ClearActivePairOffsetState();

        _initialized = true;
        _bindingDirty = false;

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
                TrySetLocomotionState(meleeCombo);
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

        if (inspectorAnimProfile != null)
            SetAnimProfileOverride(inspectorAnimProfile);
    }

    private void OnEnable()
    {
        InvalidateAnimationBinding();
    }

    private void ResolveReferences()
    {
        if (!ctx)
        {  
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
        if (ExternalCommandBlockedByChain())
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
        if (ExternalCommandBlockedByChain())
            return;

        _reloadDuration = Mathf.Max(0.01f, reloadDuration);

        _pendingAction = PendingAction.Reload;

        if (!TryInitialize())
            return;

        PlayReloadNow();
    }

    public void PlayDash(float dashDuration, Vector2 dashDirLocal)
    {
        if (!CanStartAnimation(CharacterAnimationMode.Dash, CharacterAnimationTransitionReason.NormalCommand))
            return;

        _dashDuration = Mathf.Max(0.01f, dashDuration);
        _dashDirLocal = dashDirLocal;

        if (!TryInitialize())
            return;
        
        if (ReloadMode == CharacterAnimProfileSO.ReloadBodyMode.FullBody)
            StopReloadAction();
        if (locomotionSM.CurrentState == dashState)
            TryResetLocomotionState(dashState);
        else
            TrySetLocomotionState(dashState);
    }

    public void EndDashNow()
    {
        if (ExternalCommandBlockedByChain())
            return;

        if (!TryInitialize()) return;
        TrySetLocomotionState(locomotion);
    }

    private void HandleDashEnd()
    {
        if (!_initialized || locomotionSM.CurrentState != dashState)
            return;

        TrySetLocomotionState(locomotion);
    }

    public bool PlayKnockback(KnockbackData knockback)
    {
        if (!knockback.IsValid)
            return false;

        if (!TryInitialize() || KnockbackClip == null || !KnockbackClip.IsValid)
            return false;

        if (!CanStartAnimation(CharacterAnimationMode.Knockback, CharacterAnimationTransitionReason.NormalCommand))
            return false;

        StopReloadAction();
        knockbackState.SetKnockback(knockback);

        try
        {
            return TryResetLocomotionState(knockbackState);
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

        TrySetLocomotionState(IsDowned ? crawlState : locomotion);
    }

    internal bool TryStartMeleePlayback(MeleeComboSO combo, MeleeComboSO.Step firstStep, int stepIndex)
    {
        if (!CanStartAnimation(CharacterAnimationMode.Melee, CharacterAnimationTransitionReason.NormalCommand))
            return false;
        if (!TryInitialize())
            return false;

        StopReloadAction();
        meleeCombo.PrepareForStart(combo, firstStep, stepIndex);
        return TrySetLocomotionState(meleeCombo);
    }

    internal void AdvanceMeleeStep(MeleeComboSO.Step step, int stepIndex)
    {
        if (locomotionSM.CurrentState != meleeCombo)
            return;
        meleeCombo.PlayStepExternal(step, stepIndex);
    }

    internal void CompleteMeleePlayback()
    {
        if (locomotionSM.CurrentState != meleeCombo)
            return;

        meleeCombo.EndVfxSession();
        MeleeComboEnded?.Invoke();
        EmitPlaybackSignal(PlaybackKind.Melee, PlaybackPhase.Completed, 0);

        bool exited = TrySetLocomotionState(IsDowned ? crawlState : locomotion);
        if (!exited)
            ExitExclusiveLocomotion(false);
    }

    /// <summary>True while the MapRun stage intro pose owns locomotion.</summary>
    public bool IsStageIntroPlaybackActive =>
        _initialized && locomotionSM.CurrentState == stageIntroState;

    private ClipTransition StageIntroClip => AnimProfile != null ? AnimProfile.stageIntroClip : null;

    /// <summary>
    /// Enters the exclusive, root-motion-free stage intro pose. Falls back to the locomotion idle
    /// blend when the profile has no authored <see cref="CharacterAnimProfileSO.stageIntroClip"/>.
    /// </summary>
    public bool TryPlayStageIntro()
    {
        if (_initialized &&
            !CanStartAnimation(CharacterAnimationMode.StageIntro, CharacterAnimationTransitionReason.CinematicOverride))
        {
            return false;
        }

        AbortActiveChainPlaybackForExternalState();
        InterruptActiveSkillRequest();
        InterruptActiveUtilityRequest();

        if (!TryInitialize())
            return false;

        StopReloadAction();

        if (locomotionSM.CurrentState == stageIntroState)
            return TryResetLocomotionState(stageIntroState);

        return TrySetLocomotionState(stageIntroState);
    }

    /// <summary>Leaves the stage intro pose and returns to the normal locomotion/crawl state.</summary>
    public void StopStageIntro()
    {
        if (!_initialized)
            return;

        if (locomotionSM.CurrentState != stageIntroState)
            return;

        bool exited = TrySetLocomotionState(IsDowned ? crawlState : locomotion);
        if (!exited)
            ExitExclusiveLocomotion(false);
    }

    public void PlayDead()
    {
        if (_initialized &&
            !CanStartAnimation(CharacterAnimationMode.Dead, CharacterAnimationTransitionReason.LifeStateOverride))
        {
            return;
        }

        AbortActiveChainPlaybackForExternalState();
        
        if (!TryInitialize()) return;

        StopReloadAction();

        TrySetLocomotionState(deadState);
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

        ForceSetLocomotionState(IsDowned ? crawlState : locomotion);
    }

    private bool ConsumePendingCrawlIntro()
    {
        bool shouldPlayIntro = _pendingCrawlIntro;
        _pendingCrawlIntro = false;
        return shouldPlayIntro;
    }

    internal void SetStatusLocomotionIntent(StatusLocomotionPose intent)
    {
        if (_statusLocomotionIntent == intent)
            return;

        _statusLocomotionIntent = intent;
        RefreshStatusLocomotion();
    }

    public void PlaySkill()
    {
        PlaySkill(null);
    }

    public void PlaySkill(SkillGemDefinition skillDef)
    {
        if (!TryInitialize() || !HasValidSkillClip(skillDef))
            return;

        if (!CanStartAnimation(CharacterAnimationMode.Skill, CharacterAnimationTransitionReason.NormalCommand))
            return;

        StopReloadAction();
        ClearActiveSkillRequest();
        _skillChannel.Request.Definition = skillDef;
        TryResetLocomotionState(skill);
    }

    public bool TryPlaySkill(int requestId, float castPointNormalized)
    {
        return TryPlaySkill(requestId, null, castPointNormalized, null, usePlanarRootMotion: false);
    }

    public bool TryPlaySkill(int requestId, SkillGemDefinition skillDef, float castPointNormalized)
    {
        return TryPlaySkill(requestId, skillDef, castPointNormalized, null, usePlanarRootMotion: false);
    }

    public bool TryPlaySkill(
        int requestId,
        SkillGemDefinition skillDef,
        float castPointNormalized,
        IReadOnlyList<CombatTimelineEventName> timelineEventNames)
    {
        return TryPlaySkill(
            requestId,
            skillDef,
            castPointNormalized,
            timelineEventNames,
            usePlanarRootMotion: false);
    }

    public bool TryPlaySkill(
        int requestId,
        SkillGemDefinition skillDef,
        float castPointNormalized,
        IReadOnlyList<CombatTimelineEventName> timelineEventNames,
        bool usePlanarRootMotion)
    {
        if (requestId <= 0)
            return false;

        if (!TryInitialize() || !HasValidSkillClip(skillDef))
            return false;

        if (!CanStartAnimation(CharacterAnimationMode.Skill, CharacterAnimationTransitionReason.NormalCommand))
            return false;

        StopReloadAction();
        ArmSkillRequest(
            requestId,
            skillDef,
            castPointNormalized,
            timelineEventNames,
            usePlanarRootMotion);

        try
        {
            if (TryResetLocomotionState(skill))
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
        if (requestId <= 0)
            return false;

        if (!TryInitialize() || !HasValidUtilityWarpOutClip())
            return false;

        if (!CanStartAnimation(CharacterAnimationMode.Utility, CharacterAnimationTransitionReason.NormalCommand))
            return false;

        StopReloadAction();
        ArmUtilityRequest(requestId, UtilityWarpOutCastPointNormalized);

        try
        {
            if (TryResetLocomotionState(utility))
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
        if (ExternalCommandBlockedByChain())
            return;

        if (requestId <= 0 || requestId != _skillChannel.Request.RequestId)
            return;

        ClearActiveSkillRequest();

        if (!TryInitialize())
            return;

        if (locomotionSM.CurrentState == skill)
            TrySetLocomotionState(IsDowned ? crawlState : locomotion);
    }

    public void CancelUtilityCastRequest(int requestId)
    {
        if (ExternalCommandBlockedByChain())
            return;

        if (requestId <= 0 || requestId != _utilityChannel.RequestId)
            return;

        ClearActiveUtilityRequest();

        if (!TryInitialize())
            return;

        if (locomotionSM.CurrentState == utility)
            TrySetLocomotionState(IsDowned ? crawlState : locomotion);
    }

    public void CancelMeleeNow()
    {
        if (ExternalCommandBlockedByChain())
            return;

        if (!TryInitialize())
            return;
        if (locomotionSM.CurrentState != meleeCombo)
            return;

        TrySetLocomotionState(IsDowned ? crawlState : locomotion);
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
            TrySetLocomotionState(IsDowned ? crawlState : locomotion);
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
                TryResetLocomotionState(fullBodyReloadState);
            else
                TrySetLocomotionState(fullBodyReloadState);
            return;
        }

        actionSM.TrySetState(reloadState);
    }

    public void StopReloadAction()
    {
        if (ExternalCommandBlockedByChain())
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
            TrySetLocomotionState(IsDowned ? crawlState : locomotion);
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
        bool previousApplyRootMotion = AnimatorAppliesRootMotion;
        SetRootMotionActive(usesRootMotion);

        if (preserveFireHoldIntent)
            SuspendFireHoldIntent();
        else
            DropFireHoldIntent();

        ClearActionLayerForExclusiveLocomotion();
        return previousApplyRootMotion;
    }

    private void ExitExclusiveLocomotion(bool previousApplyRootMotion)
    {
        SetRootMotionActive(false);
        RestoreAnimatorRootMotionIfUnowned(previousApplyRootMotion);

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
        if (!_initialized)
            return;

        if (!CharacterAnimationTransitionPolicy.AllowsExternalCommand(
                CurrentAnimationMode,
                CharacterAnimationTransitionReason.StatusOverride))
        {
            return;
        }

        if (locomotionSM.CurrentState == deadState)
            return;

        if (locomotionSM.CurrentState == knockbackState)
            return;

        StatusLocomotionPose desired = _statusLocomotionIntent;
        if (desired != StatusLocomotionPose.None && GetStatusLocomotionClip(desired) == null)
            desired = StatusLocomotionPose.None;

        if (desired == StatusLocomotionPose.None)
        {
            if (locomotionSM.CurrentState == statusEffectState)
            {
                _currentStatusLocomotionPose = StatusLocomotionPose.None;
                TrySetLocomotionState(IsDowned ? crawlState : locomotion);
            }

            return;
        }

        bool hardOverride = IsHardStatusLocomotion(desired);

        if (hardOverride && locomotionSM.CurrentState == fullBodyReloadState)
            fullBodyReloadState.CancelNow();

        CharacterAnimationMode requestedStatusMode = hardOverride
            ? CharacterAnimationMode.HardStatus
            : CharacterAnimationMode.SoftStatus;

        if (!CanStartAnimation(requestedStatusMode, CharacterAnimationTransitionReason.StatusOverride))
            return;

        statusEffectState.SetKind(desired);

        if (locomotionSM.CurrentState == statusEffectState)
        {
            if (_currentStatusLocomotionPose != desired)
            {
                _currentStatusLocomotionPose = desired;
                TryResetLocomotionState(statusEffectState);
            }

            return;
        }

        _currentStatusLocomotionPose = desired;
        TrySetLocomotionState(statusEffectState);
    }

    private bool IsHardStatusLocomotion(StatusLocomotionPose kind)
    {
        return kind == StatusLocomotionPose.MiniStun ||
               kind == StatusLocomotionPose.Stun ||
               kind == StatusLocomotionPose.Freeze ||
               kind == StatusLocomotionPose.ChainReady;
    }

    private bool ShouldInterruptActionLayer(StatusLocomotionPose kind)
    {
        return IsHardStatusLocomotion(kind);
    }

    private ClipTransition GetStatusLocomotionClip(StatusLocomotionPose kind)
    {
        return kind switch
        {
            StatusLocomotionPose.MiniStun => MiniStuneClip,
            StatusLocomotionPose.Stun => StuneClip,
            StatusLocomotionPose.Root => RootClip,
            StatusLocomotionPose.Freeze => FreezClip,
            StatusLocomotionPose.ChainReady => ChainReadyClip,
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

    internal bool TryResolveSkillAnimationClip(SkillGemDefinition skillDef, out AnimationClip clip)
    {
        clip = null;

        if (skillDef == null || !TryInitialize())
            return false;

        return TryExtractAnimationClip(ResolveSkillClip(skillDef), out clip);
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

        if (_initialized && locomotionSM.CurrentState == stageIntroState)
            return PlaybackKind.StageIntro;

        if (_initialized && locomotionSM.CurrentState == specialReactionState)
            return PlaybackKind.SpecialReaction;

        return PlaybackKind.None;
    }

    private PlaybackKind ResolveActiveChainPlaybackKind()
    {
        return _activeChainKind switch
        {
            ChainPlaybackKind.Skill => PlaybackKind.ChainSkill,
            ChainPlaybackKind.UtilityWarpOut => PlaybackKind.ChainUtilityWarpOut,
            ChainPlaybackKind.UtilityWarpIn => PlaybackKind.ChainUtilityWarpIn,
            ChainPlaybackKind.Cutscene => PlaybackKind.ChainCutscene,
            _ => PlaybackKind.None,
        };
    }

    internal void NotifySkillCastMoment()
    {
        if (!_skillChannel.Request.TryReleaseCast())
            return;

        EmitPlaybackSignal(PlaybackKind.Skill, PlaybackPhase.CastMoment, _skillChannel.Request.RequestId);
    }

    internal void NotifyUtilityCastMoment()
    {
        if (!_utilityChannel.Request.TryReleaseCast())
            return;

        EmitPlaybackSignal(PlaybackKind.UtilityWarpOut, PlaybackPhase.CastMoment, _utilityChannel.RequestId);
    }

    internal void NotifySkillStateExited(bool completedNormally)
    {
        PlaybackSessionClose close = _skillChannel.Request.Close(completedNormally);

        if (close.OwesCastMoment)
            EmitPlaybackSignal(PlaybackKind.Skill, PlaybackPhase.CastMoment, close.RequestId);

        ClearActiveSkillRequest();

        // A request-less PlaySkill() still reports completion, with request id 0.
        if (completedNormally)
            EmitPlaybackSignal(PlaybackKind.Skill, PlaybackPhase.Completed, close.RequestId);

        if (close.OwesInterrupted)
            EmitPlaybackSignal(PlaybackKind.Skill, PlaybackPhase.Interrupted, close.RequestId);
    }

    internal void NotifyUtilityStateExited(bool completedNormally)
    {
        PlaybackSessionClose close = _utilityChannel.Request.Close(completedNormally);

        if (close.OwesCastMoment)
            EmitPlaybackSignal(PlaybackKind.UtilityWarpOut, PlaybackPhase.CastMoment, close.RequestId);

        ClearActiveUtilityRequest();

        if (completedNormally)
            EmitPlaybackSignal(PlaybackKind.UtilityWarpOut, PlaybackPhase.Completed, close.RequestId);

        if (close.OwesInterrupted)
            EmitPlaybackSignal(PlaybackKind.UtilityWarpOut, PlaybackPhase.Interrupted, close.RequestId);
    }

    internal void BindActiveSkillTimelineEvents(AnimancerEvent.Sequence runtimeEvents)
    {
        ClipTransition clip = ResolveSkillClip(_skillChannel.Request.Definition);
        BindTimelineEventCallbacks(
            runtimeEvents,
            _skillChannel.Request.TimelineEventNames,
            clip,
            RaiseSkillTimelineEvent,
            warnMissing: true);

        BindVfxTimelineEventCallbacks(
            runtimeEvents,
            _skillChannel.Request.Definition,
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
        if (_activeChainKind == ChainPlaybackKind.Cutscene)
        {
            BindActiveChainCutsceneVfxTimelineEvents(runtimeEvents);
            return;
        }

        if (_activeChainKind != ChainPlaybackKind.Skill)
            return;

        ClipTransition clip = ResolveSkillClip(_chainChannel.Request.Definition);
        BindTimelineEventCallbacks(
            runtimeEvents,
            _chainChannel.Request.TimelineEventNames,
            clip,
            RaiseChainSkillTimelineEvent,
            warnMissing: true);

        BindVfxTimelineEventCallbacks(
            runtimeEvents,
            _chainChannel.Request.Definition,
            clip,
            RaiseChainSkillVfxTimelineEvent);
    }

    private void BindActiveChainCutsceneVfxTimelineEvents(AnimancerEvent.Sequence runtimeEvents)
    {
        CutsceneDef def = _activeChainCutsceneDef;
        if (runtimeEvents == null ||
            def?.cutsceneVfxEvents == null ||
            def.cutsceneVfxEvents.Count == 0)
        {
            return;
        }

        int boundCueCount = AnimationVfxEventBinder.Bind(
            runtimeEvents,
            RaiseChainCutsceneVfxTimelineEvent,
            invokeStartEventsImmediately: true);
        if (boundCueCount == 0)
        {
            string clipName = _activeChainCutsceneClip?.Clip != null
                ? _activeChainCutsceneClip.Clip.name
                : "<none>";
            Debug.LogWarning(
                $"[CharacterAnimBrain] Chain cutscene clip '{clipName}' has VFX data but no 'Vfx' timeline event.",
                this);
        }
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
        SkillGemDefinition skillDef = _skillChannel.Request.Definition;
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
        if (!_skillChannel.Request.ReleaseRequested ||
            _skillChannel.Request.RequestId <= 0 ||
            !CombatTimelineEventNames.IsValid(eventName))
        {
            return;
        }

        SkillTimelineEventRaised?.Invoke(_skillChannel.Request.RequestId, eventName);
    }

    private void RaiseChainSkillTimelineEvent(CombatTimelineEventName eventName)
    {
        if (_activeChainKind != ChainPlaybackKind.Skill ||
            !_chainChannel.Request.ReleaseRequested ||
            _chainChannel.Request.RequestId <= 0 ||
            !CombatTimelineEventNames.IsValid(eventName))
        {
            return;
        }

        SkillTimelineEventRaised?.Invoke(_chainChannel.Request.RequestId, eventName);
    }

    private void RaiseSkillVfxTimelineEvent(int cueIndex)
    {
        if (!_skillChannel.Request.ReleaseRequested || _skillChannel.Request.RequestId <= 0 || cueIndex < 0)
            return;

        SkillAnimationVfxCueRaised?.Invoke(new SkillAnimationVfxCueSignal(
            _skillChannel.Request.RequestId, _skillChannel.Request.Definition, SkillAnimationVfxPhase.MainSkill, cueIndex));
        SkillTimelineEventRaised?.Invoke(_skillChannel.Request.RequestId, CombatTimelineEventName.Vfx);
    }

    private void RaiseChainSkillVfxTimelineEvent(int cueIndex)
    {
        if (_activeChainKind != ChainPlaybackKind.Skill ||
            !_chainChannel.Request.ReleaseRequested ||
            _chainChannel.Request.RequestId <= 0 ||
            cueIndex < 0)
        {
            return;
        }

        SkillAnimationVfxCueRaised?.Invoke(new SkillAnimationVfxCueSignal(
            _chainChannel.Request.RequestId, _chainChannel.Request.Definition, SkillAnimationVfxPhase.MainSkill, cueIndex));
        SkillTimelineEventRaised?.Invoke(_chainChannel.Request.RequestId, CombatTimelineEventName.Vfx);
    }

    private void RaiseChainCutsceneVfxTimelineEvent(int cueIndex)
    {
        if (_activeChainKind != ChainPlaybackKind.Cutscene ||
            !_chainChannel.Request.ReleaseRequested ||
            _chainChannel.Request.RequestId <= 0 ||
            cueIndex < 0)
        {
            return;
        }

        SkillAnimationVfxCueRaised?.Invoke(new SkillAnimationVfxCueSignal(
            _chainChannel.Request.RequestId, null, SkillAnimationVfxPhase.Cutscene, cueIndex));
    }

    private void RaiseCutsceneVfxCueInternal(int cueIndex)
    {
        if (!_skillChannel.Request.ReleaseRequested || _skillChannel.Request.RequestId <= 0 || cueIndex < 0)
            return;

        SkillAnimationVfxCueRaised?.Invoke(new SkillAnimationVfxCueSignal(
            _skillChannel.Request.RequestId, _skillChannel.Request.Definition, SkillAnimationVfxPhase.Cutscene, cueIndex));
    }

    private void ArmSkillRequest(
        int requestId,
        SkillGemDefinition skillDef,
        float castPointNormalized,
        IReadOnlyList<CombatTimelineEventName> timelineEventNames,
        bool usePlanarRootMotion)
    {
        _skillChannel.Request.Definition = skillDef;
        _skillChannel.Request.Begin(requestId, castPointNormalized);
        _skillChannel.Request.UsesPlanarRootMotion = usePlanarRootMotion;
        _skillChannel.Request.IgnoresCharacterCollisionDuringRootMotion =
            skillDef == null || skillDef.IgnoreCharacterCollisionDuringRootMotion;
        SetActiveSkillTimelineEventNames(skillDef, timelineEventNames);
        EnsureSkillVfxPresenter(skillDef);
    }

    internal void ApplyActiveSkillRootMotionPolicy() => SetRootMotionShape(
        _skillChannel.Request.UsesPlanarRootMotion,
        _skillChannel.Request.IgnoresCharacterCollisionDuringRootMotion);

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
        _utilityChannel.Request.Begin(requestId, castPointNormalized);
        _utilityChannel.Kind = PlaybackKind.UtilityWarpOut;
    }

    private void ClearActiveSkillRequest()
    {
        _skillChannel.Clear();
        _skillChannel.Kind = PlaybackKind.Skill;
    }

    private void SetActiveSkillTimelineEventNames(
        SkillGemDefinition skillDef,
        IReadOnlyList<CombatTimelineEventName> timelineEventNames)
    {
        _skillChannel.Request.TimelineEventNames.Clear();

        skillDef?.CollectTimelineEventNames(_skillChannel.Request.TimelineEventNames);

        if (timelineEventNames == null || timelineEventNames.Count == 0)
            return;

        for (int i = 0; i < timelineEventNames.Count; i++)
        {
            CombatTimelineEventName eventName = timelineEventNames[i];
            if (!CombatTimelineEventNames.IsValid(eventName))
                continue;

            if (_skillChannel.Request.TimelineEventNames.Contains(eventName))
                continue;

            _skillChannel.Request.TimelineEventNames.Add(eventName);
        }
    }

    private void ClearActiveUtilityRequest()
    {
        _utilityChannel.Clear();
        _utilityChannel.Kind = PlaybackKind.UtilityWarpOut;
    }

    private void InterruptActiveSkillRequest()
    {
        PlaybackSessionClose close = _skillChannel.Request.Close(completedNormally: false);

        ClearActiveSkillRequest();

        if (close.OwesInterrupted)
            EmitPlaybackSignal(PlaybackKind.Skill, PlaybackPhase.Interrupted, close.RequestId);
    }

    private void InterruptActiveUtilityRequest()
    {
        PlaybackSessionClose close = _utilityChannel.Request.Close(completedNormally: false);

        ClearActiveUtilityRequest();

        if (close.OwesInterrupted)
            EmitPlaybackSignal(PlaybackKind.UtilityWarpOut, PlaybackPhase.Interrupted, close.RequestId);
    }

    private void OnDisable()
    {
        dashState?.EndVfxSession();
        reloadState?.EndVfxSession();
        fullBodyReloadState?.EndVfxSession();
        meleeCombo?.EndVfxSession();
        RestoreWorldAnimationSpeed();
        ClearRootMotionPolicy();
        InterruptActiveSkillRequest();
        InterruptActiveUtilityRequest();
        InterruptActiveChainRequest();
        ReleaseExclusiveLocomotionForDisable();
    }

    /// <summary>
    /// Disabling clears the playback requests but leaves the locomotion state machine parked in
    /// whatever exclusive state was running, so re-enabling would resume a state that has no
    /// request behind it (and keep tripping the chain watchdog). Hand locomotion back first.
    /// </summary>
    private void ReleaseExclusiveLocomotionForDisable()
    {
        if (!_initialized || !animancer || animancer.Animator == null)
            return;

        // Every state that can refuse to exit has to be released first, or the transition below
        // silently fails and the FSM stays parked. Re-enabling does not rebuild the states when
        // the binding is unchanged, so a stuck state stays stuck for the rest of the run.
        AllowChainStateExit();

        if (actionSM.CurrentState == reloadState)
            reloadState.CancelNow();

        if (locomotionSM.CurrentState == fullBodyReloadState)
            fullBodyReloadState.CancelNow();

        if (!IsExclusiveLocomotionActive)
            return;

        TrySetLocomotionState(IsDowned ? crawlState : locomotion);
    }

    private void OnDestroy()
    {
        dashState?.EndVfxSession();
        reloadState?.EndVfxSession();
        fullBodyReloadState?.EndVfxSession();
        meleeCombo?.EndVfxSession();
        RestoreWorldAnimationSpeed();
        ClearRootMotionPolicy();
        InterruptActiveSkillRequest();
        InterruptActiveUtilityRequest();
        InterruptActiveChainRequest();
    }
}
