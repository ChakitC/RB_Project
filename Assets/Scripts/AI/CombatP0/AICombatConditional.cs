using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.GraphDesigner.Runtime.Variables;

public abstract class AICombatConditional : Conditional
{
    public SharedVariable<CharacteContext> context;
    public SharedVariable<AICombatState> combatState;
    public SharedVariable<GameObject> target;
    protected CharacteContext ctx;
    protected AITargetSensor sensor;
    protected AICombatState State => combatState != null ? combatState.Value : null;

    protected void CacheReferences()
    {
        ctx = AIContextBinding.Read(context);
        sensor = ctx != null ? ctx.AITargetSensor : null;
    }
    public override void OnStart() => CacheReferences();
    public override TaskStatus OnUpdate() => EvaluateStatus();
    public override TaskStatus OnReevaluateUpdate() => EvaluateStatus();
    TaskStatus EvaluateStatus()
    {
        CacheReferences();
        return ctx != null && Evaluate() ? TaskStatus.Success : TaskStatus.Failure;
    }
    protected abstract bool Evaluate();
}
