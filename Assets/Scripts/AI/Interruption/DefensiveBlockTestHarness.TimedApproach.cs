using System.Collections;
using System.Text;
using UnityEngine;

public sealed partial class DefensiveBlockTestHarness
{
    IEnumerator ValidateTimedApproach(StringBuilder report)
    {
        yield return new WaitForSecondsRealtime(2f);
        var settings = chargeSkill.defensiveBlock;
        var savedMode = settings.mode;
        settings.mode = DefensiveBlockMode.TimedApproach;
        float savedDuration = settings.timedApproachSeconds;
        int savedRate = Application.targetFrameRate, savedSync = QualitySettings.vSyncCount;
        try
        {
            foreach (float duration in new[] { .22f, .5f, 1f })
            foreach (float distance in new[] { 1.5f, 2f, 4f, 6f, 8f })
            foreach (bool late in new[] { false, true })
            {
                settings.timedApproachSeconds = duration;
                startDistance = distance; autoBlock = false; ResetTrial();
                yield return new WaitForSecondsRealtime(1f);
                float hp = Player.HealthSystem.currentHealth, ahp = Ally.HealthSystem.currentHealth;
                int commits = 0, releases = 0, impacts = 0;
                Rector.SkillManager.CastCommitted += _ => commits++;
                Rector.SkillManager.CastReleased += _ => releases++;
                StartCharge();
                if (late) yield return new WaitForSecondsRealtime(.12f);
                float accepted = Time.realtimeSinceStartup, impactAt = -1f;
                Vector3 origin = Rector.transform.position;
                var command = Player.interruptionCommand.TryExecuteInterruptionCommand();
                var attack = Rector.DefensiveBlockAttack;
                var guard = attack.Defender;
                Vector3 endpoint = attack.ApproachDestination;
                float endpointError = float.PositiveInfinity;
                float approachTravel = float.PositiveInfinity;
                if (guard != null) guard.Impacted += _ =>
                {
                    impacts++;
                    impactAt = Time.realtimeSinceStartup - accepted;
                    // The next coroutine tick runs after knockback has already moved the actor.
                    endpointError = Vector3.Distance(endpoint, Rector.transform.position);
                    approachTravel = Vector3.Distance(origin, Rector.transform.position);
                };
                bool owned = attack.IsTimedApproach, animated = false, noRoot = owned, premature = false;
                float firstPose = 0f;
                if (guard != null) Rector.AnimBrain.TryGetActiveSkillNormalizedTime(guard.RequestId, out firstPose);
                float deadline = accepted + duration + .5f;
                while (attack.IsTimedApproach && Time.realtimeSinceStartup < deadline)
                {
                    noRoot &= !Rector.AnimBrain.RootMotionActive;
                    premature |= impacts != 0 || attack.SuccessCount != 0 || Rector.KnockbackMotor.IsActive;
                    if (Rector.AnimBrain.TryGetActiveSkillNormalizedTime(guard.RequestId, out float pose)) animated |= pose > firstPose + .01f;
                    yield return null;
                }
                bool movementMatches = Vector3.Distance(origin, endpoint) <= .05f
                    ? approachTravel < .1f : approachTravel > .05f;
                bool reached = endpointError < .1f;
                yield return new WaitForSecondsRealtime(2f);
                bool passed = command == InterruptionCommandResult.Success && owned && animated && noRoot && !premature && movementMatches && reached &&
                    impacts == 1 && impactAt >= duration - .025f && attack.SuccessCount == 1 && commits == 1 && releases == 1 &&
                    Player.HealthSystem.currentHealth == hp && Ally.HealthSystem.currentHealth == ahp &&
                    guard != null && !guard.IsExecuting && !guard.ActorContext.FieldAllyMember.IsReserved && CameraReturned;
                report.AppendLine($"{(passed ? "PASS" : "FAIL")} timed {duration}s {distance}m late={late}: command={command} owned={owned} animated={animated} noRoot={noRoot} movement/reached={movementMatches}/{reached} travel={approachTravel:0.###} impact={impactAt:0.###} count={impacts} costs={commits}/{releases} loss={hp-Player.HealthSystem.currentHealth}/{ahp-Ally.HealthSystem.currentHealth} result={attack.LastResult}");
            }

            settings.timedApproachSeconds = .5f;
            yield return ValidatePlayerFallback(report);
            // An already applied hit still rejects the same request.
            startDistance = 4; ResetTrial(); yield return new WaitForSecondsRealtime(1f);
            float before = Player.HealthSystem.currentHealth;
            StartCharge(); yield return new WaitForSecondsRealtime(2f);
            bool lateRejected = Player.HealthSystem.currentHealth < before &&
                Rector.DefensiveBlockAttack.RequestBlock(Player) != InterruptionCommandResult.Success;
            report.AppendLine($"{(lateRejected ? "PASS" : "FAIL")} no Block retains normal damage; late Block cannot undo it");

            foreach (string reason in new[] { "Reset", "DisableEnemy", "DisableGuard", "EnemyDeath", "GuardDeath", "EnemyDown", "GuardDown", "EnemyControlLoss", "GuardControlLoss", "Knockback" })
            {
                settings.timedApproachSeconds = 1f;
                startDistance = 8; ResetTrial(); yield return new WaitForSecondsRealtime(1f);
                var enemy = Rector; var guard = Ally.DefensiveBlock; var attack = enemy.DefensiveBlockAttack;
                int token = enemy.stateHub.AcquireExternalControlBlockToken(ControlBlockFlags.Shoot);
                StartCharge(); Player.interruptionCommand.TryExecuteInterruptionCommand();
                yield return new WaitForSecondsRealtime(.15f);
                bool began = attack.IsTimedApproach;
                switch (reason)
                {
                    case "Reset": attack.ResetExecution(); break;
                    case "DisableEnemy": attack.enabled = false; break;
                    case "DisableGuard": guard.enabled = false; break;
                    case "EnemyDeath": enemy.HealthSystem.Die(); break;
                    case "GuardDeath": Ally.HealthSystem.Die(); break;
                    case "EnemyDown": enemy.stateHub.LifeSM.TryChange(LifeStateId.Down); break;
                    case "GuardDown": Ally.stateHub.LifeSM.TryChange(LifeStateId.Down); break;
                    case "EnemyControlLoss": enemy.AnimDriver.InterruptActivePlaybackForExternalControlLoss(); break;
                    case "GuardControlLoss": Ally.AnimDriver.InterruptActivePlaybackForExternalControlLoss(); break;
                    case "Knockback": enemy.KnockbackMotor.ApplyKnockback(KnockbackData.FromOrigin(Ally.transform.position, enemy.transform.position, .5f, .2f, ImpactReactionKind.MiniStun, true, null), forceReplace: true); break;
                }
                yield return new WaitForSecondsRealtime(1.3f);
                bool clean = !attack.IsTimedApproach && attack.SuccessCount == 0 && !guard.IsExecuting && !Ally.FieldAllyMember.IsReserved;
                bool owner = (enemy.stateHub.ActiveControlBlockFlags & ControlBlockFlags.Shoot) != 0;
                enemy.stateHub.ReleaseExternalControlBlockToken(token);
                report.AppendLine($"{(began && clean && owner ? "PASS" : "FAIL")} timed {reason}: began={began} clean={clean} otherOwner={owner}");
            }

            startDistance = 8; ResetTrial(); yield return new WaitForSecondsRealtime(1f);
            GameObject wall = null;
            try
            {
                StartCharge(); Player.interruptionCommand.TryExecuteInterruptionCommand();
                yield return new WaitForSecondsRealtime(.1f);
                bool began = Rector.DefensiveBlockAttack.IsTimedApproach;
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = Vector3.Lerp(Rector.transform.position, Rector.DefensiveBlockAttack.ApproachDestination, .5f) + Vector3.up * 1.5f;
                wall.transform.localScale = new Vector3(6, 3, .2f); Physics.SyncTransforms();
                yield return new WaitForSecondsRealtime(1.5f);
                bool cancelled = began && Rector.DefensiveBlockAttack.SuccessCount == 0 && !Rector.DefensiveBlockAttack.IsTimedApproach && !Ally.DefensiveBlock.IsExecuting;
                report.AppendLine($"{(cancelled ? "PASS" : "FAIL")} obstacle during approach cancels without timed impact");
            }
            finally { if (wall != null) Destroy(wall); }

            ResetTrial(); yield return new WaitForSecondsRealtime(1f);
            StartCharge(); Player.interruptionCommand.TryExecuteInterruptionCommand();
            yield return new WaitForSecondsRealtime(.15f);
            int pause = GlobalTimeScaleManager.Instance.AcquirePauseToken(), slow = 0;
            try
            {
                yield return null;
                float progress = Rector.DefensiveBlockAttack.ApproachProgress;
                Vector3 position = Rector.transform.position;
                yield return new WaitForSecondsRealtime(.2f);
                bool paused = Mathf.Abs(progress - Rector.DefensiveBlockAttack.ApproachProgress) < .001f && Vector3.Distance(position, Rector.transform.position) < .001f;
                GlobalTimeScaleManager.Instance.ReleasePauseToken(pause); pause = 0;
                slow = TimeSlowManager.Instance.StartSlow(.1f, 10, AnimationCurve.Constant(0,1,1));
                yield return new WaitForSecondsRealtime(.3f);
                bool slowed = Rector.DefensiveBlockAttack.IsTimedApproach && Rector.DefensiveBlockAttack.ApproachProgress - progress < .15f;
                float hp = Player.HealthSystem.currentHealth;
                Player.HealthSystem.TakeDamage(default(DamageContext).WithDamage(1));
                bool vulnerable = Player.HealthSystem.currentHealth < hp;
                TimeSlowManager.Instance.StopSlow(slow); slow = 0;
                yield return new WaitForSecondsRealtime(3f);
                report.AppendLine($"{(paused && slowed && vulnerable && Rector.DefensiveBlockAttack.SuccessCount == 1 ? "PASS" : "FAIL")} timed pause/slow/unrelated damage: paused={paused} slowed={slowed} vulnerable={vulnerable}");
            }
            finally
            {
                if (pause != 0) GlobalTimeScaleManager.Instance.ReleasePauseToken(pause);
                if (slow != 0) TimeSlowManager.Instance.StopSlow(slow);
            }

            settings.timedApproachSeconds = .5f;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = 15;
            ResetTrial(); yield return new WaitForSecondsRealtime(1f);
            before = Player.HealthSystem.currentHealth;
            StartCharge(); Player.interruptionCommand.TryExecuteInterruptionCommand();
            yield return new WaitForSecondsRealtime(3f);
            report.AppendLine($"{(Rector.DefensiveBlockAttack.SuccessCount == 1 && Player.HealthSystem.currentHealth == before ? "PASS" : "FAIL")} timed impact at 15fps occurs once");
        }
        finally
        {
            settings.timedApproachSeconds = savedDuration;
            settings.mode = savedMode;
            Application.targetFrameRate = savedRate; QualitySettings.vSyncCount = savedSync;
            startDistance = 8; autoBlock = false; ResetTrial();
        }
    }
}
