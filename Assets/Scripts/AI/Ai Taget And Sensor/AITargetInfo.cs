using System.Collections.Generic;
using UnityEngine;

public class AITargetInfo : MonoBehaviour, IAITargetable
{
    [SerializeField] private Transform aimPoint;
    [SerializeField] private Transform chainAttackPoint;
    [SerializeField] private int teamId = 0;
    [SerializeField] private bool isAlive = true;
    [SerializeField] private bool isTargetable = true;

    readonly HashSet<int> _untargetableTokens = new();
    int _nextUntargetableToken = 1;

    public Transform AimPoint => aimPoint != null ? aimPoint : transform;
    public Transform ChainAttackPoint => chainAttackPoint != null ? chainAttackPoint : AimPoint;
    public bool IsAlive => isAlive;
    public bool IsTargetable => isTargetable && _untargetableTokens.Count == 0;
    public int TeamId => teamId;

    public void SetAlive(bool value) => isAlive = value;
    public void SetTargetable(bool value) => isTargetable = value;

    public int AcquireUntargetableToken()
    {
        int token = _nextUntargetableToken++;
        _untargetableTokens.Add(token);
        return token;
    }

    public void ReleaseUntargetableToken(int token)
    {
        if (token <= 0)
            return;

        _untargetableTokens.Remove(token);
    }
}
