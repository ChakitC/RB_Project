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
    public UnityEngine.AI.NavMeshAgent NavigationAgent;
    public CharacterMovementMotor MovementMotor;
    public AITargetSensor AITargetSensor;
    public AgentMoveDriver AgentMoveDriver;
    public AimAxisDriver AimAxisDriver;

    [Header("Common Modules")]
    public AITargetInfo TargetInfo;
    public CharacterEquipment Equipment;
    public AccessoryLoadout AccessoryLoadout;
    public WeaponSystem WeaponSystem;
    public CharacterAnimBrain AnimBrain;
    public CharacterAnimDriver AnimDriver;
    public RootMotionCCDriver RootMotionDriver;
    public CharacterPairOffsetApplier PairOffsetApplier;
    public MeleeController MeleeController;
    public CharacterAudioEmitter AudioEmitter;
    bool runtimeModulesPrepared;
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
    public virtual bool IsSprinting => false;
    public virtual void CancelSprint() { }
    public virtual bool IsCrouching => false;
    public virtual bool IsSliding => false;
    public virtual void CancelSlide() { }
    public virtual bool CanStandForDash() => true;
    public virtual void PrepareForAcceptedDash() { }

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
        stateHub = ResolveStatePeer(stateHub);
        StatsHub = ResolveActorComponent(StatsHub);
        CombatEventBus = ResolveActorComponent(CombatEventBus);
        rb = ResolveActorComponent(rb);
        cc = ResolveActorComponent(cc);
        NavigationAgent = ResolveActorComponent(NavigationAgent);
        AITargetSensor = ResolveStatePeer(AITargetSensor);
        AgentMoveDriver = ResolveStatePeer(AgentMoveDriver);
        AimAxisDriver = ResolveStatePeer(AimAxisDriver);
        if (Application.isPlaying) EnsureMovementMotor();
        else
        {
            if (!MovementMotor) MovementMotor = GetComponent<CharacterMovementMotor>();
            if (MovementMotor) MovementMotor.Bind(this);
        }

        TargetInfo = ResolveActorComponent(TargetInfo);
        Equipment = ResolveActorComponent(Equipment);
        AccessoryLoadout = ResolveActorComponent(AccessoryLoadout);
        if (AccessoryLoadout == null &&
            Application.isPlaying &&
            UsesPersistentLoadouts)
        {
            AccessoryLoadout = gameObject.AddComponent<AccessoryLoadout>();
        }
        WeaponSystem = ResolveStatePeer(WeaponSystem);
        if (Equipment == null && Application.isPlaying && AutoCreatesRuntimeEquipment &&
            (currentWeapon != null || WeaponSystem != null))
            Equipment = gameObject.AddComponent<CharacterEquipment>();
        AnimBrain = ResolveStatePeer(AnimBrain);
        AnimDriver = ResolveActorComponent(AnimDriver);
        RootMotionDriver = ResolveActorComponent(RootMotionDriver);
        PairOffsetApplier = ResolveActorComponent(PairOffsetApplier);
        if (PairOffsetApplier == null &&
            Application.isPlaying &&
            baseStats != null &&
            baseStats.animProfile != null &&
            baseStats.animProfile.pairOffsetProfiles != null)
        {
            PairOffsetApplier = gameObject.AddComponent<CharacterPairOffsetApplier>();
        }
        MeleeController = ResolveStatePeer(MeleeController);
        AudioEmitter = ResolveStatePeer(AudioEmitter);
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
        HealthSystem = ResolveStatePeer(HealthSystem);
        StaminaSystem = ResolveActorComponent(StaminaSystem);
        DashSystem = ResolveStatePeer(DashSystem);
        KnockbackMotor = ResolveStatePeer(KnockbackMotor);
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
        stateHub?.RefreshOwnerBindings();
    }

    public void PrepareRuntimeModules()
    {
        if (!Application.isPlaying || runtimeModulesPrepared) return;
        runtimeModulesPrepared = true;
        ResolveReferences();
        if (!AudioEmitter) AudioEmitter = gameObject.AddComponent<CharacterAudioEmitter>();
        if (!MeleeController) MeleeController = gameObject.AddComponent<MeleeController>();
        if (stateHub != null && !stateHub.TryGetComponent<StateHubDebugView>(out _))
        {
            var view = stateHub.gameObject.AddComponent<StateHubDebugView>();
            stateHub.ConfigureLegacyDebugView(view);
        }
        ResolveReferences();
    }

    /// <summary>Explicit actor initialization; ordinary reference inspection does not create a motor.</summary>
    public CharacterMovementMotor EnsureMovementMotor()
    {
        if (!MovementMotor) MovementMotor = GetComponent<CharacterMovementMotor>();
        if (!MovementMotor) MovementMotor = gameObject.AddComponent<CharacterMovementMotor>();
        MovementMotor.Bind(this);
        return MovementMotor;
    }

    readonly System.Collections.Generic.HashSet<System.Type> ambiguousPeers = new();
    T ResolveStatePeer<T>(T current) where T : Component
    {
        // Explicit bindings describe intentional layouts, including peers above the context.
        if (current != null) return current;
        var candidates = new System.Collections.Generic.HashSet<T>();
        foreach (var candidate in GetComponentsInChildren<T>(true))
            if (CharacterContextModuleLookup.ResolveContext(candidate) == this) candidates.Add(candidate);
        for (var parent = transform.parent; parent != null; parent = parent.parent)
        {
            var contexts = parent.GetComponentsInChildren<CharacteContext>(true);
            if (contexts.Length != 1 || contexts[0] != this) break;
            foreach (var candidate in parent.GetComponents<T>()) candidates.Add(candidate);
        }
        if (candidates.Count == 1)
            foreach (var candidate in candidates) return candidate;
        if (candidates.Count > 1 && ambiguousPeers.Add(typeof(T)))
            Debug.LogError($"[{name}] Multiple {typeof(T).Name} peers need an explicit context binding.", this);
        return null;
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
