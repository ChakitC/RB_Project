using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Executes once per attack/Dash identity within an existing timer, including skipped attempts.</summary>
[Serializable]
public sealed class PassiveExpiryEventStep : PassiveStep
{
    public string timerVariable;
    public PassiveOnceScope identity = PassiveOnceScope.Attack;
    [SerializeReference] public List<PassiveStep> steps = new();
    public override PassiveResult Execute(PassiveExecution execution)
    {
        string id = identity == PassiveOnceScope.Dash
            ? (execution.Event.Metadata.DashExecutionId == 0 ? null : execution.Event.Metadata.DashExecutionId.ToString())
            : execution.Event.AttackId;
        return execution.Runtime.ClaimExpiryEvent(execution.ResolveVariableKey(timerVariable), execution.Key, id)
            ? execution.Run(steps) : new(PassiveOutcome.Skipped);
    }
}
