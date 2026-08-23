/// <summary>
/// How a character is used once it is loaded into the party.
///
/// A party slot is a shared rig that any character can be loaded into, so this is the character's
/// own declaration of what it is for. It decides which half of the Skill Loadout applies: a field
/// character casts from command slots, a helper never does - it is summoned by a trigger and
/// performs one assist.
/// </summary>
public enum CharacterPartyRole
{
    /// <summary>Fights in the field. Uses Skill Slots; helper procs do not apply.</summary>
    Stryker = 0,

    /// <summary>Summoned to perform an assist. Uses Helper Procs; command slots do not apply.</summary>
    Helper = 1,
}
