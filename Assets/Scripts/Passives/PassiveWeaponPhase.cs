using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One prepared primary shot. Cancellation restores only this preparation's reserved charges.</summary>
public sealed class PassiveWeaponPhase : IDisposable
{
    public WeaponShotBuildContext Shot;
    public WeaponDamageBuildContext Damage;
    public readonly bool IsDamage;
    readonly PassiveController controller;
    readonly int life;
    List<(PassiveExecution execution, string variable, float amount, int epoch, string signal)> reservations;
    bool finished;
    public PassiveWeaponPhase(PassiveController controller, WeaponShotBuildContext shot)
    { this.controller = controller; Shot = shot; life = controller.Owner.LifeGeneration; }
    public PassiveWeaponPhase(PassiveController controller, WeaponDamageBuildContext damage)
    { this.controller = controller; Damage = damage; IsDamage = true; life = controller.Owner.LifeGeneration; }
    public bool Reserve(PassiveExecution execution, string variable, float amount, string signal = null)
    {
        float available = execution.ReadVariable(variable);
        amount = Mathf.Min(available, Mathf.Max(0, amount));
        if (amount <= 0) return false;
        reservations ??= new(); reservations.Add((execution, variable, amount, execution.Runtime.VariableEpoch(execution.ResolveVariableKey(variable)), signal));
        execution.WriteVariable(variable, available - amount);
        return true;
    }
    public void Commit()
    {
        if (finished) return;
        finished = true;
        if (reservations != null) foreach (var entry in reservations)
            if (entry.execution.Runtime.IsAlive && controller.ContainsRuntime(entry.execution.Runtime))
                entry.execution.Runtime.Feedback(entry.signal, Mathf.RoundToInt(entry.execution.PeekVariable(entry.variable)));
        reservations?.Clear();
        if (controller != null && controller.isActiveAndEnabled && controller.Owner.LifeGeneration == life)
            controller.ProcessWeaponPhase(this, PassiveRuleTrigger.ShotCommitted);
    }
    public void Dispose()
    {
        if (finished) return;
        finished = true;
        if (reservations == null) return;
        RollbackFrom(0);
    }
    internal int ReservationCount => reservations?.Count ?? 0;
    internal void RollbackFrom(int start)
    {
        if (reservations == null) return;
        for (int i = reservations.Count - 1; i >= start; i--)
        {
            var entry = reservations[i];
            if (entry.execution.Runtime.IsAlive && controller.ContainsRuntime(entry.execution.Runtime) &&
                entry.epoch == entry.execution.Runtime.VariableEpoch(entry.execution.ResolveVariableKey(entry.variable)))
                entry.execution.WriteVariable(entry.variable, entry.execution.ReadVariable(entry.variable) + entry.amount);
            reservations.RemoveAt(i);
        }
    }
}
