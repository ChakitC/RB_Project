using Opsive.GraphDesigner.Runtime.Variables;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using UnityEngine;

public class StartMelee : ActionNode
{
    public SharedVariable<CharacteContext> context;
    [SerializeField] private MeleeType meleeType = MeleeType.Heavy;

    private StateHub _stateHub;

    public override void OnStart()
    {
        _stateHub = AIContextBinding.Read(context)?.stateHub;

        if (_stateHub == null)
            return;

        _stateHub.RequestOnMelee(meleeType);
    }
    public override TaskStatus OnUpdate() => _stateHub != null ? TaskStatus.Success : TaskStatus.Failure;
}
