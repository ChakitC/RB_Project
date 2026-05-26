using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterAudioEmitter : MonoBehaviour
{
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private StateHub stateHub;
    [SerializeField] private HealthSystem healthSystem;
    [SerializeField] private CharacterSkillManager skillManager;
    [SerializeField] private CharacterKnockbackMotor knockbackMotor;

    readonly Dictionary<CharacterSkillVoiceLine, float> skillVoiceReadyAt = new();
    readonly Dictionary<CharacterEventVoiceLine, float> eventVoiceReadyAt = new();
    float defaultSkillVoiceReadyAt;
    float nextSkillVoiceAllowedAt;
    bool lowHpVoiceArmed = true;

    void Awake()
    {
        ResolveReferences();
    }

    void ResolveReferences()
    {
        if (!ctx)
        {
            TryGetComponent(out ctx);
            if (!ctx)
                ctx = GetComponentInParent<CharacteContext>();
        }

        ctx?.ResolveReferences();

        if (!stateHub && ctx != null)
            stateHub = ctx.stateHub;
        if (!stateHub)
            TryGetComponent(out stateHub);

        if (!healthSystem && ctx != null)
            healthSystem = ctx.HealthSystem;
        if (!healthSystem)
            TryGetComponent(out healthSystem);

        if (!skillManager && ctx != null)
            skillManager = ctx.SkillManager;
        if (!skillManager)
            TryGetComponent(out skillManager);
        if (!skillManager && ctx != null)
            skillManager = ctx.GetComponentInChildren<CharacterSkillManager>(true);

        if (!knockbackMotor && ctx != null)
            knockbackMotor = ctx.KnockbackMotor;
        if (!knockbackMotor)
            TryGetComponent(out knockbackMotor);
        if (!knockbackMotor && ctx != null)
            knockbackMotor = ctx.GetComponentInChildren<CharacterKnockbackMotor>(true);
    }

    void OnEnable()
    {
        ResolveReferences();

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

        if (skillManager != null)
            skillManager.CastReleased += OnSkillCastReleased;

        if (knockbackMotor != null)
            knockbackMotor.KnockbackStarted += OnKnockbackStarted;
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

        if (skillManager != null)
            skillManager.CastReleased -= OnSkillCastReleased;

        if (knockbackMotor != null)
            knockbackMotor.KnockbackStarted -= OnKnockbackStarted;
    }

    void Update()
    {
        RefreshLowHpVoiceArmState();
    }

    void OnDashStarted(float _, Vector3 __)
    {
        var stats = GetStats();
        PlayCue(stats != null ? stats.dashCue : null);

        CharacterVoiceProfile voiceProfile = GetVoiceProfile();
        TryPlayEventVoice(voiceProfile != null ? voiceProfile.dashVoice : null);
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
        TryPlayLowHpVoice();
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
        lowHpVoiceArmed = true;

        var stats = GetStats();
        PlayCue(stats != null ? stats.reviveCue : null);
    }

    void OnKnockbackStarted(KnockbackData _)
    {
        CharacterVoiceProfile voiceProfile = GetVoiceProfile();
        TryPlayEventVoice(voiceProfile != null ? voiceProfile.knockbackVoice : null);
    }

    void OnSkillCastReleased(ActiveSkillCastInfo castInfo)
    {
        TryPlaySkillVoice(castInfo.SkillDef);
    }

    public bool TryPlaySkillVoice(SkillGemDefinition skillDef)
    {
        ResolveReferences();

        CharacterVoiceProfile voiceProfile = GetVoiceProfile();
        if (voiceProfile == null)
            return false;

        float now = Time.unscaledTime;
        if (voiceProfile.globalSkillVoiceCooldown > 0f && now < nextSkillVoiceAllowedAt)
            return false;

        if (TryGetSkillVoiceLine(voiceProfile, skillDef, out CharacterSkillVoiceLine voiceLine))
        {
            if (!CanPlaySkillVoiceLine(voiceLine, now))
                return false;

            if (!PlayCue(voiceLine.cue))
                return false;

            StampSkillVoiceCooldowns(voiceProfile, voiceLine, now);
            return true;
        }

        if (!CanPlayDefaultSkillVoice(voiceProfile, now))
            return false;

        if (!PlayCue(voiceProfile.defaultSkillVoiceCue))
            return false;

        StampDefaultSkillVoiceCooldowns(voiceProfile, now);
        return true;
    }

    CharacterStats GetStats()
    {
        return ctx != null ? ctx.baseStats : null;
    }

    CharacterVoiceProfile GetVoiceProfile()
    {
        CharacterStats stats = GetStats();
        return stats != null ? stats.voiceProfile : null;
    }

    bool TryGetSkillVoiceLine(
        CharacterVoiceProfile voiceProfile,
        SkillGemDefinition skillDef,
        out CharacterSkillVoiceLine voiceLine)
    {
        voiceLine = null;
        if (voiceProfile == null || voiceProfile.skillCastLines == null)
            return false;

        for (int i = 0; i < voiceProfile.skillCastLines.Count; i++)
        {
            CharacterSkillVoiceLine candidate = voiceProfile.skillCastLines[i];
            if (candidate == null || candidate.cue == null)
                continue;

            if (candidate.MatchesExactSkill(skillDef))
            {
                voiceLine = candidate;
                return true;
            }
        }

        for (int i = 0; i < voiceProfile.skillCastLines.Count; i++)
        {
            CharacterSkillVoiceLine candidate = voiceProfile.skillCastLines[i];
            if (candidate == null || candidate.cue == null)
                continue;

            if (candidate.MatchesTags(skillDef))
            {
                voiceLine = candidate;
                return true;
            }
        }

        return false;
    }

    bool CanPlaySkillVoiceLine(CharacterSkillVoiceLine voiceLine, float now)
    {
        if (voiceLine == null || voiceLine.cue == null)
            return false;

        if (voiceLine.cooldown > 0f &&
            skillVoiceReadyAt.TryGetValue(voiceLine, out float readyAt) &&
            now < readyAt)
        {
            return false;
        }

        return Random.value <= Mathf.Clamp01(voiceLine.chance);
    }

    bool CanPlayDefaultSkillVoice(CharacterVoiceProfile voiceProfile, float now)
    {
        if (voiceProfile == null || voiceProfile.defaultSkillVoiceCue == null)
            return false;

        if (voiceProfile.defaultSkillVoiceCooldown > 0f && now < defaultSkillVoiceReadyAt)
            return false;

        return Random.value <= Mathf.Clamp01(voiceProfile.defaultSkillVoiceChance);
    }

    void StampSkillVoiceCooldowns(CharacterVoiceProfile voiceProfile, CharacterSkillVoiceLine voiceLine, float now)
    {
        if (voiceProfile != null && voiceProfile.globalSkillVoiceCooldown > 0f)
            nextSkillVoiceAllowedAt = now + voiceProfile.globalSkillVoiceCooldown;

        if (voiceLine != null && voiceLine.cooldown > 0f)
            skillVoiceReadyAt[voiceLine] = now + voiceLine.cooldown;
    }

    void StampDefaultSkillVoiceCooldowns(CharacterVoiceProfile voiceProfile, float now)
    {
        if (voiceProfile != null && voiceProfile.globalSkillVoiceCooldown > 0f)
            nextSkillVoiceAllowedAt = now + voiceProfile.globalSkillVoiceCooldown;

        if (voiceProfile != null && voiceProfile.defaultSkillVoiceCooldown > 0f)
            defaultSkillVoiceReadyAt = now + voiceProfile.defaultSkillVoiceCooldown;
    }

    bool TryPlayEventVoice(CharacterEventVoiceLine voiceLine)
    {
        Transform followTarget = ctx != null ? ctx.transform : transform;
        return CharacterVoicePlayback.TryPlayAttached(voiceLine, followTarget, Vector3.zero, eventVoiceReadyAt);
    }

    void TryPlayLowHpVoice()
    {
        CharacterVoiceProfile voiceProfile = GetVoiceProfile();
        if (voiceProfile == null || healthSystem == null || healthSystem.currentHealth <= 0f)
            return;

        if (!TryGetHealthPercent(out float healthPercent))
            return;

        float threshold = Mathf.Clamp01(voiceProfile.lowHpThreshold);
        if (healthPercent > threshold)
        {
            lowHpVoiceArmed = true;
            return;
        }

        if (!lowHpVoiceArmed)
            return;

        if (TryPlayEventVoice(voiceProfile.lowHpVoice))
            lowHpVoiceArmed = false;
    }

    void RefreshLowHpVoiceArmState()
    {
        CharacterVoiceProfile voiceProfile = GetVoiceProfile();
        if (voiceProfile == null || !TryGetHealthPercent(out float healthPercent))
            return;

        if (healthPercent > Mathf.Clamp01(voiceProfile.lowHpThreshold))
            lowHpVoiceArmed = true;
    }

    bool TryGetHealthPercent(out float healthPercent)
    {
        healthPercent = 1f;

        if (healthSystem == null || healthSystem.maximumHealth <= 0f)
            return false;

        healthPercent = Mathf.Clamp01(healthSystem.currentHealth / healthSystem.maximumHealth);
        return true;
    }

    bool PlayCue(AudioCue cue)
    {
        if (cue == null)
            return false;

        Transform followTarget = ctx != null ? ctx.transform : transform;
        AudioHandle handle = AudioService.Instance.PlayAttached(cue, followTarget, Vector3.zero);
        return handle.IsValid;
    }
}

