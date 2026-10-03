using UnityEngine;

public struct AITargetScanPolicy
{
    public Vector3 Origin;
    public Vector3 Forward;
    public float Radius;
    public bool UseFov;
    public float Fov;
    public bool RequireLos;
    public bool RequireAlive;
    public bool TeamFilter;
    public int OwnerTeam;
    public LayerMask TargetLayers;
    public LayerMask Obstacles;
    public QueryTriggerInteraction Triggers;
    public float FallbackHeight;
    public float DistanceWeight;
    public float PlayerBonus;
    public float CompanionBonus;
    public float TankBonus;
    public float HealerBonus;
    public float SupportBonus;
    public float SniperBonus;
    public float AssaultBonus;
    public float Stickiness;
    public float LosPenalty;
    public bool UseThreat;
    public float ThreatDecay;
    public float ThreatWeight;
}
