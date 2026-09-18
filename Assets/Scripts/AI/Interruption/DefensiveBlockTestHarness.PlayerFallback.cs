using System.Collections;
using System.Text;
using UnityEngine;

public sealed partial class DefensiveBlockTestHarness
{
    void DisableFallbackTestCompanions()
    {
        foreach (var actor in partySpawn.CurrentParty.Actors)
            if (actor.Context != null && actor.Context != Player) actor.Context.gameObject.SetActive(false);
    }

    IEnumerator ValidatePlayerFallback(StringBuilder report)
    {
        foreach (float distance in new[] { 4f, 6f, 8f })
        {
            startDistance = distance; autoBlock = false; ResetTrial();
            yield return new WaitForSecondsRealtime(1f);
            DisableFallbackTestCompanions();
            var guard = Player.DefensiveBlock;
            var origin = Player.transform.position;
            float hp = Player.HealthSystem.currentHealth;
            bool movementBefore = Player.movement.enabled;
            bool pushed = false;
            Rector.KnockbackMotor.KnockbackStarted += _ => pushed = true;
            StartCharge();
            var result = Player.interruptionCommand.TryExecuteInterruptionCommand();
            bool self = guard.IsSelfGuard && Rector.DefensiveBlockAttack.Defender == guard &&
                Vector3.Distance(origin, Player.transform.position) < .01f;
            bool locked = Player.FieldAllyMember.IsReserved && !Player.movement.enabled &&
                !Player.stateHub.CanMove() && !Player.HealthSystem.IsInvincible;
            yield return new WaitForSecondsRealtime(3.5f);
            bool clean = !guard.IsExecuting && !Player.FieldAllyMember.IsReserved &&
                Player.movement.enabled == movementBefore && Player.stateHub.CanMove() && CameraReturned;
            float recoil = Vector3.Distance(origin, Player.transform.position);
            bool passed = result == InterruptionCommandResult.Success && self && locked && pushed && clean &&
                Mathf.Abs(recoil - guard.slideDistance) < .1f &&
                Rector.DefensiveBlockAttack.SuccessCount == 1 && Mathf.Approximately(hp, Player.HealthSystem.currentHealth);
            report.AppendLine($"{(passed ? "PASS" : "FAIL")} Player self guard {distance}m: same zero-damage/knockback result; " +
                $"self={self} locked={locked} pushed={pushed} recoil={recoil:0.###} loss={hp - Player.HealthSystem.currentHealth:0.###} clean={clean}");
        }

        startDistance = 8; ResetTrial(); yield return new WaitForSecondsRealtime(1f);
        DisableFallbackTestCompanions();
        GameObject recoilWall = null;
        try
        {
            Player.DefensiveBlock.Impacted += guard =>
            {
                recoilWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                recoilWall.name = "Player guard recoil validation wall";
                recoilWall.transform.position = Player.transform.position + Vector3.back * .95f + Vector3.up * 1.5f;
                recoilWall.transform.localScale = new Vector3(6f, 3f, .2f);
                Physics.SyncTransforms();
            };
            StartCharge();
            Player.interruptionCommand.TryExecuteInterruptionCommand();
            yield return new WaitForSecondsRealtime(3.5f);
            bool wallSafe = recoilWall != null && Rector.DefensiveBlockAttack.SuccessCount == 1 &&
                Player.cc.bounds.min.z >= recoilWall.GetComponent<Collider>().bounds.max.z - .05f &&
                !Player.DefensiveBlock.IsExecuting && !Player.FieldAllyMember.IsReserved;
            report.AppendLine($"{(wallSafe ? "PASS" : "FAIL")} Player self recoil stops before a wall and releases control");
        }
        finally { if (recoilWall != null) Destroy(recoilWall); }

        ResetTrial(); yield return new WaitForSecondsRealtime(1f);
        DisableFallbackTestCompanions();
        object owner = new object();
        bool reserved = Player.FieldAllyMember.TryReserve(owner);
        StartCharge();
        yield return new WaitForSecondsRealtime(.05f);
        var cue = Player.GetComponentInChildren<DefensiveBlockReadyCue>();
        bool unavailable = reserved && !Player.interruptionCommand.TrySelectDefensiveBlockAttack(out _) &&
            Player.interruptionCommand.TrySelectDefensiveBlockThreat(out var threat) && threat == Rector.DefensiveBlockAttack &&
            cue != null && cue.ThreatAttack == threat && !cue.IsReady && !cue.IsPromptVisible;
        report.AppendLine($"{(unavailable ? "PASS" : "FAIL")} unavailable receivers keep the charge telegraph without a ready prompt");
        Player.FieldAllyMember.ReleaseReservation(owner);

        foreach (string reason in new[] { "Timeout", "Disable", "Down", "Death", "ControlLoss" })
        {
            ResetTrial(); yield return new WaitForSecondsRealtime(1f);
            DisableFallbackTestCompanions();
            var player = Player;
            var guard = player.DefensiveBlock;
            // A separate owner must retain its control restriction when guard cleanup runs.
            int otherToken = player.stateHub.AcquireExternalControlBlockToken(ControlBlockFlags.Shoot);
            StartCharge();
            var command = player.interruptionCommand.TryExecuteInterruptionCommand();
            bool began = guard.IsSelfGuard;
            switch (reason)
            {
                case "Timeout": Rector.transform.position += Vector3.right * 12f; break;
                case "Disable": guard.enabled = false; break;
                case "Down": player.stateHub.LifeSM.TryChange(LifeStateId.Down); break;
                case "Death": player.HealthSystem.Die(); break;
                case "ControlLoss": player.AnimDriver.InterruptActivePlaybackForExternalControlLoss(); break;
            }
            yield return new WaitForSecondsRealtime(reason == "Timeout" ? 3.2f : .7f);
            bool clean = !guard.IsExecuting && !player.FieldAllyMember.IsReserved &&
                player.AnimBrain.BlockPhase == BlockAnimationPhase.None && CameraReturned;
            bool otherOwnerPreserved = (player.stateHub.ActiveControlBlockFlags & ControlBlockFlags.Shoot) != 0;
            player.stateHub.ReleaseExternalControlBlockToken(otherToken);
            report.AppendLine($"{(command == InterruptionCommandResult.Success && began && clean && otherOwnerPreserved ? "PASS" : "FAIL")} " +
                $"Player guard {reason} releases reservation/animation/camera and preserves another owner's control token; began={began} clean={clean}");
        }

        startDistance = 4f; ResetTrial(); yield return new WaitForSecondsRealtime(1f);
        DisableFallbackTestCompanions();
        float beforeHit = Player.HealthSystem.currentHealth;
        StartCharge();
        float deadline = Time.realtimeSinceStartup + 3f;
        while (Player.HealthSystem.currentHealth >= beforeHit && Time.realtimeSinceStartup < deadline) yield return null;
        bool late = Player.HealthSystem.currentHealth < beforeHit &&
            Rector.DefensiveBlockAttack.RequestBlock(Player) != InterruptionCommandResult.Success &&
            !Player.DefensiveBlock.IsExecuting && Rector.DefensiveBlockAttack.SuccessCount == 0;
        report.AppendLine($"{(late ? "PASS" : "FAIL")} Player fallback cannot repair damage already applied by the same request");
    }
}
