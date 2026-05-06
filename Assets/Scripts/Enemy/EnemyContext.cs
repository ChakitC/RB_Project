using System;
using UnityEngine;
using UnityEngine.AI;

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

    float _baseAgentAcceleration = 8f;
    float _baseAgentAngularSpeed = 120f;
    float _baseAnimatorSpeed = 1f;
    bool _hasCachedAgentTuning;
    bool _hasCachedAnimatorSpeed;

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

    void ResolveReferences()
    {
        if (!Agent)
            Agent = GetComponent<NavMeshAgent>();

        if (!AgentMoveDriver)
            AgentMoveDriver = GetComponent<AgentMoveDriver>();

        if (!animator)
            animator = GetComponentInChildren<Animator>(true);

        if (!AnimBrain)
            AnimBrain = GetComponentInChildren<CharacterAnimBrain>(true);
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
