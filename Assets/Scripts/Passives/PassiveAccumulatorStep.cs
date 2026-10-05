using System;

public enum PassiveAccumulatorOperation { Add, Subtract, Set }

[Serializable]
public sealed class PassiveAccumulatorStep : PassiveStep
{
    public string variable;
    public PassiveAccumulatorOperation operation;
    public PassiveNumber amount = new(1);
    public override PassiveResult Execute(PassiveExecution execution)
    {
        float current = execution.ReadVariable(variable), delta = amount.Resolve(execution);
        execution.WriteVariable(variable, operation == PassiveAccumulatorOperation.Set ? delta :
            operation == PassiveAccumulatorOperation.Subtract ? current - delta : current + delta);
        return PassiveResult.Success();
    }
}
