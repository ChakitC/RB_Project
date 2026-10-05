using System;
using System.Collections.Generic;
using Animancer;
using Animancer.FSM;
using UnityEngine;

[DefaultExecutionOrder(1100)]
public sealed partial class CharacterAnimBrain : MonoBehaviour
{
    private const float ActionLayerActiveWeightThreshold = 0.001f;

    private readonly CharacterAnimationBinding binding = new();
    internal AnimancerComponent BoundAnimancer => tearingDown || !_initialized ? null : animancer;
    internal event Action AnimationBindingChanged;
    internal int BindingGeneration => binding.Generation;
    internal CharacterAnimationDiagnosticSnapshot GetDiagnosticSnapshot() => new(
        locomotionSM.CurrentState?.GetType().Name, CurrentAnimationMode, sessions,
        presentation.PendingCleanupCount, signals.PendingCount, timeline?.CallbackCount ?? 0,
        timeline?.VfxSessionCount ?? 0, presentation.LocomotionWeight, presentation.UpperWeight, presentation.FullBodyWeight);
    private readonly CharacterAnimationUnboundState unboundState = new();
    private bool suspendingBinding;
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
    public Animator BoundAnimator => animancer != null ? animancer.Animator : null;
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

    private Locomotion_Dead deadState;

    private Locomotion_StageIntro stageIntroState;

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
        Block = 13,
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

    public SkillComboStep CurrentMeleeStep { get; internal set; }
    public int CurrentMeleeStepIndex { get; internal set; }
    public bool IsMeleePlaybackActive => IsBasicMeleeExecution &&
        (_skillChannel.IsActive || (_initialized && locomotionSM.CurrentState == skill));
    internal bool IsBasicMeleeExecution => _skillChannel.Request.ExecutionKind == SkillExecutionKind.BasicMelee;
    internal PlaybackKind ActiveSkillPlaybackKind => IsBasicMeleeExecution ? PlaybackKind.Melee : PlaybackKind.Skill;
    public event Action MeleeHitStart;
    public event Action MeleeHitEnd;
    public event Action MeleeComboEnded;
    public event Action MeleeChainWindowOpened;
    public event Action MeleeChainWindowClosed;
    public event Action MeleeStepCompleted;

    private Action onMeleeHitStartCache;
    private Action onMeleeHitEndCache;
    

    // ----- Runtime inputs -----
    public float MoveSpeed01 { get; set; } // 0..1
    public bool IsSprinting { get; set; }
    public bool IsCrouching { get; set; }
    public bool IsSliding { get; set; }
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
    private Action_Empty empty;
    private Action_ShootPulse shootOnce;
    private Action_ShootHold shootHold;

