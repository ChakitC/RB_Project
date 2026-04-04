using UnityEngine;

public interface IAITargetable
{
    Transform AimPoint { get; }
    bool IsAlive { get; }
    int TeamId { get; }
}
