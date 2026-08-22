/// <summary>
/// Where one playback request is in its life.
///
/// <code>
/// Idle -> Started -> CastReleased -> Completed
///                \-------------+---> Interrupted
/// </code>
///
/// A session only leaves <see cref="Started"/> or <see cref="CastReleased"/> once, which is what
/// makes "at most one terminal event per request" a property of the type rather than something
/// each call site has to recompute from a pair of booleans.
/// </summary>
internal enum PlaybackSessionStatus
{
    /// <summary>No request is armed. A request-less playback (<c>PlaySkill()</c>) stays here.</summary>
    Idle = 0,

    /// <summary>Armed with a request id, cast moment not yet delivered.</summary>
    Started = 1,

    /// <summary>The cast moment has been delivered exactly once.</summary>
    CastReleased = 2,

    /// <summary>Closed by the clip finishing.</summary>
    Completed = 3,

    /// <summary>Closed by something taking the playback away.</summary>
    Interrupted = 4,
}
