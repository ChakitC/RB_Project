using System;
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

    [Header("Layer Indices")]
    [SerializeField] private int locomotionLayerIndex = 0;
    [SerializeField] private int actionLayerIndex = 1;
    public Vector2 MoveDirLocal { get; set; }
    private Vector2 _dashDirLocal;
    private float _dashDuration = 0.12f;
    private Locomotion_Dash dashState;
    private Action onDashEndCache;

    private Locomotion_Dead deadState;
    private Action onDeadEndCache;

    private float _reloadDuration = 0f;

    public enum MeleeType { Light, Heavy }
    private enum StatusLocomotionKind { None, Root, MiniStune, Stune, Freez }
    
    public bool IsDowned { get; private set; }
    private LocomotionState_Crawl crawlState;
    private Locomotion_StatusEffect statusEffectState;
    private StatusLocomotionKind _currentStatusLocomotionKind;

    // pending (กรณีเรียก SetDowned ก่อน init)
    private bool _pendingDownedSet;
    private bool _pendingDownedValue;

    public bool RootMotionActive { get; private set; }
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
    public bool IsHoldingFire { get; private set; }

    // ----- State machines (orthogonal) -----
    private readonly StateMachine<LocomotionState> locomotionSM = new();
    private readonly StateMachine<ActionState> actionSM = new();

    private Locomotion_Skill skill;
    private Action_Reload reloadState;
    private LocomotionState_Live locomotion;
    private Locomotion_MeleeCombo meleeCombo;
    private Action_Empty empty;
    private Action_ShootPulse shootOnce;
    private Action_ShootHold shootHold;

    // cache delegates ลด alloc
    private Action onShootEndCache;
    private SkillGemDefinition _activeSkillDefinition;
    private int _activeSkillRequestId;
    private float _activeSkillCastPointNormalized = 0.35f;
    private bool _activeSkillReleaseRequested;
    private bool _activeSkillReleased;

    private AnimancerLayer LocoLayer => animancer.Layers[locomotionLayerIndex];
    private AnimancerLayer ActLayer => animancer.Layers[actionLayerIndex];

    private CharacterAnimProfileSO AnimProfile => _boundAnimProfile;

    private AvatarMask UpperBodyMask => AnimProfile.upperBodyMask;
    private float ActionFadeIn => AnimProfile.actionFadeIn;
    private float ActionFadeOut => AnimProfile.actionFadeOut;
    private MixerTransition2D LocomotionMixer => AnimProfile.locomotionMixer;
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
    private ClipTransition LegacySkillClip => AnimProfile.skillClip;
    private ClipTransition SkillClip => ResolveSkillClip(_activeSkillDefinition);
    private ClipTransition MiniStuneClip => AnimProfile.miniStune;
    private ClipTransition StuneClip => AnimProfile.stune;
    private ClipTransition RootClip => AnimProfile.root;
    private ClipTransition FreezClip => AnimProfile.freez;
    private bool HasActiveSkillClip => HasValidSkillClip(_activeSkillDefinition);
    internal float ActiveSkillCastPointNormalized => _activeSkillCastPointNormalized;
    internal bool HasPendingSkillReleaseRequest => _activeSkillReleaseRequested;
    public bool IsSkillActive =>
        _activeSkillReleaseRequested ||
        _activeSkillRequestId != 0 ||
        (_initialized && locomotionSM.CurrentState == skill);
   

    public event Action<int> SkillCastMomentReached;
    public event Action<int> SkillCastInterrupted;
    public event Action SkillCompleted;

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
            (_activeSkillRequestId != 0 || _activeSkillReleaseRequested))
        {
            InterruptActiveSkillRequest();
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
        deadState = new Locomotion_Dead(this);
        meleeCombo = new Locomotion_MeleeCombo(this);
        crawlState = new LocomotionState_Crawl(this);
        skill = new Locomotion_Skill(this);
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
                actionSM.TrySetState(shootHold);
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

    public void NotifyShotFired()
    {
        if (!TryInitialize())
        {
            _pendingPulse = true;
            return;
        }

        actionSM.TryResetState(shootOnce);
    }

    public void FireDown()
    {
        IsHoldingFire = true;

        if (!TryInitialize())
        {
            _pendingAction = PendingAction.Hold;
            return;
        }
    }

    public void FireUp()
    {
        IsHoldingFire = false;

        if (!TryInitialize())
            return;

        _pendingAction = PendingAction.Empty;
        actionSM.TrySetState(empty);
    }

    public void PlayReload(float reloadDuration)
    {
        _reloadDuration = Mathf.Max(0.01f, reloadDuration);

        _pendingAction = PendingAction.Reload;

        if (!TryInitialize())
            return;

        actionSM.TrySetState(reloadState);
    }

    public void PlayDash(float dashDuration, Vector2 dashDirLocal)
    {
        _dashDuration = Mathf.Max(0.01f, dashDuration);
        _dashDirLocal = dashDirLocal;

        if (!TryInitialize())
            return;
        
        StopReloadAction();
        locomotionSM.TrySetState(dashState);
    }

    public void EndDashNow()
    {
        if (!TryInitialize()) return;
        locomotionSM.TrySetState(locomotion);
    }

    public void PressMelee(MeleeType type)
    {
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
        
        if (!TryInitialize()) return;

        StopReloadAction();

        locomotionSM.TrySetState(deadState);
    }

    public void SetDowned(bool downed)
    {
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
        if (!TryInitialize() || !HasValidSkillClip(skillDef))
            return;

        ClearActiveSkillRequest();
        _activeSkillDefinition = skillDef;
        locomotionSM.TryResetState(skill);
    }

    public bool TryPlaySkill(int requestId, float castPointNormalized)
    {
        return TryPlaySkill(requestId, null, castPointNormalized);
    }

    public bool TryPlaySkill(int requestId, SkillGemDefinition skillDef, float castPointNormalized)
    {
        if (requestId <= 0)
            return false;

        if (!TryInitialize() || !HasValidSkillClip(skillDef))
            return false;

        ArmSkillRequest(requestId, skillDef, castPointNormalized);

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

    public void CancelSkillCastRequest(int requestId)
    {
        if (requestId <= 0 || requestId != _activeSkillRequestId)
            return;

        ClearActiveSkillRequest();

        if (!TryInitialize())
            return;

        if (locomotionSM.CurrentState == skill)
            locomotionSM.TrySetState(IsDowned ? crawlState : locomotion);
    }

    public void CancelMeleeNow()
    {
        if (!TryInitialize())
            return;
        if (locomotionSM.CurrentState != meleeCombo)
            return;

        locomotionSM.TrySetState(IsDowned ? crawlState : locomotion);
    }
    
    // ===================== Helpers =====================
    
    public void StopReloadAction()
    {
        if (!TryInitialize()) return;

        if (actionSM.CurrentState == reloadState)
        {
            reloadState.CancelNow();
            actionSM.TrySetState(empty);
        }
    }
    private void HandleShootPulseEnd()
    {
        if (IsHoldingFire && ShootHoldLoopClip != null)
            actionSM.TrySetState(shootHold);
        else
            actionSM.TrySetState(empty);
    }

    private void RefreshStatusLocomotion()
    {
        if (!_initialized)
            return;

        if (locomotionSM.CurrentState == deadState)
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

    private bool HasValidSkillClip(SkillGemDefinition skillDef)
    {
        var clip = ResolveSkillClip(skillDef);
        return clip != null && clip.IsValid;
    }

    internal void NotifySkillCastMoment()
    {
        if (!_activeSkillReleaseRequested || _activeSkillReleased)
            return;

        _activeSkillReleased = true;
        SkillCastMomentReached?.Invoke(_activeSkillRequestId);
    }

    internal void NotifySkillStateExited(bool completedNormally)
    {
        int requestId = _activeSkillRequestId;
        SkillGemDefinition activeSkillDefinition = _activeSkillDefinition;
        bool interrupted = _activeSkillReleaseRequested && !_activeSkillReleased;
        bool shouldDeactivateOwner =
            deactivateOwnerOnSkillExit &&
            activeSkillDefinition == null &&
            gameObject.activeSelf;

        ClearActiveSkillRequest();

        if (completedNormally)
        {
            SkillCompleted?.Invoke();

            if (shouldDeactivateOwner)
                gameObject.SetActive(false);
        }

        if (interrupted && requestId > 0)
            SkillCastInterrupted?.Invoke(requestId);
    }

    private void ArmSkillRequest(int requestId, SkillGemDefinition skillDef, float castPointNormalized)
    {
        _activeSkillDefinition = skillDef;
        _activeSkillRequestId = requestId;
        _activeSkillCastPointNormalized = Mathf.Clamp(castPointNormalized, 0f, 0.999f);
        _activeSkillReleaseRequested = true;
        _activeSkillReleased = false;
    }

    private void ClearActiveSkillRequest()
    {
        _activeSkillDefinition = null;
        _activeSkillRequestId = 0;
        _activeSkillCastPointNormalized = 0.35f;
        _activeSkillReleaseRequested = false;
        _activeSkillReleased = false;
    }

    private void InterruptActiveSkillRequest()
    {
        int requestId = _activeSkillRequestId;
        bool shouldNotify = _activeSkillReleaseRequested && !_activeSkillReleased && requestId > 0;

        ClearActiveSkillRequest();

        if (shouldNotify)
            SkillCastInterrupted?.Invoke(requestId);
    }

    private void OnDisable()
    {
        InterruptActiveSkillRequest();
    }

    private void OnDestroy()
    {
        InterruptActiveSkillRequest();
    }
}
