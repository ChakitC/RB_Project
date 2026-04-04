using UnityEngine;

public class AITargetInfo : MonoBehaviour, IAITargetable
{
    [SerializeField] private Transform aimPoint;
    [SerializeField] private Transform chainAttackPoint;
    [SerializeField] private int teamId = 0;
    [SerializeField] private bool isAlive = true;

    public Transform AimPoint => aimPoint != null ? aimPoint : transform;
    public Transform ChainAttackPoint => chainAttackPoint != null ? chainAttackPoint : AimPoint;
    public bool IsAlive => isAlive;
    public int TeamId => teamId;

    public void SetAlive(bool value) => isAlive = value;
}
