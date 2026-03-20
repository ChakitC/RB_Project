using System;
using UnityEngine;
using UnityEngine.AI;

public class EnemyContext : CharacteContext
{
    [Header("Collider")]
    public CapsuleCollider Collider;
    public AgentMoveDriver AgentMoveDriver;
    
    [Header("NavMeshAgent")]
    public NavMeshAgent  Agent;
    
    [Header("Drop Item")]
    public EnemyDropper  dropper;
    public Enemy EnemyInfomation;
    
    [Header("Animator")]
    public Animator animator;


    private void Start()
    {
        if (Agent != null)
            Agent.speed = GetMoveSpeedForCurrentLifeState();
    }
}
