using System;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class InterruptionCommandController
{
    public CooperativeBlockSession ActiveCooperativeBlock { get; private set; }
    public string BlockBindingLabel { get; internal set; } = "Block";
    public event Action<CooperativeBlockSession> CooperativeBlockStarted;

    public InterruptionCommandResult TryExecuteBlockInput(double eventTime) => ActiveCooperativeBlock != null
        ? (ActiveCooperativeBlock.PressAt(eventTime) ? InterruptionCommandResult.Success : InterruptionCommandResult.SkillRejected)
        : TryExecuteInterruptionCommand();

    internal void SetCooperativeSession(CooperativeBlockSession session)
    {
        ActiveCooperativeBlock = session;
        var input = playerContext.GetComponentInChildren<PlayerInput>(true);
        var action = input != null ? input.actions?.FindAction("Block") : null;
        if (action != null) BlockBindingLabel = action.GetBindingDisplayString();
        CooperativeBlockHud.Ensure(playerContext, this);
        CooperativeBlockStarted?.Invoke(session);
    }
    internal void ClearCooperativeSession(CooperativeBlockSession session)
    {
        if (ReferenceEquals(ActiveCooperativeBlock, session)) ActiveCooperativeBlock = null;
    }
    internal bool TrySelectCooperativeAlly(DefensiveBlockAttack attack, out DefensiveBlockController selected)
        => TrySelectCooperativeAlly(attack, out selected, out _);

    internal bool TrySelectCooperativeAlly(DefensiveBlockAttack attack, out DefensiveBlockController selected, out string reason)
    {
        selected = null;
        reason = "Cooperative command unavailable or another session active";
        if (!defensiveBlockEnabled || playerContext == null || playerContext.fieldAllyManager == null ||
            ActiveCooperativeBlock != null || !attack.CanAcceptCommand(playerContext)) return false;
        int bestRole = int.MaxValue;
        reason = "No Cooperative Ally in party";
        foreach (var member in playerContext.fieldAllyManager.RegisteredMembers)
        {
            if (member == null || member.ActorRole == ChainActorRole.Player || member.ActorContext == null ||
                member.ActorContext.TargetIdentity != AITargetIdentity.Companion || (int)member.ActorRole >= bestRole) continue;
            var guard = member.ActorContext.DefensiveBlock;
            if (guard == null) { reason = "Party Ally has no Block controller"; continue; }
            if (!attack.TryPlanCooperative(playerContext, guard, out _, out _, out _, out _, out string candidateReason))
            { reason = candidateReason; continue; }
            selected = guard; bestRole = (int)member.ActorRole;
        }
        if (selected != null) reason = "Ready";
        return selected != null;
    }
    void OnDisable() => ActiveCooperativeBlock?.Cancel();
}