public static class CharacterVoicePlayback
{
    public static bool TryPlayAttached(
        CharacterEventVoiceLine voiceLine,
        Transform target,
        Vector3 offset,
        Dictionary<CharacterEventVoiceLine, float> readyAt)
    {
        if (target == null || !CanPlay(voiceLine, readyAt, out float now))
            return false;

        AudioHandle handle = AudioService.Instance.PlayAttached(voiceLine.cue, target, offset);
        if (!handle.IsValid)
            return false;

        StampCooldown(voiceLine, readyAt, now);
        return true;
    }

    public static bool TryPlayAtPosition(
        CharacterEventVoiceLine voiceLine,
        Vector3 position,
        Dictionary<CharacterEventVoiceLine, float> readyAt)
    {
        if (!CanPlay(voiceLine, readyAt, out float now))
            return false;

        AudioHandle handle = AudioService.Instance.PlayAtPosition(voiceLine.cue, position);
        if (!handle.IsValid)
            return false;

        StampCooldown(voiceLine, readyAt, now);
        return true;
    }

    static bool CanPlay(
        CharacterEventVoiceLine voiceLine,
        Dictionary<CharacterEventVoiceLine, float> readyAt,
        out float now)
    {
        now = Time.unscaledTime;

        if (voiceLine == null || voiceLine.cue == null)
            return false;

        if (voiceLine.cooldown > 0f &&
            readyAt != null &&
            readyAt.TryGetValue(voiceLine, out float readyTime) &&
            now < readyTime)
        {
            return false;
        }

        return Random.value <= Mathf.Clamp01(voiceLine.chance);
    }

    static void StampCooldown(
        CharacterEventVoiceLine voiceLine,
        Dictionary<CharacterEventVoiceLine, float> readyAt,
        float now)
    {
        if (voiceLine == null || readyAt == null || voiceLine.cooldown <= 0f)
            return;

        readyAt[voiceLine] = now + voiceLine.cooldown;
    }
}
