using System;
using System.Collections.Generic;
using Animancer;
using Animancer.FSM;
using UnityEngine;

[DefaultExecutionOrder(-120)]
public sealed partial class CharacterAnimBrain : MonoBehaviour
{
    private bool _initialized;
    private Animator _boundAnimator;
    private CharacterAnimProfileSO _boundAnimProfile;

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

    public bool IsDowned { get; private set; }
    private LocomotionState_Crawl crawlState;
    private Locomotion_StatusEffect statusEffectState;
    private StatusLocomotionKind _currentStatusLocomotionKind;

    // pending (กรณีเรียก SetDowned ก่อน init)
    private bool _pendingDownedSet;
    private bool _pendingDownedValue;

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
    private LocomotionState_Live locomotion;
    private Locomotion_MeleeCombo meleeCombo;
    private Action_Empty empty;
    private Action_ShootPulse shootOnce;
    private Action_ShootHold shootHold;

    // cache delegates ลด alloc
    private Action onShootEndCache;
    private Action onUtilityCastMomentCache;
    private SkillGemDefinition _activeSkillDefinition;
    private int _activeSkillRequestId;
    private float _activeSkillCastPointNormalized = 0.35f;
    private bool _activeSkillReleaseRequested;
    private bool _activeSkillReleased;
    private readonly List<StringReference> _activeSkillTimelineEventNames = new List<StringReference>();
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
    private MixerTransition2D LocomotionMixer => AnimProfile != null ? AnimProfile.ResolveLocomotionMixer() : null;
    private float LocomotionParamLerp => AnimProfile.locomotionParamLerp;
    private bool SnapTo8Directions => AnimProfile.snapTo8Directions;
    private ClipTransition DashForward => AnimProfile.dashF;
    private ClipTransition DashBackward => AnimProfile.dashB;
    private ClipTransition DashLeft => AnimProfile.dashL;
    private ClipTransition DashRight => AnimProfile.dashR;
    private ClipTransition DeadClip => AnimProfile.dead;
    private ClipTransition ShootPulseClip => AnimProfile.shootPulse;
    private ClipTransition ShootHoldLoopClip => AnimProfile.shootHoldLoop;
    private float HoldPulseMinInterval => AnimProfile.holdPulseMinInterval;
    private ClipTransition ReloadClip => AnimProfile.reload;
    private MeleeComboSO DefaultMeleeCombo => AnimProfile.meleeCombo;
    private MeleeComboSO LightCombo => AnimProfile.lightCombo;
    private MeleeComboSO HeavyCombo => AnimProfile.heavyCombo;
    private MixerTransition2D CrawlMixer => AnimProfile.crawlMixer;
    private float CrawlParamLerp => AnimProfile.crawlParamLerp;
    private float CrawlSpeedMultiplier01 => AnimProfile.crawlSpeedMultiplier01;
    private ClipTransition UtilityWarpOutClip => AnimProfile.utilityWarpOutClip;
    private float UtilityWarpOutCastPointNormalized => AnimProfile.utilityWarpOutCastPointNormalized;
    private ClipTransition UtilityWarpInClip => AnimProfile.utilityWarpInClip;
    private float UtilityWarpInCastPointNormalized => AnimProfile.utilityWarpInCastPointNormalized;
    private ClipTransition LegacySkillClip => AnimProfile.skillClip;
    private ClipTransition SkillClip => ResolveSkillClip(_activeSkillDefinition);
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
         locomotionSM.CurrentState == knockbackState ||
         locomotionSM.CurrentState == deadState ||
         locomotionSM.CurrentState == statusEffectState);
    public PlaybackKind CurrentPlaybackKind => ResolveCurrentPlaybackKind();

    public event Action<PlaybackSignal> PlaybackEvent;
    public event Action<int> SkillCastMomentReached;
    public event Action<int> SkillCastInterrupted;
    public event Action<int, StringReference> SkillTimelineEventRaised;
    public event Action SkillCompleted;

    internal bool TryGetActiveSkillNormalizedTime(int requestId, out float normalizedTime)
    {
        normalizedTime = 0f;

        if (requestId <= 0 ||
            requestId != _activeSkillRequestId ||
            !_activeSkillReleaseRequested ||
            !_initialized ||
            locomotionSM.CurrentState != skill ||
            skill == null)
        {
            return false;
        }

        return skill.TryGetNormalizedTime(out normalizedTime);
    }

    private bool TryGetAnimProfile(out CharacterAnimProfileSO animProfile)
    {
        animProfile = null;

        if (!ctx) ctx = GetComponent<CharacteContext>();
        if (ctx == null || ctx.baseStats == null)
            return false;

        animProfile = ctx.baseStats.animProfile;
        return animProfile != null;
    }

    private string GetInitializationError()
    {
        if (!animancer) return "AnimancerComponent missing.";
        if (animancer.Animator == null) return "Animancer.Animator missing.";
        if (!ctx) ctx = GetComponent<CharacteContext>();
        if (ctx == null) return "CharacteContext missing.";
        if (ctx.baseStats == null) return "CharacteContext.baseStats missing.";
        if (ctx.baseStats.animProfile == null)
            return $"CharacterStats '{ctx.baseStats.name}' is missing animProfile.";

        return "Unknown initialization error.";
    }

    private bool TryInitialize()
    {
        if (!animancer) animancer = GetComponent<AnimancerComponent>();
        if (!animancer || animancer.Animator == null)
            return false;

        if (!statusEffectController)
            statusEffectController = GetComponent<StatusEffectController>();

        if (!TryGetAnimProfile(out var animProfile))
            return false;

        if (_initialized &&
            (animancer.Animator != _boundAnimator || animProfile != _boundAnimProfile) &&
            ((_activeSkillRequestId != 0 || _activeSkillReleaseRequested) ||
             (_activeUtilityRequestId != 0 || _activeUtilityReleaseRequested)))
        {
            InterruptActiveSkillRequest();
            InterruptActiveUtilityRequest();
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
                actionSM.TrySetState(reloadState);
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
        if (!animancer) animancer = GetComponent<AnimancerComponent>();
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

        RefreshStatusLocomotion();
        locomotionSM.CurrentState?.Update();
        actionSM.CurrentState?.Update();
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

    public void PlayReload(float reloadDuration)
    {
        if (IsChainPlaybackActive)
            return;

        _reloadDuration = Mathf.Max(0.01f, reloadDuration);

        _pendingAction = PendingAction.Reload;

        if (!TryInitialize())
            return;

        actionSM.TrySetState(reloadState);
    }

    public void PlayDash(float dashDuration, Vector2 dashDirLocal)
    {
        if (IsChainPlaybackActive)
            return;

        _dashDuration = Mathf.Max(0.01f, dashDuration);
        _dashDirLocal = dashDirLocal;

        if (!TryInitialize())
            return;
        
        StopReloadAction();
        locomotionSM.TrySetState(dashState);
    }

    public void EndDashNow()
    {
        if (IsChainPlaybackActive)
            return;

        if (!TryInitialize()) return;
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
        
        locomotionSM.ForceSetState(IsDowned ? crawlState : locomotion);
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
        IReadOnlyList<StringReference> timelineEventNames)
    {
        if (IsChainPlaybackActive)
            return false;

        if (requestId <= 0)
            return false;

        if (!TryInitialize() || !HasValidSkillClip(skillDef))
            return false;

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
    }

    private void ClearActionLayerForExclusiveLocomotion()
    {
        _pendingAction = PendingAction.Empty;
        _pendingPulse = false;

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
            statusEffectController = GetComponent<StatusEffectController>();

        var activeEffects = statusEffectController?.ActiveEffects;
        if (activeEffects == null || activeEffects.Count == 0)
            return StatusLocomotionKind.None;

        StatusLocomotionKind best = StatusLocomotionKind.None;
        int bestPriority = 0;

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
        if (runtimeEvents == null || _activeSkillTimelineEventNames.Count == 0)
            return;

        ClipTransition clip = ResolveSkillClip(_activeSkillDefinition);
        string clipName = clip != null && clip.Clip != null ? clip.Clip.name : "<none>";

        for (int i = 0; i < _activeSkillTimelineEventNames.Count; i++)
        {
            StringReference eventName = _activeSkillTimelineEventNames[i];
            if (eventName == null || string.IsNullOrWhiteSpace(eventName.String))
                continue;

            StringReference capturedEventName = eventName;
            int count = runtimeEvents.SetCallbacks(
                capturedEventName,
                () => RaiseSkillTimelineEvent(capturedEventName));

            if (count == 0)
            {
                Debug.LogWarning(
                    $"[CharacterAnimBrain] Skill clip '{clipName}' is missing timeline event '{capturedEventName}'.",
                    this);
            }
        }
    }

    private void RaiseSkillTimelineEvent(StringReference eventName)
    {
        if (!_activeSkillReleaseRequested ||
            _activeSkillRequestId <= 0 ||
            eventName == null ||
            string.IsNullOrWhiteSpace(eventName.String))
        {
            return;
        }

        SkillTimelineEventRaised?.Invoke(_activeSkillRequestId, eventName);
    }

    private void ArmSkillRequest(
        int requestId,
        SkillGemDefinition skillDef,
        float castPointNormalized,
        IReadOnlyList<StringReference> timelineEventNames)
    {
        _activeSkillDefinition = skillDef;
        _activeSkillRequestId = requestId;
        _activeSkillCastPointNormalized = Mathf.Clamp(castPointNormalized, 0f, 0.999f);
        _activeSkillReleaseRequested = true;
        _activeSkillReleased = false;
        SetActiveSkillTimelineEventNames(timelineEventNames);
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

    private void SetActiveSkillTimelineEventNames(IReadOnlyList<StringReference> timelineEventNames)
    {
        _activeSkillTimelineEventNames.Clear();

        if (timelineEventNames == null || timelineEventNames.Count == 0)
            return;

        for (int i = 0; i < timelineEventNames.Count; i++)
        {
            StringReference eventName = timelineEventNames[i];
            if (eventName == null || string.IsNullOrWhiteSpace(eventName.String))
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
        InterruptActiveSkillRequest();
        InterruptActiveUtilityRequest();
        InterruptActiveChainRequest();
    }

    private void OnDestroy()
    {
        InterruptActiveSkillRequest();
        InterruptActiveUtilityRequest();
        InterruptActiveChainRequest();
    }
}
