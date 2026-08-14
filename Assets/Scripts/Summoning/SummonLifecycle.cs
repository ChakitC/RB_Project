using System;

public enum SummonMobility
{
    Mobile,
    Stationary,
}

public enum SummonLifecycleState
{
    Active,
    Despawning,
    Destroyed,
}

public enum SummonDespawnReason
{
    Expired,
    Killed,
    OwnerDead,
    RoomTransition,
    RunEnded,
    PlacementFailed,
    CapEvicted,
    ControllerDestroyed,
}

public interface ISummonLifecycleReceiver
{
    void OnSummonDespawnRequested(SummonDespawnReason reason);
}
