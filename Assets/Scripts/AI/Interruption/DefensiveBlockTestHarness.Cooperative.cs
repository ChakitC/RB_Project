using System.Collections;
using System.Text;
using UnityEngine;

public sealed partial class DefensiveBlockTestHarness
{
    public DefensiveBlockActorProfile cooperativePlayerProfile;
    public DefensiveBlockActorProfile cooperativeAllyProfile;
    CharacterStats cooperativePlayerDefinition, cooperativeAllyDefinition;

    void ConfigureCooperativeTrial()
    {
        if (cooperativePlayerDefinition != null) Destroy(cooperativePlayerDefinition);
        if (cooperativeAllyDefinition != null) Destroy(cooperativeAllyDefinition);
        if (chargeSkill?.defensiveBlock?.mode != DefensiveBlockMode.Cooperative) return;
        if (Player != null && cooperativePlayerProfile != null)
        {
            cooperativePlayerDefinition = Instantiate(Player.baseStats);
            cooperativePlayerDefinition.defensiveBlock = cooperativePlayerProfile;
            Player.baseStats = cooperativePlayerDefinition;
        }
        if (Ally != null && cooperativeAllyProfile != null)
        {
            cooperativeAllyDefinition = Instantiate(Ally.baseStats);
            cooperativeAllyDefinition.defensiveBlock = cooperativeAllyProfile;
            Ally.baseStats = cooperativeAllyDefinition;
        }
    }

    [ContextMenu("Run Cooperative Validation")]
    public void RunCooperativeValidation()
    {
        if (!Application.isPlaying || validation != null || chargeSkill?.defensiveBlock?.mode != DefensiveBlockMode.Cooperative) return;
        savedFrameRate = Application.targetFrameRate; savedVSync = QualitySettings.vSyncCount;
        validation = StartCoroutine(ValidateCooperative());
    }

