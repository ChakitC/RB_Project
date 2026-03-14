using UnityEngine;
[DefaultExecutionOrder(-110)]
[DisallowMultipleComponent]
public sealed class CharacterAnimDriver : MonoBehaviour
{
    [SerializeField] private StateHub hub;
    [SerializeField] private StatsHub StatsHub;
    [SerializeField] private CharacterAnimBrain brain;
    [SerializeField] private HealthSystem _HealthSystem;
    [SerializeField] private WeaponSystem _WeaponSystem;
    [SerializeField] private CharacteContext CTX;
    

    private bool _animHold;     // ตอนนี้ brain กำลังอยู่ hold ไหม
    private bool _inputHold;    // input ระดับ hub (กดค้าง/ปล่อย)

    void Awake()
    {
        if (!hub) hub = GetComponent<StateHub>();
        if (!brain) brain = GetComponent<CharacterAnimBrain>();
        _animHold = false;
        _inputHold = false;
    }

    void OnEnable()
    {
        if (hub == null) return;
        

        hub.ShotFired += OnShotFired;
        hub.FireHeldChanged += OnFireHeldChanged;
        
        hub.ReloadStarted += OnReloadStarted;
        hub.DashStarted   += OnDashStarted;
        hub.Melee += OnMeleeStarted;
        
        
        brain.MeleeComboEnded += OnMeleeEnded;

        _HealthSystem.CharacterDead += OnCharacterDead;
        _HealthSystem.CharacterDown += OnCharacterDown;
        CTX.HealthSystem.CharacterRevive += OnCharacterRevive;


        // hub.StunStarted   += OnStunStarted;
        // hub.Died          += OnDied;
    }

    void OnDisable()
    {
        if (hub != null)
        {
            hub.ShotFired -= OnShotFired;
            hub.FireHeldChanged -= OnFireHeldChanged;
            hub.Melee            -= OnMeleeStarted;
            hub.ReloadStarted -= OnReloadStarted;
            hub.DashStarted   -= OnDashStarted;
            
            brain.MeleeComboEnded -= OnMeleeEnded;
            
            _HealthSystem.CharacterDead -= OnCharacterDead;
            _HealthSystem.CharacterDown -= OnCharacterDown;
            CTX.HealthSystem.CharacterRevive -= OnCharacterRevive;
            
            // hub.StunStarted   -= OnStunStarted;
            
        }

        // reset กันค้าง (เผื่อ disable ตอนกำลัง hold)
        if (_animHold && brain != null)
        {
            brain.FireUp();
            _animHold = false;
        }
        _inputHold = false;
    }

    void LateUpdate()
    {
        if (hub == null || brain == null) return;

        // 1) locomotion param (poll ทุกเฟรม)
        brain.MoveSpeed01 = hub.MoveSpeed01;

        // 2) hold ยิงต้องเช็ค CanShoot ที่เปลี่ยนได้ตลอดเวลา
        ApplyHold(_inputHold && hub.CanShoot());
    }

    // ----------- Event handlers -----------
    void OnCharacterRevive()
    {
        brain.SetDowned(false);
    }

    void OnCharacterDown()
    {
        Debug.Log("brain.SetDowned");
        brain.SetDowned(true);
    }
    
    
    void OnCharacterDead()
    {
        brain.PlayDead();
        Debug.Log("play Dead");
    }

    void OnShotFired()
    {
        if (brain != null) brain.NotifyShotFired();
    }

    void OnFireHeldChanged(bool held)
    {
        _inputHold = held;
        // ไม่รีบ ApplyHold ที่นี่ก็ได้ เพราะ LateUpdate จะจัดให้ (แต่ทำก็ได้)
        if (hub != null) ApplyHold(_inputHold && hub.CanShoot());
    }

    void OnReloadStarted(float reloadTime)
    {
        if (brain != null) brain.PlayReload(reloadTime);
    }

    void OnDashStarted(float duration, Vector3 dirWorld)
    {
        Vector3 local3 = transform.InverseTransformDirection(dirWorld);
        Vector2 dashDirLocal = new Vector2(local3.x, local3.z);
        brain.PlayDash(duration, dashDirLocal);
    }

    void OnMeleeStarted(CharacterAnimBrain.MeleeType MeleeType)
    {
        if (brain != null) brain.PressMelee(MeleeType);
        hub.WeaponSM.TryChange(WeaponStateId.Melee);
    }

    void OnMeleeEnded()
    {
        hub.WeaponSM.TryChange(WeaponStateId.Ready);
    }
    
    //
    // void OnStunStarted()
    // {
    //     if (brain != null) brain.PlayStun();
    // }
    //
   

    // ----------- Helpers -----------

    void ApplyHold(bool shouldHold)
    {
        if (brain == null) return;

        if (shouldHold && !_animHold) { brain.FireDown(); _animHold = true; }
        else if (!shouldHold && _animHold) { brain.FireUp(); _animHold = false; }
    }
}
