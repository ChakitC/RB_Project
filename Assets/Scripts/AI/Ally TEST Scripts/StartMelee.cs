using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using UnityEngine;

public class StartMelee : ActionNode
{
    [SerializeField] private MeleeType meleeType = MeleeType.Heavy;

    private StateHub _stateHub;

    public override void OnStart()
    {
        if (_stateHub == null)
            _stateHub = gameObject.GetComponentInParent<StateHub>();

        if (_stateHub == null)
            return;

        _stateHub.RequestOnMelee(meleeType);
    }
}
