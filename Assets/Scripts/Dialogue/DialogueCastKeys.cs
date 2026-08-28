using System;

/// <summary>
/// The strings a sequence uses to name a cast member.
///
/// Two kinds exist. A **character id** (`ID.Roma`) names one specific character and is what fixed
/// actors — NPCs above all — use. A **party role** (`role.Player`, `role.PartySlot1`) names a
/// position in the player's line-up and resolves against whatever party is actually deployed, so a
/// conversation written for "the player and their first squadmate" survives the player changing their
/// team, which they can do at any time in the Basement.
///
/// Both are just dictionary keys to everything downstream, so a sequence can mix them freely: cast
/// the NPC by id and the squad by role.
/// </summary>
public static class DialogueCastKeys
{
    public const string RolePrefix = "role.";

    public const string Player = RolePrefix + nameof(ChainActorRole.Player);
    public const string PartySlot1 = RolePrefix + nameof(ChainActorRole.PartySlot1);
    public const string PartySlot2 = RolePrefix + nameof(ChainActorRole.PartySlot2);
    public const string Helper = RolePrefix + nameof(ChainActorRole.Helper);

    public static string ForRole(ChainActorRole role) => RolePrefix + role;

    public static bool IsRoleKey(string key)
    {
        return !string.IsNullOrWhiteSpace(key) &&
               key.StartsWith(RolePrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Parses a `role.` key. False for a character id, or for a role name that does not exist.</summary>
    public static bool TryParseRole(string key, out ChainActorRole role)
    {
        role = ChainActorRole.None;
        if (!IsRoleKey(key))
            return false;

        string name = key.Substring(RolePrefix.Length);
        return Enum.TryParse(name, ignoreCase: true, out role) && role != ChainActorRole.None;
    }
}
