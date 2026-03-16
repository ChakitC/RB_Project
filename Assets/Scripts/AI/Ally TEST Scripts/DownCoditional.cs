using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.GraphDesigner.Runtime.Variables;
using Unity.Behavior;


public class DownCoditional : Conditional
{
    private CharacteContext ctx;

    [SerializeField] private string _CurrentState;

    public override void OnStart()
    {
        ctx = gameObject.GetComponent<CharacteContext>();
    }

    public override TaskStatus OnUpdate()
    {

        if (ctx == null || ctx.stateHub == null || ctx.stateHub.LifeSM == null)
            return TaskStatus.Failure;

        var life = ctx.stateHub.LifeSM.CurrentId;
        
        _CurrentState = life.ToString();

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