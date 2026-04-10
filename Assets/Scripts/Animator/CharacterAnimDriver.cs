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
        if (!hub) hub = GetComponent<StateHub>();
        if (!brain) brain = GetComponent<CharacterAnimBrain>();
    }

    void OnEnable()
    {
        if (hub == null) return;

        hub.ShotFired += OnShotFired;
        hub.ReloadStarted += OnReloadStarted;
        hub.DashStarted += OnDashStarted;

        _HealthSystem.CharacterDead += OnCharacterDead;
        _HealthSystem.CharacterDown += OnCharacterDown;
        CTX.HealthSystem.CharacterRevive += OnCharacterRevive;
    }

    void OnDisable()
    {
        if (hub != null)
        {
            hub.ShotFired -= OnShotFired;
            hub.ReloadStarted -= OnReloadStarted;
            hub.DashStarted -= OnDashStarted;

            _HealthSystem.CharacterDead -= OnCharacterDead;
            _HealthSystem.CharacterDown -= OnCharacterDown;
            CTX.HealthSystem.CharacterRevive -= OnCharacterRevive;
        }

        if (brain != null)
            brain.SetFireHoldContext(false, false);
    }

    void LateUpdate()
    {
        if (hub == null || brain == null) return;

        brain.MoveSpeed01 = hub.MoveSpeed01;
        brain.SetFireHoldContext(hub.DesiredFireHeld, hub.CanShoot());
    }

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
}
