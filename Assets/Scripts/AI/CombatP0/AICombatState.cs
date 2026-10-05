using System;
using UnityEngine;

/// <summary>Graph-local data. CanAct creates a fresh value for each tree/actor lifetime.</summary>
[Serializable]
public sealed class AICombatState
{
    [NonSerialized] public AITargetHandle Owner;
    [NonSerialized] public AITargetHandle NavigationTarget;
    [NonSerialized] public Vector3 Destination;
    [NonSerialized] public Vector3 GoalDestination;
    [NonSerialized] public string NavigationGoal;
    [NonSerialized] public string NavigationFailure;
    [NonSerialized] public bool NavigationActive;
    [NonSerialized] public int RecoveryAttempts;
    [NonSerialized] public float CooldownRemaining;
    [NonSerialized] public AITargetHandle FiringTarget;
    [NonSerialized] public Vector3 FiringPosition;
    [NonSerialized] public bool HasFiringPosition;
    [NonSerialized] public AITargetHandle SearchTarget;
    [NonSerialized] public float SearchObservationTime;
    [NonSerialized] public Vector3 SearchCenter;
    [NonSerialized] public float SearchElapsed;
    [NonSerialized] public bool SearchActive;
    [NonSerialized] public AITargetHandle ConsumedTarget;
    [NonSerialized] public float ConsumedTime = float.NegativeInfinity;

    public void BeginNavigation(string goal, AITargetHandle target, Vector3 destination)
    {
        if (NavigationGoal != goal || NavigationTarget != target)
            RecoveryAttempts = 0;
        NavigationGoal = goal;
        NavigationTarget = target;
        Destination = destination;
        GoalDestination = destination;
        NavigationFailure = null;
        NavigationActive = true;
    }

    public void FailNavigation(string reason)
    {
        NavigationFailure = reason;
        NavigationActive = false;
    }

    public void CompleteNavigation()
    {
        NavigationActive = false;
        NavigationFailure = null;
        RecoveryAttempts = 0;
    }

    public bool IsConsumed(AITargetSnapshot snapshot) =>
        ConsumedTarget == snapshot.LastSeen && ConsumedTime >= snapshot.LastSeenTime;

    public void ConsumeSearch()
    {
        if (!SearchActive) return;
        ConsumedTarget = SearchTarget;
        ConsumedTime = SearchObservationTime;
        SearchActive = false;
    }

    public void ObserveTarget(AITargetHandle current)
    {
        if (HasFiringPosition && FiringTarget != current) HasFiringPosition = false;
        if (NavigationTarget.InstanceId != 0 && NavigationTarget != current &&
            !(SearchActive && NavigationTarget == SearchTarget && current.InstanceId == 0))
        {
            NavigationFailure = null;
            NavigationActive = false;
            NavigationGoal = null;
            NavigationTarget = default;
            Destination = GoalDestination = default;
            RecoveryAttempts = 0;
        }
        if (SearchActive && current.InstanceId != 0 && current != SearchTarget)
            SearchActive = false;
    }
}
