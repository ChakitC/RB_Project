using System;

[Serializable]
public sealed class PassiveSignalStep : PassiveStep
{
    public string signal;
    public PassiveNumber value = new(0);
    public override PassiveResult Execute(PassiveExecution execution) =>
        execution.Runtime.Controller.PublishChildEvent(execution.Event, PassiveEventType.PassiveSignal,
            execution.Event.Target, signal, value.Resolve(execution), execution.Runtime.Grant.Definition.RuntimeId, execution.Path)
            ? PassiveResult.Success() : default;
}
