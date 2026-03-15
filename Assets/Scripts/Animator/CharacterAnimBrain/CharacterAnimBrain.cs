using System;
using Animancer;
using Animancer.FSM;
using UnityEngine;

[DefaultExecutionOrder(-120)]
public sealed partial class CharacterAnimBrain : MonoBehaviour
{
    private bool _initialized;
    private Animator _boundAnimator;

    private enum PendingAction { None, Empty, Hold, Reload, Melee }
    private PendingAction _pendingAction;
    private bool _pendingPulse;

    [Header("Core")]
    [SerializeField] private AnimancerComponent animancer;
    [SerializeField] private CharacteContext ctx;

    [Header("Layers")]
    [SerializeField] private int locomotionLayerIndex = 0;
    [SerializeField] private int actionLayerIndex = 1;
    [SerializeField] private AvatarMask upperBodyMask;
    [SerializeField] private float actionFadeIn = 0.06f;
    [SerializeField] private float actionFadeOut = 0.08f;

    [Header("Locomotion (Layer 0)")]
    [SerializeField] private MixerTransition2D locomotionMixer; // Idle(0) / Walk(1)
    [SerializeField] private float locomotionParamLerp = 14f;
    [SerializeField] private bool snapTo8Directions = true;
    public Vector2 MoveDirLocal { get; set; }

    [Header("Dash (Layer 0)")]
    [SerializeField] private ClipTransition dashF;
    [SerializeField] private ClipTransition dashB;
    [SerializeField] private ClipTransition dashL;
    [SerializeField] private ClipTransition dashR;
    private Vector2 _dashDirLocal;
    private float _dashDuration = 0.12f;
    private Locomotion_Dash dashState;
    private Action onDashEndCache;

    [Header("Dead (Layer 0)")]
    [SerializeField] private ClipTransition Dead;
    private Locomotion_Dead deadState;
    private Action onDeadEndCache;

    [Header("Shoot (Layer 1)")]
    [SerializeField] private ClipTransition shootPulse;
    [SerializeField] private ClipTransition shootHoldLoop; // optional
    [SerializeField] private float holdPulseMinInterval = 0.08f;

    [Header("Reload (Layer 1)")]
    [SerializeField] private ClipTransition reload;
    private float _reloadDuration = 0f;
    private Action onReloadEndCache;

    [Header("Melee Combo (Layer 0)")]
    [SerializeField] private MeleeComboSO meleeComboSO;
    [SerializeField] private bool meleeCanInterruptReload = true;
    public enum MeleeType { Light, Heavy }
    [SerializeField] private MeleeComboSO lightCombo;
    [SerializeField] private MeleeComboSO heavyCombo;

    [Header("Downed (Layer 0)")]
    [SerializeField] private MixerTransition2D crawlMixer;
    [SerializeField] private float crawlParamLerp = 10f;
    [SerializeField, Range(0f, 1f)] private float crawlSpeedMultiplier01 = 0.35f;

    [Header("Skill (Layer 0)")]
    [SerializeField] private ClipTransition skillClip;
    
    public bool IsDowned { get; private set; }
    private LocomotionState_Crawl crawlState;

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

    // private WeaponSystem WS => ctx != null ? ctx.WeaponSystem : null;
    // private DashSystem DS => ctx != null ? ctx.DashSystem : null;
    // private bool IsReloading => WS != null && WS.IsReloading;

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

    private AnimancerLayer LocoLayer => animancer.Layers[locomotionLayerIndex];
    private AnimancerLayer ActLayer => animancer.Layers[actionLayerIndex];

    private bool TryInitialize()
    {
        if (!animancer) animancer = GetComponent<AnimancerComponent>();
        if (!animancer || animancer.Animator == null)
            return false;

        // ถ้า Animator เปลี่ยน (เช่น rebuild model) ต้อง init ใหม่
        if (_initialized && animancer.Animator == _boundAnimator)
            return true;

        _boundAnimator = animancer.Animator;

        // Setup action layer
        if (upperBodyMask != null)
            ActLayer.Mask = upperBodyMask;

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
                Debug.LogWarning("[CharacterAnimBrain] Initialization failed.", this);
                _initWarned = true;
            }
            return;
        }

        _initWarned = false;

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
        var selected = (type == MeleeType.Light) ? lightCombo : heavyCombo;
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
        if (!TryInitialize())
            return;
        
        locomotionSM.TrySetState(skill);
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
        if (IsHoldingFire && shootHoldLoop != null)
            actionSM.TrySetState(shootHold);
        else
            actionSM.TrySetState(empty);
    }
}
