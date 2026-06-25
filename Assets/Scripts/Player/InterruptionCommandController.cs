using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InterruptionCommandController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerContext playerContext;

    [Header("Target Search")]
    [SerializeField] private LayerMask targetSearchMask = ~0;
    [SerializeField, Min(0.5f)] private float targetSearchRadius = 4f;

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
    }

    public InterruptionCommandResult TryExecuteInterruptionCommand()
    {
        int attemptId = ++_attemptCounter;

        if (playerContext == null || playerContext.aimTarget == null)
            return Finish(attemptId, InterruptionCommandResult.MissingConfiguration, "playerContext or aimTarget is missing");

        LogCommand(attemptId,
            $"started center={playerContext.aimTarget.position} radius={targetSearchRadius:0.##} mask={targetSearchMask.value}");

        if (!TryFindTarget(out var targetCtx, out bool windowExistButClosed, out string targetDiagnostics))
        {
            return Finish(attemptId, windowExistButClosed
                ? InterruptionCommandResult.TargetWindowClosed
                : InterruptionCommandResult.NoValidTarget,
                targetDiagnostics);
        }

        Transform targetAnchor = ResolveTargetAnchor(targetCtx.Transform);
        LogCommand(attemptId,
            $"target selected name='{ResolveName(targetCtx.Transform)}' requestId={targetCtx.Block.ActiveRequestId}; {targetDiagnostics}");

        if (!TrySelectAlly(targetAnchor, out AllyInterruptionController allyCtrl, out FieldAllyMember allyMember, out string allyDiagnostics))
            return Finish(attemptId, InterruptionCommandResult.NoAvailableAlly, allyDiagnostics);

        if (!allyCtrl.TryResolveSafePose(targetAnchor, out Vector3 safePos, out Quaternion safeRot))
            return Finish(attemptId, InterruptionCommandResult.TeleportFailed,
                $"ally='{ResolveName(allyMember.TransformRef)}' safe pose could not be resolved");

        if (!allyMember.TryReserve(allyCtrl))
            return Finish(attemptId, InterruptionCommandResult.NoAvailableAlly,
                $"ally='{ResolveName(allyMember.TransformRef)}' reservation was rejected");

        LogCommand(attemptId,
            $"ally selected name='{ResolveName(allyMember.TransformRef)}' safePosition={safePos}; {allyDiagnostics}");

        if (!targetCtx.Block.TryReserveBlock(gameObject, holdSpeedMultiplier, holdSafetyMargin, out PreCastBlockReservation reservation))
        {
            allyMember.ReleaseReservation(allyCtrl);
            return Finish(attemptId, InterruptionCommandResult.TargetWindowClosed,
                $"target='{ResolveName(targetCtx.Transform)}' block reservation was rejected");
        }

        LogCommand(attemptId,
            $"block reserved requestId={reservation.RequestId} reservationId={reservation.ReservationId}");

        if (!allyCtrl.BeginInterruption(targetCtx, reservation, safePos, safeRot))
        {
            allyMember.ReleaseReservation(allyCtrl);
            return Finish(attemptId, InterruptionCommandResult.SkillRejected,
                $"ally='{ResolveName(allyMember.TransformRef)}' rejected interruption start");
        }

        CommandStarted?.Invoke(new InterruptionCommandExecution
        {
            AllyName = allyMember.TransformRef != null ? allyMember.TransformRef.name : allyMember.name,
            TargetName = targetCtx.Transform != null ? targetCtx.Transform.name : "<unknown>"
        });

        return Finish(attemptId, InterruptionCommandResult.Success,
            $"ally='{ResolveName(allyMember.TransformRef)}' target='{ResolveName(targetCtx.Transform)}' requestId={reservation.RequestId} reservationId={reservation.ReservationId}");
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
                    Transform = block.transform,
                    Anchor = ResolveTargetAnchor(block.transform),
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
        out AllyInterruptionController bestCtrl,
        out FieldAllyMember bestMember,
        out string diagnostics)
    {
        bestCtrl = null;
        bestMember = null;
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

            if (!ctrl.TryResolveSafePose(targetAnchor, out _, out _))
            {
                noSafePoseCount++;
                continue;
            }

            candidateCount++;
            Transform allyTransform = member.TransformRef;
            float dist = allyTransform != null ? Vector3.Distance(targetPos, allyTransform.position) : float.MaxValue;
            if (dist < bestDist)
            {
                bestDist = dist;
                bestCtrl = ctrl;
                bestMember = member;
            }
        }

        if (logInterruptionFlow)
        {
            diagnostics =
                $"allyScan registered={registeredCount} missingController={missingControllerCount} notReady={notReadyCount} noSafePose={noSafePoseCount} candidates={candidateCount}";
        }

        return bestCtrl != null;
    }

    Transform ResolveTargetAnchor(Transform targetTransform)
    {
        return ChainAttackTargetingUtility.TryResolveTargetAnchor(targetTransform, out Transform anchor)
            ? anchor
            : targetTransform;
    }

    InterruptionCommandResult Finish(int attemptId, InterruptionCommandResult result, string details)
    {
        CommandFinished?.Invoke(result);
        bool warning = result == InterruptionCommandResult.MissingConfiguration
            || result == InterruptionCommandResult.SkillRejected;
        LogCommand(attemptId, $"finished result={result}; {details}", warning);
        return result;
    }

    void LogCommand(int attemptId, string message, bool warning = false)
    {
        if (!logInterruptionFlow)
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
