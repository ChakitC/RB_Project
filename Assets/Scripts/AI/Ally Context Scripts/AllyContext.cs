using System;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-90)]
public class AllyContext : CharacteContext
{
    [Header("NavMeshAgent")]
    
    public AITargetSensor AITargetSensor;
    
    public NavMeshAgent  agent;

    public AgentMoveDriver AgentMoveDriver;

    public override bool ShouldBeInMoveState()
    {
        return AgentMoveDriver != null && AgentMoveDriver.agentismoving;
    }
}
