using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime.Tasks;

/// <summary>One action's path and progress tracker; shared graph data is only its handoff record.</summary>
public sealed class AICombatNavigation
{
    readonly CharacteContext ctx;
    readonly NavMeshAgent agent;
    readonly AICombatState state;
    readonly AITargetHandle owner;
    readonly NavMeshPath path = new NavMeshPath();
    Vector3 lastProgressPosition;
    Vector3 commandedDestination;
    float progressElapsed, pendingElapsed, repathElapsed;
    bool ownsPath;

    public AICombatNavigation(CharacteContext context, AICombatState data)
    {
        ctx = context;
        owner = new AITargetHandle(context);
        agent = ctx != null ? ctx.NavigationAgent : null;
        state = data;
        lastProgressPosition = ctx != null ? ctx.transform.position : Vector3.zero;
        repathElapsed = .25f;
    }
    public bool CanUse => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
    public static bool TryPath(NavMeshAgent agent, Vector3 candidate, NavMeshPath path, out Vector3 point)
    {
        point = default;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, Mathf.Max(.5f, agent.radius * 2f), filter)) return false;
        // Sampling at a ledge must leave room for the actor's footprint.
        if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edge, filter) && edge.distance < agent.radius) return false;
        if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        point = hit.position;
        return true;
    }
    public static float PathLength(NavMeshPath path)
    {
        float length = 0f;
        var corners = path.corners;
        for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
        return length;
    }
    public bool Command(Vector3 candidate)
    {
        if (!CanUse || !AICombatTaskUtility.CanMove(ctx) || AICombatTaskUtility.IsPaused || repathElapsed < .25f) return false;
        repathElapsed = 0f;
        if (!TryPath(agent, candidate, path, out Vector3 point)) return false;
        if (!agent.SetPath(path)) return false;
        agent.isStopped = false;
        commandedDestination = point;
        ownsPath = true;
        if (state != null) state.Destination = point;
        return true;
    }
    public bool MayRepath => repathElapsed >= .25f;
    public void AdvanceRepathClock() => repathElapsed += AICombatTaskUtility.Delta(ctx);
    public TaskStatus Tick(Vector3 destination, float arrival = .35f, bool completeGoal = true)
    {
        if (!CanUse) return Failure("Agent is disabled or off NavMesh");
        float dt = AICombatTaskUtility.Delta(ctx);
        repathElapsed += dt;
        if (!AICombatTaskUtility.CanMove(ctx) || AICombatTaskUtility.IsPaused)
        {
            progressElapsed = pendingElapsed = 0f;
            lastProgressPosition = ctx.transform.position;
            return TaskStatus.Running;
        }
        if (agent.pathPending)
        {
            pendingElapsed += dt;
            return pendingElapsed >= 2f ? Failure("Path pending timeout") : TaskStatus.Running;
        }
        pendingElapsed = 0f;
        if (AICombatTaskUtility.PlanarDistance(ctx.transform.position, destination) <= arrival)
        {
            if (completeGoal) state?.CompleteNavigation();
            return TaskStatus.Success;
        }
        if (!ownsPath || !agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete)
            return Failure("No complete path");
        progressElapsed += dt;
        if (Vector3.Distance(ctx.transform.position, lastProgressPosition) >= .2f)
        {
            lastProgressPosition = ctx.transform.position;
            progressElapsed = 0f;
        }
        return progressElapsed >= 1.5f ? Failure("Navigation stuck") : TaskStatus.Running;
    }
    public void TrackExisting(Vector3 destination)
    {
        if (!ownsPath && state != null) state.BeginNavigation("Orbit", state.NavigationTarget, destination);
        ownsPath = true;
        commandedDestination = destination;
        if (state != null) state.Destination = destination;
        if (state != null) state.GoalDestination = destination;
    }
    TaskStatus Failure(string reason)
    {
        state?.FailNavigation(reason);
        return TaskStatus.Failure;
    }
    public void Cleanup()
    {
        // A new sequence/root-motion owner may already have replaced our path/flags.
        if (ownsPath && owner.IsLive && CanUse && agent.updatePosition &&
            (ctx.MovementMotor == null || !ctx.MovementMotor.IsAgentTransformSuspended) &&
            (ctx.AnimBrain == null || !ctx.AnimBrain.RootMotionActive) &&
            (ctx.FieldAllyMember == null || !ctx.FieldAllyMember.IsReserved) &&
            AICombatTaskUtility.PlanarDistance(agent.destination, commandedDestination) < .1f)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        ownsPath = false;
        if (state != null) state.NavigationActive = false;
    }
}
