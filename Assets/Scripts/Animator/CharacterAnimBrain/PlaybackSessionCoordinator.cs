using System;

internal sealed class PlaybackSessionCoordinator
{
    internal readonly PlaybackChannel Skill = new() { Kind = CharacterAnimBrain.PlaybackKind.Skill };
    internal readonly PlaybackChannel Utility = new() { Kind = CharacterAnimBrain.PlaybackKind.UtilityWarpOut };
    internal readonly PlaybackChannel Chain = new();
    internal readonly PlaybackChannel SpecialReaction = new() { Kind = CharacterAnimBrain.PlaybackKind.SpecialReaction };
    private StateHub commandControlHub;
    private int commandControlToken;
    internal int BindingGeneration { get; set; }
    internal PlaybackSessionToken Capture(PlaybackChannel channel) => new(channel, BindingGeneration);
    internal bool IsCurrent(PlaybackSessionToken token) => token.Channel != null &&
        token.BindingGeneration == BindingGeneration && token.RequestId == token.Channel.RequestId &&
        token.PlaybackGeneration == token.Channel.Request.Generation;
    internal void AcquireCommandControl(StateHub hub)
    {
        ReleaseCommandControl();
        commandControlHub = hub;
        if (hub != null)
            commandControlToken = hub.AcquireExternalControlBlockToken(
                ControlBlockFlags.Move | ControlBlockFlags.Rotate, nameof(CharacterAnimBrain));
    }
    internal void ReleaseCommandControl()
    {
        var hub = commandControlHub;
        int token = commandControlToken;
        commandControlHub = null;
        commandControlToken = 0;
        if (hub != null && token != 0) hub.ReleaseExternalControlBlockToken(token);
    }
    internal void Close(PlaybackChannel channel, CharacterAnimBrain.PlaybackKind kind, bool completed,
        bool owesCast, bool owesAdvance, bool legacyCompletion, Action clear,
        Action<CharacterAnimBrain.PlaybackKind, CharacterAnimBrain.PlaybackPhase, int> emit)
    {
        bool open = channel.IsActive;
        bool advance = completed && owesAdvance && channel.Request.TryReleaseAdvance();
        var result = channel.Request.Close(completed);
        clear();
        if (owesCast && result.OwesCastMoment) emit(kind, CharacterAnimBrain.PlaybackPhase.CastMoment, result.RequestId);
        if (advance && result.RequestId > 0) emit(kind, CharacterAnimBrain.PlaybackPhase.AdvanceMoment, result.RequestId);
        if (completed && (open || legacyCompletion)) emit(kind, CharacterAnimBrain.PlaybackPhase.Completed, result.RequestId);
        if (result.OwesInterrupted) emit(kind, CharacterAnimBrain.PlaybackPhase.Interrupted, result.RequestId);
    }
}
