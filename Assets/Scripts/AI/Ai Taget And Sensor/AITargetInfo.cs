using UnityEngine;

public class AITargetInfo : MonoBehaviour, IAITargetable
{
    [SerializeField] private Transform aimPoint;
    [SerializeField] private int teamId = 0;
    [SerializeField] private bool isAlive = true;

    public Transform AimPoint => aimPoint != null ? aimPoint : transform;
    public bool IsAlive => isAlive;
    public int TeamId => teamId;

    public void SetAlive(bool value) => isAlive = value;
}