    IEnumerator ValidateCooperative()
    {
        var report = new StringBuilder(); ValidationReport = "Cooperative validation running";
        autoBlock = false;
        foreach (string trial in new[] { "Success", "Failure", "Pause", "Protection", "CancelPreparing", "CancelStruggling", "Disable", "Reset", "EnemyDeath", "AllyDeath", "Swap", "ControlLoss" })
        {
            ResetTrial();
            while (resetting) yield return null;
            yield return new WaitForSecondsRealtime(.7f);
            float playerHp = Player.HealthSystem.currentHealth, allyHp = Ally.HealthSystem.currentHealth;
            int playerHits = 0, allyHits = 0, commits = 0, releases = 0;
            Rector.SkillManager.CastCommitted += _ => commits++;
            Rector.SkillManager.CastReleased += _ => releases++;
            Player.HealthSystem.DamageTaken += (_, __) => playerHits++;
            Ally.HealthSystem.DamageTaken += (_, __) => allyHits++;
            StartCharge();
            double deadline = Time.realtimeSinceStartupAsDouble + 5d;
            while (!Rector.DefensiveBlockAttack.CanRequestBlock(Player) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (trial == "Success") report.AppendLine(ValidateCooperativeSelection());
            var accepted = Rector.DefensiveBlockAttack.RequestBlock(Player);
            var session = Player.interruptionCommand.ActiveCooperativeBlock;
            bool extra = true;
            if (session != null)
            {
                if (trial == "CancelPreparing") session.Cancel();
                deadline = Time.realtimeSinceStartupAsDouble + 4d;
                while (session.State == CooperativeBlockState.Preparing && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (trial == "Pause" && session.State == CooperativeBlockState.Struggling)
                {
                    int token = GlobalTimeScaleManager.Instance.AcquirePauseToken();
                    float meterBefore = session.Meter, timeBefore = session.Remaining;
                    yield return new WaitForSecondsRealtime(.5f);
                    extra = !session.Press() && Mathf.Approximately(meterBefore, session.Meter) && Mathf.Approximately(timeBefore, session.Remaining);
                    GlobalTimeScaleManager.Instance.ReleasePauseToken(token);
                }
                if (trial == "CancelStruggling") session.Cancel();
                if (trial == "Protection") extra = ValidateCooperativeProtection(session);
                if (trial == "Disable") Ally.DefensiveBlock.enabled = false;
                if (trial == "Reset") Rector.DefensiveBlockAttack.ResetExecution();
                if (trial == "EnemyDeath") Rector.HealthSystem.Die();
                if (trial == "AllyDeath") Ally.HealthSystem.Die();
                if (trial == "Swap") Player.baseStats = cooperativeAllyDefinition;
                if (trial == "ControlLoss") Ally.AnimDriver.InterruptActivePlaybackForExternalControlLoss();
                deadline = Time.realtimeSinceStartupAsDouble + 5d;
                while (session.IsActive && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    if ((trial == "Success" || trial == "Pause") && session.State == CooperativeBlockState.Struggling)
                        session.Press();
                    yield return new WaitForSecondsRealtime(.06f);
                }
            }
            yield return new WaitForSecondsRealtime(1.2f);
            deadline = Time.realtimeSinceStartupAsDouble + 3d;
            while ((Player.KnockbackMotor.IsActive || Ally.KnockbackMotor.IsActive ||
                (Player.HealthSystem.IsAlive && Player.FieldAllyMember.IsBusy) ||
                (Ally.stateHub.IsAlive && Ally.FieldAllyMember.IsBusy) || !CameraReturned) &&
                Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            bool won = trial == "Success" || trial == "Pause";
            var expected = won ? CooperativeBlockState.Success : trial == "Failure" ? CooperativeBlockState.Failure : CooperativeBlockState.Cancelled;
            bool damageValid = trial == "Failure" ? playerHits == 1 && allyHits == 1 && Player.HealthSystem.currentHealth < playerHp && Ally.HealthSystem.currentHealth < allyHp : playerHits == 0 && allyHits == 0;
            bool clean = !Player.DefensiveBlock.IsExecuting && !Ally.DefensiveBlock.IsExecuting &&
                !Player.FieldAllyMember.IsBusy && (!Ally.stateHub.IsAlive || !Ally.FieldAllyMember.IsBusy) &&
                !Player.FieldAllyMember.IsReserved && !Ally.FieldAllyMember.IsReserved &&
                !Ally.FieldAllyMember.HasActiveSequenceExecution && !Ally.FieldAllyMember.HasDeferredSequenceCleanup &&
                Player.interruptionCommand.ActiveCooperativeBlock == null &&
                Player.movement.enabled && CameraReturned && !GlobalTimeScaleManager.Instance.IsPaused;
            bool passed = accepted == InterruptionCommandResult.Success && session?.Outcome == expected && !session.IsActive && damageValid && clean && extra && commits == 1 && releases == 1;
            report.AppendLine($"{(passed ? "PASS" : "FAIL")} {trial}: accepted={accepted} result={session?.Outcome} hits={playerHits}/{allyHits} loss={playerHp - Player.HealthSystem.currentHealth:0.##}/{allyHp - Ally.HealthSystem.currentHealth:0.##} clean={clean} extra={extra} costs={commits}/{releases} {Rector.DefensiveBlockAttack.LastResult}");
            ValidationReport = report.ToString();
        }
        yield return ValidateCooperativeRecovery(report);
        yield return ValidateCooperativeInput(report);
        ValidationReport = report.ToString();
        Debug.Log("Cooperative Block PlayMode\n" + ValidationReport);
        validation = null;
    }

    IEnumerator ValidateCooperativeRecovery(StringBuilder report)
    {
        ResetTrial();
        while (resetting) yield return null;
        yield return new WaitForSecondsRealtime(.7f);
        var playerAnimation = Instantiate(Player.baseStats.animProfile);
        var allyAnimation = Instantiate(Ally.baseStats.animProfile);
        playerAnimation.block = Player.AnimBrain.BlockAnimation.Copy();
        allyAnimation.block = Ally.AnimBrain.BlockAnimation.Copy();
        playerAnimation.block.impactSeconds = 1f; playerAnimation.block.exitSeconds = .12f;
        allyAnimation.block.impactSeconds = 3f; allyAnimation.block.exitSeconds = .9f;
        Player.AnimBrain.SetAnimProfileOverride(playerAnimation);
        Ally.AnimBrain.SetAnimProfileOverride(allyAnimation);
        CooperativeBlockSession session = null;
        try
        {
            StartCharge();
            double deadline = Time.realtimeSinceStartupAsDouble + 5d;
            while (!Rector.DefensiveBlockAttack.CanRequestBlock(Player) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            var accepted = Rector.DefensiveBlockAttack.RequestBlock(Player);
            session = Player.interruptionCommand.ActiveCooperativeBlock;
            while (session != null && session.State == CooperativeBlockState.Preparing && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            while (session != null && session.State == CooperativeBlockState.Struggling) session.Press();
            bool won = session?.State == CooperativeBlockState.Success;
            bool pausedRecovery = false, cameraBefore = false, cameraAt80 = false, separateRelease = false, longRecovery = false;
            bool protection = true;
            bool slowBefore = false, slowAt80 = false, normalSpeedWhileAllyRecovers = false;
            var cameraOwner = typeof(GameplayCameraController).GetField("blockShotOwner",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (won)
            {
                // Changing authoring after acceptance must not shorten the active playback/watchdog.
                allyAnimation.block.impactSeconds = .01f; allyAnimation.block.exitSeconds = .01f;
                int pause = GlobalTimeScaleManager.Instance.AcquirePauseToken();
                float before = Player.AnimBrain.BlockImpactProgress;
                try
                {
                    yield return new WaitForSecondsRealtime(.3f);
                    pausedRecovery = session.IsActive && Mathf.Approximately(before, Player.AnimBrain.BlockImpactProgress);
                }
                finally { GlobalTimeScaleManager.Instance.ReleasePauseToken(pause); }
            }
            deadline = Time.realtimeSinceStartupAsDouble + 8d;
            float recoveryElapsed = 0f;
            while (session != null && session.IsActive && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return new WaitForEndOfFrame();
                recoveryElapsed += Time.deltaTime;
                bool ownsCamera = ReferenceEquals(cameraOwner.GetValue(LiveCamera), session);
                float progress = Player.AnimBrain.BlockImpactProgress;
                if (Player.AnimBrain.BlockPhase == BlockAnimationPhase.Impact)
                {
                    if (progress > .2f && progress < .7f)
                    {
                        cameraBefore |= ownsCamera;
                        slowBefore |= TimeSlowManager.Instance.WorldTimeScale < 1f;
                    }
                    if (progress >= .8f && progress < 1f && !ownsCamera)
                    {
                        cameraAt80 = true;
                        slowAt80 |= Mathf.Approximately(TimeSlowManager.Instance.WorldTimeScale, 1f);
                    }
                }
                if (Player.DefensiveBlock.IsExecuting) protection &= Player.HealthSystem.IsInvincible;
                if (Ally.DefensiveBlock.IsExecuting) protection &= Ally.HealthSystem.IsInvincible;
                if (!Player.DefensiveBlock.IsExecuting && Ally.DefensiveBlock.IsExecuting)
                {
                    separateRelease |= !Player.HealthSystem.IsInvincible && Player.stateHub.CanMove() && Ally.HealthSystem.IsInvincible;
                    normalSpeedWhileAllyRecovers |= Mathf.Approximately(TimeSlowManager.Instance.WorldTimeScale, 1f);
                }
                if (recoveryElapsed > 2.2f && Ally.DefensiveBlock.IsExecuting) longRecovery = true;
            }
            bool clean = session != null && !session.IsActive && !Player.HealthSystem.IsInvincible &&
                !Ally.HealthSystem.IsInvincible && !Player.DefensiveBlock.IsExecuting && !Ally.DefensiveBlock.IsExecuting;
            bool passed = accepted == InterruptionCommandResult.Success && won && pausedRecovery && cameraBefore &&
                cameraAt80 && slowBefore && slowAt80 && normalSpeedWhileAllyRecovers && separateRelease && longRecovery && protection && clean;
            report.AppendLine($"{(passed ? "PASS" : "FAIL")} Recovery: pause={pausedRecovery} cameraBefore={cameraBefore} camera80={cameraAt80} slowBefore={slowBefore} slowReleased80={slowAt80} normalSpeedWhileAllyRecovers={normalSpeedWhileAllyRecovers} separateRelease={separateRelease} over2s={longRecovery} protection={protection} clean={clean}");
        }
        finally
        {
            session?.Cancel();
            Player.AnimBrain.ClearAnimProfileOverride(); Ally.AnimBrain.ClearAnimProfileOverride();
            Destroy(playerAnimation); Destroy(allyAnimation);
        }
    }

    bool ValidateCooperativeProtection(CooperativeBlockSession session)
    {
        var effect = ScriptableObject.CreateInstance<StatusEffectDef>(); effect.effectId = "cooperative-validation";
        bool passed = session.State == CooperativeBlockState.Struggling;
        foreach (var actor in new CharacteContext[] { Player, Ally, Rector })
        {
            var hit = new DamageContext(10f, gameObject, "cooperative-test", "external", 0, 0, PassiveEventOrigin.External);
            var kb = KnockbackData.FromOrigin(actor.transform.position - Vector3.forward, actor.transform.position, 2, .4f, ImpactReactionKind.MiniStun, true, null);
            passed &= !actor.HealthSystem.TakeDamage(in hit).Applied && actor.StatusEffects.ApplyEffect(effect, gameObject) == null &&
                !actor.KnockbackMotor.ApplyKnockback(kb, forceReplace: true) && !actor.UsesWorldSlow;
        }
        int hp = Player.HealthSystem.AcquireInvincibilityToken();
        int status = Player.StatusEffects.AcquireApplicationBlockToken();
        int knockback = Player.KnockbackMotor.AcquireApplicationBlockToken();
        session.Cancel(); session.Cancel();
        var after = new DamageContext(10f, gameObject, "cooperative-test", "external", 0, 0, PassiveEventOrigin.External);
        var push = KnockbackData.FromOrigin(Player.transform.position - Vector3.forward, Player.transform.position, 2, .4f, ImpactReactionKind.MiniStun, true, null);
        passed &= !Player.HealthSystem.TakeDamage(in after).Applied && Player.StatusEffects.ApplyEffect(effect, gameObject) == null &&
            !Player.KnockbackMotor.ApplyKnockback(push, forceReplace: true);
        Player.HealthSystem.ReleaseInvincibilityToken(hp); Player.StatusEffects.ReleaseApplicationBlockToken(status);
        Player.KnockbackMotor.ReleaseApplicationBlockToken(knockback);
        Destroy(effect); return passed;
    }

    string ValidateCooperativeSelection()
    {
        var source = Rector.DefensiveBlockAttack;
        var command = Player.interruptionCommand;
        bool playerReady, noAlly, busy, order, leftBlocked, bothBlocked, floorGone;
        bool initial = command.TrySelectCooperativeAlly(source, out var selected) && selected == Ally.DefensiveBlock;
        Player.DefensiveBlock.enabled = false;
        playerReady = !command.TrySelectCooperativeAlly(source, out _); Player.DefensiveBlock.enabled = true;
        // Production profiles can opt additional party members in. Isolate the unavailable
        // helper checks, then restore them before testing normal party-slot fallback.
        var otherGuards = new System.Collections.Generic.List<DefensiveBlockController>();
        foreach (var member in Player.fieldAllyManager.RegisteredMembers)
        {
            var guard = member != null ? member.ActorContext?.DefensiveBlock : null;
            if (guard == null || guard == Player.DefensiveBlock || guard == Ally.DefensiveBlock || !guard.enabled) continue;
            otherGuards.Add(guard); guard.enabled = false;
        }
        var reservation = new object();
        bool reserved;
        try
        {
            Ally.DefensiveBlock.enabled = false;
            noAlly = !command.TrySelectCooperativeAlly(source, out _); Ally.DefensiveBlock.enabled = true;
            reserved = Ally.FieldAllyMember.TryReserve(reservation);
            busy = reserved && !command.TrySelectCooperativeAlly(source, out _);
        }
        finally
        {
            Ally.DefensiveBlock.enabled = true;
            Ally.FieldAllyMember.ReleaseReservation(reservation);
            foreach (var guard in otherGuards) guard.enabled = true;
        }
        var second = partySpawn.CurrentParty.GetActor(ChainActorRole.PartySlot2).Context;
        var definition = second.baseStats; second.baseStats = cooperativeAllyDefinition;
        order = command.TrySelectCooperativeAlly(source, out selected) && selected == Ally.DefensiveBlock;
        reserved = Ally.FieldAllyMember.TryReserve(reservation);
        order &= reserved && command.TrySelectCooperativeAlly(source, out selected) && selected == second.DefensiveBlock;
        Ally.FieldAllyMember.ReleaseReservation(reservation); second.baseStats = definition;
        Vector3 forward = (Rector.transform.position - Player.transform.position).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float separation = Player.DefensiveBlock.CooperativeRadius + Ally.DefensiveBlock.CooperativeRadius + .15f;
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Cooperative placement validation wall";
        wall.transform.localScale = new Vector3(.7f, 2f, .7f);
        wall.transform.position = Player.transform.position - right * separation + Vector3.up;
        Physics.SyncTransforms();
        leftBlocked = source.TryPlanCooperative(Player, Ally.DefensiveBlock, out _, out var pose, out _, out _) &&
            CharacterPlacementResolver.TryResolve(pose, CharacterPlacementReservationRegistry.Shared, out var result) &&
            Vector3.Dot(result.StartPosition - Player.transform.position, right) > 0f;
        var wall2 = Instantiate(wall); wall2.transform.position = Player.transform.position + right * separation + Vector3.up;
        Physics.SyncTransforms(); bothBlocked = !source.CanRequestBlock(Player);
        wall.SetActive(false); wall2.SetActive(false); Destroy(wall); Destroy(wall2); Physics.SyncTransforms();
        var floor = GameObject.Find("Arena Floor").GetComponent<Collider>();
        floor.enabled = false; Physics.SyncTransforms(); floorGone = !source.CanRequestBlock(Player);
        floor.enabled = true; Physics.SyncTransforms();
        bool passed = initial && playerReady && noAlly && busy && order && leftBlocked && bothBlocked && floorGone;
        return $"{(passed ? "PASS" : "FAIL")} selection/placement: initial={initial} playerUnavailable={playerReady} noHelper={noAlly} busy={busy} partyOrder={order} leftWallUsesRight={leftBlocked} bothWalls={bothBlocked} missingFloor={floorGone}";
    }
}