    // cache delegates ลด alloc
    private Action onUtilityCastMomentCache;
    private readonly PlaybackSessionCoordinator sessions = new();
    private PlaybackChannel _skillChannel => sessions.Skill;
    private AnimationTimelineBinding timeline;
    private AnimationTimelineBinding Timeline => timeline ??= new AnimationTimelineBinding(this, sessions);
    private PlaybackChannel _utilityChannel => sessions.Utility;

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
    private SkillGemDefinition DefaultMeleeCombo => AnimProfile.meleeSkill;
    private SkillGemDefinition LightCombo => AnimProfile.lightMeleeSkill;
    private SkillGemDefinition HeavyCombo => AnimProfile.heavyMeleeSkill;
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
    public bool IsSkillPlaybackActive => !IsBasicMeleeExecution &&
        (_skillChannel.IsActive || (_initialized && locomotionSM.CurrentState == skill));
    private bool IsUtilityPlaybackActive =>
        _utilityChannel.IsActive ||
        (_initialized && locomotionSM.CurrentState == utility);
    /// <summary>True when any skill, utility, or chain playback blocks shooting.</summary>
    public bool IsShootBlockingPlaybackActive =>
        IsSkillPlaybackActive ||
        IsMeleePlaybackActive ||
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
         IsMeleePlaybackActive ||
         locomotionSM.CurrentState == fullBodyReloadState ||
         locomotionSM.CurrentState == knockbackState ||
         locomotionSM.CurrentState == deadState ||
         locomotionSM.CurrentState == statusEffectState ||
         locomotionSM.CurrentState == stageIntroState ||
         locomotionSM.CurrentState == specialReactionState ||
         IsBlockPlaybackActive);
    public PlaybackKind CurrentPlaybackKind => ResolveCurrentPlaybackKind();

    /// <summary>What owns locomotion right now, in the vocabulary <see cref="CharacterAnimationTransitionPolicy"/> speaks.</summary>
    public CharacterAnimationMode CurrentAnimationMode
    {
        get
        {
            if (!_initialized)
                return CharacterAnimationMode.None;

            if (locomotionSM.CurrentState == deadState) return CharacterAnimationMode.Dead;
            if (IsBlockPlaybackActive) return CharacterAnimationMode.Block;

            // Chain/skill/utility are read through their channels, not the FSM state, because a
            // request is armed just before its state is entered and the gates have always treated
            // that window as occupied.
            if (IsChainPlaybackActive) return CharacterAnimationMode.Chain;
            if (IsUtilityPlaybackActive) return CharacterAnimationMode.Utility;
            if (IsSkillPlaybackActive) return CharacterAnimationMode.Skill;
            if (IsMeleePlaybackActive) return CharacterAnimationMode.Melee;

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

    internal bool TryGetActiveSkillPlaybackTiming(
        int requestId,
        out float remainingDuration,
        out float totalDuration)
    {
        remainingDuration = 0f;
        totalDuration = 0f;

        if (!_initialized ||
            requestId <= 0 ||
            requestId != _skillChannel.Request.RequestId ||
            locomotionSM.CurrentState != skill ||
            skill == null)
        {
            return false;
        }

        return skill.TryGetPlaybackTiming(out remainingDuration, out totalDuration);
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
        if (_animProfileOverride == null && ctx == null) ResolveReferences();
        animProfile = binding.ResolveProfile(_animProfileOverride, ctx);
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

    private bool tearingDown;

    private bool TryInitialize()
    {
        if (tearingDown) return false;
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
            ReferenceEquals(animancer, binding.Animancer) &&
            animancer.IsGraphInitialized && ReferenceEquals(animancer.Graph, binding.Graph) &&
            TryPeekAnimProfile(out CharacterAnimProfileSO boundProfile) &&
            boundProfile == _boundAnimProfile)
        {
            return true;
        }

        return TryRebind();
    }

    private bool TryRebind()
    {
        using var commandScope = signals.Begin();
        ResolveReferences();
        if (!animancer || animancer.Animator == null || !TryGetAnimProfile(out var animProfile))
        {
            if (_initialized)
            {
                SuspendBinding();
                timeline?.Clear();
                presentation.DisposePresentationLayers();
                _initialized = false;
                _bindingDirty = true;
                SafeAnimationEvents.Invoke(AnimationBindingChanged);
            }
            return false;
        }

        // Exit old states while their binding and presentation still exist. Explicit invalidation
        // also rebuilds a model that kept the same Animator and profile references.
        if (_initialized) SuspendBinding();

        timeline?.Clear();
        presentation.DisposePresentationLayers();
        binding.Commit(animancer, animProfile);
        sessions.BindingGeneration = binding.Generation;
        Timeline.Bind(binding.Snapshot);
        _boundAnimator = binding.Animator;
        _boundAnimProfile = animProfile;

        presentation.Bind(binding.Snapshot, locomotionLayerIndex, actionLayerIndex);

        locomotion = new LocomotionState_Live(this);
        empty = new Action_Empty(this);
        shootOnce = new Action_ShootPulse(this);
        shootHold = new Action_ShootHold(this);
        reloadState = new Action_Reload(this);
        fullBodyReloadState = new Locomotion_Reload(this);
        dashState = new Locomotion_Dash(this);
        knockbackState = new Locomotion_Knockback(this);
        deadState = new Locomotion_Dead(this);
        crawlState = new LocomotionState_Crawl(this);
        skill = new Locomotion_Skill(this);
        utility = new Locomotion_Utility(this);
        chain = new Locomotion_Chain(this);
        statusEffectState = new Locomotion_StatusEffect(this);
        stageIntroState = new Locomotion_StageIntro(this);
        specialReactionState = new Locomotion_SpecialReaction(this);

        ClearActivePairOffsetState();
        ForceSetLocomotionState(locomotion);
        actionSM.ForceSetState(empty);

        _initialized = true;
        _bindingDirty = false;
        SafeAnimationEvents.Invoke(AnimationBindingChanged);

        if (_pendingDownedSet)
        {
            _pendingDownedSet = false;
            SetDowned(_pendingDownedValue);
        }

        ApplyPending();
        return true;
    }

    private void SuspendBinding()
    {
        suspendingBinding = true;
        try
        {
            StopCommanBlock();
            AllowChainStateExit();
            ForceSetLocomotionState(unboundState);
            actionSM.ForceSetState(empty);
            InterruptActiveSkillRequest();
            InterruptActiveUtilityRequest();
            InterruptActiveChainRequest();
            CloseSpecialReactionSession(false);
            sessions.ReleaseCommandControl();
            ClearRootMotionPolicy();
        }
        finally { suspendingBinding = false; }
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
                ctx?.MeleeController?.PressMelee(MeleeType.Heavy);
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
        tearingDown = false;
        InvalidateAnimationBinding();
    }

    private void ResolveReferences()
    {
        binding.Resolve(this, ref ctx, ref animancer);
        if (ctx != null && ctx.AnimBrain != this) ctx.AnimBrain = this;
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
        // Slide can take over before the dash clip's authored duration has elapsed.
        if (IsSliding) HandleDashEnd();
        locomotionSM.CurrentState?.Update();
        actionSM.CurrentState?.Update();
        presentation.UpdatePresentationTransitions();
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
        if (!CharacterAnimationTransitionPolicy.AllowsAction(CurrentAnimationMode, PairOffsetUpperAction.ShootPulse))
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
        presentation.GetBaseContributions(_activePairBasePoseWeights);
        _activePairBasePose = PairOffsetBasePose.None;
        float dominantWeight = 0f;
        foreach (var pose in _activePairBasePoseWeights)
        {
            if (pose.Weight <= dominantWeight) continue;
            dominantWeight = pose.Weight;
            _activePairBasePose = pose.Pose;
        }
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
        presentation.GetBaseContributions(_activePairBasePoseWeights);

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
        if (!CharacterAnimationTransitionPolicy.AllowsAction(CurrentAnimationMode, PairOffsetUpperAction.Reload))
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

    internal bool TryStartMeleePlayback(SkillGemDefinition combo, SkillComboStep firstStep, int stepIndex)
    {
        CurrentMeleeStep = firstStep;
        CurrentMeleeStepIndex = stepIndex;
        return ctx != null && ctx.SkillManager != null &&
            ctx.SkillManager.TryStartMeleeStep(firstStep, ctx.MeleeController != null ? ctx.MeleeController.CurrentMeleeType : MeleeType.Light).Started;
    }

    internal void AdvanceMeleeStep(SkillComboStep step, int stepIndex)
    {
        ctx?.MeleeController?.PlayCurrentStep();
    }

    internal void CompleteMeleePlayback() => ctx?.MeleeController?.InterruptMelee();

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
        using var commandScope = signals.Begin();
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
        using var commandScope = signals.Begin();
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
        using var commandScope = signals.Begin();
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
        using var commandScope = signals.Begin();
        if (!TryInitialize() || !HasValidSkillClip(skillDef))
            return;

        if (!CanStartAnimation(CharacterAnimationMode.Skill, CharacterAnimationTransitionReason.NormalCommand))
            return;

        StopReloadAction();
        if (locomotionSM.CurrentState == skill) ForceSetLocomotionState(locomotion);
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
        bool usePlanarRootMotion,
        SkillExecutionKind executionKind = SkillExecutionKind.StandardSkill,
        Vector2 meleeChainWindow = default)
    {
        using var commandScope = signals.Begin();
        if (requestId <= 0)
            return false;

        if (!TryInitialize() || !HasValidSkillClip(skillDef))
            return false;

        if (!CanStartAnimation(executionKind == SkillExecutionKind.BasicMelee ? CharacterAnimationMode.Melee : CharacterAnimationMode.Skill, CharacterAnimationTransitionReason.NormalCommand))
            return false;

        StopReloadAction();
        if (locomotionSM.CurrentState == skill) ForceSetLocomotionState(locomotion);
        ArmSkillRequest(
            requestId,
            skillDef,
            castPointNormalized,
            timelineEventNames,
            usePlanarRootMotion);

        _skillChannel.Request.ExecutionKind = executionKind;
        _skillChannel.Request.MeleeChainWindow = meleeChainWindow;
        _skillChannel.Kind = ActiveSkillPlaybackKind;
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
        using var commandScope = signals.Begin();
        if (requestId <= 0)
            return false;

        if (!TryInitialize() || !HasValidUtilityWarpOutClip())
            return false;

        if (!CanStartAnimation(CharacterAnimationMode.Utility, CharacterAnimationTransitionReason.NormalCommand))
            return false;

        StopReloadAction();
        if (locomotionSM.CurrentState == utility) ForceSetLocomotionState(locomotion);
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
        using var commandScope = signals.Begin();
        if (ExternalCommandBlockedByChain())
            return;

        if (requestId <= 0 || requestId != _skillChannel.Request.RequestId)
            return;

        InterruptActiveSkillRequest();

        if (!TryInitialize())
            return;

        if (locomotionSM.CurrentState == skill)
            TrySetLocomotionState(IsDowned ? crawlState : locomotion);
    }

    public void CancelUtilityCastRequest(int requestId)
    {
        using var commandScope = signals.Begin();
        if (ExternalCommandBlockedByChain())
            return;

        if (requestId <= 0 || requestId != _utilityChannel.RequestId)
            return;

        InterruptActiveUtilityRequest();

        if (!TryInitialize())
            return;

        if (locomotionSM.CurrentState == utility)
            TrySetLocomotionState(IsDowned ? crawlState : locomotion);
    }

    public void CancelMeleeNow()
    {
        if (IsMeleePlaybackActive)
            CancelSkillCastRequest(_skillChannel.Request.RequestId);
    }

    public void InterruptActivePlaybackForExternalControlLoss()
    {
        using var commandScope = signals.Begin();
        AbortActiveChainPlaybackForExternalState();
        InterruptActiveSkillRequest();
        InterruptActiveUtilityRequest();

        if (!TryInitialize())
            return;

        StopReloadAction();

        if (locomotionSM.CurrentState == skill ||
            locomotionSM.CurrentState == utility ||
            IsMeleePlaybackActive ||
            locomotionSM.CurrentState == dashState ||
            locomotionSM.CurrentState == blockState)
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
        if (tearingDown || suspendingBinding) { IsHoldingFire = false; return; }
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

        if (actionSM.CurrentState == empty && CharacterAnimationTransitionPolicy.AllowsAction(CurrentAnimationMode, PairOffsetUpperAction.ShootHold))
            actionSM.TrySetState(shootHold);
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
        if (IsBlockPlaybackActive) return PlaybackKind.Block;
        if (IsChainPlaybackActive)
            return ResolveActiveChainPlaybackKind();

        if (IsUtilityPlaybackActive)
            return PlaybackKind.UtilityWarpOut;

        if (IsSkillPlaybackActive)
            return PlaybackKind.Skill;

        if (_initialized && IsMeleePlaybackActive)
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

        EmitPlaybackSignal(ActiveSkillPlaybackKind, PlaybackPhase.CastMoment, _skillChannel.Request.RequestId);
    }

    internal void ReleaseBasicMeleeCast(int requestId)
    {
        if (IsBasicMeleeExecution && _skillChannel.Request.RequestId == requestId)
            NotifySkillCastMoment();
    }

    internal void RaiseMeleeChainWindow(int requestId, bool open)
    {
        if (!IsMeleePlaybackActive || _skillChannel.Request.RequestId != requestId) return;
        signals.Enqueue(() => SafeAnimationEvents.Invoke(open ? MeleeChainWindowOpened : MeleeChainWindowClosed));
    }

    internal void ReportMeleeComboEnded() => signals.Enqueue(() => SafeAnimationEvents.Invoke(MeleeComboEnded));
    internal void ReportMeleeStepCompleted() => signals.Enqueue(() => SafeAnimationEvents.Invoke(MeleeStepCompleted));

    internal void NotifyUtilityCastMoment()
    {
        if (!_utilityChannel.Request.TryReleaseCast())
            return;

        EmitPlaybackSignal(PlaybackKind.UtilityWarpOut, PlaybackPhase.CastMoment, _utilityChannel.RequestId);
    }

    internal void NotifySkillStateExited(bool completedNormally)
    {
        sessions.Close(_skillChannel, ActiveSkillPlaybackKind, completedNormally, true, false,
            _skillChannel.RequestId == 0, ClearActiveSkillRequest, EmitPlaybackSignal);
    }

    internal void NotifyUtilityStateExited(bool completedNormally)
    {
        sessions.Close(_utilityChannel, PlaybackKind.UtilityWarpOut, completedNormally, true, false,
            _utilityChannel.RequestId == 0, ClearActiveUtilityRequest, EmitPlaybackSignal);
    }

    internal void BindActiveSkillTimelineEvents(AnimancerEvent.Sequence runtimeEvents)
    {
        int requestId = _skillChannel.Request.RequestId;
        ClipTransition clip = ResolveSkillClip(_skillChannel.Request.Definition);
        BindTimelineEventCallbacks(
            runtimeEvents,
            _skillChannel.Request.TimelineEventNames,
            clip,
            Timeline.Guard<CombatTimelineEventName>(_skillChannel, RaiseSkillTimelineEvent),
            warnMissing: true);

        BindVfxTimelineEventCallbacks(
            runtimeEvents,
            _skillChannel.Request.Definition,
            clip,
            Timeline.Guard<int>(_skillChannel, RaiseSkillVfxTimelineEvent));

        BindActiveSkillPreCastTimelineEvents(runtimeEvents);
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
            Timeline.Guard<CombatTimelineEventName>(_chainChannel, RaiseChainSkillTimelineEvent),
            warnMissing: true);

        BindVfxTimelineEventCallbacks(
            runtimeEvents,
            _chainChannel.Request.Definition,
            clip,
            Timeline.Guard<int>(_chainChannel, RaiseChainSkillVfxTimelineEvent));
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
            Timeline.Guard<int>(_chainChannel, RaiseChainCutsceneVfxTimelineEvent),
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
        Timeline.BindTimelineEventCallbacks(runtimeEvents, eventNames, clip, raiseTimelineEvent, warnMissing);
    }

    private void BindVfxTimelineEventCallbacks(
        AnimancerEvent.Sequence runtimeEvents,
        SkillGemDefinition skillDef,
        ClipTransition clip,
        Action<int> raiseVfxCue)
    {
        Timeline.BindVfxTimelineEventCallbacks(runtimeEvents, skillDef, clip, raiseVfxCue);
    }

    private void BindTimelineEventCallback(
        AnimancerEvent.Sequence runtimeEvents,
        CombatTimelineEventName eventName,
        string clipName,
        Action<CombatTimelineEventName> raiseTimelineEvent,
        bool warnMissing)
    {
        Timeline.BindTimelineEventCallback(runtimeEvents, eventName, clipName, raiseTimelineEvent, warnMissing);
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
            Timeline.Guard<CombatTimelineEventName>(_skillChannel, RaiseSkillTimelineEvent),
            warnMissing: false);

        BindTimelineEventCallback(
            runtimeEvents,
            skillDef.PreCastCloseEventName,
            clipName,
            Timeline.Guard<CombatTimelineEventName>(_skillChannel, RaiseSkillTimelineEvent),
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
        runtimeEvents.Add(clampedTime, Timeline.Guard(_skillChannel, () => RaiseSkillTimelineEvent(capturedEventName)));
    }

    private void RaiseSkillTimelineEvent(CombatTimelineEventName eventName)
    {
        if (!_skillChannel.Request.ReleaseRequested ||
            _skillChannel.Request.RequestId <= 0 ||
            !CombatTimelineEventNames.IsValid(eventName))
        {
            return;
        }

        int requestId = _skillChannel.Request.RequestId;
        bool melee = IsBasicMeleeExecution;
        signals.Enqueue(() =>
        {
            SafeAnimationEvents.Invoke(SkillTimelineEventRaised, requestId, eventName);
            if (melee)
            {
                if (eventName == CombatTimelineEventName.HitStart) SafeAnimationEvents.Invoke(MeleeHitStart);
                else if (eventName == CombatTimelineEventName.HitEnd) SafeAnimationEvents.Invoke(MeleeHitEnd);
            }
        });
    }

    private void RaiseChainSkillTimelineEvent(CombatTimelineEventName eventName)
    {
        if (_activeChainKind != ChainPlaybackKind.Skill || !_chainChannel.Request.ReleaseRequested ||
            _chainChannel.RequestId <= 0 || !CombatTimelineEventNames.IsValid(eventName)) return;
        int requestId = _chainChannel.RequestId;
        signals.Enqueue(() => SafeAnimationEvents.Invoke(SkillTimelineEventRaised, requestId, eventName));
    }

    private void RaiseSkillVfxTimelineEvent(int cueIndex)
    {
        if (!_skillChannel.Request.ReleaseRequested || _skillChannel.RequestId <= 0 || cueIndex < 0) return;
        DispatchSkillVfx(_skillChannel, cueIndex);
    }

    private void RaiseChainSkillVfxTimelineEvent(int cueIndex)
    {
        if (_activeChainKind != ChainPlaybackKind.Skill || !_chainChannel.Request.ReleaseRequested ||
            _chainChannel.RequestId <= 0 || cueIndex < 0) return;
        DispatchSkillVfx(_chainChannel, cueIndex);
    }

    private void DispatchSkillVfx(PlaybackChannel channel, int cueIndex)
    {
        int requestId = channel.RequestId;
        var cue = new SkillAnimationVfxCueSignal(requestId, channel.Request.Definition,
            SkillAnimationVfxPhase.MainSkill, cueIndex);
        Timeline.HandleSkillVfxCue(channel, cueIndex);
        signals.Enqueue(() =>
        {
            SafeAnimationEvents.Invoke(SkillAnimationVfxCueRaised, cue);
            SafeAnimationEvents.Invoke(SkillTimelineEventRaised, requestId, CombatTimelineEventName.Vfx);
        });
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

        var cue = new SkillAnimationVfxCueSignal(_chainChannel.RequestId, null, SkillAnimationVfxPhase.Cutscene, cueIndex);
        signals.Enqueue(() => SafeAnimationEvents.Invoke(SkillAnimationVfxCueRaised, cue));
    }

    private void RaiseCutsceneVfxCueInternal(int cueIndex)
    {
        if (!_skillChannel.Request.ReleaseRequested || _skillChannel.Request.RequestId <= 0 || cueIndex < 0)
            return;

        var cue = new SkillAnimationVfxCueSignal(_skillChannel.RequestId, _skillChannel.Request.Definition, SkillAnimationVfxPhase.Cutscene, cueIndex);
        signals.Enqueue(() => SafeAnimationEvents.Invoke(SkillAnimationVfxCueRaised, cue));
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
        Timeline.EnsureSkillVfxPresenter(skillDef);
    }

    internal void ApplyActiveSkillRootMotionPolicy() => SetRootMotionShape(
        _skillChannel.Request.UsesPlanarRootMotion,
        _skillChannel.Request.IgnoresCharacterCollisionDuringRootMotion);

    private void ArmUtilityRequest(int requestId, float castPointNormalized)
    {
        _utilityChannel.Request.Begin(requestId, castPointNormalized);
        _utilityChannel.Kind = PlaybackKind.UtilityWarpOut;
    }

    private void ClearActiveSkillRequest()
    {
        timeline?.EndSkillVfxSession(_skillChannel);
        _skillChannel.Clear();
        _skillChannel.Kind = PlaybackKind.Skill;
    }

    internal void EndManagedSkillVfxSessions(int requestId = 0) => timeline?.EndSkillVfxSessions(requestId);

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
        sessions.Close(_skillChannel, ActiveSkillPlaybackKind, false, false, false, false,
            ClearActiveSkillRequest, EmitPlaybackSignal);
    }

    private void InterruptActiveUtilityRequest()
    {
        sessions.Close(_utilityChannel, PlaybackKind.UtilityWarpOut, false, false, false, false,
            ClearActiveUtilityRequest, EmitPlaybackSignal);
    }

    private void OnDisable()
    {
        tearingDown = true;
        SafeAnimationEvents.Invoke(AnimationBindingChanged);
        using var commandScope = signals.Begin();
        StopCommanBlock();
        dashState?.EndVfxSession();
        reloadState?.EndVfxSession();
        fullBodyReloadState?.EndVfxSession();
        RestoreWorldAnimationSpeed();
        ClearRootMotionPolicy();
        InterruptActiveSkillRequest();
        InterruptActiveUtilityRequest();
        InterruptActiveChainRequest();
        ReleaseExclusiveLocomotionForDisable();
        if (_initialized && animancer != null && animancer.Animator != null)
            actionSM.ForceSetState(empty);
        timeline?.Clear();
        presentation.StopPresentationTransitions();
    }

    /// <summary>
    /// Disabling clears the playback requests but leaves the locomotion state machine parked in
    /// whatever exclusive state was running, so re-enabling would resume a state that has no
    /// request behind it (and keep tripping the chain watchdog). Hand locomotion back first.
    /// </summary>
    private void ReleaseExclusiveLocomotionForDisable()
    {
        if (!_initialized) return;
        if (!animancer || animancer.Animator == null)
        {
            SuspendBinding();
            return;
        }

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
        tearingDown = true;
        using var commandScope = signals.Begin();
        if (_initialized) SuspendBinding();
        commanBlockState?.Dispose();
        dashState?.EndVfxSession();
        reloadState?.EndVfxSession();
        fullBodyReloadState?.EndVfxSession();
        RestoreWorldAnimationSpeed();
        ClearRootMotionPolicy();
        InterruptActiveSkillRequest();
        InterruptActiveUtilityRequest();
        InterruptActiveChainRequest();
        CloseSpecialReactionSession(false);
        sessions.ReleaseCommandControl();
        timeline?.Clear();
        presentation.DisposePresentationLayers();
    }
}
