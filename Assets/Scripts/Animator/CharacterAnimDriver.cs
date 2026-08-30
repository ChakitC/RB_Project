using System.Collections.Generic;
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

    bool _missingBrainWarningLogged;
    bool _usesHealthLifeFallback;

    StatusEffectController _statusEffects;
    StatusLocomotionPose _externalStatusLocomotionPose;
    StatusLocomotionPose _staggerStatusLocomotionPose;
    readonly StatusLocomotionIntentResolver _statusResolver = new();

    public CharacterAnimBrain Brain => brain;

    void Awake()
    {
        ResolveReferences();
    }

    void OnEnable()
    {
        ResolveReferences();
        _usesHealthLifeFallback = false;

        if (hub != null)
        {
            hub.ShotFired += OnShotFired;
            hub.ReloadStarted += OnReloadStarted;
            hub.DashStarted += OnDashStarted;
            hub.LifeStateChanged += OnLifeStateChanged;
        }
        else if (_HealthSystem != null)
        {
            _usesHealthLifeFallback = true;
            _HealthSystem.CharacterDead += OnCharacterDead;
            _HealthSystem.CharacterDown += OnCharacterDown;
            _HealthSystem.CharacterRevive += OnCharacterRevive;
        }

        if (_statusEffects != null)
            _statusEffects.EffectsChanged += OnEffectsChanged;
    }

    void OnDisable()
    {
        if (hub != null)
        {
            hub.ShotFired -= OnShotFired;
            hub.ReloadStarted -= OnReloadStarted;
            hub.DashStarted -= OnDashStarted;
        }

        if (_usesHealthLifeFallback)
        {
            if (_HealthSystem != null)
            {
                _HealthSystem.CharacterDead -= OnCharacterDead;
                _HealthSystem.CharacterDown -= OnCharacterDown;
                _HealthSystem.CharacterRevive -= OnCharacterRevive;
            }
        }
        else if (hub != null)
        {
            hub.LifeStateChanged -= OnLifeStateChanged;
        }

        _usesHealthLifeFallback = false;

        if (_statusEffects != null)
            _statusEffects.EffectsChanged -= OnEffectsChanged;

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

        if (!_statusEffects && CTX != null)
            _statusEffects = CTX.StatusEffects;
        if (!_statusEffects)
            TryGetComponent(out _statusEffects);
        if (!_statusEffects && CTX != null)
            _statusEffects = CTX.GetComponentInChildren<StatusEffectController>(true);
    }

    void LateUpdate()
    {
        if (hub == null || brain == null) return;

        brain.MoveSpeed01 = hub.MoveSpeed01;
        brain.MoveDirLocal = hub.MoveDirLocal;
        brain.SetFireHoldContext(hub.DesiredFireHeld, hub.CanShoot());
    }

    void OnLifeStateChanged(LifeStateId from, LifeStateId to)
    {
        switch (to)
        {
            case LifeStateId.Dead:
                brain?.PlayDead();
                break;
            case LifeStateId.Down:
                brain?.SetDowned(true);
                break;
            case LifeStateId.Alive:
                brain?.SetDowned(false);
                break;
        }
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
        if (brain != null &&
            _WeaponSystem != null &&
            _WeaponSystem.CurrentFiringMode == FiringMode.Semi)
        {
            brain.NotifyShotFired();
        }
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

    public bool PlayKnockback(KnockbackData knockback)
    {
        return CanIssueCommand(nameof(PlayKnockback)) && brain.PlayKnockback(knockback);
    }

    public void StopKnockbackPlayback()
    {
        if (CanIssueCommand(nameof(StopKnockbackPlayback)))
            brain.StopKnockbackPlayback();
    }

    public void SetExternalStatusLocomotion(ImpactReactionKind reaction)
    {
        StatusLocomotionPose desired = StatusLocomotionIntentResolver.MapReaction(reaction);
        if (_externalStatusLocomotionPose == desired)
            return;

        _externalStatusLocomotionPose = desired;
        RefreshStatusIntent();
    }

    public void SetStaggerStatusLocomotion(ImpactReactionKind reaction)
    {
        StatusLocomotionPose desired = StatusLocomotionIntentResolver.MapReaction(reaction);
        if (_staggerStatusLocomotionPose == desired)
            return;

        _staggerStatusLocomotionPose = desired;
        RefreshStatusIntent();
    }

    public void SetStaggerStatusLocomotionPose(StatusLocomotionPose pose)
    {
        if (_staggerStatusLocomotionPose == pose)
            return;

        _staggerStatusLocomotionPose = pose;
        RefreshStatusIntent();
    }

    void OnEffectsChanged()
    {
        RefreshStatusIntent();
    }

    void RefreshStatusIntent()
    {
        if (!CanIssueCommand(nameof(RefreshStatusIntent)))
            return;

        var activeEffects = _statusEffects != null ? _statusEffects.ActiveEffects : null;
        StatusLocomotionPose intent = _statusResolver.Resolve(
            activeEffects,
            _externalStatusLocomotionPose,
            _staggerStatusLocomotionPose);

        brain.SetStatusLocomotionIntent(intent);
    }

    /// <summary>
    /// Starts the one-shot Special Point reaction. Like every other playback command, this goes
    /// through the Driver: gameplay never talks to the Brain directly.
    /// </summary>
    public bool TryPlaySpecialReaction(int requestId, float missingClipFallbackSeconds)
    {
        return CanIssueCommand(nameof(TryPlaySpecialReaction)) &&
               brain.TryPlaySpecialReaction(requestId, missingClipFallbackSeconds);
    }

    public void CancelSpecialReaction(int requestId)
    {
        if (CanIssueCommand(nameof(CancelSpecialReaction)))
            brain.CancelSpecialReaction(requestId);
    }

    public bool TryPlayStageIntro()
    {
        return CanIssueCommand(nameof(TryPlayStageIntro)) && brain.TryPlayStageIntro();
    }

    public void StopStageIntro()
    {
        if (CanIssueCommand(nameof(StopStageIntro)))
            brain.StopStageIntro();
    }

    public void InterruptActivePlaybackForExternalControlLoss()
    {
        if (CanIssueCommand(nameof(InterruptActivePlaybackForExternalControlLoss)))
            brain.InterruptActivePlaybackForExternalControlLoss();
    }

    public void PressMelee(MeleeType type)
    {
        if (!CanIssueCommand(nameof(PressMelee)))
            return;

        var meleeController = CTX != null ? CTX.MeleeController : null;
        if (meleeController != null)
            meleeController.PressMelee(type);
    }

    public void CancelMeleeNow()
    {
        if (CanIssueCommand(nameof(CancelMeleeNow)))
            brain.CancelMeleeNow();
    }

    public void StopReloadAction()
    {
        if (CanIssueCommand(nameof(StopReloadAction)))
            brain.StopReloadAction();
    }

    public void InvalidateAnimationBinding()
    {
        if (CanIssueCommand(nameof(InvalidateAnimationBinding)))
            brain.InvalidateAnimationBinding();
    }

    public void SetAnimProfileOverride(CharacterAnimProfileSO profile)
    {
        if (CanIssueCommand(nameof(SetAnimProfileOverride)))
            brain.SetAnimProfileOverride(profile);
    }

    public void ClearAnimProfileOverride()
    {
        if (CanIssueCommand(nameof(ClearAnimProfileOverride)))
            brain.ClearAnimProfileOverride();
    }

    public void PlaySkill()
    {
        if (CanIssueCommand(nameof(PlaySkill)))
            brain.PlaySkill();
    }

    public void PlaySkill(SkillGemDefinition skillDef)
    {
        if (CanIssueCommand(nameof(PlaySkill)))
            brain.PlaySkill(skillDef);
    }

    public bool TryPlaySkill(int requestId, float castPointNormalized)
    {
        return CanIssueCommand(nameof(TryPlaySkill)) &&
               brain.TryPlaySkill(requestId, castPointNormalized);
    }

    public bool TryPlaySkill(int requestId, SkillGemDefinition skillDef, float castPointNormalized)
    {
        return CanIssueCommand(nameof(TryPlaySkill)) &&
               brain.TryPlaySkill(requestId, skillDef, castPointNormalized);
    }

    public bool TryPlaySkill(
        int requestId,
        SkillGemDefinition skillDef,
        float castPointNormalized,
        IReadOnlyList<CombatTimelineEventName> timelineEventNames)
    {
        return CanIssueCommand(nameof(TryPlaySkill)) &&
               brain.TryPlaySkill(requestId, skillDef, castPointNormalized, timelineEventNames);
    }

    public bool TryPlaySkill(
        int requestId,
        SkillGemDefinition skillDef,
        float castPointNormalized,
        IReadOnlyList<CombatTimelineEventName> timelineEventNames,
        bool usePlanarRootMotion)
    {
        return CanIssueCommand(nameof(TryPlaySkill)) &&
               brain.TryPlaySkill(
                   requestId,
                   skillDef,
                   castPointNormalized,
                   timelineEventNames,
                   usePlanarRootMotion);
    }

    public bool TryPlayUtilityWarpOut(int requestId)
    {
        return CanIssueCommand(nameof(TryPlayUtilityWarpOut)) &&
               brain.TryPlayUtilityWarpOut(requestId);
    }

    public void CancelSkillCastRequest(int requestId)
    {
        if (CanIssueCommand(nameof(CancelSkillCastRequest)))
            brain.CancelSkillCastRequest(requestId);
    }

    public void CancelUtilityCastRequest(int requestId)
    {
        if (CanIssueCommand(nameof(CancelUtilityCastRequest)))
            brain.CancelUtilityCastRequest(requestId);
    }

    public bool TryAcquirePreCastHold(
        int requestId,
        float speedMultiplier,
        float safetyMarginNormalized,
        out SkillPreCastHoldHandle handle)
    {
        handle = default;
        return CanIssueCommand(nameof(TryAcquirePreCastHold)) &&
               brain.TryAcquirePreCastHold(
                   requestId,
                   speedMultiplier,
                   safetyMarginNormalized,
                   out handle);
    }

    public void ReleasePreCastHold(SkillPreCastHoldHandle handle)
    {
        if (CanIssueCommand(nameof(ReleasePreCastHold)))
            brain.ReleasePreCastHold(handle);
    }

    public bool TryPlayChainSkill(
        int requestId,
        SkillGemDefinition skillDef,
        float castPointNormalized,
        bool requestAdvanceMoment,
        float advancePointNormalized)
    {
        return CanIssueCommand(nameof(TryPlayChainSkill)) &&
               brain.TryPlayChainSkill(
                   requestId,
                   skillDef,
                   castPointNormalized,
                   requestAdvanceMoment,
                   advancePointNormalized);
    }

    public bool TryPlayChainSkill(
        int requestId,
        SkillGemDefinition skillDef,
        float castPointNormalized,
        bool requestAdvanceMoment,
        float advancePointNormalized,
        bool usePlanarRootMotion)
    {
        return CanIssueCommand(nameof(TryPlayChainSkill)) &&
               brain.TryPlayChainSkill(
                   requestId,
                   skillDef,
                   castPointNormalized,
                   requestAdvanceMoment,
                   advancePointNormalized,
                   usePlanarRootMotion);
    }

    public bool TryPlayChainCutscene(int requestId, CutsceneDef cutsceneDef)
    {
        return CanIssueCommand(nameof(TryPlayChainCutscene)) &&
               brain.TryPlayChainCutscene(requestId, cutsceneDef);
    }

    public bool TryPlayChainCutscene(int requestId, Animancer.ClipTransition clip)
    {
        return CanIssueCommand(nameof(TryPlayChainCutscene)) &&
               brain.TryPlayChainCutscene(requestId, clip);
    }

    public bool TryPlayChainUtilityWarpOut(int requestId)
    {
        return CanIssueCommand(nameof(TryPlayChainUtilityWarpOut)) &&
               brain.TryPlayChainUtilityWarpOut(requestId);
    }

    public bool TryPlayChainUtilityWarpIn(int requestId)
    {
        return CanIssueCommand(nameof(TryPlayChainUtilityWarpIn)) &&
               brain.TryPlayChainUtilityWarpIn(requestId);
    }

    public void CancelChainPlaybackRequest(int requestId)
    {
        if (CanIssueCommand(nameof(CancelChainPlaybackRequest)))
            brain.CancelChainPlaybackRequest(requestId);
    }

    bool CanIssueCommand(string commandName)
    {
        if (brain != null)
            return true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!_missingBrainWarningLogged)
        {
            _missingBrainWarningLogged = true;
            Debug.LogWarning(
                $"[{nameof(CharacterAnimDriver)}] Cannot issue animation command '{commandName}' because no {nameof(CharacterAnimBrain)} is resolved.",
                this);
        }
#endif
        return false;
    }
}
