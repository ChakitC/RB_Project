using System;
using Opsive.BehaviorDesigner.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

[DefaultExecutionOrder(112)]
[DisallowMultipleComponent]
public sealed class StaggerMeter : MonoBehaviour
{
    [Header("Profile")]
    [SerializeField] private StaggerProfileSO profile;

    [SerializeField, FoldoutGroup("Fallback Values"), Min(1f)] private float maxStagger = 100f;
    [SerializeField, FoldoutGroup("Fallback Values"), Min(0f)] private float staggerGainMultiplier = 1f;
    [SerializeField, FoldoutGroup("Fallback Values"), Min(0.01f)] private float staggerDuration = 1.5f;
    [SerializeField, FoldoutGroup("Fallback Values"), Min(1f)] private float damageTakenMultiplierWhileStaggered = 1.25f;
    [SerializeField, FoldoutGroup("Fallback Values"), Min(0f)] private float postStaggerImmunity = 1f;
    [SerializeField, FoldoutGroup("Fallback Values"), Min(0.01f)] private float chainReadyDuration = 3f;
    [SerializeField, FoldoutGroup("Fallback Values"), Min(0f)] private float decayDelay = 1.5f;
    [SerializeField, FoldoutGroup("Fallback Values"), Min(0f)] private float decayPerSecond = 20f;

    [Header("Behavior")]
    [SerializeField] private bool resetMeterOnStagger = true;
    [SerializeField] private bool ignoreStaggerGainWhileStaggered = true;

    [Header("Refs")]
    [SerializeField] private StateHub stateHub;
    // TODO(deprecate): Keep serialized Brain compatibility until prefabs are audited.
    [SerializeField] private CharacterAnimBrain animBrain;
    private CharacterAnimDriver animDriver;
    [SerializeField] private HealthSystem healthSystem;
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private BehaviorTree behaviorTree;

    [Header("Stagger Bar")]
    [SerializeField] private GameObject staggerBarPrefab;
    [SerializeField] private float staggerBarHeight = 2.25f;

    [Header("Debug")]
    [SerializeField] private float currentStagger;
    [SerializeField] private bool isStaggered;
    [SerializeField] private float staggerTimeRemaining;
    [SerializeField] private float immunityTimeRemaining;
    [SerializeField] private bool isChainReady;
    [SerializeField] private bool isChainExecutionActive;
    [SerializeField] private float chainReadyTimeRemaining;
    GameObject chainReadySource;

    Slider staggerBarSlider;
    float timeSinceLastGain;
    bool agentOverrideActive;
    bool resumeAgentStopped;
    bool resumeAgentUpdatePosition;
    bool resumeAgentUpdateRotation;
    bool resumeAgentHadPath;
    Vector3 resumeAgentDestination;
    bool behaviorTreeWasEnabled;
    bool behaviorTreeSuspended;

    public event Action StaggerStarted;
    public event Action StaggerEnded;
    public event Action<float, float> MeterChanged;
    public event Action ChainReadyStarted;
    public event Action ChainReadyEnded;
    public event Action<float, float> ChainReadyTimeChanged;

    public float CurrentStagger => currentStagger;
    public float MaxStagger => ResolveMaxStagger();
    public bool IsStaggered => isStaggered;
    public bool IsChainReady => isChainReady;
    public bool IsChainExecutionActive => isChainExecutionActive;
    public float ChainReadyTimeRemaining => chainReadyTimeRemaining;
    public float ChainReadyDuration => ResolveChainReadyDuration();
    public float DamageTakenMultiplier => isStaggered ? ResolveDamageTakenMultiplier() : 1f;

    void Awake()
    {
        ResolveRefs();
        CreateStaggerBarIfNeeded();
        ApplyStaggerBarValues();
    }

    void OnEnable()
    {
        ResolveRefs();

        if (healthSystem != null)
        {
            healthSystem.CharacterDown += OnCharacterInactive;
            healthSystem.CharacterDead += OnCharacterInactive;
        }

        ApplyStaggerBarValues();
    }

