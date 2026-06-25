using System;
using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-200)]
public class AllyContext : CharacteContext
{
    [Header("NavMeshAgent")]
    
    public AITargetSensor AITargetSensor;
    
    public NavMeshAgent  agent;

    public AgentMoveDriver AgentMoveDriver;

    public BehaviorTree BehaviorTree;

    public AIAimTargetDriver AimTargetDriver;

    public override AITargetIdentity TargetIdentity => AITargetIdentity.Companion;

    void Awake()
    {
        ResolveReferences();
    }

    public override void ResolveReferences()
    {
        base.ResolveReferences();

        if (AITargetSensor == null)
            AITargetSensor = ResolveActorComponent(AITargetSensor);

        if (agent == null)
            agent = ResolveActorComponent(agent);

        if (AgentMoveDriver == null)
            AgentMoveDriver = ResolveActorComponent(AgentMoveDriver);

        if (BehaviorTree == null)
            BehaviorTree = ResolveActorComponent(BehaviorTree);

        if (AimTargetDriver == null)
            AimTargetDriver = ResolveActorComponent(AimTargetDriver);

        if (AimTargetDriver == null && Application.isPlaying)
            AimTargetDriver = gameObject.AddComponent<AIAimTargetDriver>();
    }

    public override bool ShouldBeInMoveState()
    {
        return AgentMoveDriver != null && AgentMoveDriver.agentismoving;
    }
}
