using UnityEngine;

/// <summary>
/// Request-scoped scheduler that starts a character fade near a playback transition point.
/// Playback owners remain responsible for resolving their own channel timing and for deciding
/// whether a fully hidden actor should be deactivated.
/// </summary>
internal sealed class CharacterPlaybackAutoHideSchedule
{
    float _visibleElapsed;

    public bool IsPending { get; private set; }
    public int RequestId { get; private set; }

    public bool Start(int requestId)
    {
        Cancel();
        if (requestId <= 0)
            return false;

        RequestId = requestId;
        IsPending = true;
        return true;
    }

    public void Advance(CharacterVisibilityController visibility)
    {
        if (!IsPending || visibility == null)
            return;

        _visibleElapsed += visibility.UsesUnscaledTime
            ? Time.unscaledDeltaTime
            : Time.deltaTime;
    }

    public bool TryBeginHide(
        CharacterVisibilityController visibility,
        float remainingDuration,
        float playbackDuration)
    {
        return TryBeginHide(visibility, remainingDuration, playbackDuration, fadeDuration: -1f);
    }

    /// <summary>
    /// <paramref name="fadeDuration"/> overrides the component's authored fade length. Pass a
    /// negative value to keep the authored one.
    ///
    /// Callers that know when the playback's transition lands should scale the fade to the window
    /// they actually have: a fixed fade is either cut short on a quick playback or ends long before
    /// the transition on a slow one.
    /// </summary>
    public bool TryBeginHide(
        CharacterVisibilityController visibility,
        float remainingDuration,
        float playbackDuration,
        float fadeDuration)
    {
        if (!IsPending ||
            visibility == null ||
            !visibility.ShouldBeginAutoHideForFade(
                _visibleElapsed,
                remainingDuration,
                playbackDuration,
                fadeDuration))
        {
            return false;
        }

        Cancel();
        if (!visibility.IsHidden && !visibility.IsDisappearing)
        {
            if (fadeDuration >= 0f)
                visibility.Disappear(fadeDuration);
            else
                visibility.Disappear();
        }

        return true;
    }

    public void Cancel()
    {
        IsPending = false;
        RequestId = 0;
        _visibleElapsed = 0f;
    }
}
