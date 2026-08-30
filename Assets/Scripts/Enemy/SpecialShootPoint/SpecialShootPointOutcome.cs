/// <summary>
/// How the most recent Special Shoot Point round finished.
///
/// Stamped with the round's request id by <see cref="SpecialShootPointController"/> so a Behavior
/// Tree condition can never consume the outcome of an older activation.
/// </summary>
public enum SpecialShootPointOutcome
{
    /// <summary>No round has resolved yet, or the controller was reset.</summary>
    None = 0,

    /// <summary>Every active point was destroyed inside the active window.</summary>
    Succeeded = 1,

    /// <summary>The active window expired with points still standing.</summary>
    TimedOut = 2,

    /// <summary>Death, a cinematic, an unrelated ChainReady, or a disable ended the round early.</summary>
    Cancelled = 3,
}
