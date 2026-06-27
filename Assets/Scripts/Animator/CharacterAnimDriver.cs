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
        if (CanIssueCommand(nameof(SetExternalStatusLocomotion)))
            brain.SetExternalStatusLocomotion(reaction);
    }

    public void SetStaggerStatusLocomotion(ImpactReactionKind reaction)
    {
        if (CanIssueCommand(nameof(SetStaggerStatusLocomotion)))
            brain.SetStaggerStatusLocomotion(reaction);
    }

    public void InterruptActivePlaybackForExternalControlLoss()
    {
        if (CanIssueCommand(nameof(InterruptActivePlaybackForExternalControlLoss)))
            brain.InterruptActivePlaybackForExternalControlLoss();
    }

    public void PressMelee(CharacterAnimBrain.MeleeType type)
    {
        if (CanIssueCommand(nameof(PressMelee)))
            brain.PressMelee(type);
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
