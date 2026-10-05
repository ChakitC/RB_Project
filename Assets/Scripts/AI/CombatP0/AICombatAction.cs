using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;

public abstract class AICombatAction : Action
{
    public SharedVariable<CharacteContext> context;
    public SharedVariable<AICombatState> combatState;
    public SharedVariable<GameObject> target;
    protected CharacteContext ctx;
    protected AITargetSensor sensor;
    protected NavMeshAgent agent;
    protected AICombatNavigation navigation;
    protected AITargetHandle targetHandle;
    protected float elapsed;
    int executionGeneration;
    protected AICombatState State => combatState != null ? combatState.Value : null;

    public override void OnStart()
    {
        ctx = AIContextBinding.Read(context);
        executionGeneration = ctx != null ? ctx.LifeGeneration : 0;
        sensor = ctx != null ? ctx.AITargetSensor : null;
        agent = ctx != null ? ctx.NavigationAgent : null;
        targetHandle = sensor != null ? sensor.ReadSnapshot.Target : default;
        State?.ObserveTarget(targetHandle);
        elapsed = 0f;
        navigation = new AICombatNavigation(ctx, State);
    }
    protected bool ValidTarget() => sensor != null && sensor.ReadSnapshot.Target == targetHandle &&
        AICombatTaskUtility.Matches(targetHandle, target != null ? target.Value : null) &&
        AICombatTaskUtility.IsEligible(ctx, targetHandle);
    protected bool Ready => AIContextBinding.IsCurrent(context, ctx, executionGeneration) &&
        ctx.AITargetSensor == sensor && ctx.NavigationAgent == agent &&
        State != null && State.Owner == new AITargetHandle(ctx) && AICombatTaskUtility.CanAct(ctx);
    protected TaskStatus Fail(string reason)
    {
        State?.FailNavigation(reason);
        return TaskStatus.Failure;
    }
    public override void OnEnd() => navigation?.Cleanup();
    public override void OnBehaviorTreeStopped(bool paused)
    {
        if (!paused) OnEnd();
    }
    public override void OnDestroy() => OnEnd();
}
