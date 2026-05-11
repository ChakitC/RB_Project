using System;
using System.Collections.Generic;
using UnityEngine;

public class AITargetInfo : MonoBehaviour, IAITargetable
{
    [SerializeField] private Transform aimPoint;
    [SerializeField] private Transform chainAttackPoint;
    [SerializeField] private int teamId = 0;
    [SerializeField, HideInInspector] private AITargetRole targetRole = AITargetRole.Auto;
    [SerializeField] private AITargetIdentity targetIdentity = AITargetIdentity.Auto;
    [SerializeField] private bool overrideCombatRole = false;
    [SerializeField] private CharacterCombatRole combatRoleOverride = CharacterCombatRole.Generic;
    [SerializeField] private float baseTargetPriority = 0f;
    [SerializeField, Min(0f)] private float threatMultiplier = 1f;
    [SerializeField] private bool isAlive = true;
    [SerializeField] private bool isTargetable = true;

    readonly HashSet<int> _untargetableTokens = new HashSet<int>();
    int _nextUntargetableToken = 1;

    public static event Action<AITargetInfo> GlobalTargetStateChanged;

    CharacteContext _characterContext;
    
    

    public Transform AimPoint => aimPoint != null ? aimPoint : transform;
    public Transform ChainAttackPoint => chainAttackPoint != null ? chainAttackPoint : AimPoint;
    public bool IsAlive => isAlive;
    public bool IsTargetable => isTargetable && _untargetableTokens.Count == 0;
    public int TeamId => teamId;
    public AITargetIdentity TargetIdentity => ResolveIdentity();
    public CharacterCombatRole CombatRole => ResolveCombatRole();
    public float BaseTargetPriority => baseTargetPriority;
    public float ThreatMultiplier => Mathf.Max(0f, threatMultiplier);

    public void SetAlive(bool value)
    {
        if (isAlive == value)
            return;

        isAlive = value;
        NotifyTargetStateChanged();
    }

    public void SetTargetable(bool value)
    {
        if (isTargetable == value)
            return;

        isTargetable = value;
        NotifyTargetStateChanged();
    }

    public int AcquireUntargetableToken()
    {
        int token = _nextUntargetableToken++;
        _untargetableTokens.Add(token);
        NotifyTargetStateChanged();
        return token;
    }

    public void ReleaseUntargetableToken(int token)
    {
        if (token <= 0)
            return;

        if (_untargetableTokens.Remove(token))
            NotifyTargetStateChanged();
    }

    void NotifyTargetStateChanged()
    {
        GlobalTargetStateChanged?.Invoke(this);
    }

    AITargetIdentity ResolveIdentity()
    {
        if (targetIdentity != AITargetIdentity.Auto)
            return targetIdentity;

        if (targetRole == AITargetRole.Player)
            return AITargetIdentity.Player;

        if (targetRole == AITargetRole.Companion)
            return AITargetIdentity.Companion;

        ResolveCharacterContext();

        if (_characterContext != null)
            return _characterContext.TargetIdentity;

        return AITargetIdentity.Generic;
    }

    CharacterCombatRole ResolveCombatRole()
    {
        if (overrideCombatRole)
            return combatRoleOverride;

        switch (targetRole)
        {
            case AITargetRole.Tank:
                return CharacterCombatRole.Tank;
            case AITargetRole.Healer:
                return CharacterCombatRole.Healer;
            case AITargetRole.Support:
                return CharacterCombatRole.Support;
            case AITargetRole.Sniper:
                return CharacterCombatRole.Sniper;
        }

        ResolveCharacterContext();

        if (_characterContext != null && _characterContext.baseStats != null)
            return _characterContext.baseStats.combatRole;

        return CharacterCombatRole.Generic;
    }

    void ResolveCharacterContext()
    {
        if (_characterContext != null)
            return;

        _characterContext = GetComponentInParent<CharacteContext>();
        _characterContext?.ResolveReferences();
    }
}
