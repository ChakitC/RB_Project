using UnityEngine;

[DisallowMultipleComponent]
public class CharacterAudioEmitter : MonoBehaviour
{
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private StateHub stateHub;
    [SerializeField] private HealthSystem healthSystem;

    void Awake()
    {
        if (!ctx) ctx = GetComponent<CharacteContext>();
        if (!stateHub) stateHub = GetComponent<StateHub>();
        if (!healthSystem) healthSystem = GetComponent<HealthSystem>();
    }

    void OnEnable()
    {
        if (stateHub != null)
        {
            stateHub.DashStarted += OnDashStarted;
            stateHub.Melee += OnMeleeStarted;
        }

        if (healthSystem != null)
        {
            healthSystem.DamageTaken += OnDamageTaken;
            healthSystem.CharacterDown += OnCharacterDown;
            healthSystem.CharacterDead += OnCharacterDead;
            healthSystem.CharacterRevive += OnCharacterRevive;
        }
    }

    void OnDisable()
    {
        if (stateHub != null)
        {
            stateHub.DashStarted -= OnDashStarted;
            stateHub.Melee -= OnMeleeStarted;
        }

        if (healthSystem != null)
        {
            healthSystem.DamageTaken -= OnDamageTaken;
            healthSystem.CharacterDown -= OnCharacterDown;
            healthSystem.CharacterDead -= OnCharacterDead;
            healthSystem.CharacterRevive -= OnCharacterRevive;
        }
    }

    void OnDashStarted(float _, Vector3 __)
    {
        var stats = GetStats();
        PlayCue(stats != null ? stats.dashCue : null);
    }

    void OnMeleeStarted(CharacterAnimBrain.MeleeType meleeType)
    {
        var stats = GetStats();
        if (stats == null)
            return;

        AudioCue cue = meleeType == CharacterAnimBrain.MeleeType.Light
            ? (stats.meleeLightCue != null ? stats.meleeLightCue : stats.meleeHeavyCue)
            : (stats.meleeHeavyCue != null ? stats.meleeHeavyCue : stats.meleeLightCue);

        PlayCue(cue);
    }

    void OnDamageTaken(float _, GameObject __)
    {
        var stats = GetStats();
        PlayCue(stats != null ? stats.damagedCue : null);
    }

    void OnCharacterDown()
    {
        var stats = GetStats();
        PlayCue(stats != null ? stats.downCue : null);
    }

    void OnCharacterDead()
    {
        var stats = GetStats();
        PlayCue(stats != null ? stats.deathCue : null);
    }

    void OnCharacterRevive()
    {
        var stats = GetStats();
        PlayCue(stats != null ? stats.reviveCue : null);
    }

    CharacterStats GetStats()
    {
        return ctx != null ? ctx.baseStats : null;
    }

    void PlayCue(AudioCue cue)
    {
        if (cue == null)
            return;

        Transform followTarget = ctx != null ? ctx.transform : transform;
        AudioService.Instance.PlayAttached(cue, followTarget, Vector3.zero);
    }
}
