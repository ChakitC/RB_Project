using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-109)]
[DisallowMultipleComponent]
public sealed class MeleeController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private StateHub stateHub;
    [SerializeField] private CharacterAnimBrain brain;
    private CharacterAnimDriver animDriver;
    [SerializeField] private WeaponSystem weaponSystem;
    [SerializeField] private StatusEffectController statusEffectController;
    [SerializeField] private CombatEventBus combatEventBus;
    [SerializeField] private MeleeHitboxTrigger hitboxTrigger;

    readonly HashSet<int> _hitTargetIds = new();
    IDamageable _selfDamageable;
    readonly MeleeComboSession _session = new();
    MeleeType _currentMeleeType;

    bool _attackWindowActive;
    string _activeDamageSourceId;
    string _activeAttackId;
    ulong _activeChainId;

    void Awake()
    {
        ResolveRefs();
    }

    void OnEnable()
    {
        ResolveRefs();

        if (brain != null)
        {
            brain.MeleeHitStart += OnHitStart;
            brain.MeleeHitEnd += OnHitEnd;
            brain.MeleeComboEnded += OnComboEnded;
            brain.MeleeChainWindowOpened += OnChainWindowOpened;
            brain.MeleeChainWindowClosed += OnChainWindowClosed;
            brain.MeleeStepCompleted += OnStepCompleted;
        }

        if (hitboxTrigger != null)
            hitboxTrigger.ContactDetected += OnHitboxContact;
    }

    void OnDisable()
    {
        if (brain != null)
        {
            brain.MeleeHitStart -= OnHitStart;
            brain.MeleeHitEnd -= OnHitEnd;
            brain.MeleeComboEnded -= OnComboEnded;
            brain.MeleeChainWindowOpened -= OnChainWindowOpened;
            brain.MeleeChainWindowClosed -= OnChainWindowClosed;
            brain.MeleeStepCompleted -= OnStepCompleted;
        }

        if (hitboxTrigger != null)
            hitboxTrigger.ContactDetected -= OnHitboxContact;

        CloseAttackWindow();
    }

    public void PressMelee(MeleeType meleeType)
    {
        ResolveRefs();
        if (brain == null) return;

        if (brain.IsMeleePlaybackActive)
        {
            if (meleeType != _currentMeleeType)
                return;

            var action = _session.QueuePress();
            if (action == MeleeSessionAction.Advance)
                brain.AdvanceMeleeStep(_session.CurrentStep, _session.CurrentStepIndex);
            return;
        }

        TryStartMelee(meleeType);
    }

    public bool TryStartMelee(MeleeType meleeType)
    {
        ResolveRefs();

        if (ctx == null || stateHub == null || brain == null)
            return false;
        if (!stateHub.CanStartMelee())
            return false;
        if (brain.IsSkillPlaybackActive)
            return false;

        var combo = ResolveRequestedCombo(meleeType);
        if (combo == null || !combo.IsValid(out _))
            return false;

        if (weaponSystem != null)
        {
            if (weaponSystem.IsReloading)
            {
                if (!CanInterruptReload())
                    return false;

                weaponSystem.CancelReload();
            }

            weaponSystem.SetFiring(false);
        }

        stateHub.SetFireHeld(false);
        stateHub.WeaponSM.TryChange(WeaponStateId.Melee);

        _currentMeleeType = meleeType;
        _session.Start(combo);

        if (!brain.TryStartMeleePlayback(combo, _session.CurrentStep, _session.CurrentStepIndex))
        {
            _session.Clear();
            return false;
        }

        stateHub.ReportMeleeStarted(meleeType);
        return true;
    }

    public void InterruptMelee()
    {
        ResolveRefs();

        _session.Clear();
        CloseAttackWindow();
        animDriver?.CancelMeleeNow();

        if (stateHub != null && stateHub.WeaponSM.CurrentId == WeaponStateId.Melee)
            stateHub.WeaponSM.TryChange(WeaponStateId.Ready);
    }

    public static bool IsCombatOnlyHitbox(Collider other)
    {
        if (!other || !other.isTrigger)
            return false;

        var controller = other.GetComponentInParent<MeleeController>();
        return controller != null && controller.OwnsHitboxCollider(other);
    }

    void OnHitStart()
    {
        _attackWindowActive = true;
        _hitTargetIds.Clear();

        _activeDamageSourceId = GetMeleeDamageSourceId();
        _activeAttackId = combatEventBus != null
            ? combatEventBus.CreateAttackId($"{_activeDamageSourceId}:melee")
            : null;
        _activeChainId = combatEventBus != null ? CombatEventBus.NextChainId() : 0;

        hitboxTrigger?.Activate(_currentMeleeType);
    }

    void OnHitEnd()
    {
        CloseAttackWindow();
    }

    void OnComboEnded()
    {
        _session.Clear();
        CloseAttackWindow();

        if (stateHub != null)
            stateHub.WeaponSM.TryChange(WeaponStateId.Ready);
    }

    void OnChainWindowOpened()
    {
        if (!_session.IsActive) return;
        var action = _session.NotifyChainWindowOpened();
        if (action == MeleeSessionAction.Advance && brain != null)
            brain.AdvanceMeleeStep(_session.CurrentStep, _session.CurrentStepIndex);
    }

    void OnChainWindowClosed()
    {
        _session.NotifyChainWindowClosed();
    }

    void OnStepCompleted()
    {
        if (!_session.IsActive) return;
        var action = _session.NotifyStepCompleted();
        if (action == MeleeSessionAction.Advance && brain != null)
            brain.AdvanceMeleeStep(_session.CurrentStep, _session.CurrentStepIndex);
        else if (action == MeleeSessionAction.Complete && brain != null)
            brain.CompleteMeleePlayback();
    }

    void OnHitboxContact(Collider other)
    {
        if (!_attackWindowActive || other == null)
            return;

        var target = DamageableResolver.ResolveFrom(other);
        if (target == null || !target.IsAlive)
            return;
        if (IsSelfTarget(target))
            return;

        int targetKey = GetTargetKey(target);
        if (!_hitTargetIds.Add(targetKey))
            return;

        float finalDamage = CalculateDamage(target, other);
        if (finalDamage <= 0f)
            return;

        DamageResult result = ApplyDamageToTarget(target, finalDamage, BuildKnockback(other));
        if (!result.Applied)
            return;

        NotifyOwnerCombatTriggers(target, result.AppliedDamage, result.Killed);
        SpawnDamageNumber(other, target, result.AppliedDamage);
    }

    void ResolveRefs()
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
        if (!stateHub && ctx != null)
            stateHub = ctx.GetComponentInChildren<StateHub>(true);

        if (!brain && ctx != null)
            brain = ctx.AnimBrain;
        if (!brain)
            TryGetComponent(out brain);
        if (!brain && ctx != null)
            brain = ctx.GetComponentInChildren<CharacterAnimBrain>(true);

        if (!animDriver && ctx != null)
            animDriver = ctx.AnimDriver;
        if (!animDriver)
            TryGetComponent(out animDriver);
        if (!animDriver && ctx != null)
            animDriver = ctx.GetComponentInChildren<CharacterAnimDriver>(true);

        if (!weaponSystem && ctx != null)
            weaponSystem = ctx.WeaponSystem;
        if (!weaponSystem)
            TryGetComponent(out weaponSystem);
        if (!weaponSystem && ctx != null)
            weaponSystem = ctx.GetComponentInChildren<WeaponSystem>(true);

        if (!statusEffectController && ctx != null)
            statusEffectController = ctx.GetComponentInChildren<StatusEffectController>(true);
        if (!statusEffectController)
            TryGetComponent(out statusEffectController);

        if (!combatEventBus && ctx != null)
            combatEventBus = ctx.CombatEventBus;
        if (!combatEventBus)
            TryGetComponent(out combatEventBus);
        if (!combatEventBus && ctx != null)
            combatEventBus = ctx.GetComponentInChildren<CombatEventBus>(true);

        if (!hitboxTrigger)
            hitboxTrigger = GetComponentInChildren<MeleeHitboxTrigger>(true);

        if (ctx != null)
        {
            ctx.MeleeController = this;
            if (ctx.HealthSystem != null)
                _selfDamageable = ctx.HealthSystem;
        }

        if (_selfDamageable == null)
            _selfDamageable = GetComponent<IDamageable>();
        if (_selfDamageable == null)
            _selfDamageable = DamageableResolver.ResolveFrom(transform);
    }

    bool OwnsHitboxCollider(Collider other)
    {
        if (!other)
            return false;

        ResolveRefs();
        return hitboxTrigger != null && hitboxTrigger.IsHitboxCollider(other);
    }

    bool HasValidCombo(MeleeType meleeType)
    {
        var combo = ResolveRequestedCombo(meleeType);
        return combo != null && combo.IsValid(out _);
    }

    MeleeComboSO ResolveRequestedCombo(MeleeType meleeType)
    {
        var profile = ctx != null && ctx.baseStats != null ? ctx.baseStats.animProfile : null;
        if (profile == null)
            return null;

        var combo = meleeType == MeleeType.Light
            ? profile.lightCombo
            : profile.heavyCombo;

        return combo != null ? combo : profile.meleeCombo;
    }

    bool CanInterruptReload()
    {
        var profile = ctx != null && ctx.baseStats != null ? ctx.baseStats.animProfile : null;
        return profile == null || profile.meleeCanInterruptReload;
    }

    float CalculateDamage(IDamageable target, Collider other)
    {
        var activeWeapon = weaponSystem != null ? weaponSystem.CurrentWeapon : (ctx != null ? ctx.currentWeapon : null);

        float baseDamage = ctx != null && ctx.StatsHub != null ? ctx.StatsHub.GetSkillBaseDamage() : 0f;
        float critRate = ctx != null && ctx.StatsHub != null ? ctx.StatsHub.GetCritRatePercent(activeWeapon) : 0f;
        float critMult = ctx != null && ctx.StatsHub != null ? ctx.StatsHub.GetCritMultiplier(activeWeapon) : 1f;
        float armor = target is IHasArmor armorTarget ? armorTarget.Armor : 0f;

        float distance = 0f;
        if (other != null)
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            distance = Vector3.Distance(transform.position, hitPoint);
        }

        return DamageCalculator.CalculateFinalDamage(
            WeaponType.Melee,
            distance,
            baseDamage,
            critRate,
            critMult,
            armor);
    }

    DamageResult ApplyDamageToTarget(IDamageable target, float finalDamage, KnockbackData knockback)
    {
        var attacker = ctx != null ? ctx.gameObject : gameObject;
        var damageContext = new DamageContext(
            finalDamage,
            attacker,
            _activeDamageSourceId,
            _activeAttackId,
            _activeChainId == 0 ? CombatEventBus.NextChainId() : _activeChainId,
            0,
            PassiveEventOrigin.External,
            knockback: knockback,
            stagger: BuildStaggerPayload());

        return target.TakeDamage(in damageContext);
    }

    StaggerPayload BuildStaggerPayload()
    {
        var activeStep = _session.IsActive ? _session.CurrentStep : default(MeleeComboSO.Step);
        float staggerPower = activeStep.staggerPower;
        if (staggerPower <= 0f && ctx != null && ctx.StatsHub != null)
            staggerPower = ctx.StatsHub.GetSkillBaseDamage() * 0.5f;

        return new StaggerPayload(staggerPower, 1f, _activeDamageSourceId);
    }

    KnockbackData BuildKnockback(Collider other)
    {
        var activeStep = _session.IsActive ? _session.CurrentStep : default(MeleeComboSO.Step);
        if (!activeStep.applyKnockback)
            return default(KnockbackData);

        Vector3 hitPoint = other != null ? other.ClosestPoint(transform.position) : transform.position;
        KnockbackSettings settings = activeStep.ToKnockbackSettings();
        KnockbackBuildContext context = new KnockbackBuildContext(
            transform.position,
            hitPoint,
            transform.forward);

        return KnockbackFactory.TryBuild(in settings, in context, out KnockbackData knockback)
            ? knockback
            : default;
    }

    void NotifyOwnerCombatTriggers(IDamageable target, float appliedDamage, bool killed)
    {
        if (target == null)
            return;

        Component targetComponent = target as Component;
        GameObject targetObject = targetComponent != null ? targetComponent.gameObject : null;

        statusEffectController?.NotifyTrigger(EffectTriggerType.OnHit, targetObject);

        if (combatEventBus != null)
        {
            var hitContext = CreateOwnerEventContext(PassiveEventType.Hit, targetObject, appliedDamage);
            combatEventBus.Publish(hitContext);
        }

        if (killed)
        {
            statusEffectController?.NotifyTrigger(EffectTriggerType.OnKill, targetObject);

            if (combatEventBus != null)
            {
                var killContext = CreateOwnerEventContext(PassiveEventType.Kill, targetObject, appliedDamage);
                combatEventBus.Publish(killContext);
            }
        }
    }

    PassiveEventContext CreateOwnerEventContext(PassiveEventType type, GameObject targetObject, float value)
    {
        GameObject sourceObject = ctx != null ? ctx.gameObject : gameObject;

        if (_activeChainId != 0)
        {
            var parent = new PassiveEventContext(
                PassiveEventType.None,
                sourceObject,
                sourceObject,
                targetObject,
                _activeDamageSourceId,
                _activeAttackId,
                value,
                Time.timeAsDouble,
                _activeChainId,
                0,
                PassiveEventOrigin.External,
                null,
                null);

            return combatEventBus.CreateChildContext(
                parent,
                type,
                sourceObject,
                targetObject,
                _activeDamageSourceId,
                _activeAttackId,
                value,
                PassiveEventOrigin.External);
        }

        return combatEventBus.CreateExternalContext(
            type,
            sourceObject,
            targetObject,
            _activeDamageSourceId,
            _activeAttackId,
            value,
            PassiveEventOrigin.External);
    }

    void SpawnDamageNumber(Collider other, IDamageable target, float finalDamage)
    {
        if (other == null || VfxSpawner.Instance == null)
            return;

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        if (hitPoint == Vector3.zero)
            hitPoint = other.bounds.center;

        VfxSpawner.Instance.SpawnDamageNumber(hitPoint, finalDamage, target);
    }

    void CloseAttackWindow()
    {
        _attackWindowActive = false;
        _hitTargetIds.Clear();
        hitboxTrigger?.Deactivate();
    }

    int GetTargetKey(IDamageable target)
    {
        if (target is Component component)
            return component.GetInstanceID();

        return target.GetHashCode();
    }

    bool IsSelfTarget(IDamageable target)
    {
        if (target == null)
            return false;

        return ReferenceEquals(target, ResolveSelfDamageable());
    }

    IDamageable ResolveSelfDamageable()
    {
        if (_selfDamageable != null)
            return _selfDamageable;

        ResolveRefs();
        return _selfDamageable;
    }

    string GetMeleeDamageSourceId()
    {
        if (weaponSystem != null && weaponSystem.CurrentWeaponInstance != null &&
            !string.IsNullOrWhiteSpace(weaponSystem.CurrentWeaponInstance.instanceId))
        {
            return $"weapon:{weaponSystem.CurrentWeaponInstance.instanceId}:melee";
        }

        var activeWeapon = weaponSystem != null ? weaponSystem.CurrentWeapon : (ctx != null ? ctx.currentWeapon : null);
        string weaponName = activeWeapon ? activeWeapon.name : "unarmed";
        return $"melee:{weaponName}";
    }
}
