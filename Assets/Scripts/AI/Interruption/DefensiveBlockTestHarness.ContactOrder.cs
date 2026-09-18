using System.Collections;
using System.Text;
using UnityEngine;

public sealed partial class DefensiveBlockTestHarness
{
    IEnumerator ValidateContactOrder(StringBuilder report)
    {
        foreach (bool requestBeforeHit in new[] { false, true })
        {
            startDistance = 4f; autoBlock = false; ResetTrial();
            yield return new WaitForSecondsRealtime(1f);
            float hp = Player.HealthSystem.currentHealth;
            var attack = Rector.DefensiveBlockAttack;
            Ally.DefensiveBlock.CanBegin(Player, attack);
            // Keep the accepted defender in departure until the real charge reaches Player.
            Ally.DefensiveBlock.warpFadeOutSeconds = 2f;
            StartCharge();
            var accepted = requestBeforeHit ? attack.RequestBlock(Player) : InterruptionCommandResult.SkillRejected;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Mathf.Approximately(hp, Player.HealthSystem.currentHealth) && Time.realtimeSinceStartup < deadline)
                yield return null;
            bool damaged = Player.HealthSystem.currentHealth < hp;
            float afterHit = Player.HealthSystem.currentHealth;
            var retry = attack.RequestBlock(Player);
            bool missed = attack.SuccessCount == 0 && !Ally.DefensiveBlock.IsExecuting &&
                !Ally.DefensiveBlock.member.IsReserved && retry != InterruptionCommandResult.Success;
            yield return new WaitForSecondsRealtime(2.5f);
            bool noRollback = Player.HealthSystem.currentHealth <= afterHit;
            bool passed = damaged && missed && noRollback && attack.SuccessCount == 0 && CameraReturned &&
                (!requestBeforeHit || accepted == InterruptionCommandResult.Success);
            report.AppendLine($"{(passed ? "PASS" : "FAIL")} Player hit {(requestBeforeHit ? "during pending warp cancels guard" : "before command rejects late guard")}; " +
                $"accepted={accepted} damage={hp - afterHit:0.###} retry={retry} successes={attack.SuccessCount} clean={missed} noRollback={noRollback}");
        }

        startDistance = 8f; autoBlock = false; ResetTrial();
        yield return new WaitForSecondsRealtime(1f);
        float initialHp = Player.HealthSystem.currentHealth;
        bool movedAhead = false;
        Ally.DefensiveBlock.Arrived += guard =>
        {
            // Guard is fixed at its accepted position. Player then steps in front of it.
            Vector3 ahead = guard.GuardCenter + guard.transform.forward * 1.5f;
            ahead.y = Player.transform.position.y;
            Player.transform.position = ahead;
            Physics.SyncTransforms();
            movedAhead = true;
        };
        StartCharge();
        var request = Rector.DefensiveBlockAttack.RequestBlock(Player);
        yield return new WaitForSecondsRealtime(4f);
        bool playerFirst = request == InterruptionCommandResult.Success && movedAhead &&
            Rector.DefensiveBlockAttack.SuccessCount == 0 && Player.HealthSystem.currentHealth < initialHp &&
            !Ally.DefensiveBlock.IsExecuting && !Ally.DefensiveBlock.member.IsReserved && CameraReturned;
        report.AppendLine($"{(playerFirst ? "PASS" : "FAIL")} Player ahead of an arrived guard wins physical contact; " +
            $"moved={movedAhead} damage={initialHp - Player.HealthSystem.currentHealth:0.###} successes={Rector.DefensiveBlockAttack.SuccessCount}");
    }
}