    void OnDisable()
    {
        if (healthSystem != null)
        {
            healthSystem.CharacterDown -= OnCharacterInactive;
            healthSystem.CharacterDead -= OnCharacterInactive;
        }

        ClearStaggerState();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        TickImmunity(dt);
        TickChainReady(dt);
        TickStagger(dt);
        TickDecay(dt);
    }

    public bool ApplyStagger(in StaggerPayload payload, GameObject source = null)
    {
        if (!payload.HasValue)
            return false;

        return ApplyStagger(payload.ResolvedAmount, source);
    }

    public bool ApplyStagger(float amount, GameObject source = null)
    {
        if (!CanGainStagger(amount))
            return false;

        float applied = Mathf.Max(0f, amount) * Mathf.Max(0f, ResolveGainMultiplier());
        if (applied <= 0f)
            return false;

        currentStagger = Mathf.Clamp(currentStagger + applied, 0f, ResolveMaxStagger());
        timeSinceLastGain = 0f;
        NotifyMeterChanged();

        if (currentStagger >= ResolveMaxStagger() && !isChainReady)
            EnterChainReady(source);

        return true;
    }

    internal void ResetRuntimeState()
    {
        currentStagger = 0f;
        timeSinceLastGain = 0f;
        ClearStaggerState();
        NotifyMeterChanged();
    }

    void ResolveRefs()
    {
        Transform actorRoot = ResolveActorRoot();

        if (!stateHub)
            stateHub = ResolveActorComponent<StateHub>(actorRoot);
        if (!animBrain)
            animBrain = ResolveActorComponent<CharacterAnimBrain>(actorRoot);
        if (!animDriver)
            animDriver = ResolveActorComponent<CharacterAnimDriver>(actorRoot);
        if (!healthSystem)
            healthSystem = ResolveActorComponent<HealthSystem>(actorRoot);
        if (!navMeshAgent)
            navMeshAgent = ResolveActorComponent<NavMeshAgent>(actorRoot);
        if (!behaviorTree)
            behaviorTree = ResolveActorComponent<BehaviorTree>(actorRoot);
    }

    Transform ResolveActorRoot()
    {
        if (stateHub != null)
            return stateHub.transform;

        if (TryGetComponent(out CharacteContext localContext))
            return localContext.transform;

        CharacteContext parentContext = GetComponentInParent<CharacteContext>();
        if (parentContext != null)
            return parentContext.transform;

        StateHub parentStateHub = GetComponentInParent<StateHub>();
        if (parentStateHub != null)
            return parentStateHub.transform;

        return transform.root;
    }

    T ResolveActorComponent<T>(Transform actorRoot) where T : Component
    {
        if (actorRoot != null && actorRoot.TryGetComponent(out T rootComponent))
            return rootComponent;

        if (TryGetComponent(out T localComponent))
            return localComponent;

        T parentComponent = GetComponentInParent<T>();
        if (parentComponent != null)
            return parentComponent;

        return actorRoot != null
            ? actorRoot.GetComponentInChildren<T>(true)
            : GetComponentInChildren<T>(true);
    }

    bool CanGainStagger(float amount)
    {
        if (!isActiveAndEnabled || amount <= 0f)
            return false;

        if (healthSystem != null && !healthSystem.IsAlive)
            return false;

        if (immunityTimeRemaining > 0f)
            return false;

        if (isChainReady)
            return false;

        return !isStaggered || !ignoreStaggerGainWhileStaggered;
    }

    void EnterStagger(GameObject source)
    {
        if (isStaggered)
        {
            staggerTimeRemaining = Mathf.Max(staggerTimeRemaining, ResolveStaggerDuration());
            return;
        }

        isStaggered = true;
        staggerTimeRemaining = ResolveStaggerDuration();

        if (resetMeterOnStagger)
            currentStagger = 0f;

        ApplyControlState();
        SuspendAgent();
        NotifyMeterChanged();
        StaggerStarted?.Invoke();
    }

    void ExitStagger()
    {
        if (!isStaggered)
            return;

        isStaggered = false;
        staggerTimeRemaining = 0f;
        immunityTimeRemaining = ResolvePostStaggerImmunity();

        ClearControlState();
        RestoreAgent();
        NotifyMeterChanged();
        StaggerEnded?.Invoke();
    }

