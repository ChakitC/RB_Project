using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;

[Opsive.Shared.Utility.Category("AI/Movement")]
public sealed class MoveToFiringPosition : AICombatAction
{
    public float minRange = 5f, maxRange = 7f, timeout = 6f;
    bool commanded;
    Vector3 destination;
    public override void OnStart()
    {
        base.OnStart();
        commanded = false;
        destination = State != null ? State.FiringPosition : default;
    }
    public override TaskStatus OnUpdate()
    {
        if (!Ready || !ValidTarget() || !State.HasFiringPosition || State.FiringTarget != targetHandle)
            return TaskStatus.Failure;
        Vector3 aim = sensor.ReadSnapshot.AimPosition;
        if (!AICombatTaskUtility.InRange(AICombatTaskUtility.PlanarDistance(destination, aim), minRange, maxRange, .5f) ||
            !sensor.HasLineOfSightFrom(destination, targetHandle, aim))
        {
            State.HasFiringPosition = false;
            return TaskStatus.Failure;
        }
        if (!AICombatTaskUtility.CanMove(ctx) || AICombatTaskUtility.IsPaused) return TaskStatus.Running;
        elapsed += AICombatTaskUtility.Delta(ctx);
        if (elapsed >= timeout) return Fail("Firing position timeout");
        if (!commanded)
        {
            State.BeginNavigation("FiringPosition", targetHandle, destination);
            if (!navigation.Command(destination)) return Fail("No path to firing position");
            commanded = true;
        }
        return navigation.Tick(destination);
    }
}
