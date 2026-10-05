using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime.Tasks;

[Opsive.Shared.Utility.Category("AI/Search")]
public sealed class SearchArea : AICombatAction
{
    public float radius = 3f, searchBudget = 8f, lookDuration = .5f;
    public int maxPoints = 3;
    int pointIndex;
    float lookElapsed;
    bool commanded, looking;
    Vector3 destination;
    readonly NavMeshPath path = new NavMeshPath();
    public override void OnStart()
    {
        base.OnStart();
        pointIndex = 0;
        commanded = looking = false;
        lookElapsed = 0f;
    }
    public override TaskStatus OnUpdate()
    {
        if (!Ready || !State.SearchActive || !AICombatTaskUtility.IsEligible(ctx, State.SearchTarget))
            return TaskStatus.Failure;
        if (sensor.ReadSnapshot.HasLiveTarget) return TaskStatus.Failure;
        if (!AICombatTaskUtility.CanMove(ctx) || AICombatTaskUtility.IsPaused) return TaskStatus.Running;
        float dt = AICombatTaskUtility.Delta(ctx);
        State.SearchElapsed += dt;
        if (State.SearchElapsed >= searchBudget || pointIndex >= Mathf.Clamp(maxPoints, 0, 3))
        {
            State.ConsumeSearch();
            return TaskStatus.Success;
        }
        if (looking)
        {
            navigation.AdvanceRepathClock();
            if (ctx.stateHub.CanRotate())
                ctx.transform.Rotate(Vector3.up, 180f * dt);
            lookElapsed += dt;
            if (lookElapsed >= lookDuration) { looking = false; commanded = false; pointIndex++; }
            return TaskStatus.Running;
        }
        if (!commanded)
        {
            Vector3 candidate = State.SearchCenter + Quaternion.AngleAxis(pointIndex * 120f, Vector3.up) * Vector3.forward * radius;
            candidate.y = ctx.transform.position.y;
            if (!AICombatNavigation.TryPath(agent, candidate, path, out destination))
            {
                pointIndex++;
                return TaskStatus.Running;
            }
            State.BeginNavigation("Search", State.SearchTarget, destination);
            if (!navigation.Command(destination)) return Fail("No path to search point");
            commanded = true;
        }
        TaskStatus result = navigation.Tick(destination);
        if (result == TaskStatus.Success)
        {
            navigation.Cleanup();
            looking = true;
            lookElapsed = 0f;
            return TaskStatus.Running;
        }
        return result;
    }
}
