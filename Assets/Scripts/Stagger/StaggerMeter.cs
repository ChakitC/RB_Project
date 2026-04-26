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
    [SerializeField, FoldoutGroup("Fallback Values"), Min(0f)] private float decayDelay = 1.5f;
    [SerializeField, FoldoutGroup("Fallback Values"), Min(0f)] private float decayPerSecond = 20f;

    [Header("Behavior")]
    [SerializeField] private bool resetMeterOnStagger = true;
    [SerializeField] private bool ignoreStaggerGainWhileStaggered = true;

    [Header("Refs")]
    [SerializeField] private StateHub stateHub;
    [SerializeField] private CharacterAnimBrain animBrain;
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

    public float CurrentStagger => currentStagger;
    public float MaxStagger => ResolveMaxStagger();
    public bool IsStaggered => isStaggered;
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

        if (currentStagger >= ResolveMaxStagger())
            EnterStagger(source);

        return true;
    }

    void ResolveRefs()
    {
        if (!stateHub)
            TryGetComponent(out stateHub);
        if (!animBrain)
            TryGetComponent(out animBrain);
        if (!healthSystem)
            TryGetComponent(out healthSystem);
        if (!navMeshAgent)
            TryGetComponent(out navMeshAgent);
        if (!behaviorTree)
            TryGetComponent(out behaviorTree);
    }

    bool CanGainStagger(float amount)
    {
        if (!isActiveAndEnabled || amount <= 0f)
            return false;

        if (healthSystem != null && !healthSystem.IsAlive)
            return false;

        if (immunityTimeRemaining > 0f)
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
        if (isStaggered || currentStagger <= 0f || dt <= 0f)
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

        animBrain?.SetStaggerStatusLocomotion(ImpactReactionKind.Stun);
    }

    void ClearControlState()
    {
        stateHub?.SetStaggerControlState(ControlBlockFlags.None, false);
        animBrain?.SetStaggerStatusLocomotion(ImpactReactionKind.None);
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

    float ResolveMaxStagger() => Mathf.Max(1f, profile != null ? profile.maxStagger : maxStagger);
    float ResolveGainMultiplier() => Mathf.Max(0f, profile != null ? profile.staggerGainMultiplier : staggerGainMultiplier);
    float ResolveStaggerDuration() => Mathf.Max(0.01f, profile != null ? profile.staggerDuration : staggerDuration);
    float ResolveDamageTakenMultiplier() => Mathf.Max(1f, profile != null ? profile.damageTakenMultiplierWhileStaggered : damageTakenMultiplierWhileStaggered);
    float ResolvePostStaggerImmunity() => Mathf.Max(0f, profile != null ? profile.postStaggerImmunity : postStaggerImmunity);
    float ResolveDecayDelay() => Mathf.Max(0f, profile != null ? profile.decayDelay : decayDelay);
    float ResolveDecayPerSecond() => Mathf.Max(0f, profile != null ? profile.decayPerSecond : decayPerSecond);
}
