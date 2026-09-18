using System.Collections;
using System.Text;
using UnityEngine;

public sealed partial class DefensiveBlockTestHarness
{
    IEnumerator ValidateProductionIntegration(StringBuilder report)
    {
        startDistance = 8; autoBlock = false; ResetTrial();
        yield return new WaitForSecondsRealtime(1f);
        Player.Targeting.enabled = false;
        typeof(PlayerTargetingController).GetMethod("CommitTarget", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(Player.Targeting, new object[] { null });
        StartCharge();
        var facing = Rector.transform.rotation;
        Rector.transform.rotation = Quaternion.Euler(0, 90, 0);
        bool rejectsPassing = !Player.interruptionCommand.TrySelectDefensiveBlockAttack(out _);
        Rector.transform.rotation = facing;
        yield return new WaitForSecondsRealtime(.05f);
        var readyCue = Player.GetComponentInChildren<DefensiveBlockReadyCue>();
        bool selectsWithoutAim = Player.Targeting.CurrentTarget == null &&
            Player.interruptionCommand.TrySelectDefensiveBlockAttack(out var automatic) && automatic == Rector.DefensiveBlockAttack;
        bool cueAgrees = readyCue != null && readyCue.IsReady && readyCue.ReadyAttack == Rector.DefensiveBlockAttack;
        float playerHp = Player.HealthSystem.currentHealth, allyHp = Ally.HealthSystem.currentHealth;
        var automaticResult = Player.interruptionCommand.TryExecuteInterruptionCommand();
        yield return new WaitForSecondsRealtime(3f);
        bool noAimImpact = automaticResult == InterruptionCommandResult.Success && Rector.DefensiveBlockAttack.SuccessCount == 1 &&
            Mathf.Approximately(playerHp, Player.HealthSystem.currentHealth) && Mathf.Approximately(allyHp, Ally.HealthSystem.currentHealth);
        report.AppendLine($"{(rejectsPassing ? "PASS" : "FAIL")} automatic selection rejects a charge passing beside Player");
        report.AppendLine($"{(selectsWithoutAim && cueAgrees ? "PASS" : "FAIL")} no committed target: command and ready cue select the same incoming attack");
        report.AppendLine($"{(noAimImpact ? "PASS" : "FAIL")} normal command without aiming blocks once with no Player/Aires HP loss");

        startDistance = 8; autoBlock = false; ResetTrial();
        yield return new WaitForSecondsRealtime(1f);
        var second = partySpawn.CurrentParty.GetActor(ChainActorRole.PartySlot2).Context as AllyContext;
        second.gameObject.SetActive(false);
        StartCharge();
        bool configured = partySpawn.CurrentParty.Actors.Count == 4 && Player.CharacterLoad != null &&
            Ally.CharacterLoad != null && Player.interruptionCommand.defensiveBlockEnabled &&
            chargeSkill.defensiveBlock != null && chargeSkill == Rector.DefensiveBlockAttack.skill && LiveCamera != null;
        report.AppendLine($"{(configured ? "PASS" : "FAIL")} real party loader, skill, command and camera bindings");
        object reservation = new object();
        bool reserved = Ally.FieldAllyMember.TryReserve(reservation);
        bool blockedByReservation = reserved && Rector.DefensiveBlockAttack.CanRequestBlock(Player) &&
            Player.interruptionCommand.TrySelectDefensiveBlockDefender(Rector.DefensiveBlockAttack, out var fallback) &&
            fallback == Player.DefensiveBlock;
        Ally.FieldAllyMember.ReleaseReservation(reservation);
        report.AppendLine($"{(blockedByReservation ? "PASS" : "FAIL")} reserved companion selects Player fallback");
        var originalDefinition = Ally.baseStats;
        var unsupported = Instantiate(originalDefinition); unsupported.defensiveBlock = null;
        Ally.baseStats = unsupported;
        bool noCapability = Rector.DefensiveBlockAttack.CanRequestBlock(Player) &&
            Player.interruptionCommand.TrySelectDefensiveBlockDefender(Rector.DefensiveBlockAttack, out fallback) &&
            fallback == Player.DefensiveBlock;
        Ally.baseStats = originalDefinition; Destroy(unsupported);
        report.AppendLine($"{(noCapability ? "PASS" : "FAIL")} unsupported companion selects Player fallback");
        int layers = Ally.rb != null ? Ally.rb.excludeLayers.value : 0;
        bool vulnerable = !Ally.HealthSystem.IsInvincible;
        var result = Rector.DefensiveBlockAttack.RequestBlock(Player);
        bool unprotected = result == InterruptionCommandResult.Success && vulnerable && !Ally.HealthSystem.IsInvincible &&
            (Ally.rb == null || Ally.rb.excludeLayers.value == layers);
        report.AppendLine($"{(unprotected ? "PASS" : "FAIL")} block keeps normal damage and collision eligibility");
        Player.interruptionCommand.defensiveBlockEnabled = false;
        yield return null;
        bool toggleClean = !Ally.DefensiveBlock.IsExecuting && !Ally.FieldAllyMember.IsReserved;
        report.AppendLine($"{(toggleClean ? "PASS" : "FAIL")} disabling feature cancels accepted block and releases reservation");

        ResetTrial(); yield return new WaitForSecondsRealtime(1f);
        bool shotSeen = false; Vector3 arrival = Vector3.zero;
        Ally.DefensiveBlock.Arrived += guard => { arrival = guard.transform.position; };
        float recoil = 0f;
        StartCharge(); Rector.DefensiveBlockAttack.RequestBlock(Player);
        float deadline = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < deadline)
        {
            shotSeen |= LiveCamera != null && LiveCamera.IsDefensiveBlockShotActive;
            if (arrival != Vector3.zero && Ally.DefensiveBlock.IsExecuting)
                recoil = Mathf.Max(recoil, arrival.z - Ally.transform.position.z);
            yield return null;
        }
        bool slid = Rector.DefensiveBlockAttack.SuccessCount == 1 && recoil > 0.05f && recoil <= 1.55f;
        report.AppendLine($"{(slid ? "PASS" : "FAIL")} production trigger footprint recoil={recoil:0.###}m");
        report.AppendLine($"{(shotSeen && CameraReturned ? "PASS" : "FAIL")} production Cinemachine shot enters and returns");

        // Another caster may reuse the same request number, but cannot intercept this guard.
        ResetTrial(); yield return new WaitForSecondsRealtime(1f);
        second = partySpawn.CurrentParty.GetActor(ChainActorRole.PartySlot2).Context as AllyContext;
        second.gameObject.SetActive(false);
        var other = Instantiate(rectorPrefab, new Vector3(3, 0, 8), Quaternion.Euler(0,180,0), actors).GetComponent<EnemyContext>();
        other.ResolveReferences(); PauseAI(other);
        yield return null;
        StartCharge(); Rector.DefensiveBlockAttack.RequestBlock(Player);
        other.SkillManager.TryStartExternalSkill(chargeSkill, "BlockIsolationValidation", usePlanarRootMotion:true, primaryTarget:SkillTargetHandle.For(Player));
        bool isolated = !Ally.DefensiveBlock.IsReadyFor(other.DefensiveBlockAttack, Ally.DefensiveBlock.RequestId) &&
            other.DefensiveBlockAttack.RequestBlock(Player) != InterruptionCommandResult.Success;
        report.AppendLine($"{(isolated ? "PASS" : "FAIL")} equal request ids on different casters cannot steal guard");
        other.gameObject.SetActive(false);
        // Replacement must cancel synchronously for contact eligibility and release on Update.
        originalDefinition = Ally.baseStats;
        unsupported = Instantiate(originalDefinition); unsupported.defensiveBlock = null;
        Ally.baseStats = unsupported;
        bool rejectedStale = !Ally.DefensiveBlock.IsReadyFor(Rector.DefensiveBlockAttack, Ally.DefensiveBlock.RequestId);
        yield return null;
        bool swapped = rejectedStale && !Ally.DefensiveBlock.IsExecuting && !Ally.FieldAllyMember.IsReserved;
        Ally.baseStats = originalDefinition; Destroy(unsupported);
        report.AppendLine($"{(swapped ? "PASS" : "FAIL")} party character replacement invalidates accepted session");

        ResetTrial(); yield return new WaitForSecondsRealtime(1f);
        Ally.BehaviorTree.enabled = true;
        if (Ally.AgentMoveDriver != null) Ally.AgentMoveDriver.enabled = true;
        StartCharge();
        var liveResult = Rector.DefensiveBlockAttack.RequestBlock(Player);
        bool suspended = liveResult == InterruptionCommandResult.Success && !Ally.BehaviorTree.enabled &&
            (Ally.AgentMoveDriver == null || !Ally.AgentMoveDriver.enabled);
        yield return new WaitForSecondsRealtime(2f);
        bool resumed = Ally.BehaviorTree.enabled && (Ally.AgentMoveDriver == null || Ally.AgentMoveDriver.enabled) &&
            !Ally.FieldAllyMember.IsReserved && !Ally.DefensiveBlock.IsExecuting;
        report.AppendLine($"{(suspended && resumed ? "PASS" : "FAIL")} active companion AI suspends and resumes after its own reaction");
        yield return ValidateReactionClocks(report);
    }

