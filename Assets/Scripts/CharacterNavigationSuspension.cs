using System.Collections.Generic;
using UnityEngine.AI;

/// <summary>One baseline per agent, shared by all displacement owners on an actor.</summary>
public sealed class CharacterNavigationSuspension
{
    private sealed class AgentState
    {
        public NavMeshAgent Agent;
        public bool Position, Rotation, Stopped, StopKnown;
        public int Count, PositionOwners, RotationOwners;
        public bool ResumeAutonomy;
    }

    private struct Lease
    {
        public AgentState State;
        public bool Position, Rotation;
    }

    private readonly Dictionary<int, Lease> leases = new();
    private readonly List<AgentState> agents = new();
    private int nextToken;

    public bool OwnsPosition
    {
        get
        {
            foreach (var state in agents)
                if (state.PositionOwners > 0) return true;
            return false;
        }
    }

    public int Acquire(NavMeshAgent agent, bool position, bool rotation)
    {
        if (!agent) return 0;
        Tick();
        AgentState state = agents.Find(s => s.Agent == agent);
        if (state == null)
        {
            state = new AgentState
            {
                Agent = agent, Position = agent.updatePosition, Rotation = agent.updateRotation,
                StopKnown = agent.isActiveAndEnabled && agent.isOnNavMesh
            };
            if (state.StopKnown) state.Stopped = agent.isStopped;
            agents.Add(state);
        }
        do { unchecked { nextToken++; } } while (nextToken == 0 || leases.ContainsKey(nextToken));
        leases.Add(nextToken, new Lease { State = state, Position = position, Rotation = rotation });
        state.Count++;
        if (position) state.PositionOwners++;
        if (rotation) state.RotationOwners++;
        Apply(state);
        return nextToken;
    }

    public void Release(int token, bool resumeAutonomy = false)
    {
        if (!leases.TryGetValue(token, out var lease)) return;
        leases.Remove(token);
        var state = lease.State;
        state.ResumeAutonomy |= resumeAutonomy;
        state.Count--;
        if (lease.Position) state.PositionOwners--;
        if (lease.Rotation) state.RotationOwners--;
        Apply(state);
        Tick();
    }

    public void ReleaseAll()
    {
        leases.Clear();
        foreach (var state in agents)
        {
            state.Count = state.PositionOwners = state.RotationOwners = 0;
            Apply(state);
        }
        Tick();
    }

    // Keep pending stop restoration while disabled/off mesh. A later acquire reuses that original
    // baseline rather than capturing an override. Records retain the captured agent across rebinds.
    public void Tick()
    {
        for (int i = agents.Count - 1; i >= 0; i--)
        {
            var state = agents[i];
            if (!state.Agent) { agents.RemoveAt(i); continue; }
            Apply(state);
            if (state.Count == 0 && ((!state.StopKnown && !state.ResumeAutonomy) ||
                (state.Agent.isActiveAndEnabled && state.Agent.isOnNavMesh)))
                agents.RemoveAt(i);
        }
    }

    private static void Apply(AgentState state)
    {
        var agent = state.Agent;
        if (!agent) return;
        bool restoreDefaults = state.ResumeAutonomy && state.Count == 0;
        agent.updatePosition = state.PositionOwners == 0 && (restoreDefaults || state.Position);
        agent.updateRotation = state.RotationOwners == 0 && (restoreDefaults || state.Rotation);
        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        if (!state.StopKnown && state.Count > 0)
        {
            state.Stopped = agent.isStopped;
            state.StopKnown = true;
        }
        if (state.Count > 0) agent.isStopped = true;
        else if (state.StopKnown || restoreDefaults) agent.isStopped = restoreDefaults ? false : state.Stopped;
    }
}
