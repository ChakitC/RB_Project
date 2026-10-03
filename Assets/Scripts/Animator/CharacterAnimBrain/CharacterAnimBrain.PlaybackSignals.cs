public sealed partial class CharacterAnimBrain
{
    private readonly PlaybackSignalDispatcher signals = new();
    private bool TrySetLocomotionState(LocomotionState state)
    {
        using var transition = signals.Begin();
        return locomotionSM.TrySetState(state);
    }
    private bool TryResetLocomotionState(LocomotionState state)
    {
        using var transition = signals.Begin();
        return locomotionSM.TryResetState(state);
    }
    private void ForceSetLocomotionState(LocomotionState state)
    {
        using var transition = signals.Begin();
        locomotionSM.ForceSetState(state);
    }
    private void EmitPlaybackSignal(PlaybackKind kind, PlaybackPhase phase, int requestId)
    {
        if (kind != PlaybackKind.None)
            signals.Enqueue(() => RaisePlaybackSignal(kind, phase, requestId));
    }
    /// <summary>
    /// Single fan-out point from the canonical <see cref="PlaybackEvent"/> stream to the legacy
    /// per-subsystem events. Adding a phase or kind only needs a change here.
    /// </summary>
    private void RaisePlaybackSignal(PlaybackKind kind, PlaybackPhase phase, int requestId)
    {
        SafeAnimationEvents.Invoke(PlaybackEvent, new PlaybackSignal(kind, phase, requestId));

        bool isChain = IsChainPlaybackKind(kind);
        bool isSkillOrUtility = kind == PlaybackKind.Skill || kind == PlaybackKind.UtilityWarpOut;

        switch (phase)
        {
            case PlaybackPhase.CastMoment:
                if (isChain)
                    SafeAnimationEvents.Invoke(ChainCastMomentReached, requestId);
                else if (isSkillOrUtility || kind == PlaybackKind.Melee)
                    SafeAnimationEvents.Invoke(SkillCastMomentReached, requestId);
                break;

            case PlaybackPhase.AdvanceMoment:
                if (isChain)
                    SafeAnimationEvents.Invoke(ChainAdvanceMomentReached, requestId);
                break;

            case PlaybackPhase.Completed:
                if (isChain)
                    SafeAnimationEvents.Invoke(ChainPlaybackCompleted, requestId);
                else if (isSkillOrUtility)
                    SafeAnimationEvents.Invoke(SkillCompleted);
                break;

            case PlaybackPhase.Interrupted:
                if (isChain)
                {
                    SafeAnimationEvents.Invoke(ChainPlaybackInterrupted, requestId);

                    // A chain skill still owns a skill request, so skill runtimes must unwind too.
                    if (kind == PlaybackKind.ChainSkill)
                        SafeAnimationEvents.Invoke(SkillCastInterrupted, requestId);
                }
                else if (isSkillOrUtility || kind == PlaybackKind.Melee)
                {
                    SafeAnimationEvents.Invoke(SkillCastInterrupted, requestId);
                }

                break;
        }
    }

    private static bool IsChainPlaybackKind(PlaybackKind kind)
    {
        return kind == PlaybackKind.ChainSkill ||
               kind == PlaybackKind.ChainUtilityWarpOut ||
               kind == PlaybackKind.ChainUtilityWarpIn ||
               kind == PlaybackKind.ChainCutscene;
    }
}
