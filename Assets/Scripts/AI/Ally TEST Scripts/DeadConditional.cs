using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.GraphDesigner.Runtime.Variables;

public class DeadConditional : Conditional
{
    public SharedVariable<AllyContext> CTX;

   
    public override TaskStatus OnUpdate()
    {
        var ctx = CTX.Value;

        if (ctx == null || ctx.stateHub == null || ctx.stateHub.LifeSM == null)
            return TaskStatus.Failure;

        var life = ctx.stateHub.LifeSM.CurrentId;

        if (life == LifeStateId.Dead)
        {
            return TaskStatus.Success;
        }
        else
        {
            return TaskStatus.Failure;
        }
    }
}
