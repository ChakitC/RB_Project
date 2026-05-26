using System;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-200)]
public class EnemyContext : CharacteContext
{
    [Header("Collider")]
    public CapsuleCollider Collider;
    public AgentMoveDriver AgentMoveDriver;

    [Header("NavMeshAgent")]
    public NavMeshAgent Agent;

    [Header("Drop Item")]
    public EnemyDropper dropper;

    [Header("Animator")]
    public Animator animator;

    [Header("Enemy Ammo")]
    [SerializeField] private bool forceInfiniteReserveAmmo;

    float _baseAgentAcceleration = 8f;
    float _baseAgentAngularSpeed = 120f;
    float _baseAnimatorSpeed = 1f;
    bool _hasCachedAgentTuning;
    bool _hasCachedAnimatorSpeed;

    public override AITargetIdentity TargetIdentity => AITargetIdentity.Enemy;
    public override bool ForceInfiniteReserveAmmo => forceInfiniteReserveAmmo;

    private void Awake()
    {
        ResolveReferences();
        CacheBaseWorldSlowValues();
    }

    private void Start()
    {
        ResolveReferences();
        CacheBaseWorldSlowValues();

        if (Agent != null)
            Agent.speed = GetMoveSpeedForCurrentLifeState() * TimeSlowManager.Instance.WorldTimeScale;
    }

    private void Update()
    {
        ApplyWorldSlow();
    }

    private void OnDisable()
    {
        RestoreWorldSlowValues();
    }

    public override bool ShouldBeInMoveState()
    {
        return AgentMoveDriver != null && AgentMoveDriver.agentismoving;
    }

    public override void ResolveReferences()
    {
        base.ResolveReferences();

        if (!Agent)
            Agent = ResolveActorComponent(Agent);

        if (!AgentMoveDriver)
            AgentMoveDriver = ResolveActorComponent(AgentMoveDriver);

        if (!animator)
            animator = ResolveActorComponent(animator);

        if (!AnimBrain)
            AnimBrain = ResolveActorComponent(AnimBrain);

        if (!Collider)
            Collider = ResolveActorComponent(Collider);

        if (!dropper)
            dropper = ResolveActorComponent(dropper);
    }

    void CacheBaseWorldSlowValues()
    {
        if (Agent != null && !_hasCachedAgentTuning)
        {
            _baseAgentAcceleration = Agent.acceleration;
            _baseAgentAngularSpeed = Agent.angularSpeed;
            _hasCachedAgentTuning = true;
        }

        if (animator != null && !_hasCachedAnimatorSpeed)
        {
            _baseAnimatorSpeed = animator.speed;
            _hasCachedAnimatorSpeed = true;
        }
    }

    void ApplyWorldSlow()
    {
        ResolveReferences();
        CacheBaseWorldSlowValues();

        float scale = TimeSlowManager.Instance.WorldTimeScale;

        if (Agent != null)
        {
            if (AgentMoveDriver == null)
                Agent.speed = GetMoveSpeedForCurrentLifeState() * scale;

            if (_hasCachedAgentTuning)
            {
                Agent.acceleration = _baseAgentAcceleration * scale;
                Agent.angularSpeed = _baseAgentAngularSpeed * scale;
            }
        }

        if (ShouldApplyAnimatorSpeedSlow())
            animator.speed = _baseAnimatorSpeed * scale;
    }

    void RestoreWorldSlowValues()
    {
        if (Agent != null && _hasCachedAgentTuning)
        {
            Agent.acceleration = _baseAgentAcceleration;
            Agent.angularSpeed = _baseAgentAngularSpeed;
        }

        if (ShouldApplyAnimatorSpeedSlow())
            animator.speed = _baseAnimatorSpeed;
    }

    bool ShouldApplyAnimatorSpeedSlow()
    {
        return animator != null && _hasCachedAnimatorSpeed && AnimBrain == null;
    }
}
