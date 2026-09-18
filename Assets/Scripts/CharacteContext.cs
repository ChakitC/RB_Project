using System;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public abstract class CharacteContext : MonoBehaviour
{
    [Header("Character_BaseStatus")]    
    public CharacterStats baseStats;
    public GunConfig currentWeapon;
    
    [Header("Core")] 
    public StateHub stateHub;
    public StatsHub StatsHub;
    public CombatEventBus CombatEventBus;
    public Rigidbody rb;
    public CharacterController cc;

    [Header("Common Modules")]
    public AITargetInfo TargetInfo;
    public CharacterEquipment Equipment;
    public AccessoryLoadout AccessoryLoadout;
    public WeaponSystem WeaponSystem;
    public CharacterAnimBrain AnimBrain;
    public CharacterAnimDriver AnimDriver;
    public CharacterPairOffsetApplier PairOffsetApplier;
    public MeleeController MeleeController;
    public ThirdPersonAimRigController AimRig;
    
    [Header("Visual")] 
    public CharacterContextPartyLoader CharacterLoad;
    public CharacterVisualController Visual;
    public CharacterVisibilityController Visibility;
    public UIManager UIManager;
    public CharacterColliderRefs ColliderRefs;
    
    
    [Header("Modules")]
    public StatusEffectController StatusEffects;
    public LevelSystem levelSystem;
    public HealthSystem HealthSystem;
    public StaminaSystem StaminaSystem;
    public DashSystem DashSystem;
    public CharacterKnockbackMotor KnockbackMotor;
    public DefensiveBlockAttack DefensiveBlockAttack;
    public DefensiveBlockController DefensiveBlock;
    public FieldAllyMember FieldAllyMember;
    public CharacterVerticalMotor VerticalMotor;
    public PassiveController PassiveController;
    public CharacterActiveSkillProgress ActiveSkillProgress;
    public SkillUserSystem EnegySystem;
    public Interactor Interactor;
    public CharacterSkillManager SkillManager;

   
    
    [Header("Input Values")]
    public Vector2 moveInput;
    public Vector2 lookInput;
    
    int _worldSlowExemptionCount;
    int _lifeGeneration;

    /// <summary>
    /// Identifies the current enabled lifetime of this actor. Object pools reuse the same Unity
    /// object and instance id, so delayed combat work must pair its reference with this value.
    /// </summary>
    public int LifeGeneration => _lifeGeneration;

    public virtual bool UsesWorldSlow => _worldSlowExemptionCount <= 0;

    protected virtual void OnEnable()
    {
        unchecked
        {
            _lifeGeneration++;
            if (_lifeGeneration == 0)
                _lifeGeneration = 1;
        }

        CharacterContextRegistry.Register(this);
    }

    protected virtual void OnDisable()
    {
        CharacterContextRegistry.Unregister(this);
    }

    public void PushWorldSlowExemption()
    {
        _worldSlowExemptionCount++;
    }

    public void PopWorldSlowExemption()
    {
        if (_worldSlowExemptionCount > 0)
            _worldSlowExemptionCount--;
    }
    public virtual AITargetIdentity TargetIdentity => AITargetIdentity.Generic;
    public virtual bool ForceInfiniteReserveAmmo => false;
    public virtual bool UsesPersistentProgression =>
        TargetIdentity == AITargetIdentity.Player || TargetIdentity == AITargetIdentity.Companion;
    public virtual bool UsesPersistentLoadouts =>
        TargetIdentity == AITargetIdentity.Player || TargetIdentity == AITargetIdentity.Companion;
    public virtual bool ParticipatesInPartyRuntime =>
        TargetIdentity == AITargetIdentity.Player || TargetIdentity == AITargetIdentity.Companion;
    public virtual bool PreservesOwnedVfxDuringRoomTransition => ParticipatesInPartyRuntime;
    public virtual bool CanCollectPickups =>
        TargetIdentity == AITargetIdentity.Player || TargetIdentity == AITargetIdentity.Companion;
    public virtual bool AutoCreatesAimRig =>
        TargetIdentity == AITargetIdentity.Player || TargetIdentity == AITargetIdentity.Companion;
    public virtual bool AutoCreatesRuntimeEquipment => true;
    
    public float baseDamage => baseStats != null ? baseStats.Damage : 0f;
    public float basearmor => baseStats != null ? baseStats.armor : 0f;
    public float basemaxHealth => baseStats != null ? baseStats.maxHP : 0f;
    public float basecritRate => baseStats != null ? baseStats.critRate : 0f;
    public float basecritMultiplier => baseStats != null ? baseStats.critMultiplier : 1f;
    public float baseStamina => baseStats != null ? baseStats.maxStamina : 0f;
    public float baseEnagy => baseStats != null ? baseStats.Enagy : 0f;
    public float baseSpeed => baseStats != null ? baseStats.speed : 0f;
    
    public float SpeedDown => baseStats != null ? baseStats.speedDown : 0f;

    public virtual void ResolveReferences()
    {
        stateHub = ResolveActorComponent(stateHub);
        StatsHub = ResolveActorComponent(StatsHub);
        CombatEventBus = ResolveActorComponent(CombatEventBus);
        rb = ResolveActorComponent(rb);
        cc = ResolveActorComponent(cc);

        TargetInfo = ResolveActorComponent(TargetInfo);
        Equipment = ResolveActorComponent(Equipment);
        AccessoryLoadout = ResolveActorComponent(AccessoryLoadout);
        if (AccessoryLoadout == null &&
            Application.isPlaying &&
            UsesPersistentLoadouts)
        {
            AccessoryLoadout = gameObject.AddComponent<AccessoryLoadout>();
        }
        WeaponSystem = ResolveActorComponent(WeaponSystem);
        if (Equipment == null && Application.isPlaying && AutoCreatesRuntimeEquipment &&
            (currentWeapon != null || WeaponSystem != null))
            Equipment = gameObject.AddComponent<CharacterEquipment>();
        AnimBrain = ResolveActorComponent(AnimBrain);
        AnimDriver = ResolveActorComponent(AnimDriver);
        PairOffsetApplier = ResolveActorComponent(PairOffsetApplier);
        if (PairOffsetApplier == null &&
            Application.isPlaying &&
            baseStats != null &&
            baseStats.animProfile != null &&
            baseStats.animProfile.pairOffsetProfiles != null)
        {
            PairOffsetApplier = gameObject.AddComponent<CharacterPairOffsetApplier>();
        }
        MeleeController = ResolveActorComponent(MeleeController);
        AimRig = ResolveActorComponent(AimRig);
        if (AimRig == null &&
            Application.isPlaying &&
            AutoCreatesAimRig)
        {
            AimRig = gameObject.AddComponent<ThirdPersonAimRigController>();
        }

        CharacterLoad = ResolveActorComponent(CharacterLoad);
        Visual = ResolveActorComponent(Visual);
        Visibility = ResolveActorComponent(Visibility);
        UIManager = ResolveActorComponent(UIManager);
        ColliderRefs = ResolveActorComponent(ColliderRefs);

        StatusEffects = ResolveActorComponent(StatusEffects);
        levelSystem = ResolveActorComponent(levelSystem);
        HealthSystem = ResolveActorComponent(HealthSystem);
        StaminaSystem = ResolveActorComponent(StaminaSystem);
        DashSystem = ResolveActorComponent(DashSystem);
        KnockbackMotor = ResolveActorComponent(KnockbackMotor);
        DefensiveBlockAttack = ResolveActorComponent(DefensiveBlockAttack);
        DefensiveBlock = ResolveActorComponent(DefensiveBlock);
        FieldAllyMember = ResolveActorComponent(FieldAllyMember);
        VerticalMotor = ResolveActorComponent(VerticalMotor);
        PassiveController = ResolveActorComponent(PassiveController);
        ActiveSkillProgress = ResolveActorComponent(ActiveSkillProgress);
        if (ActiveSkillProgress == null &&
            Application.isPlaying &&
            UsesPersistentProgression)
        {
            ActiveSkillProgress = gameObject.AddComponent<CharacterActiveSkillProgress>();
        }
        EnegySystem = ResolveActorComponent(EnegySystem);
        Interactor = ResolveActorComponent(Interactor);
        SkillManager = ResolveActorComponent(SkillManager);
    }

    protected T ResolveActorComponent<T>(T current, bool includeChildren = true) where T : Component
    {
        if (current != null)
            return current;

        if (TryGetComponent(out T localComponent))
            return localComponent;

        if (includeChildren)
        {
            T childComponent = GetComponentInChildren<T>(true);
            if (childComponent != null)
                return childComponent;
        }

        return GetComponentInParent<T>();
    }

    public float GetMoveSpeedForCurrentLifeState()
    {
        ResolveReferences();

        float moveSpeed = StatsHub ? StatsHub.GetMoveSpeed() : baseSpeed;

        if (stateHub != null && stateHub.Isdown)
            return Mathf.Max(0f, SpeedDown);

        return moveSpeed;
    }

    public virtual bool ShouldBeInMoveState()
    {
        return cc != null && MoveCheck.IsMoveIntent(this);
    }

    
    
}
