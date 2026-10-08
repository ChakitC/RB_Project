using System;
using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-200)]
public class AllyContext : CharacteContext
{
    [Header("NavMeshAgent")]
    
    
    public NavMeshAgent  agent;


    public BehaviorTree BehaviorTree;

    public AIAimTargetDriver AimTargetDriver;
    public AllyInterruptionController InterruptionController;

    public override AITargetIdentity TargetIdentity => AITargetIdentity.Companion;

    void Awake()
    {
        ResolveReferences();
        PrepareRuntimeModules();
    }

    public override void ResolveReferences()
    {
        base.ResolveReferences();
        InterruptionController = ResolveActorComponent(InterruptionController);


        if (agent == null)
            agent = ResolveActorComponent(agent);


        if (BehaviorTree == null)
            BehaviorTree = ResolveActorComponent(BehaviorTree);

        if (AimTargetDriver == null)
            AimTargetDriver = ResolveActorComponent(AimTargetDriver);

        if (AimTargetDriver == null && Application.isPlaying)
            AimTargetDriver = gameObject.AddComponent<AIAimTargetDriver>();
        if (WeaponAim == null) WeaponAim = AimTargetDriver;
    }

    public override bool ShouldBeInMoveState()
    {
        return AgentMoveDriver != null && AgentMoveDriver.agentismoving;
    }
}
