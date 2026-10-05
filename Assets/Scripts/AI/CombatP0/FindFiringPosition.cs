using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime.Tasks;

[Opsive.Shared.Utility.Category("AI/Combat")]
public sealed class FindFiringPosition : AICombatAction
{
    public float minRange = 5f, maxRange = 7f;
    public int maxCandidates = 12;
    readonly NavMeshPath path = new NavMeshPath();
    public override TaskStatus OnUpdate()
    {
        if (!Ready || !ValidTarget() || !navigation.CanUse) return TaskStatus.Failure;
        State.HasFiringPosition = false;
        Vector3 aim = sensor.ReadSnapshot.AimPosition;
        Vector3 radial = Vector3.ProjectOnPlane(ctx.transform.position - aim, Vector3.up).normalized;
        if (radial.sqrMagnitude < .001f) radial = Vector3.forward;
        float best = float.PositiveInfinity;
        Vector3 chosen = default;
        int count = Mathf.Clamp(maxCandidates, 1, 12);
        for (int i = 0; i < count; i++)
        {
            float radius = i < 6 ? minRange : maxRange;
            Vector3 candidate = aim + Quaternion.AngleAxis((i % 6) * 60f, Vector3.up) * radial * radius;
            candidate.y = ctx.transform.position.y;
            if (!AICombatNavigation.TryPath(agent, candidate, path, out Vector3 point) ||
                !AICombatTaskUtility.InRange(AICombatTaskUtility.PlanarDistance(point, aim), minRange, maxRange) ||
                !sensor.HasLineOfSightFrom(point, targetHandle, aim)) continue;
            float length = AICombatNavigation.PathLength(path);
            if (length >= best) continue; // Stable candidate-order tie break.
            best = length;
            chosen = point;
        }
        if (float.IsPositiveInfinity(best)) return TaskStatus.Failure;
        State.FiringPosition = chosen;
        State.FiringTarget = targetHandle;
        State.HasFiringPosition = true;
        return TaskStatus.Success;
    }
}
