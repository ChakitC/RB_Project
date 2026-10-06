using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class StateHub : MonoBehaviour
{
    [SerializeField] private CharacteContext ctx;
    StateValue<MoveStateId> move;
    StateValue<WeaponStateId> weapon;
    StateValue<LifeStateId> life;
    StateValue<UIStateId> ui;
    public IReadOnlyState<MoveStateId> MoveSM => move?.ReadOnly;
    public IReadOnlyState<WeaponStateId> WeaponSM => weapon?.ReadOnly;
    public IReadOnlyState<LifeStateId> LifeSM => life?.ReadOnly;
    public IReadOnlyState<UIStateId> UISM => ui?.ReadOnly;
    public bool IsInitialized { get; private set; }
    [Header("Debug")]
    [SerializeField] private bool debugInInspector = true;
    [SerializeField] private bool logTransitions = true;
    [SerializeField] private bool showOnScreen = false;

    [SerializeField] private string dbgLife;
    [SerializeField] private string dbgUI;
    [SerializeField] private string dbgMove;
    [SerializeField] private string dbgWeapon;

    [TextArea(2, 8)]
    [SerializeField] private string dbgSnapshot;

    [SerializeField] private Vector2 screenPos = new Vector2(10, 10);

    [Header("Status Effect Locks")]
    [SerializeField] private ControlBlockFlags statusEffectControlBlocks = ControlBlockFlags.None;
    [SerializeField] private bool statusEffectStunned;
    [SerializeField] private ControlBlockFlags externalControlBlocks = ControlBlockFlags.None;
    [SerializeField] private bool externalStunned;
    [SerializeField] private ControlBlockFlags staggerControlBlocks = ControlBlockFlags.None;
    [SerializeField] private bool staggerStunned;


    readonly CharacterControlLocks controlLocks = new();
    readonly Dictionary<int, (UIStateId state, string owner)> uiHolds = new();
    int nextUiToken = 1;
    bool subscribed, refreshing, refreshPending, suspended, ownerReloading;
    bool applyingCancellations, menuWasBlocking, controlWasStunned;
    LifeStateId reconciledLife = LifeStateId.Alive;
    WeaponSystem boundWeapon;
    HealthSystem boundHealth;
    MeleeController boundMelee;
    DashSystem boundDash;
    CharacterKnockbackMotor boundKnockback;

    public float MoveSpeed01 { get; private set; }
    public bool IsSprinting => ctx != null && ctx.IsSprinting;
    public bool IsCrouching => ctx != null && ctx.IsCrouching;
    public bool IsSliding => ctx != null && ctx.IsSliding;
    public Vector2 MoveDirLocal { get; private set; }
    public bool DesiredFireHeld { get; private set; }
    public bool FireHeld { get; private set; }
    public Vector3 DashDirWorld { get; private set; } = Vector3.forward;
    public event Action<MeleeType> Melee;
    public event Action<float> ReloadStarted;
    public event Action<float, Vector3> DashStarted;
    public event Action StunStarted;
    public event Action Died;
    public event Action<bool> FireHeldChanged;
    public event Action ShotFired;
    public event Action<LifeStateId, LifeStateId> LifeStateChanged;
    public bool IsAlive => IsInitialized && (ctx == null || ctx.HealthSystem == null || ctx.HealthSystem.IsAlive);
    public bool Isdown => IsInitialized && ctx != null && ctx.HealthSystem != null && ctx.HealthSystem.IsDown;
    public bool IsGrounded => ctx == null || ctx.VerticalMotor == null || ctx.VerticalMotor.IsGrounded;
    public bool IsAirborne => ctx != null && ctx.VerticalMotor != null && ctx.VerticalMotor.IsAirborne;
    public float AirTime => ctx != null && ctx.VerticalMotor != null ? ctx.VerticalMotor.AirTime : 0f;
    public ControlBlockFlags ActiveControlBlockFlags => controlLocks.Flags;
    public string DescribeControlHolders() => controlLocks.Describe();
    public string DescribeUiHolders()
    {
        var result = new System.Text.StringBuilder();
        foreach (var entry in uiHolds) result.AppendLine($"{entry.Key}: {entry.Value.owner} ({entry.Value.state})");
        return result.ToString();
    }

    void Awake()
    {
        if (IsInitialized) return;
        if (!ctx) ctx = CharacterContextModuleLookup.ResolveContext(gameObject);
        ctx?.ResolveReferences();
        if (ctx != null) ctx.stateHub = this;
        move = new(MoveStateId.Stand);
        weapon = new(WeaponStateId.Ready);
        life = new(ctx != null && ctx.HealthSystem != null ? ctx.HealthSystem.LifeState : LifeStateId.Alive);
        ui = new(ResolveUiState());
        controlLocks.Set(CharacterControlSource.StatusEffects, statusEffectControlBlocks, statusEffectStunned);
        controlLocks.Set(CharacterControlSource.Stagger, staggerControlBlocks, staggerStunned);
        controlLocks.Set(CharacterControlSource.Legacy, externalControlBlocks, externalStunned);
        IsInitialized = true;
        MoveSM.OnChanged += OnMoveChanged;
        LifeSM.OnChanged += OnLifeChanged;
        RefreshOwnerBindings();
        RefreshStates();
    }

    void OnEnable()
    {
        suspended = false;
        RefreshOwnerBindings();
        RefreshStates();
    }

    void OnDisable()
    {
        suspended = true;
        UnsubscribeOwners();
        RequestCanceledFire();
        MoveSpeed01 = 0f;
        MoveDirLocal = Vector2.zero;
        if (ctx != null)
        {
            ctx.moveInput = Vector2.zero;
            ctx.CancelSprint();
            ctx.CancelSlide();
            ctx.WeaponSystem?.CancelReload();
            ctx.MeleeController?.InterruptMelee();
            ctx.DashSystem?.CancelDash();
            ctx.KnockbackMotor?.StopKnockback(clearReactionState: true);
        }
        RefreshStates();
    }

    void OnDestroy() => UnsubscribeOwners();

    // Called by context initialization/rebinding, never by admission queries.
    public void RefreshOwnerBindings()
    {
        if (!IsInitialized || suspended || ctx == null) return;
        if (subscribed && boundWeapon == ctx.WeaponSystem && boundHealth == ctx.HealthSystem &&
            boundMelee == ctx.MeleeController && boundDash == ctx.DashSystem && boundKnockback == ctx.KnockbackMotor) return;
        UnsubscribeOwners();
        boundWeapon = ctx.WeaponSystem;
        boundHealth = ctx.HealthSystem;
        boundMelee = ctx.MeleeController;
        boundDash = ctx.DashSystem;
        boundKnockback = ctx.KnockbackMotor;
        ownerReloading = boundWeapon != null && boundWeapon.IsReloading;
        if (boundWeapon) boundWeapon.RuntimeStateChanged += OnWeaponRuntimeChanged;
        if (boundHealth) boundHealth.LifeStateChanged += OnOwnerLifeChanged;
        if (boundMelee) boundMelee.ActivityChanged += RefreshStates;
        if (boundDash) boundDash.ActivityChanged += RefreshStates;
        if (boundKnockback) boundKnockback.ActivityChanged += RefreshStates;
        subscribed = true;
        RefreshStates();
    }

    void UnsubscribeOwners()
    {
        if (boundWeapon) boundWeapon.RuntimeStateChanged -= OnWeaponRuntimeChanged;
        if (boundHealth) boundHealth.LifeStateChanged -= OnOwnerLifeChanged;
        if (boundMelee) boundMelee.ActivityChanged -= RefreshStates;
        if (boundDash) boundDash.ActivityChanged -= RefreshStates;
        if (boundKnockback) boundKnockback.ActivityChanged -= RefreshStates;
        subscribed = false;
    }

    void OnWeaponRuntimeChanged(WeaponRuntimeState state)
    {
        bool started = state.Reloading && !ownerReloading;
        ownerReloading = state.Reloading;
        RefreshStates();
        if (started && boundWeapon != null && boundWeapon.IsReloading)
            ReloadStarted?.Invoke(boundWeapon.GetReloadAnimDuration());
    }
    void OnOwnerLifeChanged(LifeStateId _, LifeStateId __) => RefreshStates();
    void OnMoveChanged(MoveStateId _, MoveStateId next) { if (next == MoveStateId.Stunned) StunStarted?.Invoke(); }
    void OnLifeChanged(LifeStateId previous, LifeStateId next)
    {
        CharacterStateNotifications.Publish(LifeStateChanged, previous, next);
        if (next == LifeStateId.Dead) CharacterStateNotifications.Publish(Died);
    }

    void Update()
    {
        RefreshStates(); // Locomotion intent and playback can change each frame.
        if (DesiredFireHeld && ctx != null && ctx.WeaponSystem != null && !ctx.WeaponSystem.IsFiringHeld)
            TryStartHeldFire(false);
    }

    public CharacterActionFacts GetActionFacts()
    {
        var ws = ctx != null ? ctx.WeaponSystem : null;
        var brain = ctx != null ? ctx.AnimBrain : null;
        return new CharacterActionFacts
        {
            Initialized = IsInitialized && !suspended,
            Life = ctx != null && ctx.HealthSystem != null ? ctx.HealthSystem.LifeState : LifeStateId.Alive,
            UI = ResolveUiState(), Blocks = controlLocks.Flags, Stunned = controlLocks.Stunned,
            Dashing = ctx != null && ctx.DashSystem != null && ctx.DashSystem.IsDashing,
            Knockback = ctx != null && ctx.KnockbackMotor != null && ctx.KnockbackMotor.IsActive,
            Melee = ctx != null && ctx.MeleeController != null && ctx.MeleeController.IsComboActive,
            Reloading = ws != null && ws.IsReloading, EmptyMagazine = ws != null && ws.IsMagazineEmpty,
            FullBodyReload = ws != null && ws.IsReloading && ctx.baseStats != null && ctx.baseStats.animProfile != null &&
                ctx.baseStats.animProfile.reloadBodyMode == CharacterAnimProfileSO.ReloadBodyMode.FullBody,
            FullBodyReloadConfigured = ctx != null && ctx.baseStats != null && ctx.baseStats.animProfile != null &&
                ctx.baseStats.animProfile.reloadBodyMode == CharacterAnimProfileSO.ReloadBodyMode.FullBody,
            ShootBlockingPlayback = brain != null && brain.IsShootBlockingPlaybackActive,
            SkillPlayback = brain != null && brain.IsSkillPlaybackActive,
            CanInterruptReload = ws != null && ws.CanInterruptReloadWithFire,
            HasWeapon = ws != null, HasDash = ctx != null && ctx.DashSystem != null,
            HasMelee = ctx != null && ctx.MeleeController != null,
            HasSkill = ctx != null && ctx.SkillManager != null,
            Paused = GlobalTimeScaleManager.IsCurrentlyPaused,
            Aiming = ws != null && ws.IsAiming, Firing = ws != null && ws.IsFiringActivity,
            Crouching = IsCrouching, Sliding = IsSliding,
            RootMotion = brain != null && brain.RootMotionActive,
            ProtectedPlayback = brain != null && (brain.IsChainPlaybackActive || brain.IsStageIntroPlaybackActive) || CutsceneDirector.IsCinematicPlaying,
            MeleeCanInterruptReload = ctx != null && ctx.baseStats != null && ctx.baseStats.animProfile != null && ctx.baseStats.animProfile.meleeCanInterruptReload
        };
    }
    public ActionAdmissionResult GetAdmission(CharacterAction action) => applyingCancellations
        ? new(ActionBlockReason.Busy) : CharacterActionPolicy.Evaluate(action, GetActionFacts());
    public bool CanContinue(CharacterAction action) => CharacterActionPolicy.Evaluate(action, GetActionFacts(), continuing: true).Allowed;

    // Only the owner calls this, after readiness has succeeded and before publishing its start.
    internal void ApplyAcceptedAction(ActionAdmissionResult decision)
    {
        if (!decision.Allowed || applyingCancellations) return;
        applyingCancellations = true;
        try
        {
            var cancel = decision.Cancellations;
            if ((cancel & ActionCancellation.Sprint) != 0) ctx?.CancelSprint();
            if ((cancel & ActionCancellation.Slide) != 0) ctx?.CancelSlide();
            if ((cancel & ActionCancellation.Dash) != 0) ctx?.DashSystem?.CancelDash();
            if ((cancel & ActionCancellation.Reload) != 0) ctx?.WeaponSystem?.CancelReload();
            if ((cancel & ActionCancellation.Firing) != 0)
            {
                ctx?.WeaponSystem?.SetFiring(false);
                SetFireHeld(false);
            }
        }
        finally { applyingCancellations = false; }
    }

    void ReconcileControlTransitions(in CharacterActionFacts facts)
    {
        bool menu = facts.UI != UIStateId.Normal || facts.Paused;
        bool lostLife = facts.Life != LifeStateId.Alive && facts.Life != reconciledLife;
        bool stunned = facts.Stunned && !controlWasStunned;
        bool openedMenu = menu && !menuWasBlocking;
        menuWasBlocking = menu;
        controlWasStunned = facts.Stunned;
        reconciledLife = facts.Life;
        if (!openedMenu && !stunned && !lostLife) return;
        if (ctx == null) return;
        ctx.moveInput = Vector2.zero;
        ctx.CancelSprint();
        ctx.CancelSlide();
        if (ctx is PlayerContext player) player.movement?.ResetCrouchInput();
        RequestCanceledFire();
        ctx.WeaponSystem?.OnAim(false);
        if ((stunned && !facts.ProtectedPlayback) || lostLife)
        {
            ctx.WeaponSystem?.CancelReload();
            ctx.DashSystem?.CancelDash();
            ctx.MeleeController?.InterruptMelee();
            ctx.SkillManager?.TryCancelActiveCast(SkillCastCancelReason.InvalidState);
            ctx.AnimBrain?.StopOrdinaryActionPlayback();
            ctx.DefensiveBlock?.Cancel();
        }
    }
    public bool CanShoot() => GetAdmission(CharacterAction.Shoot).Allowed;
    public bool CanMove() => GetAdmission(CharacterAction.Move).Allowed;
    public bool CanDash() => GetAdmission(CharacterAction.Dash).Allowed;
    public bool CanUseSkill() => GetAdmission(CharacterAction.Skill).Allowed;
    public bool CanStartMelee() => GetAdmission(CharacterAction.Melee).Allowed;
    public bool CanRotate() => GetAdmission(CharacterAction.Rotate).Allowed;

    void RefreshStates()
    {
        if (!IsInitialized) return;
        if (refreshing) { refreshPending = true; return; }
        refreshing = true;
        try
        {
            do
            {
                refreshPending = false;
                var facts = GetActionFacts();
                ReconcileControlTransitions(facts);
                facts = GetActionFacts();
                life.Set(facts.Life);
                ui.Set(facts.UI);
                move.Set(facts.Knockback ? MoveStateId.Knockback : facts.Stunned ? MoveStateId.Stunned :
                    facts.Dashing ? MoveStateId.Dash : CanMove() && IsSliding ? MoveStateId.Slide :
                    CanMove() && IsCrouching ? MoveStateId.Crouch : CanMove() && IsSprinting ? MoveStateId.Sprint :
                    CanMove() && ctx != null && ctx.ShouldBeInMoveState() ? MoveStateId.Moveing : MoveStateId.Stand);
                var ws = ctx != null ? ctx.WeaponSystem : null;
                weapon.Set(facts.Melee ? WeaponStateId.Melee : facts.Reloading ? WeaponStateId.Reloading :
                    facts.EmptyMagazine ? WeaponStateId.NoBullet : ws != null && ws.IsFiringActivity ? WeaponStateId.Firing : WeaponStateId.Ready);
                if (!CanShoot())
                {
                    ws?.SetFiring(false);
                    SetFireHeld(false);
                }
            } while (refreshPending);
        }
        finally { refreshing = false; }
    }

    void SetControlSource(CharacterControlSource source, ControlBlockFlags flags, bool stunned)
    {
        if (!controlLocks.Set(source, flags, stunned)) return;
        RefreshStates();
    }
    public void SetStatusEffectControlState(ControlBlockFlags controlBlocks, bool stunned)
    {
        statusEffectControlBlocks = controlBlocks; statusEffectStunned = stunned;
        SetControlSource(CharacterControlSource.StatusEffects, controlBlocks, stunned);
    }
    public void SetStaggerControlState(ControlBlockFlags controlBlocks, bool stunned)
    {
        staggerControlBlocks = controlBlocks; staggerStunned = stunned;
        SetControlSource(CharacterControlSource.Stagger, controlBlocks, stunned);
    }
    public void SetExternalControlState(ControlBlockFlags controlBlocks, bool stunned)
    {
        externalControlBlocks = controlBlocks; externalStunned = stunned;
        SetControlSource(CharacterControlSource.Legacy, controlBlocks, stunned);
    }
    public void SetReactionControlState(ControlBlockFlags flags, bool stunned) => SetControlSource(CharacterControlSource.Reaction, flags, stunned);
    public void AddExternalControlBlock(ControlBlockFlags flag) => SetExternalControlState(externalControlBlocks | flag, externalStunned);
    public void RemoveExternalControlBlock(ControlBlockFlags flag) => SetExternalControlState(externalControlBlocks & ~flag, externalStunned);
    public int AcquireExternalControlBlockToken(ControlBlockFlags flags) => AcquireExternalControlBlockToken(flags, "External");
    public int AcquireExternalControlBlockToken(ControlBlockFlags flags, string owner)
    {
        int token = controlLocks.Acquire(flags, owner);
        RefreshStates();
        return token;
    }
    public void ReleaseExternalControlBlockToken(int token) { if (controlLocks.Release(token)) RefreshStates(); }

    UIStateId ResolveUiState()
    {
        var result = UIStateId.Normal;
        foreach (var hold in uiHolds.Values)
        {
            if (hold.state == UIStateId.Pause) return UIStateId.Pause;
            if (hold.state == UIStateId.Inventory) result = UIStateId.Inventory;
        }
        return result;
    }
    public int AcquireUIStateToken(UIStateId state, string owner = "UI")
    {
        if (state == UIStateId.Normal) return 0;
        int token;
        do { token = nextUiToken; nextUiToken = token == int.MaxValue ? 1 : token + 1; } while (uiHolds.ContainsKey(token));
        uiHolds.Add(token, (state, owner));
        RefreshStates();
        return token;
    }
    public void ReleaseUIStateToken(int token) { if (uiHolds.Remove(token)) RefreshStates(); }

    void TryStartHeldFire(bool fresh)
    {
        if (!GetAdmission(fresh ? CharacterAction.FireRequest : CharacterAction.Shoot).Allowed) return;
        var ws = ctx.WeaponSystem;
        ws.TryStartFiring(fresh);
        SetFireHeld(ws.IsFiringHeld && CanShoot());
    }
    public void RequestReload() { if (GetAdmission(CharacterAction.Reload).Allowed) ctx.WeaponSystem.TryReload(); }
    public void RequestOnFire()
    {
        var facts = GetActionFacts();
        if (facts.Initialized && (facts.UI != UIStateId.Normal || facts.Paused || facts.Stunned || facts.Life != LifeStateId.Alive)) return;
        DesiredFireHeld = true;
        TryStartHeldFire(true);
    }
    public void RequestCanceledFire() { DesiredFireHeld = false; ctx?.WeaponSystem?.SetFiring(false); SetFireHeld(false); }
    public bool RequestOnMelee(MeleeType meleeType = MeleeType.Heavy)
    {
        return CanStartMelee() && ctx.MeleeController.TryStartMelee(meleeType);
    }
    public void RequestMeleePress(MeleeType meleeType = MeleeType.Heavy)
    {
        if (!CanStartMelee()) return;
        ctx.MeleeController.PressMelee(meleeType);
    }
    public void RequestOnDash()
    {
        if (!CanDash()) return;
        ctx.DashSystem.TryDash();
    }
    public void SetMoveSpeed01(float v01) => MoveSpeed01 = v01;
    public void SetMoveDirLocal(Vector2 value) => MoveDirLocal = value;
    public void SetDesiredFireHeld(bool held) => DesiredFireHeld = held;
    public void SetFireHeld(bool held)
    {
        if (FireHeld == held) return;
        FireHeld = held;
        FireHeldChanged?.Invoke(held);
    }
    public void ReportShotFired() => ShotFired?.Invoke();
    public void ReportDashStarted(float duration, Vector3 dashDirWorld)
    {
        if (dashDirWorld.sqrMagnitude > 0.0001f) DashDirWorld = dashDirWorld.normalized;
        DashStarted?.Invoke(duration, DashDirWorld);
    }
    public void ReportMeleeStarted(MeleeType meleeType) => Melee?.Invoke(meleeType);

    internal void ConfigureLegacyDebugView(StateHubDebugView view) =>
        view.Configure(debugInInspector, logTransitions, showOnScreen, screenPos);
}
