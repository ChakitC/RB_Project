using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PartySpawnConfig", menuName = "RB/Party/Spawn Config")]
public sealed class PartySpawnConfigSO : ScriptableObject
{
    static readonly ChainActorRole[] RequiredRoles =
    {
        ChainActorRole.Player,
        ChainActorRole.PartySlot1,
        ChainActorRole.PartySlot2,
        ChainActorRole.Helper,
    };

    [SerializeField] private PartySpawnEntry[] members = Array.Empty<PartySpawnEntry>();
    [SerializeField] private GameObject playerUIPrefab;

    public IReadOnlyList<PartySpawnEntry> Members => members;
    public GameObject PlayerUIPrefab => playerUIPrefab;

    public bool TryValidate(out string error)
    {
        var errors = new List<string>();
        Validate(errors);
        error = string.Join("\n", errors);
        return errors.Count == 0;
    }

    public void Validate(List<string> errors)
    {
        if (errors == null)
            throw new ArgumentNullException(nameof(errors));

        if (members == null || members.Length != RequiredRoles.Length)
        {
            errors.Add($"Party config must contain exactly {RequiredRoles.Length} members.");
            return;
        }

        var roles = new HashSet<ChainActorRole>();
        var partyIndices = new HashSet<int>();

        for (int i = 0; i < members.Length; i++)
        {
            PartySpawnEntry entry = members[i];
            if (entry == null)
            {
                errors.Add($"Member entry {i} is null.");
                continue;
            }

            if (!IsRequiredRole(entry.Role))
                errors.Add($"Member entry {i} has unsupported role '{entry.Role}'.");
            else if (!roles.Add(entry.Role))
                errors.Add($"Role '{entry.Role}' is assigned more than once.");

            int expectedIndex = ExpectedPartyIndex(entry.Role);
            if (entry.PartyIndex != expectedIndex)
                errors.Add($"Role '{entry.Role}' must use party index {expectedIndex}, not {entry.PartyIndex}.");
            else if (!partyIndices.Add(entry.PartyIndex))
                errors.Add($"Party index {entry.PartyIndex} is assigned more than once.");

            ValidateMemberPrefab(entry, errors);
        }

        for (int i = 0; i < RequiredRoles.Length; i++)
        {
            if (!roles.Contains(RequiredRoles[i]))
                errors.Add($"Required role '{RequiredRoles[i]}' is missing.");
        }

        if (playerUIPrefab == null)
        {
            errors.Add("Player UI prefab is missing.");
        }
        else
        {
            if (playerUIPrefab.GetComponentInChildren<PlayerUIContext>(true) == null)
                errors.Add($"Player UI prefab '{playerUIPrefab.name}' is missing PlayerUIContext.");
            if (playerUIPrefab.GetComponentInChildren<PlayerUIRuntimeBinder>(true) == null)
                errors.Add($"Player UI prefab '{playerUIPrefab.name}' is missing PlayerUIRuntimeBinder.");
        }
    }

    public PartySpawnEntry GetMember(ChainActorRole role)
    {
        if (members == null)
            return null;

        for (int i = 0; i < members.Length; i++)
        {
            if (members[i] != null && members[i].Role == role)
                return members[i];
        }

        return null;
    }

#if UNITY_EDITOR
    public void SetAuthoringData(PartySpawnEntry[] actorEntries, GameObject uiPrefab)
    {
        members = actorEntries ?? Array.Empty<PartySpawnEntry>();
        playerUIPrefab = uiPrefab;
    }

    void OnValidate()
    {
        if (!TryValidate(out string error))
            Debug.LogWarning($"[PartySpawnConfig] {name} is invalid:\n{error}", this);
    }
#endif

    static void ValidateMemberPrefab(PartySpawnEntry entry, List<string> errors)
    {
        GameObject prefab = entry.Prefab;
        if (prefab == null)
        {
            errors.Add($"Role '{entry.Role}' has no prefab.");
            return;
        }

        CharacteContext context = prefab.GetComponentInChildren<CharacteContext>(true);
        if (entry.Role == ChainActorRole.Player)
        {
            if (context is not PlayerContext)
                errors.Add($"Role '{entry.Role}' prefab '{prefab.name}' must contain PlayerContext.");
        }
        else if (context is not AllyContext)
        {
            errors.Add($"Role '{entry.Role}' prefab '{prefab.name}' must contain AllyContext.");
        }

        if (prefab.GetComponentInChildren<CharacterContextPartyLoader>(true) == null)
            errors.Add($"Role '{entry.Role}' prefab '{prefab.name}' is missing CharacterContextPartyLoader.");

        if (prefab.GetComponentInChildren<FieldAllyMember>(true) == null)
            errors.Add($"Role '{entry.Role}' prefab '{prefab.name}' is missing FieldAllyMember.");
    }

    static bool IsRequiredRole(ChainActorRole role)
    {
        for (int i = 0; i < RequiredRoles.Length; i++)
        {
            if (RequiredRoles[i] == role)
                return true;
        }

        return false;
    }

    static int ExpectedPartyIndex(ChainActorRole role)
    {
        return role switch
        {
            ChainActorRole.Player => 0,
            ChainActorRole.PartySlot1 => 1,
            ChainActorRole.PartySlot2 => 2,
            ChainActorRole.Helper => 3,
            _ => -1,
        };
    }
}
