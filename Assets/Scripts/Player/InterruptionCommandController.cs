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

    static readonly Collider[] _overlapBuffer = new Collider[32];

    public event Action<InterruptionCommandExecution> CommandStarted;
    public event Action<InterruptionCommandResult> CommandFinished;

    void Awake()
    {
        if (playerContext == null)
            TryGetComponent(out playerContext);
    }

    public InterruptionCommandResult TryExecuteInterruptionCommand()
    {
        if (playerContext == null || playerContext.aimTarget == null)
            return Finish(InterruptionCommandResult.MissingConfiguration);

        if (!TryFindTarget(out var targetCtx, out bool windowExistButClosed))
        {
            return Finish(windowExistButClosed
                ? InterruptionCommandResult.TargetWindowClosed
                : InterruptionCommandResult.NoValidTarget);
        }

        Transform targetAnchor = ResolveTargetAnchor(targetCtx.Transform);

        if (!TrySelectAlly(targetAnchor, out AllyInterruptionController allyCtrl, out FieldAllyMember allyMember))
            return Finish(InterruptionCommandResult.NoAvailableAlly);

        if (!allyCtrl.TryResolveSafePose(targetAnchor, out Vector3 safePos, out Quaternion safeRot))
            return Finish(InterruptionCommandResult.TeleportFailed);

        if (!allyMember.TryReserve(allyCtrl))
            return Finish(InterruptionCommandResult.NoAvailableAlly);

        if (!targetCtx.Block.TryReserveBlock(gameObject, holdSpeedMultiplier, holdSafetyMargin, out PreCastBlockReservation reservation))
        {
            allyMember.ReleaseReservation(allyCtrl);
            return Finish(InterruptionCommandResult.TargetWindowClosed);
        }

        if (!allyCtrl.BeginInterruption(targetCtx, reservation, safePos, safeRot))
        {
            allyMember.ReleaseReservation(allyCtrl);
            return Finish(InterruptionCommandResult.SkillRejected);
        }

        CommandStarted?.Invoke(new InterruptionCommandExecution
        {
            AllyName = allyMember.TransformRef != null ? allyMember.TransformRef.name : allyMember.name,
            TargetName = targetCtx.Transform != null ? targetCtx.Transform.name : "<unknown>"
        });

        return Finish(InterruptionCommandResult.Success);
    }

    bool TryFindTarget(out InterruptionTargetContext ctx, out bool windowExistButClosed)
    {
        ctx = default;
        windowExistButClosed = false;

        Vector3 center = playerContext.aimTarget.position;
        int count = Physics.OverlapSphereNonAlloc(center, targetSearchRadius, _overlapBuffer, targetSearchMask, QueryTriggerInteraction.Ignore);

        float bestDist = float.MaxValue;
        InterruptionTargetContext bestCtx = default;
        bool found = false;
        bool foundBlockController = false;

        for (int i = 0; i < count; i++)
        {
            var col = _overlapBuffer[i];
            var block = col.GetComponentInParent<PreCastBlockController>();
            if (block == null) continue;

            foundBlockController = true;

            if (!block.CanBlockActiveCast()) continue;
            if (block.HasActiveReservation) continue;

            var health = col.GetComponentInParent<HealthSystem>();
            if (health == null || !health.IsAlive) continue;

            var knockback = col.GetComponentInParent<CharacterKnockbackMotor>();
            if (knockback == null) continue;

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

        if (!found)
        {
            windowExistButClosed = foundBlockController;
            return false;
        }

        ctx = bestCtx;
        return true;
    }

    bool TrySelectAlly(Transform targetAnchor, out AllyInterruptionController bestCtrl, out FieldAllyMember bestMember)
    {
        bestCtrl = null;
        bestMember = null;

        FieldAllyManager allyManager = playerContext.fieldAllyManager;
        if (allyManager == null) return false;

        float bestDist = float.MaxValue;
        Vector3 targetPos = targetAnchor != null ? targetAnchor.position : Vector3.zero;

        foreach (FieldAllyMember member in allyManager.RegisteredMembers)
        {
            if (member == null) continue;

            var ctrl = member.GetComponent<AllyInterruptionController>();
            if (ctrl == null) continue;
            if (!ctrl.IsReadyForInterruption()) continue;

            if (!ctrl.TryResolveSafePose(targetAnchor, out _, out _)) continue;

            Transform allyTransform = member.TransformRef;
            float dist = allyTransform != null ? Vector3.Distance(targetPos, allyTransform.position) : float.MaxValue;
            if (dist < bestDist)
            {
                bestDist = dist;
                bestCtrl = ctrl;
                bestMember = member;
            }
        }

        return bestCtrl != null;
    }

    Transform ResolveTargetAnchor(Transform targetTransform)
    {
        return ChainAttackTargetingUtility.TryResolveTargetAnchor(targetTransform, out Transform anchor)
            ? anchor
            : targetTransform;
    }

    InterruptionCommandResult Finish(InterruptionCommandResult result)
    {
        CommandFinished?.Invoke(result);
        return result;
    }
}
