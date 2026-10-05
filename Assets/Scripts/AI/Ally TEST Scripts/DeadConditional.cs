using Opsive.GraphDesigner.Runtime.Variables;
using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;

public class DeadConditional : Conditional
{
    public SharedVariable<CharacteContext> context;
    private const string UnavailableState = "Unavailable";

    private CharacteContext ctx;
    private StateHub subscribedStateHub;
    [SerializeField] private string CurrentState;

    public override void OnStart()
    {
        RefreshCurrentState();
        SubscribeToLifeStateChanges();
    }

    public override TaskStatus OnUpdate()
    {
        SubscribeToLifeStateChanges();
        if (!TryGetLifeState(out var life))
            return TaskStatus.Failure;

        return life == LifeStateId.Dead ? TaskStatus.Success : TaskStatus.Failure;
    }

    public override void OnEnd()
    {
        RefreshCurrentState();
        UnsubscribeFromLifeStateChanges();
    }

    public override void Reset()
    {
        UnsubscribeFromLifeStateChanges();
        CurrentState = string.Empty;
    }

    private bool TryGetLifeState(out LifeStateId life)
    {
        ctx = AIContextBinding.Read(context);

        if (ctx == null || ctx.stateHub == null || ctx.stateHub.LifeSM == null)
        {
            CurrentState = UnavailableState;
            life = default;
            return false;
        }

        life = ctx.stateHub.LifeSM.CurrentId;
        CurrentState = life.ToString();
        return true;
    }

    private void RefreshCurrentState()
    {
        TryGetLifeState(out _);
    }

    private void SubscribeToLifeStateChanges()
    {
        TryGetStateHub(out var stateHub);
        if (subscribedStateHub == stateHub)
            return;

        UnsubscribeFromLifeStateChanges();
        if (stateHub == null) return;
        subscribedStateHub = stateHub;
        subscribedStateHub.LifeSM.OnChanged += HandleLifeStateChanged;
    }

    private void UnsubscribeFromLifeStateChanges()
    {
        if (subscribedStateHub == null || subscribedStateHub.LifeSM == null)
        {
            subscribedStateHub = null;
            return;
        }

        subscribedStateHub.LifeSM.OnChanged -= HandleLifeStateChanged;
        subscribedStateHub = null;
    }

    private bool TryGetStateHub(out StateHub stateHub)
    {
        ctx = AIContextBinding.Read(context);

        stateHub = ctx != null ? ctx.stateHub : null;
        return stateHub != null && stateHub.LifeSM != null;
    }

    public override TaskStatus OnReevaluateUpdate() => OnUpdate();
    public override void OnBehaviorTreeStopped(bool paused) => UnsubscribeFromLifeStateChanges();
    public override void OnDestroy() => UnsubscribeFromLifeStateChanges();

    private void HandleLifeStateChanged(LifeStateId _, LifeStateId to)
    {
        CurrentState = to.ToString();
    }
}
