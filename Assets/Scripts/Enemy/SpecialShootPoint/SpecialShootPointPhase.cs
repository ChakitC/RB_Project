/// <summary>
/// The Special Shoot Point round state machine, owned by <see cref="SpecialShootPointController"/>.
///
/// <see cref="Telegraph"/>, <see cref="Active"/>, and <see cref="Resolving"/> are the three phases a
/// Behavior Tree reports as "a round is running".
/// </summary>
public enum SpecialShootPointPhase
{
    /// <summary>Ready to accept a trigger.</summary>
    Idle = 0,

    /// <summary>Points are visible but their colliders are disabled; shots pass through to the body.</summary>
    Telegraph = 1,

    /// <summary>Point colliders are live and accept direct player ranged damage.</summary>
    Active = 2,

    /// <summary>The round has an outcome and is playing out its reaction/cleanup.</summary>
    Resolving = 3,

    /// <summary>The numeric cooldown after an accepted challenge resolved.</summary>
    Cooldown = 4,

    /// <summary>Authoring is incomplete or the component was disabled; no round can start.</summary>
    Disabled = 5,
}
