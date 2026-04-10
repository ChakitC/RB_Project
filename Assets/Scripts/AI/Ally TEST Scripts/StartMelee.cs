using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;


    public class StartMelee : ActionNode
    {
        private StateHub _stateHub;
        public override void OnStart()
        {
            if (_stateHub == null)
            {
                _stateHub = gameObject.GetComponent<StateHub>();
            }
            _stateHub.RequestOnMelee();
            
        }
    }
