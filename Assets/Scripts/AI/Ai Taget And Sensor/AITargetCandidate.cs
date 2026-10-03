using UnityEngine;

public struct AITargetCandidate
{
    public AITargetHandle Handle;
    public Vector3 Point;
    public float Distance;
    public float Score;
    public float BasePriority;
    public AITargetIdentity Identity;
    public CharacterCombatRole CombatRole;
    public bool HasLineOfSight;
    public bool IsVisible;
}
