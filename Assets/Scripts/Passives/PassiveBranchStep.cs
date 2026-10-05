using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PassiveBranchStep : PassiveStep
{
    public PassiveCondition condition = new();
    [SerializeReference] public List<PassiveStep> whenTrue = new();
    [SerializeReference] public List<PassiveStep> whenFalse = new();
    public override bool RunOnRefresh => true;
    public override PassiveResult Execute(PassiveExecution execution) =>
        execution.Run(condition.Evaluate(execution) ? whenTrue : whenFalse);
}
