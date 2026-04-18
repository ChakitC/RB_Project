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
    [SerializeField] private WeaponSystem weaponSystem;
    [SerializeField] private StatusEffectController statusEffectController;
    [SerializeField] private CombatEventBus combatEventBus;
    [SerializeField] private MeleeHitboxTrigger hitboxTrigger;

    readonly HashSet<int> _hitTargetIds = new();
    IDamageable _selfDamageable;

    bool _attackWindowActive;
    string _activeSourceId;
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
        }

        if (hitboxTrigger != null)
            hitboxTrigger.ContactDetected -= OnHitboxContact;

        CloseAttackWindow();
    }

    public bool TryStartMelee(CharacterAnimBrain.MeleeType meleeType)
    {
        ResolveRefs();

        if (ctx == null || stateHub == null || brain == null)
            return false;
        if (!stateHub.CanStartMelee())
            return false;
        if (brain.IsSkillPlaybackActive)
            return false;
        if (!HasValidCombo(meleeType))
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

        brain.PressMelee(meleeType);
        stateHub.ReportMeleeStarted(meleeType);
        return true;
    }

    public void InterruptMelee()
    {
        ResolveRefs();

        CloseAttackWindow();
        brain?.CancelMeleeNow();

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

        _activeSourceId = GetMeleeSourceId();
        _activeAttackId = combatEventBus != null
            ? combatEventBus.CreateAttackId($"{_activeSourceId}:melee")
            : null;
        _activeChainId = combatEventBus != null ? CombatEventBus.NextChainId() : 0;

        var meleeType = brain != null ? brain.CurrentMeleeType : CharacterAnimBrain.MeleeType.Light;
        hitboxTrigger?.Activate(meleeType);
    }

    void OnHitEnd()
    {
        CloseAttackWindow();
    }

    void OnComboEnded()
    {
        CloseAttackWindow();

        if (stateHub != null)
            stateHub.WeaponSM.TryChange(WeaponStateId.Ready);
    }

    void OnHitboxContact(Collider other)
    {
        if (!_attackWindowActive || other == null)
            return;

        var target = other.GetComponentInParent<IDamageable>();
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

        ApplyDamageToTarget(target, finalDamage, BuildKnockback(other));
        NotifyOwnerCombatTriggers(target, finalDamage);
        SpawnDamageNumber(other, finalDamage);
    }

    void ResolveRefs()
    {
        if (!ctx)
            TryGetComponent(out ctx);
        if (!stateHub)
            TryGetComponent(out stateHub);
        if (!brain)
            TryGetComponent(out brain);
        if (!weaponSystem)
            TryGetComponent(out weaponSystem);
        if (!statusEffectController)
            TryGetComponent(out statusEffectController);
        if (!combatEventBus)
            TryGetComponent(out combatEventBus);
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
            _selfDamageable = GetComponentInParent<IDamageable>();
    }

    bool OwnsHitboxCollider(Collider other)
    {
        if (!other)
            return false;

        ResolveRefs();
        return hitboxTrigger != null && hitboxTrigger.IsHitboxCollider(other);
    }

    bool HasValidCombo(CharacterAnimBrain.MeleeType meleeType)
    {
        var combo = ResolveRequestedCombo(meleeType);
        return combo != null && combo.IsValid(out _);
    }

    MeleeComboSO ResolveRequestedCombo(CharacterAnimBrain.MeleeType meleeType)
    {
        var profile = ctx != null && ctx.baseStats != null ? ctx.baseStats.animProfile : null;
        if (profile == null)
            return null;

        var combo = meleeType == CharacterAnimBrain.MeleeType.Light
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

    void ApplyDamageToTarget(IDamageable target, float finalDamage, KnockbackData knockback)
    {
        var attacker = ctx != null ? ctx.gameObject : gameObject;
        var damageContext = new DamageContext(
            finalDamage,
            attacker,
            _activeSourceId,
            _activeAttackId,
            _activeChainId == 0 ? CombatEventBus.NextChainId() : _activeChainId,
            0,
            PassiveEventOrigin.External,
            knockback: knockback);

        target.TakeDamage(in damageContext);
    }

    KnockbackData BuildKnockback(Collider other)
    {
        var activeStep = brain != null ? brain.CurrentMeleeStep : default(MeleeComboSO.Step);
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

    void NotifyOwnerCombatTriggers(IDamageable target, float finalDamage)
    {
        if (target == null)
            return;

        Component targetComponent = target as Component;
        GameObject targetObject = targetComponent != null ? targetComponent.gameObject : null;

        statusEffectController?.NotifyTrigger(EffectTriggerType.OnHit, targetObject);

        if (combatEventBus != null)
        {
            var hitContext = CreateOwnerEventContext(PassiveEventType.Hit, targetObject, finalDamage);
            combatEventBus.Publish(hitContext);
        }

        if (!target.IsAlive)
        {
            statusEffectController?.NotifyTrigger(EffectTriggerType.OnKill, targetObject);

            if (combatEventBus != null)
            {
                var killContext = CreateOwnerEventContext(PassiveEventType.Kill, targetObject, finalDamage);
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
                _activeSourceId,
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
                _activeSourceId,
                _activeAttackId,
                value,
                PassiveEventOrigin.External);
        }

        return combatEventBus.CreateExternalContext(
            type,
            sourceObject,
            targetObject,
            _activeSourceId,
            _activeAttackId,
            value,
            PassiveEventOrigin.External);
    }

    void SpawnDamageNumber(Collider other, float finalDamage)
    {
        if (other == null || VfxSpawner.Instance == null)
            return;

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        if (hitPoint == Vector3.zero)
            hitPoint = other.bounds.center;

        VfxSpawner.Instance.SpawnDamageNumber(hitPoint, finalDamage);
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

    string GetMeleeSourceId()
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