    IEnumerator ValidateReactionClocks(StringBuilder report)
    {
        foreach (bool exempt in new[] { false, true })
        {
            startDistance = 8; autoBlock = false; ResetTrial();
            yield return new WaitForSecondsRealtime(1f);
            var guard = Ally;
            var enemy = Rector;
            int slow = 0, pause = 0;
            if (exempt) { guard.PushWorldSlowExemption(); enemy.PushWorldSlowExemption(); }
            try
            {
                StartCharge();
                var command = Player.interruptionCommand.TryExecuteInterruptionCommand();
                float deadline = Time.realtimeSinceStartup + 3f;
                while (guard.AnimBrain.BlockPhase != BlockAnimationPhase.Impact && Time.realtimeSinceStartup < deadline)
                    yield return null;
                bool impacted = guard.AnimBrain.BlockPhase == BlockAnimationPhase.Impact;
                Vector3 origin = guard.transform.position;
                slow = TimeSlowManager.Instance.StartSlow(.25f, 10f, AnimationCurve.Constant(0, 1, 1));
                GlobalTimeScaleManager.Instance.RequestHitLag(.15f, .1f, AnimationCurve.Constant(0, 1, 1));
                yield return new WaitForSecondsRealtime(.7f);
                float recoil = Vector3.Distance(origin, guard.transform.position);
                bool matched = exempt
                    ? !enemy.KnockbackMotor.IsActive && !guard.DefensiveBlock.IsExecuting
                    : enemy.KnockbackMotor.IsActive && guard.AnimBrain.BlockPhase == BlockAnimationPhase.Impact && recoil > .05f && recoil < 1f;
                if (!exempt)
                {
                    pause = GlobalTimeScaleManager.Instance.AcquirePauseToken();
                    yield return null;
                    Vector3 guardPaused = guard.transform.position, enemyPaused = enemy.transform.position;
                    yield return new WaitForSecondsRealtime(.2f);
                    bool held = Vector3.Distance(guardPaused, guard.transform.position) < .001f &&
                        Vector3.Distance(enemyPaused, enemy.transform.position) < .001f && enemy.KnockbackMotor.IsActive &&
                        guard.AnimBrain.BlockPhase == BlockAnimationPhase.Impact;
                    report.AppendLine($"{(impacted && held ? "PASS" : "FAIL")} pause holds Block recoil and knockback during World Slow");
                    GlobalTimeScaleManager.Instance.ReleasePauseToken(pause); pause = 0;
                }
                TimeSlowManager.Instance.StopSlow(slow); slow = 0;
                yield return new WaitForSecondsRealtime(2f);
                bool clean = !guard.DefensiveBlock.IsExecuting && !guard.FieldAllyMember.IsReserved &&
                    !enemy.KnockbackMotor.IsActive && CameraReturned;
                report.AppendLine($"{(command == InterruptionCommandResult.Success && impacted && matched && clean ? "PASS" : "FAIL")} reaction clocks WorldSlow+HitLag exempt={exempt} matched={matched} recoil={recoil:0.###} clean={clean}");
            }
            finally
            {
                if (pause != 0) GlobalTimeScaleManager.Instance.ReleasePauseToken(pause);
                if (slow != 0) TimeSlowManager.Instance.StopSlow(slow);
                if (exempt) { guard.PopWorldSlowExemption(); enemy.PopWorldSlowExemption(); }
            }
        }

        ResetTrial(); yield return new WaitForSecondsRealtime(1f);
        int playerSlow = TimeSlowManager.Instance.StartSlow(.1f, 10f, AnimationCurve.Constant(0, 1, 1));
        try
        {
            Vector3 before = Player.transform.position;
            var push = KnockbackData.FromOrigin(before + Vector3.forward, before, 1f, .4f, ImpactReactionKind.MiniStun, true, null);
            bool accepted = Player.KnockbackMotor.ApplyKnockback(push, forceReplace: true);
            yield return new WaitForSecondsRealtime(.7f);
            bool finished = !Player.KnockbackMotor.IsActive && Vector3.Distance(before, Player.transform.position) > .9f;
            report.AppendLine($"{(accepted && !Player.UsesWorldSlow && finished ? "PASS" : "FAIL")} Player knockback respects World Slow exemption");
        }
        finally { TimeSlowManager.Instance.StopSlow(playerSlow); }
    }
}
