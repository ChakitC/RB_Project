using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;

[Opsive.Shared.Utility.Category("AI/Movement")]
public sealed class MoveToCombatRange : AICombatAction
{
    public float minRange = 5f, maxRange = 7f, tolerance = .5f, timeout = 6f;
    Vector3 destination;
    bool commanded;
    public override void OnStart()
    {
        base.OnStart();
        commanded = false;
    }
    public override TaskStatus OnUpdate()
    {
        if (!Ready || !ValidTarget()) return TaskStatus.Failure;
        if (!AICombatTaskUtility.CanMove(ctx) || AICombatTaskUtility.IsPaused) return TaskStatus.Running;
        elapsed += AICombatTaskUtility.Delta(ctx);
        Vector3 aim = sensor.ReadSnapshot.AimPosition;
        float distance = AICombatTaskUtility.PlanarDistance(ctx.transform.position, aim);
        if (AICombatTaskUtility.InRange(distance, minRange, maxRange, commanded ? tolerance : 0f))
        {
            State.CompleteNavigation();
            return TaskStatus.Success;
        }
        if (elapsed >= timeout) return Fail("Combat range timeout");
        if (!commanded || navigation.MayRepath)
        {
            Vector3 radial = Vector3.ProjectOnPlane(ctx.transform.position - aim, Vector3.up);
            if (radial.sqrMagnitude < .001f) radial = -ctx.transform.forward;
            destination = aim + radial.normalized * ((minRange + maxRange) * .5f);
            destination.y = ctx.transform.position.y;
            State.BeginNavigation("CombatRange", targetHandle, destination);
            if (!navigation.Command(destination)) return Fail("No path to combat range");
            destination = State.Destination;
            commanded = true;
        }
        TaskStatus result = navigation.Tick(destination);
        return result == TaskStatus.Success ? TaskStatus.Running : result;
    }
}
