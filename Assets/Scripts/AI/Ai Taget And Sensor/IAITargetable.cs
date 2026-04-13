using UnityEngine;

public interface IAITargetable
{
    Transform AimPoint { get; }
    bool IsAlive { get; }
    bool IsTargetable { get; }
    int TeamId { get; }
}
