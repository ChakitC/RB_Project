using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime.Tasks;

[Opsive.Shared.Utility.Category("AI/Navigation")]
public sealed class RecoverNavigation : AICombatAction
{
    public int maxAttempts = 2;
    public float cooldown = .5f, timeout = 3f;
    Vector3 originalDestination, recoveryDestination;
    bool commanded, nudging, exhausted;
    readonly NavMeshPath path = new NavMeshPath();
    public override void OnStart()
    {
        base.OnStart();
        commanded = nudging = exhausted = false;
        originalDestination = State != null ? State.GoalDestination : default;
    }
    public override TaskStatus OnUpdate()
    {
        if (!Ready) return TaskStatus.Failure;
        AITargetHandle current = sensor != null ? sensor.ReadSnapshot.Target : default;
        if (State.NavigationTarget.InstanceId != 0 &&
            (!AICombatTaskUtility.IsEligible(ctx, State.NavigationTarget) ||
             (State.NavigationTarget != current && !(State.SearchActive && !current.IsLive))))
        {
            State.ObserveTarget(current);
            return TaskStatus.Failure;
        }
        if (AICombatTaskUtility.IsPaused || !AICombatTaskUtility.CanMove(ctx)) return TaskStatus.Running;
        float dt = AICombatTaskUtility.Delta(ctx);
        if (State.SearchActive)
        {
            State.SearchElapsed += dt;
            if (State.SearchElapsed >= 8f && !exhausted)
            {
                exhausted = true;
                State.CooldownRemaining = cooldown;
            }
        }
        if (exhausted || (!commanded && State.RecoveryAttempts >= Mathf.Clamp(maxAttempts, 0, 2)))
        {
            if (!exhausted) State.CooldownRemaining = cooldown;
            exhausted = true;
            State.ConsumeSearch();
            State.CooldownRemaining -= dt;
            if (State.CooldownRemaining > 0f) return TaskStatus.Running;
            State.NavigationFailure = null;
            return TaskStatus.Failure;
        }
        if (!navigation.CanUse)
        {
            State.FailNavigation("Agent is disabled or off NavMesh; recovery cannot warp");
            State.RecoveryAttempts = Mathf.Clamp(maxAttempts, 0, 2);
            State.CooldownRemaining = cooldown;
            return TaskStatus.Running;
        }
        elapsed += dt;
        if (!commanded)
        {
            State.RecoveryAttempts++;
            recoveryDestination = originalDestination;
            if (State.RecoveryAttempts > 1 || !AICombatNavigation.TryPath(agent, originalDestination, path, out _))
            {
                bool found = false;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 candidate = ctx.transform.position + Quaternion.AngleAxis(i * 90f, Vector3.up) * ctx.transform.right;
                    if (!AICombatNavigation.TryPath(agent, candidate, path, out recoveryDestination)) continue;
                    found = true;
                    break;
                }
                if (!found) return Fail("Recovery nudge unavailable");
                nudging = true;
            }
            if (!navigation.Command(recoveryDestination)) return Fail("Recovery path unavailable");
            commanded = true;
        }
        if (elapsed >= timeout) return Fail("Recovery timeout");
        TaskStatus result = navigation.Tick(recoveryDestination, .35f, false);
        if (result == TaskStatus.Success && nudging)
        {
            // Keep the attempt quota across the nudge and original-path leg.
            if (!navigation.MayRepath) return TaskStatus.Running;
            if (!navigation.Command(originalDestination)) return Fail("Recovery original path unavailable");
            recoveryDestination = originalDestination;
            nudging = false;
            return TaskStatus.Running;
        }
        if (result == TaskStatus.Success) State.CompleteNavigation();
        return result;
    }
}
