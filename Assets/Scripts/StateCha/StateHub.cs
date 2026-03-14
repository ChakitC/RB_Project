
using UnityEngine;
using System;

public sealed class StateHub : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CharacteContext ctx;

    public StateMachine<MoveStateId,   CharacteContext> MoveSM   { get; private set; }
    public StateMachine<WeaponStateId, CharacteContext> WeaponSM { get; private set; }
    public StateMachine<LifeStateId,   CharacteContext> LifeSM   { get; private set; }
    public StateMachine<UIStateId,     CharacteContext> UISM     { get; private set; }

    // ---------------- DEBUG ----------------
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

    // ขนาด GUI 
    [SerializeField] private Vector2 screenPos = new Vector2(10, 10);

    void Awake()
    {
        if (!ctx) ctx = GetComponent<CharacteContext>();

        MoveSM   = new StateMachine<MoveStateId,   CharacteContext>(ctx);
        WeaponSM = new StateMachine<WeaponStateId, CharacteContext>(ctx);
        LifeSM   = new StateMachine<LifeStateId,   CharacteContext>(ctx);
        UISM     = new StateMachine<UIStateId,     CharacteContext>(ctx);

        // Register
        MoveSM
            .Add(MoveStateId.Stand,    new Move_Stand())
            .Add(MoveStateId.Dash,     new Move_Dash())
            .Add(MoveStateId.Moveing,  new Move_Moving())
            .Add(MoveStateId.Stunned,  new Move_Stunned());

        WeaponSM
            .Add(WeaponStateId.Melee,   new Weapon_Melee())
            .Add(WeaponStateId.Ready,     new Weapon_Ready())
            .Add(WeaponStateId.Firing,    new Weapon_Firing())
            .Add(WeaponStateId.Reloading, new Weapon_Reloading())
            .Add(WeaponStateId.NoBullet,  new Weapon_NoBullet());

        LifeSM
            .Add(LifeStateId.Alive, new Life_Alive())
            .Add(LifeStateId.Down, new Life_Down())
            .Add(LifeStateId.Dead,  new Life_Dead());

        UISM
            .Add(UIStateId.Normal,    new UI_Normal())
            .Add(UIStateId.Inventory, new UI_Inventory())
            .Add(UIStateId.Pause,     new UI_Pause());

        // Initial
        LifeSM.SetInitial(LifeStateId.Alive);
        MoveSM.SetInitial(MoveStateId.Stand);
        WeaponSM.SetInitial(WeaponStateId.Ready);
        UISM.SetInitial(UIStateId.Normal);

        
        
        MoveSM.OnChanged += (from, to) =>
        {
            // if (to == MoveStateId.Dash) DashStarted?.Invoke(ctx.DashSystem.dashDuration);
            if (to == MoveStateId.Stunned) StunStarted?.Invoke();
        };

        WeaponSM.OnChanged += (from, to) =>
        {
            if (to == WeaponStateId.Reloading)
                ReloadStarted?.Invoke(ctx.StatsHub.ReloadTime);
        };

        LifeSM.OnChanged += (from, to) =>
        {
            if (to == LifeStateId.Dead) Died?.Invoke();
        };
        
        // DEBUG: Hook transition logs
        if (logTransitions)
        {
            MoveSM.OnChanged   += (from, to) => LogChange("MoveSM",   from, to);
            WeaponSM.OnChanged += (from, to) => LogChange("WeaponSM", from, to);
            LifeSM.OnChanged   += (from, to) => LogChange("LifeSM",   from, to);
            UISM.OnChanged     += (from, to) => LogChange("UISM",     from, to);
        }

        // (ตัวอย่าง cross-domain rule เดิมของคุณก็อยู่ตรงนี้ต่อได้)
    }

    void Update()
    {
        float dt = Time.deltaTime;

        LifeSM.Tick(dt);
        UISM.Tick(dt);
        MoveSM.Tick(dt);
        WeaponSM.Tick(dt);

        UpdateStand();

        if (debugInInspector) UpdateDebugSnapshot();
    }
    
    // ------------ Debug helpers ------------
    void LogChange<TId>(string smName, TId from, TId to)
    {
        Debug.Log($"[{nameof(StateHub)}] {smName}: {from} -> {to}", this);
    }

    void UpdateDebugSnapshot()
    {
        dbgLife   = LifeSM.CurrentId.ToString();
        dbgUI     = UISM.CurrentId.ToString();
        dbgMove   = MoveSM.CurrentId.ToString();
        dbgWeapon = WeaponSM.CurrentId.ToString();

        dbgSnapshot =
            $"Life   : {dbgLife}\n" +
            $"UI     : {dbgUI}\n" +
            $"Move   : {dbgMove}\n" +
            $"Weapon : {dbgWeapon}\n";
    }

    void OnGUI()
    {
        if (!showOnScreen) return;
        GUI.Label(new Rect(screenPos.x, screenPos.y, 400, 120), dbgSnapshot);
    }

    // ------------ Optional: query methods ------------
    public bool IsAlive => LifeSM.CurrentId == LifeStateId.Alive ;
    public bool Isdown => LifeSM.CurrentId == LifeStateId.Down;

    public bool CanShoot() =>
        
        IsAlive &&
        !Isdown &&
        MoveSM.CurrentId != MoveStateId.Dash &&
        UISM.CurrentId != UIStateId.Inventory &&
        UISM.CurrentId != UIStateId.Pause &&
        WeaponSM.CurrentId != WeaponStateId.Reloading &&
        WeaponSM.CurrentId != WeaponStateId.Melee &&
        WeaponSM.CurrentId != WeaponStateId.NoBullet;
    

    public bool CanMove() =>    
        
        (IsAlive || Isdown) &&
        UISM.CurrentId != UIStateId.Inventory &&
        UISM.CurrentId != UIStateId.Pause &&
        WeaponSM.CurrentId != WeaponStateId.Melee &&
        MoveSM.CurrentId != MoveStateId.Dash &&
        MoveSM.CurrentId != MoveStateId.Stunned;
    
    
    // ============= Stand Chance ===================


    void UpdateStand()
    {
        var ws = ctx.WeaponSystem;
        if (ws == null) return;

        if (WeaponSM.CurrentId == WeaponStateId.Melee)
        {
            return;
        }

        if (ws.IsReloading)
        {
            WeaponSM.TryChange(WeaponStateId.Reloading);
            return;
        }

        if (ws.magazine <= 0) 
        {
            WeaponSM.TryChange(WeaponStateId.NoBullet);
            return;
        }

        if (ws.isFiring) 
        {
            WeaponSM.TryChange(WeaponStateId.Firing);
            return;
        }

        WeaponSM.TryChange(WeaponStateId.Ready);
    }
    
    // =================== Input Request ======================

    public void RequestReload()
    {
        ctx.WeaponSystem.TryReload();
    }

    public void RequestOnFire()
    {
        if(WeaponSM.CurrentId == WeaponStateId.Melee) return;
        if(MoveSM.CurrentId == MoveStateId.Dash) return;
        if (!ctx.stateHub.CanShoot()) return;
        ctx.WeaponSystem.SetFiring(true);
        ctx.stateHub.SetFireHeld(true);
    }

    public void RequestCanceledFire()
    {
        if(WeaponSM.CurrentId == WeaponStateId.Melee) return;
        if(MoveSM.CurrentId == MoveStateId.Dash) return;
        ctx.WeaponSystem.SetFiring(false);
        ctx.stateHub.SetFireHeld(false);
    }
    
    public void RequestOnMelee()
    {
        WeaponSM.TryChange(WeaponStateId.Melee);
        if (MoveSM.CurrentId == MoveStateId.Dash) return;
        
        ctx.WeaponSystem.SetFiring(false);
        ctx.stateHub.SetFireHeld(false);

        ctx.stateHub.ReportMeleeStarted(CharacterAnimBrain.MeleeType.Heavy);
    }
    

    public void RequestOnDash()
    {
        if (!ctx.stateHub.CanMove()) return;
        
        ctx.WeaponSystem.SetFiring(false);
        ctx.stateHub.SetFireHeld(false);

        if (WeaponSM.CurrentId == WeaponStateId.Reloading)
        {
            ctx.WeaponSystem.CancelReload();
        }
        
        ctx.DashSystem.TryDash();
        
    }
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    // ================== Animation Signals ==================
    public float MoveSpeed01 { get; private set; }   // 0..1 (poll)
    public bool  FireHeld    { get; private set; }   // input-level (edge)
    
    public Vector3 DashDirWorld { get; private set; } = Vector3.forward;

    public event Action <CharacterAnimBrain.MeleeType>Melee;
    public event Action <float> ReloadStarted;
    public event Action<float, Vector3> DashStarted;
    public event Action StunStarted;
    public event Action Died;
    public event Action <bool> FireHeldChanged;       // down/up
    public event Action ShotFired;                   // ยิงสำเร็จจริง 1 นัด

    public void SetMoveSpeed01(float v01)
    {
        MoveSpeed01 = v01;
    }

    public void SetFireHeld(bool held)
    {
        if (FireHeld == held) return;
        FireHeld = held;
        FireHeldChanged?.Invoke(held);
    }
    
    public void ReportShotFired()
    {
        ShotFired?.Invoke();
    }
    
    public void ReportDashStarted(float duration, Vector3 dashDirWorld)
    {
        if (dashDirWorld.sqrMagnitude > 0.0001f)
            DashDirWorld = dashDirWorld.normalized;

        DashStarted?.Invoke(duration, DashDirWorld);
    }

    public void ReportMeleeStarted(CharacterAnimBrain.MeleeType meleeType)
    {
        Melee?.Invoke(meleeType);
    }
    
}
