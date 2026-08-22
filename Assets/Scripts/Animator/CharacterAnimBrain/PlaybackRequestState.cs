using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One playback request's lifecycle, owned by a <see cref="PlaybackChannel"/>.
///
/// The status machine (<see cref="PlaybackSessionStatus"/>) is authoritative. The cast moment can
/// only be released once, and the session can only be closed once, so "exactly one terminal event
/// per request" holds even if two paths race to tear the same playback down — for example a state
/// exiting at the same time as an explicit interrupt.
/// </summary>
internal sealed class PlaybackRequestState
{
    /// <summary>Set independently of the session: <c>PlaySkill()</c> arms a clip with no request.</summary>
    public SkillGemDefinition Definition;

    public bool UsesPlanarRootMotion;
    public bool IgnoresCharacterCollisionDuringRootMotion;
    public readonly List<CombatTimelineEventName> TimelineEventNames = new();

    public PlaybackSessionStatus Status { get; private set; } = PlaybackSessionStatus.Idle;
    public int RequestId { get; private set; }
    public float CastPointNormalized { get; private set; } = 0.35f;

    /// <summary>Chain playback only: an extra mid-clip beat the sequence runner waits on.</summary>
    public bool AdvanceRequested { get; private set; }
    public float AdvancePointNormalized { get; private set; } = 1f;
    public bool AdvanceReleased { get; private set; }

    /// <summary>True while a request is armed and has not been closed.</summary>
    public bool IsOpen =>
        Status == PlaybackSessionStatus.Started ||
        Status == PlaybackSessionStatus.CastReleased;

    /// <summary>A cast moment is still owed to this request.</summary>
    public bool ReleaseRequested => IsOpen;

    /// <summary>The cast moment has already been delivered.</summary>
    public bool Released => Status == PlaybackSessionStatus.CastReleased;

    public void Begin(int requestId, float castPointNormalized)
    {
        RequestId = requestId;
        CastPointNormalized = Mathf.Clamp(castPointNormalized, 0f, 0.999f);
        Status = PlaybackSessionStatus.Started;
        AdvanceRequested = false;
        AdvancePointNormalized = 1f;
        AdvanceReleased = false;
    }

    /// <summary>Arms the chain advance beat. Must be called after <see cref="Begin"/>.</summary>
    public void RequestAdvanceMoment(float advancePointNormalized)
    {
        AdvanceRequested = true;
        AdvanceReleased = false;

        // The advance beat can never precede the cast beat.
        AdvancePointNormalized = Mathf.Clamp(
            Mathf.Max(CastPointNormalized, advancePointNormalized),
            0f,
            0.999f);
    }

    /// <summary>Delivers the cast moment. Returns false if it already happened or is not owed.</summary>
    public bool TryReleaseCast()
    {
        if (Status != PlaybackSessionStatus.Started)
            return false;

        Status = PlaybackSessionStatus.CastReleased;
        return true;
    }

    /// <summary>Delivers the chain advance beat. Returns false if it already happened.</summary>
    public bool TryReleaseAdvance()
    {
        if (!AdvanceRequested || AdvanceReleased || !IsOpen)
            return false;

        AdvanceReleased = true;
        return true;
    }

    /// <summary>
    /// Closes the session and reports what is still owed. Idempotent: closing an already-closed or
    /// never-started session reports nothing owed, which is the terminal-once guarantee.
    /// </summary>
    public PlaybackSessionClose Close(bool completedNormally)
    {
        bool wasOpen = IsOpen;
        bool hasRequest = RequestId > 0;

        var result = new PlaybackSessionClose(
            RequestId,
            owesCastMoment: completedNormally && hasRequest && Status == PlaybackSessionStatus.Started,
            owesInterrupted: !completedNormally && hasRequest && wasOpen);

        if (wasOpen)
        {
            Status = completedNormally
                ? PlaybackSessionStatus.Completed
                : PlaybackSessionStatus.Interrupted;
        }

        return result;
    }

    public void Clear()
    {
        Definition = null;
        RequestId = 0;
        CastPointNormalized = 0.35f;
        Status = PlaybackSessionStatus.Idle;
        AdvanceRequested = false;
        AdvancePointNormalized = 1f;
        AdvanceReleased = false;
        UsesPlanarRootMotion = false;
        IgnoresCharacterCollisionDuringRootMotion = false;
        TimelineEventNames.Clear();
    }
}
