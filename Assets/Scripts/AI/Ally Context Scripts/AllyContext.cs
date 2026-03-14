using System;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-90)]
public class AllyContext : CharacteContext
{
    [Header("NavMeshAgent")]
    
    public NavMeshAgent  agent;

    public AgentMoveDriver AgentMoveDriver;
    
    
}
