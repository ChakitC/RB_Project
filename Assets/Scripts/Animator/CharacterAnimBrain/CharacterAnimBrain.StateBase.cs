using Animancer.FSM;

public sealed partial class CharacterAnimBrain
{
    public abstract class LocomotionState : IState
    {
        public virtual bool CanEnterState => true;
        public virtual bool CanExitState => true;
        public virtual void OnEnterState() { }
        public virtual void OnExitState() { }
        public virtual void Update() { }
    }

    public abstract class ActionState : IState
    {
        public virtual bool CanEnterState => true;
        public virtual bool CanExitState => true;
        public virtual void OnEnterState() { }
        public virtual void OnExitState() { }
        public virtual void Update() { }
    }
}