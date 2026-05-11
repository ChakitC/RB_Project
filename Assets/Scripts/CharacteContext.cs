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
    public WeaponSystem WeaponSystem;
    public CharacterAnimBrain AnimBrain;
    public CharacterAnimDriver AnimDriver;
    public MeleeController MeleeController;
    
    [Header("Visual")] 
    public CharacterContextPartyLoader CharacterLoad;
    public CharacterVisualController Visual;
    public UIManager UIManager;
    
    
    [Header("Modules")]
    
    public LevelSystem levelSystem;
    public HealthSystem HealthSystem;
    public StaminaSystem StaminaSystem;
    public DashSystem DashSystem;
    public CharacterKnockbackMotor KnockbackMotor;
    public PassiveController PassiveController;
    public PlayerPassiveProgress PassiveProgress;
    public SkillUserSystem EnegySystem;
    public Interactor Interactor;
    public CharacterSkillManager SkillManager;

   
    
    [Header("Input Values")]
    public Vector2 moveInput;
    public Vector2 lookInput;
    
    public virtual bool UsesWorldSlow => true;
    public virtual AITargetIdentity TargetIdentity => AITargetIdentity.Generic;
    
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
        WeaponSystem = ResolveActorComponent(WeaponSystem);
        if (Equipment == null && Application.isPlaying && (currentWeapon != null || WeaponSystem != null))
            Equipment = gameObject.AddComponent<CharacterEquipment>();
        AnimBrain = ResolveActorComponent(AnimBrain);
        AnimDriver = ResolveActorComponent(AnimDriver);
        MeleeController = ResolveActorComponent(MeleeController);

        CharacterLoad = ResolveActorComponent(CharacterLoad);
        Visual = ResolveActorComponent(Visual);
        UIManager = ResolveActorComponent(UIManager);

        levelSystem = ResolveActorComponent(levelSystem);
        HealthSystem = ResolveActorComponent(HealthSystem);
        StaminaSystem = ResolveActorComponent(StaminaSystem);
        DashSystem = ResolveActorComponent(DashSystem);
        KnockbackMotor = ResolveActorComponent(KnockbackMotor);
        PassiveController = ResolveActorComponent(PassiveController);
        PassiveProgress = ResolveActorComponent(PassiveProgress);
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