    void ClearStaggerState()
    {
        CancelChainReady();

        bool wasStaggered = isStaggered;
        isStaggered = false;
        staggerTimeRemaining = 0f;
        immunityTimeRemaining = 0f;

        ClearControlState();
        RestoreAgent();

        if (wasStaggered)
            StaggerEnded?.Invoke();
    }

    void TickStagger(float dt)
    {
        if (!isStaggered || dt <= 0f)
            return;

        SuspendAgent();

        staggerTimeRemaining = Mathf.Max(0f, staggerTimeRemaining - dt);
        if (staggerTimeRemaining <= 0f)
            ExitStagger();
    }

    void TickImmunity(float dt)
    {
        if (immunityTimeRemaining <= 0f || dt <= 0f)
            return;

        immunityTimeRemaining = Mathf.Max(0f, immunityTimeRemaining - dt);
    }

    void TickDecay(float dt)
    {
        if (isStaggered || isChainReady || currentStagger <= 0f || dt <= 0f)
            return;

        timeSinceLastGain += dt;
        if (timeSinceLastGain < ResolveDecayDelay())
            return;

        float decay = ResolveDecayPerSecond() * dt;
        if (decay <= 0f)
            return;

        currentStagger = Mathf.Max(0f, currentStagger - decay);
        NotifyMeterChanged();
    }

    void ApplyControlState()
    {
        stateHub?.SetStaggerControlState(
            ControlBlockFlags.Move | ControlBlockFlags.Shoot | ControlBlockFlags.Skill,
            true);

        animDriver?.SetStaggerStatusLocomotion(ImpactReactionKind.Stun);
    }

    void ClearControlState()
    {
        stateHub?.SetStaggerControlState(ControlBlockFlags.None, false);
        animDriver?.SetStaggerStatusLocomotion(ImpactReactionKind.None);
    }

    void SuspendAgent()
    {
        if (behaviorTree != null && !behaviorTreeSuspended)
        {
            behaviorTreeWasEnabled = behaviorTree.enabled;
            if (behaviorTreeWasEnabled)
            {
                behaviorTree.enabled = false;
                behaviorTreeSuspended = true;
            }
        }

        if (navMeshAgent == null || !navMeshAgent.enabled)
            return;

        if (!agentOverrideActive)
        {
            resumeAgentStopped = navMeshAgent.isStopped;
            resumeAgentUpdatePosition = navMeshAgent.updatePosition;
            resumeAgentUpdateRotation = navMeshAgent.updateRotation;
            resumeAgentHadPath = navMeshAgent.hasPath || navMeshAgent.pathPending;
            resumeAgentDestination = navMeshAgent.isOnNavMesh ? navMeshAgent.destination : transform.position;
            agentOverrideActive = true;
        }

        navMeshAgent.isStopped = true;
        navMeshAgent.updateRotation = false;

        if (navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.velocity = Vector3.zero;
            navMeshAgent.ResetPath();
        }
    }

    void RestoreAgent()
    {
        if (agentOverrideActive)
        {
            agentOverrideActive = false;

            if (navMeshAgent != null && navMeshAgent.enabled)
            {
                navMeshAgent.updatePosition = resumeAgentUpdatePosition;
                navMeshAgent.updateRotation = resumeAgentUpdateRotation;
                navMeshAgent.isStopped = resumeAgentStopped;

                if (resumeAgentHadPath &&
                    !resumeAgentStopped &&
                    navMeshAgent.isOnNavMesh &&
                    !navMeshAgent.pathPending &&
                    !navMeshAgent.hasPath)
                {
                    navMeshAgent.SetDestination(resumeAgentDestination);
                }
            }
        }

        if (!behaviorTreeSuspended)
            return;

        if (behaviorTreeWasEnabled && behaviorTree != null)
            behaviorTree.enabled = true;

        behaviorTreeSuspended = false;
        behaviorTreeWasEnabled = false;
    }

    void OnCharacterInactive()
    {
        currentStagger = 0f;
        NotifyMeterChanged();
        CancelChainReady();
        ClearStaggerState();
    }

