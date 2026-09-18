using System.Collections;
using System.Text;
using UnityEngine;

public sealed partial class DefensiveBlockTestHarness
{
    IEnumerator ValidateChargeWindup(StringBuilder report)
    {
        // Let initial scene/party presentation finish before issuing the first trial.
        yield return new WaitForSecondsRealtime(2f);
        float windup = chargeSkill.defensiveBlock.windupSeconds;
        if (windup <= 0f)
        {
            startDistance = 4; autoBlock = false; ResetTrial();
            yield return new WaitForSecondsRealtime(1f);
            Vector3 origin = Rector.transform.position;
            float hp = Player.HealthSystem.currentHealth;
            StartCharge();
            bool unheld = !Rector.DefensiveBlockAttack.IsPreparingCharge && Rector.DefensiveBlockAttack.WindowOpen;
            yield return new WaitForSecondsRealtime(.2f);
            bool advancing = Vector3.Distance(origin, Rector.transform.position) > .1f;
            yield return new WaitForSecondsRealtime(2.5f);
            report.AppendLine($"{(unheld && advancing && Player.HealthSystem.currentHealth < hp ? "PASS" : "FAIL")} zero windup: uninterrupted skill motion and normal damage");
            ResetTrial();
            yield break;
        }
        foreach (float distance in new[] { 4f, 6f, 8f })
        foreach (bool late in new[] { false, true })
        {
            startDistance = distance; autoBlock = false; ResetTrial();
            yield return new WaitForSecondsRealtime(1f);
            var enemy = Rector;
            var guard = Ally.DefensiveBlock;
            float hp = Player.HealthSystem.currentHealth, allyHp = Ally.HealthSystem.currentHealth;
            Vector3 origin = enemy.transform.position;
            int releases = 0;
            enemy.SkillManager.CastReleased += _ => releases++;
            float start = Time.realtimeSinceStartup, impactAt = -1f;
            guard.Impacted += _ => impactAt = Time.realtimeSinceStartup - start;
            StartCharge();
            bool held = enemy.DefensiveBlockAttack.IsPreparingCharge && enemy.DefensiveBlockAttack.WindowOpen;
            InterruptionCommandResult command = InterruptionCommandResult.TargetWindowClosed;
            if (!late) command = Player.interruptionCommand.TryExecuteInterruptionCommand();
            yield return new WaitForSecondsRealtime(windup * 0.5f);
            var cue = Player.GetComponentInChildren<DefensiveBlockReadyCue>();
            bool stationary = enemy.DefensiveBlockAttack.IsPreparingCharge && releases == 0 &&
                Vector3.Distance(origin, enemy.transform.position) < 0.05f &&
                Player.HealthSystem.currentHealth == hp && Ally.HealthSystem.currentHealth == allyHp;
            bool telegraph = cue != null && (late ? cue.IsReady : guard.HasArrived);
            if (late)
            {
                yield return new WaitForSecondsRealtime(windup * 0.5f + 0.05f);
                command = Player.interruptionCommand.TryExecuteInterruptionCommand();
            }
            yield return new WaitForSecondsRealtime(3f);
            bool passed = held && stationary && telegraph && command == InterruptionCommandResult.Success &&
                impactAt >= windup && releases == 1 && enemy.DefensiveBlockAttack.SuccessCount == 1 &&
                Player.HealthSystem.currentHealth == hp && Ally.HealthSystem.currentHealth == allyHp &&
                !guard.IsExecuting && !Ally.FieldAllyMember.IsReserved && CameraReturned;
            report.AppendLine($"{(passed ? "PASS" : "FAIL")} windup {distance}m late={late}: " +
                $"held={held} stationary={stationary} cue/arrival={telegraph} command={command} impact={impactAt:0.###} releases={releases}");
        }

        startDistance = 4; autoBlock = false; ResetTrial();
        yield return new WaitForSecondsRealtime(1f);
        float noGuardHp = Player.HealthSystem.currentHealth;
        StartCharge();
        yield return new WaitForSecondsRealtime(windup * 0.5f);
        bool safePreparation = Rector.DefensiveBlockAttack.IsPreparingCharge && Player.HealthSystem.currentHealth == noGuardHp;
        yield return new WaitForSecondsRealtime(3f);
        report.AppendLine($"{(safePreparation && Player.HealthSystem.currentHealth < noGuardHp && Rector.DefensiveBlockAttack.SuccessCount == 0 ? "PASS" : "FAIL")} no Block: windup then normal charge damage");

        foreach (bool exempt in new[] { false, true })
        {
            startDistance = 8; ResetTrial(); yield return new WaitForSecondsRealtime(1f);
            var enemy = Rector;
            if (exempt) enemy.PushWorldSlowExemption();
            int slow = TimeSlowManager.Instance.StartSlow(.1f, 10f, AnimationCurve.Constant(0, 1, 1));
            int pause = 0;
            try
            {
                StartCharge();
                bool held = enemy.DefensiveBlockAttack.IsPreparingCharge;
                pause = GlobalTimeScaleManager.Instance.AcquirePauseToken();
                yield return null;
                Vector3 pausedAt = enemy.transform.position;
                yield return new WaitForSecondsRealtime(.25f);
                held &= enemy.DefensiveBlockAttack.IsPreparingCharge && Vector3.Distance(pausedAt, enemy.transform.position) < .01f;
                GlobalTimeScaleManager.Instance.ReleasePauseToken(pause); pause = 0;
                yield return new WaitForSecondsRealtime(windup + .15f);
                bool clockMatches = enemy.DefensiveBlockAttack.IsPreparingCharge == !exempt;
                TimeSlowManager.Instance.StopSlow(slow); slow = 0;
                yield return new WaitForSecondsRealtime(.7f);
                report.AppendLine($"{(held && clockMatches && !enemy.DefensiveBlockAttack.IsPreparingCharge ? "PASS" : "FAIL")} windup obeys pause and caster World Slow exemption={exempt}");
            }
            finally
            {
                if (pause != 0) GlobalTimeScaleManager.Instance.ReleasePauseToken(pause);
                if (slow != 0) TimeSlowManager.Instance.StopSlow(slow);
                if (exempt) enemy.PopWorldSlowExemption();
            }
        }

        foreach (string reason in new[] { "Reset", "Disable", "Death" })
        {
            ResetTrial(); yield return new WaitForSecondsRealtime(1f);
            var attack = Rector.DefensiveBlockAttack;
            StartCharge();
            bool held = attack.IsPreparingCharge;
            Player.interruptionCommand.TryExecuteInterruptionCommand();
            if (reason == "Reset") attack.ResetExecution();
            else if (reason == "Disable") attack.enabled = false;
            else Rector.HealthSystem.Die();
            yield return new WaitForSecondsRealtime(windup + .2f);
            bool clean = !attack.IsPreparingCharge && !attack.OwnsCurrentSkill &&
                !Ally.DefensiveBlock.IsExecuting && !Ally.FieldAllyMember.IsReserved;
            report.AppendLine($"{(held && clean ? "PASS" : "FAIL")} {reason} during windup clears hold and defender");
        }
        ResetTrial();
    }
}
