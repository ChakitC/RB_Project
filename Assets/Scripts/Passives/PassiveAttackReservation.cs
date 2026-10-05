using System;
using System.Collections.Generic;

/// <summary>Reservation only lives through attack preparation. Committed damage is copied into the shot/hitbox.</summary>
public sealed class PassiveAttackReservation : IDisposable
{
    readonly List<(PassiveExecution execution, string variable, float value, float percent)> entries = new();
    readonly string attackId;
    bool finished;
    float percent;
    public float Multiplier => 1f + percent * 0.01f;
    public string AttackId => attackId;
    public PassiveAttackReservation(string attackId) { this.attackId = attackId; }
    public void Add(PassiveExecution execution, string variable, float value, float damagePercent)
    {
        entries.Add((execution, variable, value, damagePercent)); percent += damagePercent;
        execution.Runtime.NotifyAttack(attackId, PassiveAttackPhase.Reserved, damagePercent);
    }
    public void Commit()
    {
        if (finished) return;
        finished = true;
        foreach (var entry in entries)
            if (entry.execution.Runtime.IsAlive && entry.execution.Runtime.Controller.ContainsRuntime(entry.execution.Runtime))
                entry.execution.Runtime.NotifyAttack(attackId, PassiveAttackPhase.Committed, entry.percent);
        entries.Clear();
    }
    public void Dispose()
    {
        if (finished) return;
        finished = true;
        foreach (var entry in entries)
            if (entry.execution.Runtime.IsAlive && entry.execution.Runtime.Controller.ContainsRuntime(entry.execution.Runtime))
            {
                entry.execution.WriteVariable(entry.variable, entry.execution.ReadVariable(entry.variable) + entry.value);
                entry.execution.Runtime.NotifyAttack(attackId, PassiveAttackPhase.Cancelled, entry.percent);
            }
        entries.Clear();
    }
}
