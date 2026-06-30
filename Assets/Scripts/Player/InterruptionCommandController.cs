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

    [Header("Target Search")]
    [SerializeField] private LayerMask targetSearchMask = ~0;
    [SerializeField, Min(0.5f)] private float targetSearchRadius = 4f;

    [Header("Player Interrupt")]
    [SerializeField, Min(0.5f)] private float playerInterruptRange = 4f;

    [Header("Hold Tuning")]
    [SerializeField, Range(0f, 1f)] private float holdSpeedMultiplier = 0.1f;
    [SerializeField, Range(0f, 0.5f)] private float holdSafetyMargin = 0.08f;

    [Header("Debug")]
    [SerializeField] private bool logInterruptionFlow;

    static readonly Collider[] _overlapBuffer = new Collider[32];
    int _attemptCounter;

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

        if (playerContext == null || playerContext.aimTarget == null)
            return Finish(attemptId, InterruptionCommandResult.MissingConfiguration, "playerContext or aimTarget is missing");

        if (playerInterruptionController != null && playerInterruptionController.IsExecuting)
            return Finish(attemptId, InterruptionCommandResult.SkillRejected, "player interruption already executing");

        LogCommand(attemptId,
            $"started center={playerContext.aimTarget.position} radius={targetSearchRadius:0.##} mask={targetSearchMask.value}");

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
            if (playerInterruptionController.TryResolvePlacement(targetAnchor, targetCtx.Transform, out var playerPlacement))
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
        if (playerInterruptionController.TryResolvePlacement(targetAnchor, targetCtx.Transform, out var warpPlacement))
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
        if (!targetCtx.Block.TryReserveBlock(gameObject, holdSpeedMultiplier, holdSafetyMargin, out PreCastBlockReservation reservation))
            return Finish(attemptId, InterruptionCommandResult.TargetWindowClosed,
                $"target='{ResolveName(targetCtx.Transform)}' block reservation was rejected");

        LogCommand(attemptId,
            $"block reserved requestId={reservation.RequestId} reservationId={reservation.ReservationId}");

        if (!playerInterruptionController.BeginInterruption(targetCtx, reservation, placement))
        {
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

        Vector3 center = playerContext.aimTarget.position;
        int count = Physics.OverlapSphereNonAlloc(center, targetSearchRadius, _overlapBuffer, targetSearchMask, QueryTriggerInteraction.Ignore);

        float bestDist = float.MaxValue;
        InterruptionTargetContext bestCtx = default;
        bool found = false;
        bool foundOpenWindow = false;
        int blockControllerCount = 0;
        int closedWindowCount = 0;
        int reservedCount = 0;
        int unavailableHealthCount = 0;
        int missingKnockbackCount = 0;
        int candidateCount = 0;

        for (int i = 0; i < count; i++)
        {
            var col = _overlapBuffer[i];
            var block = col.GetComponentInParent<PreCastBlockController>();
            if (block == null) continue;

            blockControllerCount++;

            if (!block.CanBlockActiveCast())
            {
                closedWindowCount++;
                continue;
            }

            foundOpenWindow = true;

            if (block.HasActiveReservation)
            {
                reservedCount++;
                continue;
            }

            CharacteContext targetContext = block.GetComponentInParent<CharacteContext>();
            targetContext?.ResolveReferences();
            Transform targetRoot = targetContext != null ? targetContext.transform : block.transform;

            HealthSystem health = targetContext != null
                ? targetContext.HealthSystem
                : col.GetComponentInParent<HealthSystem>();
            if (health == null || !health.IsAlive)
            {
                unavailableHealthCount++;
                continue;
            }

            CharacterKnockbackMotor knockback = targetContext != null
                ? targetContext.KnockbackMotor
                : col.GetComponentInParent<CharacterKnockbackMotor>();
            if (knockback == null)
            {
                missingKnockbackCount++;
                continue;
            }

            candidateCount++;
            float dist = Vector3.Distance(center, col.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestCtx = new InterruptionTargetContext
                {
                    Transform = targetRoot,
                    Anchor = ResolveTargetAnchor(targetRoot),
                    Block = block,
                    Knockback = knockback,
                    Health = health
                };
                found = true;
            }
        }

        if (logInterruptionFlow)
        {
            diagnostics =
                $"targetScan overlaps={count}/{_overlapBuffer.Length} blockControllers={blockControllerCount} closed={closedWindowCount} reserved={reservedCount} unavailableHealth={unavailableHealthCount} missingKnockback={missingKnockbackCount} candidates={candidateCount}";
        }

        if (!found)
        {
            windowExistButClosed = blockControllerCount > 0 && !foundOpenWindow;
            return false;
        }

        ctx = bestCtx;
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
            if (dist < bestDist)
            {
                bestDist = dist;
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
