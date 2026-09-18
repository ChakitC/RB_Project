using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InterruptionCommandController : MonoBehaviour
{
    readonly struct AllyCandidate
    {
        public readonly AllyInterruptionController Controller;
        public readonly FieldAllyMember Member;
        public readonly TargetedSkillPlacementResult Placement;

        public AllyCandidate(
            AllyInterruptionController controller,
            FieldAllyMember member,
            TargetedSkillPlacementResult placement)
        {
            Controller = controller;
            Member = member;
            Placement = placement;
        }

        public bool IsValid => Controller != null && Member != null && Placement.IsValid;
    }

    [Header("Refs")]
    [SerializeField] private PlayerContext playerContext;
    [SerializeField] private PlayerInterruptionController playerInterruptionController;

    [Header("Player Interrupt")]
    [SerializeField, Min(0.5f)] private float playerInterruptRange = 4f;

    [Header("No-Warp")]
    [SerializeField, Min(0f)] private float noWarpStartDistance = 0.5f;
    [SerializeField, Min(0f)] private float noWarpTargetDistance = 1.5f;

    [Header("Hold Tuning")]
    [SerializeField, Range(0f, 1f)] private float holdSpeedMultiplier = 0.1f;
    [SerializeField, Range(0f, 0.5f)] private float holdSafetyMargin = 0.08f;

    [Header("Debug")]
    [SerializeField] private bool logInterruptionFlow;

    int _attemptCounter;
    [Header("Defensive Block")]
    public bool defensiveBlockEnabled;

    // Shared by the command and ready cue. No camera, aim target, overlap query or reservation.
    public bool TrySelectDefensiveBlockAttack(out DefensiveBlockAttack selected)
        => TrySelectDefensiveBlockThreat(out selected, true);

    public bool TrySelectDefensiveBlockThreat(out DefensiveBlockAttack selected, bool requireReady = false)
    {
        selected = null;
        if (!defensiveBlockEnabled || playerContext == null) return false;
        float bestTime = float.PositiveInfinity, bestDistance = float.PositiveInfinity;
        int bestId = int.MaxValue;
        var contexts = CharacterContextRegistry.ActiveContexts;
        for (int i = 0; i < contexts.Count; i++)
        {
            var actor = contexts[i];
            if (actor == null || !actor.isActiveAndEnabled || actor.TargetIdentity != AITargetIdentity.Enemy) continue;
            var attack = actor.DefensiveBlockAttack;
            if (attack == null || !attack.TryGetIncomingThreat(playerContext, out float time, requireReady)) continue;
            Vector3 delta = actor.transform.position - playerContext.transform.position;
            delta.y = 0f;
            float distance = delta.sqrMagnitude;
            int id = actor.GetInstanceID();
            if (!DefensiveBlockGeometry.PreferThreat(time, distance, id, bestTime, bestDistance, bestId) ||
                (requireReady && !TrySelectDefensiveBlockDefender(attack, out _))) continue;
            selected = attack; bestTime = time; bestDistance = distance; bestId = id;
        }
        return selected != null;
    }

    public bool TrySelectDefensiveBlockDefender(DefensiveBlockAttack attack, out DefensiveBlockController selected)
    {
        selected = null;
        if (!defensiveBlockEnabled || playerContext == null || attack == null || !attack.CanAcceptCommand(playerContext)) return false;
        if (TrySelectDefensiveBlockAlly(attack, out var companion)) selected = companion.DefensiveBlock;
        else if (playerContext.DefensiveBlock != null && playerContext.DefensiveBlock.CanBeginSelf(playerContext, attack))
            selected = playerContext.DefensiveBlock;
        return selected != null;
    }

    public bool TrySelectDefensiveBlockAlly(DefensiveBlockAttack attack, out AllyContext selected)
    {
        selected = null;
        if (!defensiveBlockEnabled || playerContext == null || playerContext.fieldAllyManager == null ||
            attack == null || !attack.CanAcceptCommand(playerContext)) return false;
        int bestRole = int.MaxValue;
        foreach (var member in playerContext.fieldAllyManager.RegisteredMembers)
        {
            if (member == null || member.ActorRole == ChainActorRole.Player || member.IsBusy || member.IsReserved ||
                member.IsInKnockback || !(member.ActorContext is AllyContext actor) ||
                !actor.isActiveAndEnabled || actor.DefensiveBlock == null ||
                !actor.DefensiveBlock.CanBegin(playerContext, attack)) continue;
            int role = (int)member.ActorRole;
            if (role >= bestRole) continue;
            selected = actor;
            bestRole = role;
        }
        return selected != null;
    }

    public event Action<InterruptionCommandExecution> CommandStarted;
    public event Action<InterruptionCommandResult> CommandFinished;

    void Awake()
    {
        if (playerContext == null)
            TryGetComponent(out playerContext);
        if (playerInterruptionController == null)
            TryGetComponent(out playerInterruptionController);
        if (playerInterruptionController == null)
            playerInterruptionController = GetComponentInParent<PlayerInterruptionController>();
        if (playerInterruptionController == null)
            playerInterruptionController = GetComponentInChildren<PlayerInterruptionController>();
    }

    public InterruptionCommandResult TryExecuteInterruptionCommand()
    {
        int attemptId = ++_attemptCounter;

        if (playerContext == null)
            return Finish(attemptId, InterruptionCommandResult.MissingConfiguration, "player context is missing");

        if (playerInterruptionController != null && playerInterruptionController.IsExecuting)
            return Finish(attemptId, InterruptionCommandResult.SkillRejected, "player interruption already executing");

        if (TrySelectDefensiveBlockAttack(out var incoming))
            return Finish(attemptId, incoming.RequestBlock(playerContext), incoming.LastResult);

        if (playerContext.Targeting == null)
            return Finish(attemptId, InterruptionCommandResult.NoValidTarget, "no incoming defensive attack");

        LogCommand(attemptId, "no ready defensive threat; checking legacy committed target");

        if (playerContext.Targeting.TryGetTarget(out CharacteContext defensiveTarget))
        {
            defensiveTarget.ResolveReferences();
            var defensive = defensiveTarget.DefensiveBlockAttack;
            if (defensive != null && defensive.OwnsCurrentSkill)
                return Finish(attemptId, InterruptionCommandResult.NoValidTarget, "no ready defensive receiver; legacy interruption is not used for this cast");
        }

        if (!TryFindTarget(out var targetCtx, out bool windowExistButClosed, out string targetDiagnostics))
        {
            return Finish(attemptId, windowExistButClosed
                ? InterruptionCommandResult.TargetWindowClosed
                : InterruptionCommandResult.NoValidTarget,
                targetDiagnostics);
        }

        Transform targetAnchor = targetCtx.Anchor != null
            ? targetCtx.Anchor
            : ResolveTargetAnchor(targetCtx.Transform);
        LogCommand(attemptId,
            $"target selected name='{ResolveName(targetCtx.Transform)}' requestId={targetCtx.Block.ActiveRequestId}; {targetDiagnostics}");

        bool playerReady = playerInterruptionController != null && playerInterruptionController.IsReadyForInterruption();
        float playerDist = playerReady ? XZDistance(playerContext.transform, targetAnchor) : float.MaxValue;
        bool playerClose = playerReady && playerDist <= playerInterruptRange;

        LogCommand(attemptId,
            $"playerReady={playerReady} playerDist={playerDist:0.##} playerClose={playerClose} playerInterruptRange={playerInterruptRange}");

        if (playerClose)
        {
            if (playerInterruptionController.TryResolvePlacement(targetAnchor, targetCtx.Transform, noWarpStartDistance, noWarpTargetDistance, out var playerPlacement))
                return ExecutePlayer(attemptId, targetCtx, targetAnchor, playerPlacement);

            LogCommand(attemptId, "player close but placement failed; falling back to ally");
            if (TrySelectAlly(targetAnchor, targetCtx.Transform, out AllyCandidate allyFallback, out _, out string allyFallbackDiag)
                && TryExecuteAlly(attemptId, targetCtx, allyFallback, out var allyResult, allyFallbackDiag))
                return allyResult;

            return Finish(attemptId, InterruptionCommandResult.TeleportFailed,
                "player placement failed and no ally available");
        }

        if (TrySelectAlly(targetAnchor, targetCtx.Transform, out AllyCandidate allyCandidate, out bool placementFailed, out string allyDiagnostics)
            && TryExecuteAlly(attemptId, targetCtx, allyCandidate, out var allyExecResult, allyDiagnostics))
            return allyExecResult;

        if (!playerReady)
            return Finish(attemptId,
                placementFailed ? InterruptionCommandResult.TeleportFailed : InterruptionCommandResult.NoAvailableInterrupter,
                $"no ally available and player not ready; {allyDiagnostics}");

        LogCommand(attemptId, "no ally available; attempting player warp");
        if (playerInterruptionController.TryResolvePlacement(targetAnchor, targetCtx.Transform, noWarpStartDistance, noWarpTargetDistance, out var warpPlacement))
            return ExecutePlayer(attemptId, targetCtx, targetAnchor, warpPlacement);

        return Finish(attemptId, InterruptionCommandResult.TeleportFailed,
            "player warp placement failed");
    }

    InterruptionCommandResult ExecutePlayer(
        int attemptId,
        InterruptionTargetContext targetCtx,
        Transform targetAnchor,
        TargetedSkillPlacementResult placement)
    {
        if (!targetCtx.IsCurrentLife)
            return Finish(attemptId, InterruptionCommandResult.NoValidTarget,
                "target life changed before player reservation");

        if (!targetCtx.Block.TryReserveBlock(gameObject, holdSpeedMultiplier, holdSafetyMargin, out PreCastBlockReservation reservation))
            return Finish(attemptId, InterruptionCommandResult.TargetWindowClosed,
                $"target='{ResolveName(targetCtx.Transform)}' block reservation was rejected");

        LogCommand(attemptId,
            $"block reserved requestId={reservation.RequestId} reservationId={reservation.ReservationId}");

        if (!playerInterruptionController.BeginInterruption(targetCtx, reservation, placement))
        {
            if (targetCtx.IsCurrentLife)
                targetCtx.Block.CancelReservedBlock(reservation);
            return Finish(attemptId, InterruptionCommandResult.SkillRejected,
                "player rejected interruption start");
        }

        string playerName = playerContext != null ? playerContext.transform.name : name;
        string targetName = targetCtx.Transform != null ? targetCtx.Transform.name : "<unknown>";
        CommandStarted?.Invoke(new InterruptionCommandExecution
        {
            AllyName = playerName,
            TargetName = targetName,
            ExecutorKind = InterruptionExecutorKind.Player,
            ExecutorName = playerName
        });

        return Finish(attemptId, InterruptionCommandResult.Success,
            $"player='{playerName}' target='{targetName}' requestId={reservation.RequestId} reservationId={reservation.ReservationId}");
    }

    bool TryExecuteAlly(
        int attemptId,
        InterruptionTargetContext targetCtx,
        AllyCandidate allyCandidate,
        out InterruptionCommandResult result,
        string allyDiagnostics)
    {
        result = default;
        AllyInterruptionController allyCtrl = allyCandidate.Controller;
        FieldAllyMember allyMember = allyCandidate.Member;
        TargetedSkillPlacementResult placement = allyCandidate.Placement;

        if (!allyMember.TryReserve(allyCtrl))
        {
            result = Finish(attemptId, InterruptionCommandResult.NoAvailableAlly,
                $"ally='{ResolveName(allyMember.TransformRef)}' reservation was rejected");
            return true;
        }

        if (!targetCtx.IsCurrentLife)
        {
            allyMember.ReleaseReservation(allyCtrl);
            result = Finish(attemptId, InterruptionCommandResult.NoValidTarget,
                "target life changed before ally reservation");
            return true;
        }

        LogCommand(attemptId,
            $"ally selected name='{ResolveName(allyMember.TransformRef)}' mode={placement.Mode} " +
            $"startPosition={placement.StartPosition}; {allyDiagnostics}");

        if (!targetCtx.Block.TryReserveBlock(gameObject, holdSpeedMultiplier, holdSafetyMargin, out PreCastBlockReservation reservation))
        {
            allyMember.ReleaseReservation(allyCtrl);
            result = Finish(attemptId, InterruptionCommandResult.TargetWindowClosed,
                $"target='{ResolveName(targetCtx.Transform)}' block reservation was rejected");
            return true;
        }

        LogCommand(attemptId,
            $"block reserved requestId={reservation.RequestId} reservationId={reservation.ReservationId}");

        if (!allyCtrl.BeginInterruption(targetCtx, reservation, placement))
        {
            if (targetCtx.IsCurrentLife)
                targetCtx.Block.CancelReservedBlock(reservation);
            allyMember.ReleaseReservation(allyCtrl);
            result = Finish(attemptId, InterruptionCommandResult.SkillRejected,
                $"ally='{ResolveName(allyMember.TransformRef)}' rejected interruption start");
            return true;
        }

        string allyName = allyMember.TransformRef != null ? allyMember.TransformRef.name : allyMember.name;
        string targetName = targetCtx.Transform != null ? targetCtx.Transform.name : "<unknown>";
        CommandStarted?.Invoke(new InterruptionCommandExecution
        {
            AllyName = allyName,
            TargetName = targetName,
            ExecutorKind = InterruptionExecutorKind.Ally,
            ExecutorName = allyName
        });

        result = Finish(attemptId, InterruptionCommandResult.Success,
            $"ally='{ResolveName(allyMember.TransformRef)}' target='{ResolveName(targetCtx.Transform)}' requestId={reservation.RequestId} reservationId={reservation.ReservationId}");
        return true;
    }

    bool TryFindTarget(out InterruptionTargetContext ctx, out bool windowExistButClosed, out string diagnostics)
    {
        ctx = default;
        windowExistButClosed = false;
        diagnostics = null;

        if (!playerContext.Targeting.TryGetTarget(out CharacteContext targetContext))
        {
            diagnostics = "committed target is missing or invalid";
            return false;
        }

        targetContext.ResolveReferences();
        PreCastBlockController block = targetContext.GetComponent<PreCastBlockController>();
        if (block == null)
            block = targetContext.GetComponentInChildren<PreCastBlockController>(true);
        if (block == null)
            block = targetContext.GetComponentInParent<PreCastBlockController>();

        if (block == null)
        {
            diagnostics = $"target '{targetContext.name}' has no pre-cast block controller";
            return false;
        }

        if (!block.CanBlockActiveCast())
        {
            windowExistButClosed = true;
            diagnostics = $"target '{targetContext.name}' has no open interruption window";
            return false;
        }

        if (block.HasActiveReservation)
        {
            diagnostics = $"target '{targetContext.name}' interruption window is already reserved";
            return false;
        }

        HealthSystem health = targetContext.HealthSystem;
        CharacterKnockbackMotor knockback = targetContext.KnockbackMotor;
        if (health == null || !health.IsAlive || knockback == null)
        {
            diagnostics = $"target '{targetContext.name}' is missing live health or knockback";
            return false;
        }

        ctx = new InterruptionTargetContext
        {
            Context = targetContext,
            LifeHandle = SkillTargetHandle.For(targetContext),
            Transform = targetContext.transform,
            Anchor = ResolveTargetAnchor(targetContext.transform),
            Block = block,
            Knockback = knockback,
            Health = health
        };
        diagnostics = $"committed target='{targetContext.name}' life={targetContext.LifeGeneration}";
        return true;
    }

    bool TrySelectAlly(
        Transform targetAnchor,
        Transform targetRoot,
        out AllyCandidate bestCandidate,
        out bool placementFailed,
        out string diagnostics)
    {
        bestCandidate = default;
        placementFailed = false;
        diagnostics = null;

        FieldAllyManager allyManager = playerContext.fieldAllyManager;
        if (allyManager == null)
        {
            diagnostics = "fieldAllyManager is missing";
            return false;
        }

        float bestDist = float.MaxValue;
        bool bestNoWarp = false;
        Vector3 targetPos = targetAnchor != null ? targetAnchor.position : Vector3.zero;
        int registeredCount = 0;
        int missingControllerCount = 0;
        int notReadyCount = 0;
        int noSafePoseCount = 0;
        int candidateCount = 0;
        List<string> safePoseFailureReasons = new List<string>(4);

        foreach (FieldAllyMember member in allyManager.RegisteredMembers)
        {
            if (member == null) continue;
            registeredCount++;

            var ctrl = member.GetComponent<AllyInterruptionController>();
            if (ctrl == null)
            {
                missingControllerCount++;
                continue;
            }

            if (!ctrl.IsReadyForInterruption())
            {
                notReadyCount++;
                continue;
            }

            if (!ctrl.TryResolvePlacement(
                    targetAnchor,
                    targetRoot,
                    noWarpStartDistance,
                    noWarpTargetDistance,
                    out TargetedSkillPlacementResult placement))
            {
                noSafePoseCount++;
                if (safePoseFailureReasons.Count < 4)
                {
                    string reason = string.IsNullOrEmpty(placement.FailureReason)
                        ? "unknown"
                        : placement.FailureReason;
                    safePoseFailureReasons.Add($"{ResolveName(member.TransformRef)}: {reason}");
                }
                continue;
            }

            candidateCount++;
            Transform allyTransform = member.TransformRef;
            float dist = allyTransform != null ? Vector3.Distance(targetPos, allyTransform.position) : float.MaxValue;
            bool candidateNoWarp = !placement.RequiresPositionSnap;

            bool better;
            if (!bestCandidate.IsValid)
                better = true;
            else if (candidateNoWarp != bestNoWarp)
                better = candidateNoWarp;
            else
                better = dist < bestDist;

            if (better)
            {
                bestDist = dist;
                bestNoWarp = candidateNoWarp;
                bestCandidate = new AllyCandidate(ctrl, member, placement);
            }
        }

        diagnostics =
            $"allyScan registered={registeredCount} missingController={missingControllerCount} notReady={notReadyCount} noSafePose={noSafePoseCount} candidates={candidateCount}";
        if (safePoseFailureReasons.Count > 0)
            diagnostics += $"; safePoseFailures=[{string.Join(" | ", safePoseFailureReasons)}]";

        placementFailed = candidateCount == 0 && noSafePoseCount > 0;
        return bestCandidate.IsValid;
    }

    Transform ResolveTargetAnchor(Transform targetTransform)
    {
        return ChainAttackTargetingUtility.TryResolveTargetAnchor(targetTransform, out Transform anchor)
            ? anchor
            : targetTransform;
    }

    static float XZDistance(Transform a, Transform b)
    {
        if (a == null || b == null) return float.MaxValue;
        Vector3 pa = a.position;
        Vector3 pb = b.position;
        pa.y = 0f;
        pb.y = 0f;
        return Vector3.Distance(pa, pb);
    }

    InterruptionCommandResult Finish(int attemptId, InterruptionCommandResult result, string details)
    {
        CommandFinished?.Invoke(result);
        bool warning = result == InterruptionCommandResult.MissingConfiguration
            || result == InterruptionCommandResult.NoAvailableAlly
            || result == InterruptionCommandResult.NoAvailableInterrupter
            || result == InterruptionCommandResult.TeleportFailed
            || result == InterruptionCommandResult.SkillRejected;
        LogCommand(attemptId, $"finished result={result}; {details}", warning);
        return result;
    }

    void LogCommand(int attemptId, string message, bool warning = false)
    {
        if (!logInterruptionFlow && !warning)
            return;

        string formatted = $"[PreCast.Command] attemptId={attemptId} {message}";
        if (warning)
            Debug.LogWarning(formatted, this);
        else
            Debug.Log(formatted, this);
    }

    static string ResolveName(Transform target)
    {
        return target != null ? target.name : "<none>";
    }
}
