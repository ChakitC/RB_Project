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

    void Awake()
    {
        ResolveReferences();
    }

    void OnEnable()
    {
        ResolveReferences();

        if (hub != null)
        {
            hub.ShotFired += OnShotFired;
            hub.ReloadStarted += OnReloadStarted;
            hub.DashStarted += OnDashStarted;
        }

        if (_HealthSystem != null)
        {
            _HealthSystem.CharacterDead += OnCharacterDead;
            _HealthSystem.CharacterDown += OnCharacterDown;
            _HealthSystem.CharacterRevive += OnCharacterRevive;
        }
    }

    void OnDisable()
    {
        if (hub != null)
        {
            hub.ShotFired -= OnShotFired;
            hub.ReloadStarted -= OnReloadStarted;
            hub.DashStarted -= OnDashStarted;

        }

        if (_HealthSystem != null)
        {
            _HealthSystem.CharacterDead -= OnCharacterDead;
            _HealthSystem.CharacterDown -= OnCharacterDown;
            _HealthSystem.CharacterRevive -= OnCharacterRevive;
        }

        if (brain != null)
            brain.SetFireHoldContext(false, false);
    }

    void ResolveReferences()
    {
        if (!CTX)
        {
            TryGetComponent(out CTX);
            if (!CTX)
                CTX = GetComponentInParent<CharacteContext>();
        }

        CTX?.ResolveReferences();

        if (CTX != null && CTX.AnimDriver != this)
            CTX.AnimDriver = this;

        if (!hub && CTX != null)
            hub = CTX.stateHub;
        if (!hub)
            TryGetComponent(out hub);
        if (!hub && CTX != null)
            hub = CTX.GetComponentInChildren<StateHub>(true);

        if (!StatsHub && CTX != null)
            StatsHub = CTX.StatsHub;
        if (!StatsHub)
            TryGetComponent(out StatsHub);
        if (!StatsHub && CTX != null)
            StatsHub = CTX.GetComponentInChildren<StatsHub>(true);

        if (!brain && CTX != null)
            brain = CTX.AnimBrain;
        if (!brain)
            TryGetComponent(out brain);
        if (!brain && CTX != null)
            brain = CTX.GetComponentInChildren<CharacterAnimBrain>(true);

        if (!_HealthSystem && CTX != null)
            _HealthSystem = CTX.HealthSystem;
        if (!_HealthSystem)
            TryGetComponent(out _HealthSystem);
        if (!_HealthSystem && CTX != null)
            _HealthSystem = CTX.GetComponentInChildren<HealthSystem>(true);

        if (!_WeaponSystem && CTX != null)
            _WeaponSystem = CTX.WeaponSystem;
        if (!_WeaponSystem)
            TryGetComponent(out _WeaponSystem);
        if (!_WeaponSystem && CTX != null)
            _WeaponSystem = CTX.GetComponentInChildren<WeaponSystem>(true);
    }

    void LateUpdate()
    {
        if (hub == null || brain == null) return;

        brain.MoveSpeed01 = hub.MoveSpeed01;
        brain.SetFireHoldContext(hub.DesiredFireHeld, hub.CanShoot());
    }

    void OnCharacterRevive()
    {
        brain?.SetDowned(false);
    }

    void OnCharacterDown()
    {
        Debug.Log("brain.SetDowned");
        brain?.SetDowned(true);
    }

    void OnCharacterDead()
    {
        brain?.PlayDead();
        Debug.Log("play Dead");
    }

    void OnShotFired()
    {
        if (brain != null) brain.NotifyShotFired();
    }

    void OnReloadStarted(float reloadTime)
    {
        if (brain != null) brain.PlayReload(reloadTime);
    }

    void OnDashStarted(float duration, Vector3 dirWorld)
    {
        if (brain == null)
            return;

        Vector3 local3 = transform.InverseTransformDirection(dirWorld);
        Vector2 dashDirLocal = new Vector2(local3.x, local3.z);
        brain.PlayDash(duration, dashDirLocal);
    }
}
