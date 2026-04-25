using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-108)]
public sealed class FieldAllyManager : MonoBehaviour
{
    [SerializeField] private PlayerContext playerContext;
    [SerializeField] private FieldAllyMember[] members;
    [SerializeField] private bool logRegistration;

    readonly Dictionary<ChainActorRole, FieldAllyMember> _membersByRole = new();

    public IEnumerable<FieldAllyMember> RegisteredMembers => _membersByRole.Values;

    void Awake()
    {
        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();

        RebuildRegistry();

        if (playerContext != null && playerContext.fieldAllyManager == null)
            playerContext.fieldAllyManager = this;
    }

    public bool TryGetMember(ChainActorRole role, out FieldAllyMember member)
    {
        if (_membersByRole.TryGetValue(role, out member) && member != null)
            return true;

        member = null;
        return false;
    }

    public void Register(FieldAllyMember member)
    {
        if (member == null || member.ActorRole == ChainActorRole.None)
            return;

        if (_membersByRole.TryGetValue(member.ActorRole, out FieldAllyMember existing) &&
            existing != null &&
            existing != member)
        {
            Debug.LogWarning(
                $"[FieldAllyManager] Role '{member.ActorRole}' is already registered by '{existing.name}'. Replacing with '{member.name}'.",
                this);
        }

        _membersByRole[member.ActorRole] = member;

        if (logRegistration)
            Debug.Log($"[FieldAllyManager] Registered '{member.name}' as '{member.ActorRole}'.", this);
    }

    public void Unregister(FieldAllyMember member)
    {
        if (member == null)
            return;

        if (_membersByRole.TryGetValue(member.ActorRole, out FieldAllyMember existing) && existing == member)
            _membersByRole.Remove(member.ActorRole);
    }

    public void FinalizeSequenceReservations(object owner, bool interrupted)
    {
        if (owner == null)
            return;

        HashSet<FieldAllyMember> processed = new();
        foreach (FieldAllyMember member in _membersByRole.Values)
        {
            if (member == null || !processed.Add(member))
                continue;

            member.FinalizeSequenceParticipation(owner, interrupted);
        }
    }

    public bool HasOwnedSequenceWork(object owner)
    {
        if (owner == null)
            return false;

        HashSet<FieldAllyMember> processed = new();
        foreach (FieldAllyMember member in _membersByRole.Values)
        {
            if (member == null || !processed.Add(member))
                continue;

            if (member.OwnsSequenceWork(owner))
                return true;
        }

        return false;
    }

    public string DescribeOwnedSequenceWork(object owner)
    {
        if (owner == null)
            return "owner=null";

        HashSet<FieldAllyMember> processed = new();
        List<string> descriptions = new();

        foreach (FieldAllyMember member in _membersByRole.Values)
        {
            if (member == null || !processed.Add(member))
                continue;

            if (member.TryDescribeOwnedSequenceWork(owner, out string description) &&
                !string.IsNullOrWhiteSpace(description))
            {
                descriptions.Add(description);
            }
        }

        return descriptions.Count > 0
            ? string.Join("; ", descriptions)
            : "none";
    }

    void RebuildRegistry()
    {
        _membersByRole.Clear();

        if (members != null)
        {
            for (int i = 0; i < members.Length; i++)
                Register(members[i]);
        }

        FieldAllyMember[] childMembers = GetComponentsInChildren<FieldAllyMember>(true);
        for (int i = 0; i < childMembers.Length; i++)
            Register(childMembers[i]);
    }
}
