using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;

public class DownCoditional : Conditional
{
    private const string UnavailableState = "Unavailable";

    private CharacteContext ctx;
    private StateHub subscribedStateHub;

    [SerializeField] private string _CurrentState;

    public override void OnStart()
    {
        RefreshCurrentState();
        SubscribeToLifeStateChanges();
    }

    public override TaskStatus OnUpdate()
    {
        if (!TryGetLifeState(out var life))
            return TaskStatus.Failure;

        // This task is wired as a guard for "still able to act":
        // once the actor is Down/Dead the guard should fail.
        return life == LifeStateId.Down || life == LifeStateId.Dead
            ? TaskStatus.Failure
            : TaskStatus.Success;
    }

    public override void OnEnd()
    {
        RefreshCurrentState();
        UnsubscribeFromLifeStateChanges();
    }

    public override void Reset()
    {
        UnsubscribeFromLifeStateChanges();
        _CurrentState = string.Empty;
    }

    private bool TryGetLifeState(out LifeStateId life)
    {
        if (ctx == null)
            ctx = gameObject.GetComponent<CharacteContext>();

        if (ctx == null || ctx.stateHub == null || ctx.stateHub.LifeSM == null)
        {
            _CurrentState = UnavailableState;
            life = default;
            return false;
        }

        life = ctx.stateHub.LifeSM.CurrentId;
        _CurrentState = life.ToString();
        return true;
    }

    private void RefreshCurrentState()
    {
        TryGetLifeState(out _);
    }

    private void SubscribeToLifeStateChanges()
    {
        if (!TryGetStateHub(out var stateHub) || subscribedStateHub == stateHub)
            return;

        UnsubscribeFromLifeStateChanges();
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
        if (ctx == null)
            ctx = gameObject.GetComponent<CharacteContext>();

        stateHub = ctx != null ? ctx.stateHub : null;
        return stateHub != null && stateHub.LifeSM != null;
    }

    private void HandleLifeStateChanged(LifeStateId _, LifeStateId to)
    {
        _CurrentState = to.ToString();
    }
}
