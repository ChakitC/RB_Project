/// <summary>
/// What the caller still owes the outside world after closing a playback session. Produced once
/// per session by <see cref="PlaybackRequestState.Close"/>; a second call reports nothing to do.
/// </summary>
internal readonly struct PlaybackSessionClose
{
    /// <summary>The request that was closed. Zero for a request-less playback.</summary>
    public readonly int RequestId;

    /// <summary>
    /// The clip finished before the cast point was reached, so the cast moment is owed before the
    /// completion. A completed request has always seen its cast moment; consumers rely on that.
    /// </summary>
    public readonly bool OwesCastMoment;

    /// <summary>The session was open and carried a real request, so an interruption is owed.</summary>
    public readonly bool OwesInterrupted;

    public PlaybackSessionClose(int requestId, bool owesCastMoment, bool owesInterrupted)
    {
        RequestId = requestId;
        OwesCastMoment = owesCastMoment;
        OwesInterrupted = owesInterrupted;
    }
}
