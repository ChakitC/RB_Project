using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.GraphDesigner.Runtime.Variables;
using Unity.Behavior;


public class DownCoditional : Conditional
{

    public SharedVariable<AllyContext> CTX;
    
    public override void OnStart()
    {
       
    }

    public override TaskStatus OnUpdate()
    {
        var ctx = CTX.Value;

        if (ctx == null || ctx.stateHub == null || ctx.stateHub.LifeSM == null)
            return TaskStatus.Failure;

        var life = ctx.stateHub.LifeSM.CurrentId;

        if (life == LifeStateId.Down)
        {
            return TaskStatus.Failure;
        }
        else
        {
            return TaskStatus.Success;
        }
    }
    
}