using System;
using System.Collections.Generic;

[Serializable]
public sealed class PassiveFragmentStep : PassiveStep
{
    public PassiveFragment fragment;
    public List<PassiveParameter> overrides = new();
    public List<PassiveVariableBinding> bindings = new();
    public List<PassiveVariableBinding> parameterBindings = new();
    public override bool RunOnRefresh => true;
    public override PassiveResult Execute(PassiveExecution execution) =>
        fragment != null ? execution.Enter(this).Run(fragment.steps) : new(PassiveOutcome.Failed);
}