    void CreateStaggerBarIfNeeded()
    {
        if (!staggerBarPrefab || staggerBarSlider)
            return;

        var instance = Instantiate(
            staggerBarPrefab,
            transform.position + Vector3.up * staggerBarHeight,
            Quaternion.identity,
            transform);

        staggerBarSlider = instance.GetComponentInChildren<Slider>();
    }

    void ApplyStaggerBarValues()
    {
        if (!staggerBarSlider)
            return;

        staggerBarSlider.maxValue = ResolveMaxStagger();
        staggerBarSlider.value = currentStagger;
    }

    void NotifyMeterChanged()
    {
        ApplyStaggerBarValues();
        MeterChanged?.Invoke(currentStagger, ResolveMaxStagger());
    }

    void EnterChainReady(GameObject source)
    {
        if (isChainReady)
            return;

        isChainReady = true;
        isChainExecutionActive = false;
        chainReadySource = source;
        chainReadyTimeRemaining = ResolveChainReadyDuration();

        ApplyChainReadyControlState();
        SuspendAgent();
        NotifyMeterChanged();
        ChainReadyStarted?.Invoke();
        ChainReadyTimeChanged?.Invoke(chainReadyTimeRemaining, ResolveChainReadyDuration());
    }

    void TickChainReady(float dt)
    {
        if (!isChainReady || dt <= 0f)
            return;

        SuspendAgent();

        if (isChainExecutionActive)
            return;

        chainReadyTimeRemaining = Mathf.Max(0f, chainReadyTimeRemaining - dt);
        ChainReadyTimeChanged?.Invoke(chainReadyTimeRemaining, ResolveChainReadyDuration());

        if (chainReadyTimeRemaining <= 0f)
            CompleteChainReadyAndEnterStagger();
    }

    void ApplyChainReadyControlState()
    {
        stateHub?.SetStaggerControlState(
            ControlBlockFlags.Move | ControlBlockFlags.Shoot | ControlBlockFlags.Skill,
            true);

        animDriver?.SetStaggerStatusLocomotionPose(StatusLocomotionPose.ChainReady);
        animDriver?.InterruptActivePlaybackForExternalControlLoss();
    }

    public void BeginChainExecution()
    {
        if (!isChainReady)
            return;

        isChainExecutionActive = true;
    }

    public bool CompleteChainReadyAndEnterStagger()
    {
        if (!isChainReady)
            return false;

        isChainReady = false;
        isChainExecutionActive = false;
        chainReadyTimeRemaining = 0f;
        ChainReadyEnded?.Invoke();

        EnterStagger(chainReadySource);
        chainReadySource = null;
        return true;
    }

    void CancelChainReady()
    {
        if (!isChainReady)
            return;

        isChainReady = false;
        isChainExecutionActive = false;
        chainReadyTimeRemaining = 0f;
        chainReadySource = null;

        ClearControlState();
        RestoreAgent();
        ChainReadyEnded?.Invoke();
    }

    float ResolveMaxStagger() => Mathf.Max(1f, profile != null ? profile.maxStagger : maxStagger);
    float ResolveGainMultiplier() => Mathf.Max(0f, profile != null ? profile.staggerGainMultiplier : staggerGainMultiplier);
    float ResolveStaggerDuration() => Mathf.Max(0.01f, profile != null ? profile.staggerDuration : staggerDuration);
    float ResolveDamageTakenMultiplier() => Mathf.Max(1f, profile != null ? profile.damageTakenMultiplierWhileStaggered : damageTakenMultiplierWhileStaggered);
    float ResolvePostStaggerImmunity() => Mathf.Max(0f, profile != null ? profile.postStaggerImmunity : postStaggerImmunity);
    float ResolveChainReadyDuration() => Mathf.Max(0.01f, profile != null ? profile.chainReadyDuration : chainReadyDuration);
    float ResolveDecayDelay() => Mathf.Max(0f, profile != null ? profile.decayDelay : decayDelay);
    float ResolveDecayPerSecond() => Mathf.Max(0f, profile != null ? profile.decayPerSecond : decayPerSecond);
}
