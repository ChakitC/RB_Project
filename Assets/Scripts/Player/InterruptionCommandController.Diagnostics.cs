using System.Text;
using UnityEngine;

public sealed partial class InterruptionCommandController
{
    [Header("Defensive Block Diagnostics")]
    [Tooltip("Log each command's rejected candidates and accepted guard lifecycle to Console and a session file. Does not change Block eligibility.")]
    public bool logDefensiveBlock;

    public void LogDefensiveBlock(string message)
    {
        if (logDefensiveBlock) DefensiveBlockDiagnostics.Write(message, this);
    }

    [ContextMenu("Open Defensive Block Log Folder")]
    public void OpenDefensiveBlockLogFolder() => DefensiveBlockDiagnostics.OpenFolder();

    [ContextMenu("Capture Defensive Block State")]
    public void CaptureDefensiveBlockState() => LogDefensiveBlockAttempt(_attemptCounter, true);

    void LogDefensiveBlockAttempt(int attempt, bool snapshot = false)
    {
        if (!logDefensiveBlock) return;
        var text = new StringBuilder($"{(snapshot ? "Snapshot after attempt" : "Attempt")} {attempt} player='{playerContext?.name}' " +
            $"position={playerContext?.transform.position} enabled={defensiveBlockEnabled} " +
            $"legacyExecuting={playerInterruptionController != null && playerInterruptionController.IsExecuting}");
        int enemies = 0;
        foreach (var actor in CharacterContextRegistry.ActiveContexts)
        {
            if (actor == null || actor.TargetIdentity != AITargetIdentity.Enemy) continue;
            enemies++;
            var attack = actor.DefensiveBlockAttack;
            text.Append($"\n Enemy '{actor.name}' id={actor.GetInstanceID()} position={actor.transform.position}: ");
            if (attack == null) { text.Append("Missing DefensiveBlockAttack"); continue; }
            text.Append(attack.DescribeBlockCommand(playerContext));
            if (playerContext == null) continue;
            var party = playerContext.fieldAllyManager;
            int allies = 0;
            if (party != null) foreach (var member in party.RegisteredMembers)
            {
                if (member == null || member.ActorRole == ChainActorRole.Player) continue;
                allies++;
                var ally = member.ActorContext;
                text.Append($"\n  Ally '{ally?.name}' character='{ally?.baseStats?.characterName}' role={member.ActorRole} busy={member.IsBusy} " +
                    $"reserved={member.IsReserved} knockback={member.IsInKnockback}: ");
                text.Append(ally is AllyContext && ally.DefensiveBlock != null
                    ? ally.DefensiveBlock.DescribeBlockReadiness(playerContext, attack)
                    : "Missing actor/DefensiveBlockController");
            }
            if (allies == 0) text.Append("\n  No registered companion candidates");
            text.Append("\n  Player fallback: " + (playerContext.DefensiveBlock != null
                ? playerContext.DefensiveBlock.DescribeBlockReadiness(playerContext, attack)
                : "Missing DefensiveBlockController"));
        }
        if (enemies == 0) text.Append("\n No active enemy contexts");
        LogDefensiveBlock(text.ToString());
    }
}
