using System;
using Animancer;

public sealed partial class CharacterAnimBrain
{
    /// <summary>
    /// The dedicated one-shot Special Point reaction request.
    ///
    /// A separate channel rather than a reuse of the skill channel: this playback carries no cast
    /// moment, no timeline events, and no skill definition, and its terminal callback is what the
    /// Special Shoot Point round hands ChainReady off from. Sharing the skill channel would have
    /// made an ordinary skill interruption look like a reaction interruption.
    /// </summary>
    private readonly PlaybackChannel _specialReactionChannel = new() { Kind = PlaybackKind.SpecialReaction };

    private float _specialReactionFallbackSeconds = 0.6f;

    /// <summary>Reason a Special Point reaction ended without reaching the end of its clip.</summary>
    public enum SpecialReactionInterruptReason
    {
        /// <summary>An explicit cancel from the owning controller.</summary>
        Cancelled = 0,

        /// <summary>Death, down, a cinematic, or another higher authority took locomotion.</summary>
        OwnershipLost = 1,
    }

    /// <summary>Raised exactly once per request that reaches the end of its playback.</summary>
    public event Action<int> SpecialReactionCompleted;

    /// <summary>Raised exactly once per request that ends early.</summary>
    public event Action<int, SpecialReactionInterruptReason> SpecialReactionInterrupted;

    /// <summary>True while a Special Point reaction owns locomotion.</summary>
    public bool IsSpecialReactionPlaybackActive =>
        _specialReactionChannel.IsActive ||
        (_initialized && locomotionSM.CurrentState == specialReactionState);

    /// <summary>
    /// The clip the reaction plays. v1 deliberately reuses the profile's Mini Stun clip rather than
    /// introducing Light/Heavy variants.
    /// </summary>
    private ClipTransition SpecialReactionClip => AnimProfile != null ? AnimProfile.miniStune : null;

    /// <summary>
    /// Starts the Special Point reaction.
    /// </summary>
    /// <param name="requestId">Round-scoped identity echoed back on the terminal callbacks.</param>
    /// <param name="missingClipFallbackSeconds">
    /// How long to hold the gameplay lock when the profile has no valid clip. Owned by the Special
    /// Shoot Point profile, so the animation layer does not invent its own timing.
    /// </param>
    /// <returns>
    /// False when Death/Down, a cinematic, or an active chain owns locomotion. A missing clip is
    /// <em>not</em> a failure: the reaction still starts, holds, and completes normally.
    /// </returns>
    public bool TryPlaySpecialReaction(int requestId, float missingClipFallbackSeconds)
    {
        if (requestId <= 0)
            return false;

        if (_initialized &&
            !CanStartAnimation(
                CharacterAnimationMode.SpecialReaction,
                CharacterAnimationTransitionReason.SpecialReactionOverride))
        {
            return false;
        }

        if (!TryInitialize())
            return false;

        // Everything below the reaction in the priority order gives way. Already-released
        // projectiles are untouched; only the caster-side request is unwound.
        InterruptActiveSkillRequest();
        InterruptActiveUtilityRequest();
        StopReloadAction();

        // A full-body reload refuses to exit inside its locked window, exactly as it does for a hard
        // status pose, so it is cancelled the same way rather than silently losing the reaction.
        if (locomotionSM.CurrentState == fullBodyReloadState)
            fullBodyReloadState.CancelNow();

        _specialReactionChannel.Kind = PlaybackKind.SpecialReaction;
        _specialReactionChannel.Request.Begin(requestId, 0f);
        _specialReactionFallbackSeconds = missingClipFallbackSeconds > 0f
            ? missingClipFallbackSeconds
            : 0.6f;

        bool started = locomotionSM.CurrentState == specialReactionState
            ? TryResetLocomotionState(specialReactionState)
            : TrySetLocomotionState(specialReactionState);

        if (!started)
            _specialReactionChannel.Clear();

        return started;
    }

    /// <summary>
    /// Cancels an in-flight reaction. Ignored when <paramref name="requestId"/> is not the live
    /// request, so a late cancel from an older round cannot cut a newer one short.
    /// </summary>
    public void CancelSpecialReaction(int requestId)
    {
        if (!_initialized || requestId <= 0)
            return;

        if (_specialReactionChannel.RequestId != requestId || !_specialReactionChannel.IsActive)
            return;

        if (locomotionSM.CurrentState == specialReactionState)
        {
            bool exited = TrySetLocomotionState(IsDowned ? crawlState : locomotion);
            if (!exited)
                ForceSetLocomotionState(IsDowned ? crawlState : locomotion);

            return;
        }

        CloseSpecialReactionSession(false);
    }

    /// <summary>
    /// Terminal-once close. <see cref="PlaybackRequestState.Close"/> is what guarantees a request
    /// reports either a completion or an interruption, never both and never twice, even when the
    /// state exits at the same moment as an explicit cancel.
    /// </summary>
    private void CloseSpecialReactionSession(bool completedNormally)
    {
        PlaybackSessionClose close = _specialReactionChannel.Request.Close(completedNormally);
        _specialReactionChannel.Request.Clear();
        _specialReactionChannel.Kind = PlaybackKind.SpecialReaction;

        if (close.RequestId <= 0)
            return;

        if (completedNormally)
        {
            EmitPlaybackSignal(PlaybackKind.SpecialReaction, PlaybackPhase.Completed, close.RequestId);
            SpecialReactionCompleted?.Invoke(close.RequestId);
            return;
        }

        if (!close.OwesInterrupted)
            return;

        EmitPlaybackSignal(PlaybackKind.SpecialReaction, PlaybackPhase.Interrupted, close.RequestId);
        SpecialReactionInterrupted?.Invoke(close.RequestId, SpecialReactionInterruptReason.OwnershipLost);
    }
}
