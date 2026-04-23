using System;
using UnityEngine;
[DefaultExecutionOrder(-100)]
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
    public WeaponSystem WeaponSystem;
    public CharacterAnimBrain AnimBrain;
    public CharacterAnimDriver AnimDriver;
    public MeleeController MeleeController;
    
    [Header("Visual")] 
    public CharacterContextPartyLoader CharacterLoad;
    public PlayerVisual Visual;
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
    
    
    public float baseDamage => baseStats.Damage;
    public float basearmor => baseStats.armor;
    public float basemaxHealth => baseStats.maxHP;
    public float basecritRate => baseStats.critRate;
    public float basecritMultiplier => baseStats.critMultiplier;
    public float baseStamina => baseStats.maxStamina;
    public float baseEnagy => baseStats.Enagy;
    public float baseSpeed => baseStats.speed;
    
    public float SpeedDown => baseStats.speedDown;

    public float GetMoveSpeedForCurrentLifeState()
    {
        if (StatsHub == null)
            StatsHub = GetComponent<StatsHub>();
        if (stateHub == null)
            stateHub = GetComponent<StateHub>();

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
