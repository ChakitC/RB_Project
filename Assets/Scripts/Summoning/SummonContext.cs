using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public sealed class SummonContext : CharacteContext
{
    [Header("Summon")]
    [SerializeField] private SummonMobility mobility = SummonMobility.Stationary;
    [SerializeField] private SummonedEntityRuntime summonedRuntime;

    [Header("AI")]
    public AITargetSensor AITargetSensor;
    public NavMeshAgent Agent;
    public AgentMoveDriver AgentMoveDriver;
    public BehaviorTree BehaviorTree;

    public SummonMobility Mobility => mobility;
    public SummonedEntityRuntime SummonedRuntime => summonedRuntime;
    public override AITargetIdentity TargetIdentity => AITargetIdentity.Companion;
    public override bool UsesPersistentProgression => false;
    public override bool UsesPersistentLoadouts => false;
    public override bool ParticipatesInPartyRuntime => false;
    public override bool PreservesOwnedVfxDuringRoomTransition => true;
    public override bool CanCollectPickups => false;
    public override bool AutoCreatesAimRig => false;
    public override bool AutoCreatesRuntimeEquipment => false;

    void Awake()
    {
        ResolveReferences();
    }

    public override void ResolveReferences()
    {
        base.ResolveReferences();

        if (summonedRuntime == null)
            summonedRuntime = ResolveActorComponent(summonedRuntime);
        if (AITargetSensor == null)
            AITargetSensor = ResolveActorComponent(AITargetSensor);
        if (Agent == null)
            Agent = ResolveActorComponent(Agent);
        if (AgentMoveDriver == null)
            AgentMoveDriver = ResolveActorComponent(AgentMoveDriver);
        if (BehaviorTree == null)
            BehaviorTree = ResolveActorComponent(BehaviorTree);
    }

    public override bool ShouldBeInMoveState()
    {
        return AgentMoveDriver != null && AgentMoveDriver.agentismoving;
    }

    public void SetMobility(SummonMobility value)
    {
        mobility = value;
    }
}
