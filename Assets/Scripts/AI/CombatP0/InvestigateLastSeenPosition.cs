using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;

[Opsive.Shared.Utility.Category("AI/Search")]
public sealed class InvestigateLastSeenPosition : AICombatAction
{
    public float maxObservationAge = 10f, searchBudget = 8f;
    bool commanded;
    public override void OnStart()
    {
        base.OnStart();
        commanded = false;
        if (sensor == null || State == null) return;
        AITargetSnapshot s = sensor.ReadSnapshot;
        if (!State.SearchActive || State.SearchTarget != s.LastSeen || State.SearchObservationTime != s.LastSeenTime)
        {
            State.SearchTarget = s.LastSeen;
            State.SearchObservationTime = s.LastSeenTime;
            State.SearchCenter = s.LastSeenPosition;
            State.SearchElapsed = 0f;
            State.SearchActive = true;
        }
    }
    public override TaskStatus OnUpdate()
    {
        if (!Ready || !State.SearchActive || !AICombatTaskUtility.IsEligible(ctx, State.SearchTarget))
            return TaskStatus.Failure;
        if (sensor.ReadSnapshot.HasLiveTarget) return TaskStatus.Failure;
        float age = sensor.ObservationClock - State.SearchObservationTime;
        if (age < 0f || age > maxObservationAge || State.IsConsumed(sensor.ReadSnapshot))
        {
            State.ConsumeSearch();
            return TaskStatus.Failure;
        }
        if (!AICombatTaskUtility.CanMove(ctx) || AICombatTaskUtility.IsPaused) return TaskStatus.Running;
        State.SearchElapsed += AICombatTaskUtility.Delta(ctx);
        if (State.SearchElapsed >= searchBudget)
        {
            State.ConsumeSearch();
            return TaskStatus.Failure;
        }
        if (!commanded)
        {
            State.BeginNavigation("Investigate", State.SearchTarget, State.SearchCenter);
            if (!navigation.Command(State.SearchCenter)) return Fail("No path to last observation");
            commanded = true;
        }
        return navigation.Tick(State.Destination);
    }
}
