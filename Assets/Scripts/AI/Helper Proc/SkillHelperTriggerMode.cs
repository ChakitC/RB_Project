/// <summary>What makes a helper proc fire.</summary>
public enum SkillHelperTriggerMode
{
    /// <summary>Rolls against a combat event on the bus. The original behaviour.</summary>
    CombatEventProc = 0,

    /// <summary>
    /// Fires deterministically when an eligible party member's health drops to or below a
    /// threshold. No proc roll: an assist the player relies on to survive must not be a coin flip.
    /// </summary>
    PartyHealthThreshold = 1,
}
